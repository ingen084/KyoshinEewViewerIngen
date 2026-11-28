using Microsoft.CodeAnalysis;
using System.Text;

namespace KyoshinEewViewer.CodeAnalysis.JmaXml;

internal static class JmaXmlNames
{
	public const string Namespace = "KyoshinEewViewer.JmaXmlParser";
	public const string TypeAttribute = Namespace + ".JmaXmlTypeAttribute";
	public const string RootAttribute = Namespace + ".JmaXmlRootAttribute";
	public const string MemberAttribute = Namespace + ".JmaXmlAttribute";
	public const string ViewAttribute = Namespace + ".JmaXmlViewAttribute";
	public const string LibraryAttribute = Namespace + ".JmaXmlTypeLibraryAttribute";
	public const string SelectedAttribute = Namespace + ".JmaXmlSelectedAttribute";

	public const string Support = "global::" + Namespace + ".Streaming.JmaXmlReaderSupport";
	public const string Tokenizer = "global::" + Namespace + ".Streaming.XmlTokenizer";
	public const string TokenKind = "global::" + Namespace + ".Streaming.XmlTokenKind";
	public const string ParseException = "global::" + Namespace + ".JmaXmlParseException";
	public const string XmlList = "global::" + Namespace + ".JmaXmlList";
	public const string View = "global::" + Namespace + ".IJmaXmlView";
	public const string List = "global::System.Collections.Generic.List";
	public const string Hidden = "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)] public ";

	public static bool HasAttribute(ISymbol symbol, string name)
	{
		foreach (var attribute in symbol.GetAttributes())
		{
			if (attribute.AttributeClass?.ToDisplayString() == name)
				return true;
		}
		return false;
	}

	public static string KeyOf(INamedTypeSymbol definition)
		=> definition.ContainingNamespace.IsGlobalNamespace ? definition.Name : definition.ContainingNamespace.ToDisplayString() + "." + definition.Name;

	public static INamedTypeSymbol? XmlTypeOf(ITypeSymbol type)
		=> type is INamedTypeSymbol { IsGenericType: true } named && HasAttribute(named.OriginalDefinition, TypeAttribute) ? named.OriginalDefinition : null;
}

internal sealed class ContainerModel(string name, bool required, string flag)
{
	public string Name { get; } = name;
	public bool Required { get; } = required;
	/// <summary>この要素があったことを記録するフィールドの名前</summary>
	public string Flag { get; } = flag;
}

internal sealed class PropModel
{
	public string Name = "";
	public string CsType = "";
	/// <summary>text / attr / obj / list / selftext / selfname</summary>
	public string Kind = "";
	/// <summary>str / int / float / dt / ndt</summary>
	public string Conv = "str";
	public string? Elem, Attr, TypeKey;
	/// <summary>一覧の各要素から読む子要素の名前 (<c>Kind*/Property!</c> の Property)</summary>
	public string? ItemChild;
	public bool Required, Nil, IsMethod;
	public List<ContainerModel> Containers = [];
	/// <summary>型の中でのメンバーの番号 (名前順)。読み取り対象かどうかのビットに使う</summary>
	public int Bit;
	/// <summary>経路や型の誤り</summary>
	public string? Error;
	public string LocationPath = "";
	public int LocationStart, LocationLength, StartLine, StartCharacter, EndLine, EndCharacter;

	public string Field => "__" + Name;
	public string HasField => "__" + Name + "_has";
	public string MissingItemField => "__" + Name + "_missing";
	public string Mask => "0x" + (1UL << Bit).ToString("X") + "UL";

	public static string Sanitize(string name)
	{
		var sb = new StringBuilder(name.Length);
		foreach (var c in name)
			sb.Append(char.IsLetterOrDigit(c) ? c : '_');
		return sb.ToString();
	}

	public string Signature => string.Join(",", Name, CsType, Kind, Conv, Elem, Attr, TypeKey, ItemChild, Required, Nil, IsMethod, Error,
		string.Join("/", Containers.Select(c => c.Name + (c.Required ? "!" : "?"))), LocationPath, LocationStart, LocationLength);

