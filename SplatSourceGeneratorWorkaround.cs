// Splat.DependencyInjection.SourceGenerator 2.2.2 が生成するコードで
// namespace Splat 内から global:: なしに Microsoft.CodeAnalysis.Embedded を参照しているため、
// Splat.Microsoft.Extensions.Logging パッケージの Splat.Microsoft 名前空間と衝突する。
// この shim で Splat.Microsoft.CodeAnalysis.EmbeddedAttribute を定義し衝突を解消する。
namespace Splat.Microsoft.CodeAnalysis
{
	[global::System.AttributeUsage(
		global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct |
		global::System.AttributeTargets.Enum | global::System.AttributeTargets.Interface |
		global::System.AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
	[global::System.Diagnostics.Conditional("NEVER_DEFINED")]
	internal sealed class EmbeddedAttribute : global::System.Attribute
	{
	}
}
