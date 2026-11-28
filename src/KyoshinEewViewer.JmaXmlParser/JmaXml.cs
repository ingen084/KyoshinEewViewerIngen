using System;
using System.Buffers;
using System.IO;

namespace KyoshinEewViewer.JmaXmlParser;

/// <summary>
/// 気象庁防災情報XMLを読み取る
/// </summary>
public static class JmaXml
{
	/// <summary>
	/// XML を解析し、<typeparamref name="TView"/> に対して参照されているメンバーだけを読み取る
	/// </summary>
	/// <typeparam name="TView"><see cref="JmaXmlViewAttribute"/> を付けた読み取りの単位</typeparam>
	/// <param name="utf8Xml">UTF-8 の XML 全体</param>
	/// <exception cref="FormatException">XMLのフォーマットが正しくない</exception>
	/// <exception cref="JmaXmlParseException">ルートノードが Report ではない</exception>
	public static JmaXmlDocument<TView> Read<TView>(ReadOnlySpan<byte> utf8Xml) where TView : IJmaXmlView<TView>
		=> TView.Read(utf8Xml);

	/// <summary>
	/// ストリームを最後まで読み込み、XML を解析して <typeparamref name="TView"/> に対して参照されているメンバーだけを読み取る
	/// </summary>
	/// <typeparam name="TView"><see cref="JmaXmlViewAttribute"/> を付けた読み取りの単位</typeparam>
	/// <param name="utf8Xml">UTF-8 の XML 全体を返すストリーム</param>
	/// <exception cref="FormatException">XMLのフォーマットが正しくない</exception>
	/// <exception cref="JmaXmlParseException">ルートノードが Report ではない</exception>
	public static JmaXmlDocument<TView> Read<TView>(Stream utf8Xml) where TView : IJmaXmlView<TView>
	{
		var buffer = ArrayPool<byte>.Shared.Rent(utf8Xml.CanSeek ? checked((int)(utf8Xml.Length - utf8Xml.Position) + 1) : 16 * 1024);
		try
		{
			var length = 0;
			while (true)
			{
				if (length == buffer.Length)
				{
					var expanded = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
					buffer.AsSpan(0, length).CopyTo(expanded);
					ArrayPool<byte>.Shared.Return(buffer);
					buffer = expanded;
				}
				var read = utf8Xml.Read(buffer, length, buffer.Length - length);
				if (read == 0)
					break;
				length += read;
			}
			return TView.Read(buffer.AsSpan(0, length));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}

/// <summary>
/// 読み取りの単位。実装はソースジェネレーターが生成する
/// </summary>
public interface IJmaXmlView<TSelf> where TSelf : IJmaXmlView<TSelf>
{
	static abstract JmaXmlDocument<TSelf> Read(ReadOnlySpan<byte> utf8Xml);
}
