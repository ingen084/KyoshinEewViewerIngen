using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace KyoshinEewViewer.CodeAnalysis;

/// <summary>
/// AXAML の Command バインディングを静的解析し、Avalonia 12 のコンパイル済みバインディングで
/// 実行時クラッシュにつながるメソッドバインドをコンパイルエラーにするアナライザ。
///
/// Avalonia 12.1 のコンパイル済みバインディングは、同名メソッドが複数あると 1引数版を優先して選択する
/// (`object` 1個 &gt; その他の 1引数版 &gt; 引数なし)。このため引数なし版を呼ぶつもりのバインドでも
/// CommandParameter (未指定時は null) が 1引数版へ渡され、引数が値型の場合は unbox 時に
/// NullReferenceException でクラッシュする (RadarSeries.Reload で発生した事象)。
///
/// 解析対象は AdditionalFiles として渡された .axaml 内の
/// `Command="{Binding ...}"` 属性と `&lt;Setter Property="Command" Value="{Binding ...}"&gt;`。
/// パスは x:DataType / DataTemplate.DataType / キャスト構文 / $parent / #name を辿って解決し、
/// 型を特定できないバインドは誤検知を避けるため報告しない。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class XamlCommandBindingAnalyzer : DiagnosticAnalyzer
{
	public const string OverloadDiagnosticId = "KEVIXAML001";
	public const string ValueTypeParameterDiagnosticId = "KEVIXAML002";

	private static readonly DiagnosticDescriptor OverloadRule = new(
		OverloadDiagnosticId,
		"Command バインドされたメソッドにオーバーロードがあります",
		"'{0}.{1}' には {2} 個のオーバーロードがあります。コンパイル済みバインディングは 1引数版を優先して選択するため、意図しないオーバーロードが呼ばれ実行時にクラッシュする可能性があります。XAML 用に別名の引数なしメソッド (例: {1}FromGui) を用意してバインドし直してください",
		"XamlBinding",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Avalonia 12.1 のコンパイル済みバインディングは同名メソッドから 1引数版を優先して選択する (object 1個 > その他の 1引数版 > 引数なし)。CommandParameter 未指定 (null) のまま値型 1引数版が選ばれると unbox で NullReferenceException になる。",
		customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

	private static readonly DiagnosticDescriptor ValueTypeParameterRule = new(
		ValueTypeParameterDiagnosticId,
		"Command バインドされたメソッドの値型引数へ null または文字列が渡されます",
		"'{0}.{1}' は値型 ({2}) の引数を取りますが、CommandParameter が未指定 (null) または文字列リテラルのため、実行時の unbox で NullReferenceException / InvalidCastException になります。引数を {2} 型として供給するバインディングを CommandParameter に指定するか、引数なしメソッドをバインドしてください",
		"XamlBinding",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "コンパイル済みバインディングの Command は CommandParameter を unbox して引数へ渡すため、値型引数に null (未指定) や文字列リテラルが渡ると実行時にクラッシュする。",
		customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

	private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

	private static readonly Regex BindingRegex = new(
		@"^\{\s*(Binding|CompiledBinding|ReflectionBinding)\b(.*)\}\s*$",
		RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	// ((ns:Type)Expr) 形式のキャスト
	private static readonly Regex CastRegex = new(
		@"^\(\(\s*([\w:]+)\s*\)\s*\w+\s*\)$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	// Expr(ns:Type) 形式の後置キャスト
	private static readonly Regex PostfixCastRegex = new(
		@"^\w+\(\s*([\w:]+)\s*\)$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex IdentifierRegex = new(
		@"^\w+$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		=> ImmutableArray.Create(OverloadRule, ValueTypeParameterRule);

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationAction(AnalyzeCompilation);
	}

	private static void AnalyzeCompilation(CompilationAnalysisContext context)
	{
		foreach (var file in context.Options.AdditionalFiles)
		{
			if (!file.Path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
				continue;
			var source = file.GetText(context.CancellationToken);
			if (source == null)
				continue;

			XDocument doc;
			try
			{
				doc = XDocument.Parse(source.ToString(), LoadOptions.SetLineInfo);
			}
			catch (XmlException)
			{
				continue;
			}
			if (doc.Root == null)
				continue;

			new DocumentAnalyzer(context, file.Path, source, doc.Root).Analyze();
		}
	}

	private sealed class DocumentAnalyzer(CompilationAnalysisContext context, string path, SourceText source, XElement root)
	{
		/// <summary>各要素の位置で有効な DataContext の型 (x:DataType / DataTemplate.DataType から算出)</summary>
		private Dictionary<XElement, INamedTypeSymbol?> Scopes { get; } = [];
		/// <summary>x:Name / Name が付与された要素</summary>
		private Dictionary<string, XElement> NamedElements { get; } = new(StringComparer.Ordinal);

		public void Analyze()
		{
			ComputeScopes(root, null);

			foreach (var element in root.DescendantsAndSelf())
			{
				foreach (var attribute in element.Attributes())
				{
					string? bindingValue = null;
					var isSetter = false;
					if (attribute.Name.Namespace == XNamespace.None && attribute.Name.LocalName == "Command")
						bindingValue = attribute.Value;
					else if (element.Name.LocalName == "Setter" && attribute.Name.LocalName == "Value" &&
						(string?)element.Attribute("Property") == "Command")
					{
						bindingValue = attribute.Value;
						isSetter = true;
					}
					if (bindingValue == null)
						continue;

					AnalyzeBinding(element, attribute, bindingValue, isSetter);
				}
			}
		}

		/// <summary>要素ツリーを辿り、各要素で有効な DataContext 型と名前付き要素を収集する</summary>
		private void ComputeScopes(XElement element, INamedTypeSymbol? inherited)
		{
			var scope = inherited;
			var dataTypeAttribute = element.Attribute(XamlNs + "DataType")
				?? (element.Name.LocalName == "DataTemplate" ? element.Attribute("DataType") : null);
			if (dataTypeAttribute != null)
				// 解決できない DataType が指定されている場合、継承値を使うと誤検知になるため「不明」に落とす
				scope = ResolvePrefixedType(element, dataTypeAttribute.Value);
			Scopes[element] = scope;

			var name = (string?)element.Attribute(XamlNs + "Name") ?? (string?)element.Attribute("Name");
			if (name != null && !NamedElements.ContainsKey(name))
				NamedElements[name] = element;

			foreach (var child in element.Elements())
				ComputeScopes(child, scope);
		}

		private void AnalyzeBinding(XElement element, XAttribute attribute, string raw, bool isSetter)
		{
			var match = BindingRegex.Match(raw);
			if (!match.Success)
				return;

			var bindingPath = ExtractPath(match.Groups[2].Value);
			if (bindingPath is null or "" or ".")
				return;

			var segments = SplitTopLevel(bindingPath, '.');
			if (segments.Count == 0)
				return;

			// パスを前から評価する。current が「現在のオブジェクトの静的型」、
			// pendingDataContextScope は $parent / #name 直後の「その要素の DataContext 型」
			var current = Scopes[element] as ITypeSymbol;
			INamedTypeSymbol? pendingDataContextScope = null;
			for (var i = 0; i < segments.Count; i++)
			{
				var segment = segments[i].Trim().TrimStart('!').Trim();
				if (segment.Length == 0)
					return;
				var isLast = i == segments.Count - 1;

				if (segment.StartsWith("$parent", StringComparison.Ordinal))
				{
					var target = FindParentElement(element, segment);
					pendingDataContextScope = target != null && Scopes.TryGetValue(target, out var parentScope) ? parentScope : null;
					current = target != null ? ResolveElementType(target) : null;
					if (isLast)
						return; // コントロール自体のバインドは対象外
					continue;
				}
				if (segment.StartsWith("#", StringComparison.Ordinal))
				{
					NamedElements.TryGetValue(segment.Substring(1), out var target);
					pendingDataContextScope = target != null && Scopes.TryGetValue(target, out var namedScope) ? namedScope : null;
					current = target != null ? ResolveElementType(target) : null;
					if (isLast)
						return;
					continue;
				}
				if (segment == "$self")
				{
					pendingDataContextScope = Scopes[element];
					current = ResolveElementType(element);
					if (isLast)
						return;
					continue;
				}

				var castMatch = CastRegex.Match(segment);
				if (!castMatch.Success)
					castMatch = PostfixCastRegex.Match(segment);
				if (castMatch.Success)
				{
					current = ResolvePrefixedType(element, castMatch.Groups[1].Value);
					pendingDataContextScope = null;
					if (current == null || isLast)
						return;
					continue;
				}

				if (segment == "DataContext" && pendingDataContextScope != null)
				{
					current = pendingDataContextScope;
					pendingDataContextScope = null;
					if (isLast)
						return;
					continue;
				}
				pendingDataContextScope = null;

				if (current == null || !IdentifierRegex.IsMatch(segment))
					return; // 型を特定できない・インデクサ等は対象外

				if (isLast)
				{
					AnalyzeFinalMember(current, segment, element, attribute, isSetter);
					return;
				}

				current = FindPropertyOrFieldType(current, segment);
				if (current == null)
					return;
			}
		}

		/// <summary>パス終端のメンバーを検査し、必要なら診断を報告する</summary>
		private void AnalyzeFinalMember(ITypeSymbol targetType, string memberName, XElement element, XAttribute attribute, bool isSetter)
		{
			// プロパティ/フィールドが存在する場合はそちらが優先して解決される (ICommand プロパティなど)
			if (FindPropertyOrFieldType(targetType, memberName) != null)
				return;

			// オーバーライド・隠蔽の重複を除くため、シグネチャ (引数型の並び) で重複排除する
			var signatures = TypeHierarchy(targetType)
				.SelectMany(t => t.GetMembers(memberName))
				.OfType<IMethodSymbol>()
				.Where(m => m.MethodKind == MethodKind.Ordinary && IsAccessible(m))
				.GroupBy(m => string.Join(",", m.Parameters.Select(p => p.Type.ToDisplayString())), StringComparer.Ordinal)
				.Select(g => g.First())
				.ToArray();

			if (signatures.Length == 0)
				return;
			if (signatures.Length > 1)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					OverloadRule, CreateLocation(attribute),
					targetType.Name, memberName, signatures.Length));
				return;
			}

			// 唯一のメソッドが値型 1引数の場合、CommandParameter 未指定 (null) や
			// 文字列リテラル (unbox 不可) は実行時に必ずクラッシュする
			var method = signatures[0];
			if (method.Parameters.Length != 1 || isSetter)
				return; // Setter 形式は CommandParameter が適用先コントロール側にあるため検査しない
			var parameterType = method.Parameters[0].Type;
			if (!parameterType.IsValueType || parameterType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
				return;
			var commandParameter = element.Attribute("CommandParameter");
			if (commandParameter != null && commandParameter.Value.StartsWith("{", StringComparison.Ordinal))
				return; // バインディング等の供給値は静的に判定できないため対象外
			context.ReportDiagnostic(Diagnostic.Create(
				ValueTypeParameterRule, CreateLocation(attribute),
				targetType.Name, memberName, parameterType.ToDisplayString()));
		}

		/// <summary>バインディング式の本体からパス部分を取り出す</summary>
		private static string? ExtractPath(string body)
		{
			var parts = SplitTopLevel(body.Trim(), ',');
			foreach (var part in parts)
			{
				var trimmed = part.Trim();
				if (trimmed.StartsWith("Path=", StringComparison.Ordinal))
					return trimmed.Substring("Path=".Length).Trim();
			}
			var first = parts.Count > 0 ? parts[0].Trim() : "";
			// 先頭要素が名前付きパラメータならパス指定なし
			return first.IndexOf('=') >= 0 ? null : first;
		}

		/// <summary>括弧の外にある区切り文字でのみ分割する</summary>
		private static List<string> SplitTopLevel(string value, char separator)
		{
			var result = new List<string>();
			var depth = 0;
			var start = 0;
			for (var i = 0; i < value.Length; i++)
			{
				var c = value[i];
				if (c is '(' or '[' or '{')
					depth++;
				else if (c is ')' or ']' or '}')
					depth--;
				else if (c == separator && depth == 0)
				{
					result.Add(value.Substring(start, i - start));
					start = i + 1;
				}
			}
			result.Add(value.Substring(start));
			return result;
		}

		/// <summary>$parent[Type;level] 構文が指す祖先要素を XML ツリーから探す</summary>
		private static XElement? FindParentElement(XElement element, string segment)
		{
			string? typeName = null;
			var level = 0;
			var bracket = segment.IndexOf('[');
			if (bracket >= 0)
			{
				var inner = segment.Substring(bracket + 1).TrimEnd(']');
				var semicolon = inner.IndexOf(';');
				if (semicolon >= 0)
				{
					int.TryParse(inner.Substring(semicolon + 1), out level);
					inner = inner.Substring(0, semicolon);
				}
				var colon = inner.IndexOf(':');
				typeName = colon >= 0 ? inner.Substring(colon + 1) : inner;
			}

			var count = 0;
			for (var parent = element.Parent; parent != null; parent = parent.Parent)
			{
				if (typeName != null && parent.Name.LocalName != typeName)
					continue;
				if (count == level)
					return parent;
				count++;
			}
			return null;
		}

		/// <summary>要素自身の CLR 型を解決する (x:Class または名前空間プレフィックス付きタグ)</summary>
		private INamedTypeSymbol? ResolveElementType(XElement element)
		{
			var xClass = (string?)element.Attribute(XamlNs + "Class");
			if (xClass != null)
				return context.Compilation.GetTypeByMetadataName(xClass);
			var clrNamespace = ToClrNamespace(element.Name.Namespace.NamespaceName);
			return clrNamespace == null ? null : context.Compilation.GetTypeByMetadataName(clrNamespace + "." + element.Name.LocalName);
		}

		/// <summary>"prefix:TypeName" 形式の型指定をシンボルへ解決する</summary>
		private INamedTypeSymbol? ResolvePrefixedType(XElement element, string value)
		{
			value = value.Trim();
			if (value.Length == 0 || value[0] == '{')
				return null; // {x:Type ...} などのマークアップ拡張は対象外
			var prefix = "";
			var name = value;
			var colon = value.IndexOf(':');
			if (colon >= 0)
			{
				prefix = value.Substring(0, colon);
				name = value.Substring(colon + 1);
			}
			var clrNamespace = ToClrNamespace(element.GetNamespaceOfPrefix(prefix)?.NamespaceName);
			return clrNamespace == null ? null : context.Compilation.GetTypeByMetadataName(clrNamespace + "." + name);
		}

		private static string? ToClrNamespace(string? xmlNamespace)
		{
			if (xmlNamespace == null)
				return null;
			if (xmlNamespace.StartsWith("using:", StringComparison.Ordinal))
				return xmlNamespace.Substring("using:".Length);
			if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
			{
				var clrNamespace = xmlNamespace.Substring("clr-namespace:".Length);
				var semicolon = clrNamespace.IndexOf(';');
				return semicolon >= 0 ? clrNamespace.Substring(0, semicolon) : clrNamespace;
			}
			return null; // Avalonia 既定名前空間などは対象外
		}

		private static ITypeSymbol? FindPropertyOrFieldType(ITypeSymbol type, string name)
		{
			foreach (var t in TypeHierarchy(type))
			{
				foreach (var member in t.GetMembers(name))
				{
					if (member is IPropertySymbol property && IsAccessible(property))
						return property.Type;
					if (member is IFieldSymbol field && IsAccessible(field))
						return field.Type;
				}
			}
			return null;
		}

		private static IEnumerable<ITypeSymbol> TypeHierarchy(ITypeSymbol type)
		{
			if (type.TypeKind == TypeKind.Interface)
			{
				yield return type;
				foreach (var i in type.AllInterfaces)
					yield return i;
				yield break;
			}
			for (ITypeSymbol? t = type; t != null; t = t.BaseType)
				yield return t;
		}

		private static bool IsAccessible(ISymbol symbol)
			=> symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

		private Location CreateLocation(XAttribute attribute)
		{
			if (attribute is IXmlLineInfo lineInfo && lineInfo.HasLineInfo() && lineInfo.LineNumber - 1 < source.Lines.Count)
			{
				var start = Math.Min(source.Lines[lineInfo.LineNumber - 1].Start + lineInfo.LinePosition - 1, source.Length);
				var length = Math.Min(attribute.Name.LocalName.Length + attribute.Value.Length + 3, source.Length - start);
				var span = new TextSpan(start, Math.Max(length, 0));
				return Location.Create(path, span, source.Lines.GetLinePositionSpan(span));
			}
			return Location.Create(path, default, default);
		}
	}
}
