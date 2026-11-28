using System;
using System.Globalization;
using System.Text;

namespace KyoshinEewViewer.JmaXmlParser.Streaming;

/// <summary>
/// ハンドラーが受け取った UTF-8 の値を変換する
/// </summary>
public static class Utf8ValueExtensions
{
	public static string ToUtf16(this ReadOnlySpan<byte> value)
		=> Encoding.UTF8.GetString(value);

	public static bool TryToInt32(this ReadOnlySpan<byte> value, out int result)
		=> int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);

	public static bool TryToFloat32(this ReadOnlySpan<byte> value, out float result)
		=> float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

	public static bool TryToDateTimeOffset(this ReadOnlySpan<byte> value, out DateTimeOffset result)
	{
		Span<char> chars = stackalloc char[64];
		if (value.Length > chars.Length || !Encoding.UTF8.TryGetChars(value, chars, out var written))
		{
			result = default;
			return false;
		}
		return DateTimeOffset.TryParse(chars[..written], CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
	}
}
