using System;
using System.Buffers;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace KyoshinEewViewer.JmaXmlParser.Streaming;

[EditorBrowsable(EditorBrowsableState.Never)]
public enum XmlTokenKind
{
	StartElement,
	EndElement,
	Text,
}

/// <summary>
/// UTF-8 の XML を先頭から順に切り出すトークナイザー<br/>
/// 実体参照は解決せず、名前空間接頭辞は名前の一部として扱う。ソースジェネレーターが生成する読み取りコードから使う
/// </summary>
/// <remarks>
/// タグの区切りは64バイトのブロックごとにビットマスクへ変換して探す。
/// 要素名や要素間の空白は十数バイトと短く、区切りを探すたびに検索を呼び出すより、1ブロック分をまとめて比較してビットを順にたどる方が速い
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct XmlTokenizer
{
	private const int BlockShift = 6;
	private const int BlockMask = (1 << BlockShift) - 1;

	private static readonly SearchValues<byte> AttributeNameTerminators = SearchValues.Create("= \t\r\n/>"u8);

	private readonly ReadOnlySpan<byte> _data;
	private int _position;
	private int _block = -1;
	private ulong _openMask;
	private ulong _closeMask;
	private ulong _nameEndMask;
	private int _nameStart;
	private int _nameLength;
	private int _textStart;
	private int _textLength;

	public XmlTokenizer(ReadOnlySpan<byte> data)
	{
		_data = data;
		_position = data.StartsWith("﻿"u8) ? 3 : 0;
	}

	/// <summary>
	/// 次に読む位置。読み終えた開始タグの <c>&lt;</c> の位置を設定して <see cref="Read"/> を呼ぶと、その要素を読み直せる
	/// </summary>
	public int Position
	{
		readonly get => _position;
		set => _position = value;
	}

	/// <summary>
	/// 直前の <see cref="ReadAttribute"/> で読んだ属性値の開始位置
	/// </summary>
	public int AttributeValueStart { get; private set; }

	public XmlTokenKind Kind { get; private set; }

	/// <summary>
	/// 開始タグが空要素 (<c>&lt;a/&gt;</c>) かどうか。属性を読み終えた後に確定する
	/// </summary>
	public bool IsEmptyElement { get; private set; }

	public bool IsCData { get; private set; }

	public readonly int NameStart => _nameStart;
	public readonly int NameLength => _nameLength;
	public readonly int TextStart => _textStart;
	public readonly int TextLength => _textLength;

	public readonly ReadOnlySpan<byte> Name => _data.Slice(_nameStart, _nameLength);

	public readonly ReadOnlySpan<byte> Slice(int start, int length) => _data.Slice(start, length);

	/// <summary>
	/// 次のトークンを読む。<see cref="XmlTokenKind.StartElement"/> を返した後は、次の <see cref="Read"/> の前に
	/// <see cref="ReadAttribute"/> が false を返すまで読むか <see cref="SkipAttributes"/> を呼ぶ必要がある
	/// </summary>
	/// <returns>終端に達した場合 false</returns>
	public bool Read()
	{
		var data = _data;
		while (true)
		{
			if (_position >= data.Length)
				return false;

			if (data[_position] != (byte)'<')
			{
				var next = FindNext(_position, Delimiter.Open);
				var start = _position;
				var end = next < 0 ? data.Length : next;
				_position = end;
				while (start < end && IsWhiteSpace(data[start]))
					start++;
				while (end > start && IsWhiteSpace(data[end - 1]))
					end--;
				if (start == end)
					continue;
				_textStart = start;
				_textLength = end - start;
				IsCData = false;
				Kind = XmlTokenKind.Text;
				return true;
			}

			if (_position + 1 >= data.Length)
				throw NewFormatException("タグが閉じられていません");

			switch (data[_position + 1])
			{
				case (byte)'/':
					{
						var nameStart = _position + 2;
						var nameEnd = FindNext(nameStart, Delimiter.Close);
						if (nameEnd < 0)
							throw NewFormatException("終了タグが閉じられていません");
						_position = nameEnd + 1;
						while (nameEnd > nameStart && IsWhiteSpace(data[nameEnd - 1]))
							nameEnd--;
						_nameStart = nameStart;
						_nameLength = nameEnd - nameStart;
						Kind = XmlTokenKind.EndElement;
						return true;
					}
				case (byte)'?':
					SkipPast("?>"u8, 2, "処理命令が閉じられていません");
					continue;
				case (byte)'!':
					if (ReadMarkupDeclaration())
						return true;
					continue;
				default:
					{
						var nameStart = _position + 1;
						var nameEnd = FindNext(nameStart, Delimiter.NameEnd);
						if (nameEnd < 0)
							throw NewFormatException("開始タグが閉じられていません");
						if (nameEnd == nameStart)
							throw NewFormatException("要素名がありません");
						_nameStart = nameStart;
						_nameLength = nameEnd - nameStart;
						_position = nameEnd;
						IsEmptyElement = false;
						Kind = XmlTokenKind.StartElement;
						return true;
					}
			}
		}
	}

	/// <summary>
	/// 開始タグの属性を1つ読む。属性がなくなった時点でタグの終端まで読み進めて false を返す
	/// </summary>
	public bool ReadAttribute(out ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
	{
		var data = _data;
		SkipWhiteSpace();
		if (_position >= data.Length)
			throw NewFormatException("開始タグが閉じられていません");

		var c = data[_position];
		if (c == (byte)'>')
		{
			_position++;
			name = value = default;
			return false;
		}
		if (c == (byte)'/')
		{
			if (_position + 1 >= data.Length || data[_position + 1] != (byte)'>')
				throw NewFormatException("開始タグが正しく閉じられていません");
			_position += 2;
			IsEmptyElement = true;
			name = value = default;
			return false;
		}

		var nameLength = data[_position..].IndexOfAny(AttributeNameTerminators);
		if (nameLength <= 0)
			throw NewFormatException("属性名が正しくありません");
		name = data.Slice(_position, nameLength);
		_position += nameLength;

		SkipWhiteSpace();
		if (_position >= data.Length || data[_position] != (byte)'=')
			throw NewFormatException("属性に = がありません");
		_position++;
		SkipWhiteSpace();
		if (_position >= data.Length)
			throw NewFormatException("属性値がありません");

		var quote = data[_position];
		if (quote is not ((byte)'"' or (byte)'\''))
			throw NewFormatException("属性値が引用符で囲まれていません");
		_position++;
		AttributeValueStart = _position;
		var valueLength = data[_position..].IndexOf(quote);
		if (valueLength < 0)
			throw NewFormatException("属性値が閉じられていません");
		value = data.Slice(_position, valueLength);
		_position += valueLength + 1;
		return true;
	}

	public void SkipAttributes()
	{
		if (_position < _data.Length && _data[_position] == (byte)'>')
		{
			_position++;
			return;
		}
		while (ReadAttribute(out _, out _))
		{
		}
	}

	/// <summary>
	/// 読み終えた開始タグに対応する終了タグまで、中身をトークンに分けずに読み飛ばす<br/>
	/// 中身の開始タグと終了タグは数だけを数え、名前の対応は検査しない
	/// </summary>
	public void SkipElementContent()
	{
		var data = _data;
		var depth = 1;
		var position = _position;
		while (true)
		{
			position = FindNext(position, Delimiter.Open);
			if (position < 0 || position + 1 >= data.Length)
			{
				_position = data.Length;
				throw NewFormatException("閉じられていない要素があります");
			}

			switch (data[position + 1])
			{
				case (byte)'/':
					{
						var close = FindNext(position + 2, Delimiter.Close);
						if (close < 0)
						{
							_position = position;
							throw NewFormatException("終了タグが閉じられていません");
						}
						position = close + 1;
						if (--depth == 0)
						{
							_position = position;
							return;
						}
						break;
					}
				case (byte)'?':
					_position = position;
					SkipPast("?>"u8, 2, "処理命令が閉じられていません");
					position = _position;
					break;
				case (byte)'!':
					_position = position;
					ReadMarkupDeclaration();
					position = _position;
					break;
				default:
					{
						var nameEnd = FindNext(position + 1, Delimiter.NameEnd);
						_position = position;
						if (nameEnd < 0)
							throw NewFormatException("開始タグが閉じられていません");
						if (nameEnd == position + 1)
							throw NewFormatException("要素名がありません");
						if (data[nameEnd] == (byte)'>')
						{
							position = nameEnd + 1;
							depth++;
							break;
						}
						_position = nameEnd;
						IsEmptyElement = false;
						while (ReadAttribute(out _, out _))
						{
						}
						position = _position;
						if (!IsEmptyElement)
							depth++;
						break;
					}
			}
		}
	}

	private enum Delimiter
	{
		/// <summary><c>&lt;</c></summary>
		Open,
		/// <summary><c>&gt;</c></summary>
		Close,
		/// <summary>要素名の終わり (空白、<c>/</c>、<c>&gt;</c>)</summary>
		NameEnd,
	}

	/// <summary>
	/// <paramref name="position"/> 以降で最初に現れる区切りの位置を返す
	/// </summary>
	/// <returns>見つからない場合 -1</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int FindNext(int position, Delimiter delimiter)
	{
		if ((position >> BlockShift) == _block)
		{
			var mask = SelectMask(delimiter) >> (position & BlockMask);
			if (mask != 0)
				return position + BitOperations.TrailingZeroCount(mask);
		}
		return FindNextAcrossBlocks(position, delimiter);
	}

	private int FindNextAcrossBlocks(int position, Delimiter delimiter)
	{
		while (position < _data.Length)
		{
			var block = position >> BlockShift;
			if (block != _block)
				LoadBlock(block);
			var mask = SelectMask(delimiter) >> (position & BlockMask);
			if (mask != 0)
				return position + BitOperations.TrailingZeroCount(mask);
			position = (block + 1) << BlockShift;
		}
		return -1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private readonly ulong SelectMask(Delimiter delimiter) => delimiter switch
	{
		Delimiter.Open => _openMask,
		Delimiter.Close => _closeMask,
		_ => _nameEndMask,
	};

	private void LoadBlock(int block)
	{
		_block = block;
		var data = _data;
		var start = block << BlockShift;
		if (start + (1 << BlockShift) > data.Length)
		{
			ulong open = 0, close = 0, nameEnd = 0;
			for (var i = start; i < data.Length; i++)
			{
				var bit = 1UL << (i - start);
				var c = data[i];
				if (c == (byte)'<')
					open |= bit;
				else if (c == (byte)'>')
					close |= bit;
				if (c == (byte)'>' || c == (byte)'/' || IsWhiteSpace(c))
					nameEnd |= bit;
			}
			_openMask = open;
			_closeMask = close;
			_nameEndMask = nameEnd;
			return;
		}

		ref var head = ref Unsafe.Add(ref MemoryMarshal.GetReference(data), start);
		if (Vector512.IsHardwareAccelerated)
		{
			var v = Vector512.LoadUnsafe(ref head);
			var close = Vector512.Equals(v, Vector512.Create((byte)'>'));
			_openMask = Vector512.Equals(v, Vector512.Create((byte)'<')).ExtractMostSignificantBits();
			_closeMask = close.ExtractMostSignificantBits();
			_nameEndMask = (close
				| Vector512.Equals(v, Vector512.Create((byte)'/'))
				| Vector512.Equals(v, Vector512.Create((byte)' '))
				| Vector512.Equals(v, Vector512.Create((byte)'\t'))
				| Vector512.Equals(v, Vector512.Create((byte)'\r'))
				| Vector512.Equals(v, Vector512.Create((byte)'\n'))).ExtractMostSignificantBits();
			return;
		}

		LoadHalfBlock(Vector256.LoadUnsafe(ref head), out var lowerOpen, out var lowerClose, out var lowerNameEnd);
		LoadHalfBlock(Vector256.LoadUnsafe(ref head, (nuint)Vector256<byte>.Count), out var upperOpen, out var upperClose, out var upperNameEnd);
		_openMask = lowerOpen | ((ulong)upperOpen << Vector256<byte>.Count);
		_closeMask = lowerClose | ((ulong)upperClose << Vector256<byte>.Count);
		_nameEndMask = lowerNameEnd | ((ulong)upperNameEnd << Vector256<byte>.Count);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void LoadHalfBlock(Vector256<byte> v, out uint open, out uint close, out uint nameEnd)
	{
		var closeVector = Vector256.Equals(v, Vector256.Create((byte)'>'));
		open = Vector256.Equals(v, Vector256.Create((byte)'<')).ExtractMostSignificantBits();
		close = closeVector.ExtractMostSignificantBits();
		nameEnd = (closeVector
			| Vector256.Equals(v, Vector256.Create((byte)'/'))
			| Vector256.Equals(v, Vector256.Create((byte)' '))
			| Vector256.Equals(v, Vector256.Create((byte)'\t'))
			| Vector256.Equals(v, Vector256.Create((byte)'\r'))
			| Vector256.Equals(v, Vector256.Create((byte)'\n'))).ExtractMostSignificantBits();
	}

	/// <summary>
	/// <c>&lt;!</c> で始まる構造を読む
	/// </summary>
	/// <returns>CDATA セクションをテキストとして読んだ場合 true</returns>
	private bool ReadMarkupDeclaration()
	{
		var rest = _data[_position..];
		if (rest.StartsWith("<!--"u8))
		{
			SkipPast("-->"u8, 4, "コメントが閉じられていません");
			return false;
		}
		if (rest.StartsWith("<![CDATA["u8))
		{
			var start = _position + 9;
			var rel = _data[start..].IndexOf("]]>"u8);
			if (rel < 0)
				throw NewFormatException("CDATA セクションが閉じられていません");
			_textStart = start;
			_textLength = rel;
			_position = start + rel + 3;
			IsCData = true;
			Kind = XmlTokenKind.Text;
			return true;
		}

		var close = rest.IndexOf((byte)'>');
		if (close < 0)
			throw NewFormatException("宣言が閉じられていません");
		var subset = rest[..close].IndexOf((byte)'[');
		if (subset >= 0)
		{
			var subsetEnd = rest[subset..].IndexOf("]"u8);
			close = subsetEnd < 0 ? -1 : rest[(subset + subsetEnd)..].IndexOf((byte)'>');
			if (close < 0)
				throw NewFormatException("文書型宣言が閉じられていません");
			close += subset + subsetEnd;
		}
		_position += close + 1;
		return false;
	}

	private void SkipPast(ReadOnlySpan<byte> terminator, int offset, string message)
	{
		var start = _position + offset;
		var rel = start > _data.Length ? -1 : _data[start..].IndexOf(terminator);
		if (rel < 0)
			throw NewFormatException(message);
		_position = start + rel + terminator.Length;
	}

	private void SkipWhiteSpace()
	{
		var data = _data;
		while (_position < data.Length && IsWhiteSpace(data[_position]))
			_position++;
	}

	private static bool IsWhiteSpace(byte c)
		=> c is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

	public readonly FormatException NewFormatException(string message)
		=> new($"{message} (バイト位置: {_position})");
}
