using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core;

public enum ThemeType
{
	BuiltIn,
	ExternalFile,
	Temporary,
}
public record ThemeMeta(ThemeType Type, string Identifier)
{
	[JsonIgnore]
	public string DisplayName => Type switch
	{
		ThemeType.BuiltIn => "組み込みテーマ",
		ThemeType.ExternalFile => $"外部テーマ: {Identifier}",
		ThemeType.Temporary => "編集中のテーマ",
		_ => throw new ArgumentOutOfRangeException(nameof(Type), Type, null),
	};
}
