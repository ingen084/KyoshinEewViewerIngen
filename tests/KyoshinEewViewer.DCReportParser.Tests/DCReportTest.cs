using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Tests;

public class DCReportTest
{
	[Fact(DisplayName = "配列のサイズチェックができる")]
	public void InvalidLength()
	{
		// Arrange

		// Act
		var ex = Record.Exception(() => DCReport.Parse(new byte[31]));

		// Assert
		Assert.IsType<DCReportParseException>(ex);
		Assert.StartsWith("メッセージの長さが不正です", ex.Message);
	}

	[Fact(DisplayName = "プリアンブルが不正な場合例外が出せる")]
	public void BadPAB()
	{
		// Arrange
		var data = new byte[32];
		data[0] = 0xFF;

		// Act
		var ex = Record.Exception(() => DCReport.Parse(data));

		// Assert
		Assert.IsType<DCReportParseException>(ex);
		Assert.StartsWith("PAB が不正です", ex.Message);
	}

	[Fact(DisplayName = "CRCが不正な場合例外が出せる")]
	public void BadCRC()
	{
		// Arrange
		var data = new byte[32];
		data[0] = 0b01010011;

		// Act
		var ex = Record.Exception(() => DCReport.Parse(data));

		// Assert
		Assert.IsType<ChecksumErrorException>(ex);
		Assert.StartsWith("CRC エラー", ex.Message);
	}

	[Fact(DisplayName = "Rcが不正な場合例外が出せる")]
	public void BadRc()
	{
		// Arrange
		var data = new byte[32];
		data[0] = 0b01010011;
		TestUtils.SetValue(data, 14, 3, 0);
		TestUtils.SetCorrectCRC(data);

		// Act
		var ex = Record.Exception(() => DCReport.Parse(data));

		// Assert
		Assert.IsType<DCReportParseException>(ex);
		Assert.StartsWith("Rc が不正です", ex.Message);
	}

	[Fact(DisplayName = "未知の Mt をパースできる")]
	public void Success()
	{
		// Arrange
		var data = new byte[32];
		data[0] = 0b01010011;
		TestUtils.SetValue(data, 14, 3, (int)ReportClassification.Regular);
		TestUtils.SetCorrectCRC(data);

		// Act
		var report = DCReport.Parse(data);

		// Assert
		Assert.IsType<DCReport>(report);
		Assert.Equal(Preamble.PatternA, report.Preamble);
		Assert.Equal(0, report.MessageType);
		Assert.Equal(ReportClassification.Regular, report.ReportClassification);
	}
}