	public Location GetLocation() => LocationPath.Length == 0
		? Location.None
		: Location.Create(LocationPath, new Microsoft.CodeAnalysis.Text.TextSpan(LocationStart, LocationLength),
			new Microsoft.CodeAnalysis.Text.LinePositionSpan(new(StartLine, StartCharacter), new(EndLine, EndCharacter)));

	private static string ConvOf(ITypeSymbol type, out bool supported)
	{
		if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
			type = nullable.TypeArguments[0];
		supported = true;
		switch (type.SpecialType)
		{
			case SpecialType.System_Int32:
				return "int";
			case SpecialType.System_Single:
				return "float";
			case SpecialType.System_String:
				return "str";
		}
		if (type.Name == "DateTimeOffset" && type.ContainingNamespace?.ToDisplayString() == "System")
			return "dt";
		supported = false;
		return "str";
	}

	private bool SetConv(ITypeSymbol valueType)
	{
		Conv = ConvOf(valueType, out var supported);
		if (!supported)
			Error = "値の型は string / int / float / DateTimeOffset とその null 許容型のいずれかにしてください";
		return supported;
	}

	private static bool TrySplitFlag(string segment, out string name, out char flag)
	{
		name = "";
		flag = '\0';
		if (segment.Length < 2)
			return false;
		flag = segment[segment.Length - 1];
		name = segment.Substring(0, segment.Length - 1);
		return flag is '!' or '?' or '*';
	}

	public void ParsePath(string path, ITypeSymbol valueType, bool isNullable)
	{
		if (path == ".name")
		{
			Kind = "selfname";
			Required = true;
			return;
		}
		if (path == ".")
		{
			Kind = "selftext";
			Required = true;
			SetConv(valueType);
			return;
		}

		var segments = path.Split('/');
		var prefix = "";
		for (var i = 0; i < segments.Length; i++)
		{
			var segment = segments[i];
			var isLast = i == segments.Length - 1;
			var at = segment.IndexOf('@');
			if (at >= 0)
			{
				if (!isLast || !TrySplitFlag(segment.Substring(at + 1), out var attribute, out var attributeFlag) || attributeFlag == '*')
				{
					Error = "属性は経路の最後に @名前! か @名前? の形で書いてください";
					return;
				}
				if (at > 0)
				{
					if (!TrySplitFlag(segment.Substring(0, at), out var owner, out var ownerFlag) || ownerFlag == '*')
					{
						Error = "属性を持つ要素は 名前! か 名前? の形で書いてください";
						return;
					}
					prefix += "_" + Sanitize(owner);
					Containers.Add(new(owner, ownerFlag == '!', "__has" + prefix));
					Elem = owner;
				}
				else if (i > 0)
				{
					Error = "属性の前に要素名が必要です";
					return;
				}
				Kind = "attr";
				Attr = attribute;
				Required = attributeFlag == '!';
				SetConv(valueType);
				break;
			}

			if (!TrySplitFlag(segment, out var name, out var flag))
			{
				Error = "要素名の後ろに ! (必須) / ? (任意) / * (一覧) のいずれかを付けてください";
				return;
			}

			if (!isLast)
			{
				if (flag == '*')
				{
					// 一覧の各要素の子を読む形。次の区切りが最後でなければならない
					if (i != segments.Length - 2 || !TrySplitFlag(segments[i + 1], out var child, out var childFlag) || childFlag == '*' || segments[i + 1].Contains("@"))
					{
						Error = "一覧 (*) の後ろに書けるのは、各要素から読む子要素 1 つだけです";
						return;
					}
					Kind = "list";
					Elem = name;
					ItemChild = child;
					Required = childFlag == '!';
					break;
				}
				prefix += "_" + Sanitize(name);
				Containers.Add(new(name, flag == '!', "__has" + prefix));
				continue;
			}

			Elem = name;
			if (flag == '*')
			{
				Kind = "list";
				break;
			}
			Required = flag == '!';
			if (JmaXmlNames.XmlTypeOf(valueType.WithNullableAnnotation(NullableAnnotation.None)) is { } xmlType)
			{
				Kind = "obj";
				TypeKey = JmaXmlNames.KeyOf(xmlType);
			}
			else
			{
				Kind = "text";
				if (SetConv(valueType) && Nil)
				{
					if (Conv != "dt")
					{
						Error = "Nil を指定できるのは DateTimeOffset? だけです";
						return;
					}
					Conv = "ndt";
				}
			}
		}

		if (Kind == "list")
		{
			if (valueType is INamedTypeSymbol { Name: "JmaXmlList", TypeArguments.Length: 1 } list && JmaXmlNames.XmlTypeOf(list.TypeArguments[0]) is { } element)
				TypeKey = JmaXmlNames.KeyOf(element);
			else
				Error = "一覧の型は JmaXmlList<[JmaXmlType] の型> にしてください";
			return;
		}

		if (Error != null || IsMethod)
			return;
		var mayBeMissing = !Required || Containers.Any(c => !c.Required) || Conv == "ndt";
		if (mayBeMissing && !isNullable)
			Error = "任意の要素や属性を含む経路のメンバーは、null 許容型にしてください";
	}
}

