namespace KyoshinEewViewer.BufrParser;

/// <summary>
/// dmdata から分割配信される BUFR 電文（512KB超で分割、継続報は "BUFR" マジックなしの生バイト）を再結合する。
/// 無印→RRA→RRB…の順に単純連結し、Section 0 の宣言長と一致した時点で完成とみなす。
/// 例外は送出しない設計。異常系は戻り値と <see cref="FragmentDiscarded"/> イベントで通知する。
/// </summary>
public sealed class BufrFragmentAssembler
{
	private readonly MemoryStream _buffer = new();

	/// <summary>
	/// 結合待ちの未完成データを保持しているかどうか。
	/// </summary>
	public bool HasPendingData => _buffer.Length > 0;

	/// <summary>
	/// 未完成バッファが破棄された際に発火する。引数は破棄されたバイト数。
	/// </summary>
	public event Action<int>? FragmentDiscarded;

	/// <summary>
	/// チャンクを追加する。電文が完成した場合は true を返し、<paramref name="completeMessage"/> に完成した電文を格納する。
	/// </summary>
	public bool TryAppend(ReadOnlySpan<byte> chunk, out byte[]? completeMessage)
	{
		completeMessage = null;

		var isNewMessage = IsBufrMagic(chunk);
		if (isNewMessage)
		{
			if (_buffer.Length > 0)
			{
				FragmentDiscarded?.Invoke((int)_buffer.Length);
				_buffer.SetLength(0);
			}
			_buffer.Write(chunk);
		}
		else
		{
			// 継続報。未完成バッファが無ければ対応する先頭が届いていないため無視する
			if (_buffer.Length == 0)
				return false;
			_buffer.Write(chunk);
		}

		// Section 0 の宣言長（先頭8バイト）が揃うまでは完成判定できない
		if (_buffer.Length < 8)
			return false;

		var bufferSpan = _buffer.GetBuffer().AsSpan(0, (int)_buffer.Length);
		var declaredLength = (bufferSpan[4] << 16) | (bufferSpan[5] << 8) | bufferSpan[6];

		if (_buffer.Length > declaredLength)
		{
			// 宣言長を超過した場合は電文が破損しているとみなし破棄する
			FragmentDiscarded?.Invoke((int)_buffer.Length);
			_buffer.SetLength(0);
			return false;
		}

		if (_buffer.Length < declaredLength)
			return false;

		var isComplete = bufferSpan[^4] == (byte)'7' && bufferSpan[^3] == (byte)'7' &&
			bufferSpan[^2] == (byte)'7' && bufferSpan[^1] == (byte)'7';

		if (!isComplete)
		{
			FragmentDiscarded?.Invoke((int)_buffer.Length);
			_buffer.SetLength(0);
			return false;
		}

		completeMessage = bufferSpan.ToArray();
		_buffer.SetLength(0);
		return true;
	}

	/// <summary>
	/// 結合中のバッファを破棄せずに空へ戻す（イベントは発火しない）。
	/// </summary>
	public void Reset() => _buffer.SetLength(0);

	private static bool IsBufrMagic(ReadOnlySpan<byte> chunk)
		=> chunk.Length >= 4 && chunk[0] == (byte)'B' && chunk[1] == (byte)'U' && chunk[2] == (byte)'F' && chunk[3] == (byte)'R';
}
