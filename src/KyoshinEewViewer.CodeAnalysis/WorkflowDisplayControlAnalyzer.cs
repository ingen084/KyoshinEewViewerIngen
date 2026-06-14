using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace KyoshinEewViewer.CodeAnalysis;

/// <summary>
/// WorkflowTrigger / WorkflowAction を継承する型で、Control を返すプロパティ (DisplayControl 等) に
/// [JsonIgnore] が付与されていない場合にコンパイルエラーを出すアナライザ。
///
/// これらのプロパティが JSON シリアライズの型グラフに含まれると、トリミング (ILLink) によって
/// Avalonia.Input.Cursor などの依存型のコンストラクタ引数名が削除され、System.Text.Json が
/// NotSupportedException をスローして workflows.json の読み込みが丸ごと失敗する。
/// 基底の abstract 宣言に [JsonIgnore] があっても override 側には効かないため、override 側への
/// 付与を強制する。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WorkflowDisplayControlAnalyzer : DiagnosticAnalyzer
{
	public const string DiagnosticId = "KEVI001";

	private static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"ワークフローの Control プロパティには [JsonIgnore] が必要",
		"{0}.{1} は Control を返すため [JsonIgnore] を付与してください。付与しないと workflows.json の型グラフに Avalonia の Control が混入し、トリミング(リリース)ビルドでワークフローの読み込みが失敗します",
		"Serialization",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "WorkflowTrigger / WorkflowAction を継承する型の Control 返却プロパティに [JsonIgnore] が無いと、リリース(トリム)ビルドでワークフロー全体の読み込みが NotSupportedException で失敗します。");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static compilationContext =>
		{
			var triggerBase = compilationContext.Compilation.GetTypeByMetadataName("KyoshinEewViewer.Services.Workflows.WorkflowTrigger");
			var actionBase = compilationContext.Compilation.GetTypeByMetadataName("KyoshinEewViewer.Services.Workflows.WorkflowAction");
			var controlType = compilationContext.Compilation.GetTypeByMetadataName("Avalonia.Controls.Control");
			var jsonIgnore = compilationContext.Compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonIgnoreAttribute");

			// 対象アセンブリでなければ何もしない
			if ((triggerBase is null && actionBase is null) || controlType is null || jsonIgnore is null)
				return;

			compilationContext.RegisterSymbolAction(symbolContext =>
			{
				var prop = (IPropertySymbol)symbolContext.Symbol;

				// 基底の abstract 宣言自体は対象外 (override 側に付ける必要があるため)
				if (prop.IsAbstract)
					return;

				// Control (またはその派生) を返すプロパティのみ対象
				if (!IsAssignableTo(prop.Type, controlType))
					return;

				// WorkflowTrigger / WorkflowAction を継承する型のメンバーのみ対象
				if (!InheritsFrom(prop.ContainingType, triggerBase) && !InheritsFrom(prop.ContainingType, actionBase))
					return;

				// このプロパティ宣言自身に [JsonIgnore] があれば OK
				if (prop.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, jsonIgnore)))
					return;

				foreach (var location in prop.Locations.Where(l => l.IsInSource))
					symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, location, prop.ContainingType.Name, prop.Name));
			}, SymbolKind.Property);
		});
	}

	private static bool IsAssignableTo(ITypeSymbol? type, INamedTypeSymbol target)
	{
		for (var t = type; t is not null; t = t.BaseType)
			if (SymbolEqualityComparer.Default.Equals(t, target))
				return true;
		return false;
	}

	private static bool InheritsFrom(ITypeSymbol? type, INamedTypeSymbol? baseType)
	{
		if (baseType is null)
			return false;
		for (var t = type; t is not null; t = t.BaseType)
			if (SymbolEqualityComparer.Default.Equals(t, baseType))
				return true;
		return false;
	}
}
