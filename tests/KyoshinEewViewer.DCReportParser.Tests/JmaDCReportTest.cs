using KyoshinEewViewer.DCReportParser.Exceptions;

namespace KyoshinEewViewer.DCReportParser.Tests;

public class JmaDCReportTest
{
	/// <summary>
	/// JMA DC Report の基本形
	/// </summary>
	private byte[] BaseData
	{
		get {
			var data = new byte[32];
			data[0] = (byte)Preamble.PatternB;
			TestUtils.SetValue(data, 14, 3, (int)ReportClassification.Regular);
			TestUtils.SetValue(data, 8, 6, 43);
			return data;
		}
	}

	[Fact(DisplayName = "非対応の Vn の場合例外が出せる")]
	public void InvalidVersion()
	{
		// Arrange
		var data = BaseData;
		TestUtils.SetValue(data, 214, 6, 0);
		TestUtils.SetCorrectCRC(data);

		// Act
		var ex = Record.Exception(() => DCReport.Parse(data));

		// Assert
		Assert.IsType<DCReportParseException>(ex);
		Assert.StartsWith("この Vn には非対応です", ex.Message);
	}
}
