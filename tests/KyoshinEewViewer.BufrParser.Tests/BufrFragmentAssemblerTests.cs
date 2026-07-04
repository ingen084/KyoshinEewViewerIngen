using KyoshinEewViewer.BufrParser.Tests.TestData;
using Xunit;

namespace KyoshinEewViewer.BufrParser.Tests;

public class BufrFragmentAssemblerTests
{
	private static byte[] BuildMessage(int epicenterAreaCode = 286)
		=> new BufrMessageBuilder()
			.AddIntensityClass(1, 0, 4, 3.5f, 4.4f)
			.SetMessageType(0)
			.SetOriginTimeUtc(new DateTime(2026, 6, 24, 22, 36, 0, DateTimeKind.Utc))
			.SetEpicenter(epicenterAreaCode, 40.6, 141.3, 68, 61)
			.AddMesh(52, 40, 3, 2, 5, 6, 1, 1, 35)
			.Build();

	[Fact(DisplayName = "3分割された電文を順に追加すると最終チャンクで完成する")]
	public void TryAppend_分割電文を追加すると最終チャンクで完成する()
	{
		var full = BuildMessage();
		var chunkSize = (full.Length / 3) + 1;
		var chunks = SplitEvenly(full, chunkSize);
		Assert.True(chunks.Length >= 3);

		var assembler = new BufrFragmentAssembler();

		for (var i = 0; i < chunks.Length - 1; i++)
		{
			Assert.False(assembler.TryAppend(chunks[i], out var partial));
			Assert.Null(partial);
			Assert.True(assembler.HasPendingData);
		}

		Assert.True(assembler.TryAppend(chunks[^1], out var completeMessage));
		Assert.NotNull(completeMessage);
		Assert.Equal(full, completeMessage);
		Assert.False(assembler.HasPendingData);
	}

	[Fact(DisplayName = "未完成バッファが残っている状態で新規電文を受信するとFragmentDiscardedが発火し新規電文の処理を継続する")]
	public void TryAppend_未完成バッファ中の新規開始でFragmentDiscardedが発火する()
	{
		var first = BuildMessage(epicenterAreaCode: 100);
		var second = BuildMessage(epicenterAreaCode: 200);
		var partialFirst = first[..(first.Length / 2)];

		var assembler = new BufrFragmentAssembler();
		int? discardedBytes = null;
		assembler.FragmentDiscarded += bytes => discardedBytes = bytes;

		Assert.False(assembler.TryAppend(partialFirst, out _));
		Assert.True(assembler.HasPendingData);

		Assert.True(assembler.TryAppend(second, out var completeMessage));

		Assert.Equal(partialFirst.Length, discardedBytes);
		Assert.NotNull(completeMessage);
		Assert.Equal(second, completeMessage);
		Assert.False(assembler.HasPendingData);
	}

	[Fact(DisplayName = "バッファが空の状態で継続報（BUFRマジックなし）を受信すると無視されFalseを返す")]
	public void TryAppend_バッファ空で継続報を受信すると無視される()
	{
		var assembler = new BufrFragmentAssembler();
		byte[] continuation = [0x01, 0x02, 0x03, 0x04, 0x05];

		var result = assembler.TryAppend(continuation, out var completeMessage);

		Assert.False(result);
		Assert.Null(completeMessage);
		Assert.False(assembler.HasPendingData);
	}

	private static byte[][] SplitEvenly(byte[] data, int chunkSize)
	{
		var chunkCount = (data.Length + chunkSize - 1) / chunkSize;
		var chunks = new byte[chunkCount][];
		for (var i = 0; i < chunkCount; i++)
		{
			var offset = i * chunkSize;
			var length = Math.Min(chunkSize, data.Length - offset);
			chunks[i] = data[offset..(offset + length)];
		}
		return chunks;
	}
}
