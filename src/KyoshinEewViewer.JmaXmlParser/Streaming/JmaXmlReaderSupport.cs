using System;
using System.ComponentModel;

namespace KyoshinEewViewer.JmaXmlParser.Streaming;

/// <summary>
/// ソースジェネレーターが生成するコードが共通で使う部品
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class JmaXmlReaderSupport
{
	public static InvalidOperationException NotSelected(string member)
		=> new($"{member} はこの読み取りの単位の対象に含まれていません");

	public static JmaXmlParseException Missing(string name)
		=> new($"{name}が存在しません");

	/// <summary>
	/// 次の子要素の開始タグを名前まで読む
	/// </summary>
	/// <param name="nameStart">親要素の名前の開始位置</param>
	/// <param name="nameLength">親要素の名前の長さ</param>
	/// <returns>親要素の終了タグに達した場合 false</returns>
	public static bool NextChild(ref XmlTokenizer tokenizer, int nameStart, int nameLength)
	{
		while (true)
		{
			if (!tokenizer.Read())
				throw tokenizer.NewFormatException("閉じられていない要素があります");
			switch (tokenizer.Kind)
			{
				case XmlTokenKind.StartElement:
					return true;
				case XmlTokenKind.EndElement:
					if (!tokenizer.Name.SequenceEqual(tokenizer.Slice(nameStart, nameLength)))
						throw tokenizer.NewFormatException("開始タグと終了タグの名前が一致しません");
					return false;
			}
		}
	}

	/// <summary>
	/// 開始タグを名前まで読んだ要素を、終了タグまで読み飛ばす
	/// </summary>
	public static void Skip(ref XmlTokenizer tokenizer)
	{
		tokenizer.SkipAttributes();
		if (!tokenizer.IsEmptyElement)
			tokenizer.SkipElementContent();
	}

	/// <summary>
	/// 属性を読み終えた要素を、終了タグまで読み飛ばす
	/// </summary>
	public static void SkipContent(ref XmlTokenizer tokenizer)
	{
		if (!tokenizer.IsEmptyElement)
			tokenizer.SkipElementContent();
	}

	/// <summary>
	/// 属性を読み終えた要素のテキストの範囲を読み、終了タグまで進める。中身が空の場合は長さ 0 の範囲を返す
	/// </summary>
	/// <returns>子要素を持つ場合 false</returns>
	public static bool TryLeafText(ref XmlTokenizer tokenizer, int nameStart, int nameLength, out int textStart, out int textLength)
	{
		textStart = textLength = 0;
		if (tokenizer.IsEmptyElement)
			return true;
		var found = true;
		var count = 0;
		while (true)
		{
			if (!tokenizer.Read())
				throw tokenizer.NewFormatException("閉じられていない要素があります");
			switch (tokenizer.Kind)
			{
				case XmlTokenKind.Text:
					if (count++ == 0)
					{
						textStart = tokenizer.TextStart;
						textLength = tokenizer.TextLength;
					}
					break;
				case XmlTokenKind.StartElement:
					found = false;
					Skip(ref tokenizer);
					break;
				case XmlTokenKind.EndElement:
					if (!tokenizer.Name.SequenceEqual(tokenizer.Slice(nameStart, nameLength)))
						throw tokenizer.NewFormatException("開始タグと終了タグの名前が一致しません");
					return found;
			}
		}
	}
}
