using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Text;

namespace KyoshinEewViewer.CsvSourceGenerator;

[Generator(LanguageNames.CSharp)]
public partial class CsvDictionaryGenerator : ISourceGenerator
{
	public static string GenerateClassFile(StringBuilder sb, string dictionaryName, string csvText, string keyType, string keyFormat, string valueType, string valueFormat)
	{
		using var reader = new StringReader(csvText);

		sb.AppendLine($"        public static System.Collections.Generic.IReadOnlyDictionary<{keyType}, {valueType}> {dictionaryName} {{ get; }} = new System.Collections.Generic.Dictionary<{keyType}, {valueType}>(){{");

		while (true)
		{
			var line = reader.ReadLine();
			if (line == null) break;
			var fields = line.Split(',');
			sb.AppendLine($"            {{ {string.Format(keyFormat, fields)}, {string.Format(valueFormat, fields)} }},");
		}

		sb.AppendLine("        };");

		return sb.ToString();
	}
	private static StringBuilder SourceFilesFromAdditionalFiles(IEnumerable<(AdditionalText file, string keyType, string keyFormat, string valueType, string valueFormat)> pathsData)
	{
		var sb = new StringBuilder();
		sb.AppendLine(@"
#nullable enable
namespace KyoshinEewViewer {
    public static class CsvDictionary {");
		foreach (var (file, keyType, keyFormat, valueType, valueFormat) in pathsData)
		{
			var className = Path.GetFileNameWithoutExtension(file.Path);
			var csvText = file.GetText()!.ToString();
			GenerateClassFile(sb, className, csvText, keyType, keyFormat, valueType, valueFormat);
		}
		sb.AppendLine("    }\r\n}");
		return sb;
	}

	private static IEnumerable<(AdditionalText, string, string, string, string)> GetLoadOptions(GeneratorExecutionContext context)
	{
		foreach (var file in context.AdditionalFiles)
		{
			if (!Path.GetExtension(file.Path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
				continue;

			context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.Additionalfiles.KeyType", out var keyType);
			context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.Additionalfiles.KeyFormat", out var keyFormat);
			context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.Additionalfiles.ValueType", out var valueType);
			context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.Additionalfiles.ValueFormat", out var valueFormat);

			if (keyType == null)
				throw new Exception("KeyType is not defined.");
			if (keyFormat == null)
				throw new Exception("KeyFormat is not defined.");

			if (valueType == null)
				throw new Exception("ValueType is not defined.");
			if (valueFormat == null)
				throw new Exception("ValueFormat is not defined.");

			yield return (file, keyType, keyFormat, valueType, valueFormat);
		}
	}

	public void Execute(GeneratorExecutionContext context)
		=> context.AddSource($"CsvDictionary.g.cs", SourceText.From(SourceFilesFromAdditionalFiles(GetLoadOptions(context)).ToString(), Encoding.UTF8));

	public void Initialize(GeneratorInitializationContext context)
	{
	}
}