internal sealed class TypeModel : IEquatable<TypeModel>
{
	public string Key { get; }
	public string Namespace { get; }
	public string Name { get; }
	public string TypeParameter { get; }
	public string? RootElement { get; }
	public List<PropModel> Props { get; }
	public string? Error { get; }
	private string Signature { get; }

	public TypeModel(string key, string ns, string name, string typeParameter, string? rootElement, List<PropModel> props, string? error)
	{
		Key = key;
		Namespace = ns;
		Name = name;
		TypeParameter = typeParameter;
		RootElement = rootElement;
		Props = props;
		Error = error ?? (props.Count > 64 ? "メンバーが 64 個を超える型には対応していません" : null);
		var bit = 0;
		foreach (var p in props.OrderBy(p => p.Name, StringComparer.Ordinal))
			p.Bit = bit++;
		Signature = key + ";" + typeParameter + ";" + rootElement + ";" + Error + ";" + string.Join(";", props.Select(p => p.Signature));
	}

	public bool Equals(TypeModel? other) => other != null && Signature == other.Signature;
	public override bool Equals(object? obj) => Equals(obj as TypeModel);
	public override int GetHashCode() => Signature.GetHashCode();

	public static TypeModel Create(INamedTypeSymbol type)
	{
		string? rootElement = null;
		string? typeError = type.TypeParameters.Length == 1 ? null : "[JmaXmlType] の型は、型引数を 1 つだけ持つ必要があります";
		foreach (var attribute in type.GetAttributes())
		{
			var name = attribute.AttributeClass?.ToDisplayString();
			if (name == JmaXmlNames.RootAttribute && attribute.ConstructorArguments.Length == 1)
				rootElement = attribute.ConstructorArguments[0].Value as string;
			// 読み取りコードの生成では、属性を定義しているアセンブリの型だけを探す
			else if (name == JmaXmlNames.TypeAttribute && !SymbolEqualityComparer.Default.Equals(attribute.AttributeClass!.ContainingAssembly, type.ContainingAssembly))
				typeError = "[JmaXmlType] の型は、JmaXmlTypeAttribute と同じアセンブリに定義してください";
		}

		var props = new List<PropModel>();
		foreach (var member in type.GetMembers())
		{
			AttributeData? attribute = null;
			foreach (var a in member.GetAttributes())
			{
				if (a.AttributeClass?.ToDisplayString() == JmaXmlNames.MemberAttribute)
					attribute = a;
			}
			if (attribute == null || attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not string path)
				continue;

			var prop = new PropModel { Name = member.Name };
			foreach (var named in attribute.NamedArguments)
			{
				if (named.Key == "Nil" && named.Value.Value is true)
					prop.Nil = true;
			}
			if (member.Locations.FirstOrDefault(l => l.IsInSource) is { } location)
			{
				var line = location.GetLineSpan();
				prop.LocationPath = line.Path;
				prop.LocationStart = location.SourceSpan.Start;
				prop.LocationLength = location.SourceSpan.Length;
				prop.StartLine = line.StartLinePosition.Line;
				prop.StartCharacter = line.StartLinePosition.Character;
				prop.EndLine = line.EndLinePosition.Line;
				prop.EndCharacter = line.EndLinePosition.Character;
			}

			ITypeSymbol valueType;
			var isNullable = false;
			if (member is IPropertySymbol property)
			{
				valueType = property.Type;
				prop.CsType = property.Type.ToDisplayString(JmaXmlGenerator.TypeFormat);
				isNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated
					|| property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
			}
			else if (member is IMethodSymbol { Parameters.Length: 1, ReturnType.SpecialType: SpecialType.System_Boolean } method && method.Parameters[0].RefKind == RefKind.Out)
			{
				valueType = method.Parameters[0].Type;
				prop.IsMethod = true;
				prop.CsType = valueType.ToDisplayString(JmaXmlGenerator.TypeFormat);
			}
			else
			{
				prop.Error = "[JmaXml] を付けられるのは、partial プロパティと bool TryGetXxx(out T value) の形の partial メソッドです";
				props.Add(prop);
				continue;
			}

			prop.ParsePath(path, valueType, isNullable);
			if (prop.IsMethod && prop.Error == null && (prop.Kind is not ("text" or "attr" or "selftext") || prop.Conv is not ("int" or "float")))
				prop.Error = "TryGetXxx の形のメソッドで読めるのは int と float の値です";
			props.Add(prop);
		}

		return new TypeModel(
			JmaXmlNames.KeyOf(type),
			type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString(),
			type.Name,
			type.TypeParameters.Length == 1 ? type.TypeParameters[0].Name : "T",
			rootElement,
			props,
			typeError);
	}
}

