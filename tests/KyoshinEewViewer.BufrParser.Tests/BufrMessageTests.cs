using KyoshinEewViewer.BufrParser.Exceptions;
using KyoshinEewViewer.BufrParser.Tests.TestData;
using Xunit;

namespace KyoshinEewViewer.BufrParser.Tests;

public class BufrMessageTests
{
	private static byte[] BuildValidMessage()
		=> new BufrMessageBuilder()
			.AddIntensityClass(1, 0, 4, 3.5f, 4.4f)
			.SetMessageType(0)
			.SetOriginTimeUtc(new DateTime(2026, 6, 24, 22, 36, 0, DateTimeKind.Utc))
			.SetEpicenter(286, 40.6, 141.3, 68, 61)
			.AddMesh(52, 40, 3, 2, 5, 6, 1, 1, 35)
			.Build();

	[Fact(DisplayName = "正常なBUFR電文からEditionと記述子列を取得できる")]
	public void Parse_正常な電文からセクション情報を取得できる()
	{
		var data = BuildValidMessage();

		var message = BufrMessage.Parse(data);

		Assert.Equal(3, message.Edition);
		Assert.NotEmpty(message.Descriptors);
		Assert.Equal(new Primitives.BufrDescriptor(1, 5, 0), message.Descriptors[0]);

		var reader = message.CreateDataReader();
		Assert.Equal(1u, reader.ReadUInt32(8)); // 階級震度テーブルの反復回数
	}

	[Theory(DisplayName = "電文の構造が不正な場合はBufrParseExceptionを送出する")]
	[InlineData("マジック不一致")]
	[InlineData("宣言長不一致")]
	[InlineData("版不一致")]
	[InlineData("終端7777不一致")]
	public void Parse_不正な電文は例外を送出する(string caseName)
	{
		var data = BuildValidMessage();

		switch (caseName)
		{
			case "マジック不一致":
				data[0] = (byte)'X';
				break;
			case "宣言長不一致":
				data[4] = 0xFF;
				data[5] = 0xFF;
				data[6] = 0xFF;
				break;
			case "版不一致":
				data[7] = 4;
				break;
			case "終端7777不一致":
				data[^1] = (byte)'8';
				break;
		}

		Assert.Throws<BufrParseException>(() => BufrMessage.Parse(data));
	}
}
