using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.UiTests.Infrastructure;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests;

public sealed class QzssReportUiTests
{
	// 実際に配信された電文 (KyoshinEewViewer.DCReportParser.Tests のサンプルと同じもの)
	private static readonly (string Hex, Type Control, string Title)[] Samples =
	[
		("9AAF8DED25000325BA00DA4A0F5AAC5A8000000008000000200000136DCCFB40", typeof(EewReportControl), "防災気象情報(緊急地震速報)"),
		("53AD16692E80035C0000D25A192E47C99E011B8000000000000000116D6E0A80", typeof(HypocenterReportControl), "防災気象情報(震源)"),
		("53AD1D312B800312B2300000000000000000000000000000000000124DBF4040", typeof(SeismicIntensityReportControl), "防災気象情報(震度)"),
		("9AACA8BECF0001E8F67C37FF3348000000000000000000000000001316E6B240", typeof(TsunamiReportControl), "防災気象情報(津波)"),
		("C6AD34AC278002DF0011700000000000000000000000000000000010B4FF8D80", typeof(NorthwestPacificTsunamiReportControl), "防災気象情報(北西太平洋津波)"),
		("9AADC089B400009B40C22789F4691A17A00000000000000000000010FC738640", typeof(VolcanoReportControl), "防災気象情報(火山)"),
		("53AD4894CC80014C043EE4C1F07826122081309181098496F8000012AAAFCB40", typeof(AshFallReportControl), "防災気象情報(降灰)"),
		("C6AD54C59400011F05442F0544370544000000000000000000000012772C6FC0", typeof(WeatherReportControl), "防災気象情報(気象)"),
		("53AD5D3594000260A8FCD8800000000000000000000000000000001256CC1680", typeof(FloodReportControl), "防災気象情報(洪水)"),
		("C6ADE465F900065E0100002800AD90138A00F80CA30000000000001185B5F7C0", typeof(TyphoonReportControl), "防災気象情報(台風)"),
		("C6ADF465D18002C3F2587F8B101962082C41A588ACB1181623500012199FB000", typeof(MarineReportControl), "防災気象情報(海上)"),
	];

	[AvaloniaFact]
	public void 受信した災危通報は種別ごとの詳細画面でバインディングエラーなく表示される()
	{
		foreach (var (hex, controlType, title) in Samples)
		{
			var group = CreateGroup(DCReport.Parse(Convert.FromHexString(hex)));
			using var bindingErrors = new BindingErrorRecorder();
			var control = group.DetailDisplayControl ?? throw new XunitException($"{title}: 詳細画面がありません。");
			var window = new Window { Width = 420, Height = 900, Content = control };
			try
			{
				window.Show();
				Dispatcher.UIThread.RunJobs();

				Assert.IsType(controlType, control);
				if (!Ui.VisibleTexts(control).Contains(title))
					throw new XunitException($"{title}: 見出しが表示されていません。");
				if (bindingErrors.Errors is { Length: > 0 } errors)
					throw new XunitException($"{title}: バインディングエラーが発生しました。\n" + string.Join("\n", errors));
			}
			finally { window.Close(); }
		}
	}

	// QzssSeries が受信した電文から表示用のグループを作るときと同じ対応
	private static DCReportGroup CreateGroup(DCReport report) => report switch
	{
		EewReport e => new EewReportGroup(e, null),
		SeismicIntensityReport s => new SeismicIntensityReportGroup(s, null),
		HypocenterReport h => new HypocenterReportGroup(h),
		NankaiTroughEarthquakeReport n => new NankaiTroughEarthquakeReportGroup(n),
		TsunamiReport t => new TsunamiReportGroup(t, null),
		NorthwestPacificTsunamiReport n => new NorthwestPacificTsunamiReportGroup(n),
		VolcanoReport v => new VolcanoReportGroup(v),
		AshFallReport a => new AshFallReportGroup(a, null),
		WeatherReport w => new WeatherReportGroup(w, null),
		FloodReport f => new FloodReportGroup(f, null),
		TyphoonReport t => new TyphoonReportGroup(t),
		MarineReport m => new MarineReportGroup(m, null),
		_ => throw new XunitException($"想定外の電文です: {report.GetType().Name}"),
	};
}
