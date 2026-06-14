using Avalonia.Data.Converters;
using LiveMarkdown.Avalonia;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Converters;

/// <summary>
/// 文字列を LiveMarkdown.Avalonia の <see cref="ObservableStringBuilder"/> に変換する
/// MarkdownRenderer.MarkdownBuilder バインディング用
/// </summary>
public class StringToObservableStringBuilderConverter : IValueConverter
{
	public static readonly StringToObservableStringBuilderConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var builder = new ObservableStringBuilder();
		if (value is string text && !string.IsNullOrEmpty(text))
			builder.Append(text);
		return builder;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