internal sealed class TypeSet : IEquatable<TypeSet>
{
	public static TypeSet Empty { get; } = new([]);

	public Dictionary<string, TypeModel> Types { get; } = new(StringComparer.Ordinal);
	/// <summary>意味解析の前に、候補でないメンバー参照を名前だけで除くための一覧</summary>
	public HashSet<string> MemberNames { get; } = new(StringComparer.Ordinal);
	private string Signature { get; }

	public TypeSet(IEnumerable<TypeModel> types)
	{
		var sb = new StringBuilder();
		foreach (var type in types.OrderBy(t => t.Key, StringComparer.Ordinal))
		{
			Types[type.Key] = type;
			foreach (var p in type.Props)
				MemberNames.Add(p.Name);
			sb.Append(type.GetHashCode()).Append('/').Append(type.Key).Append('\n');
		}
		Signature = sb.ToString();
	}

	public bool Equals(TypeSet? other) => other != null && Signature == other.Signature && Types.All(p => other.Types.TryGetValue(p.Key, out var o) && p.Value.Equals(o));
	public override bool Equals(object? obj) => Equals(obj as TypeSet);
	public override int GetHashCode() => Signature.GetHashCode();
}

internal sealed class ViewModel(string ns, string name, string fullName, bool isSupported) : IEquatable<ViewModel>
{
	public string Namespace { get; } = ns;
	public string Name { get; } = name;
	public string FullName { get; } = fullName;
	/// <summary>名前空間直下の partial 構造体か</summary>
	public bool IsSupported { get; } = isSupported;

	public bool Equals(ViewModel? other) => other != null && FullName == other.FullName && IsSupported == other.IsSupported;
	public override bool Equals(object? obj) => Equals(obj as ViewModel);
	public override int GetHashCode() => FullName.GetHashCode();
}
