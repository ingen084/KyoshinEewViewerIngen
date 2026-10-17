using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Series.KyoshinMonitor.Controls;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.UiTests.Infrastructure;
using KyoshinMonitorLib;
using Xunit.Sdk;

namespace KyoshinEewViewer.UiTests;

public sealed class EewPanelUiTests
{
	private static readonly DateTime Occurred = new(2026, 10, 17, 8, 30, 12);

	private static readonly Eew Forecast = new()
	{
		Id = "20261017083012",
		Source = EewSource.KyoshinMonitor,
		DisplaySource = "強震モニタ",
		ReceiveTime = Occurred.AddSeconds(8),
		SerialNo = 3,
		IsFinal = false,
		MaxIntensity = JmaIntensity.Int4,
		Hypocenter = new()
		{
			OccurrenceTime = Occurred,
			Place = "千葉県北西部",
			Location = new KyoshinMonitorLib.Location(35.6f, 140.1f),
			Magnitude = 5.4f,
			Depth = 60,
			IsTemporary = false,
		},
		IsWarning = false,
	};

	private sealed record DisplayCase(string Name, Eew Eew, string[] Visible, string[] Hidden, bool WarningStyle);

	private static readonly DisplayCase[] DisplayCases =
	[
		new("予報", Forecast,
			["緊急地震速報(03報)", "17日 08:30:12", "千葉県北西部", "60", "5.4", "受信元: 強震モニタ"],
			["緊急地震速報(03報/最終)", "以上", "規模未受信", "不明", "警報地域", "キャンセルされました", "キャンセルまたは受信範囲外"],
			false),
		new("警報の最終報", Forecast with
		{
			SerialNo = 12,
			IsFinal = true,
			IsWarning = true,
			MaxIntensity = JmaIntensity.Int6Lower,
			IsIntensityOver = true,
			WarningAreas = new()
			{
				DisplaySource = "DM-D.S.S",
				SerialNo = 2,
				Codes = [301, 350],
				Names = ["千葉県北西部", "東京都２３区"],
			},
		},
			["緊急地震速報(12報/最終)", "以上", "警報地域", "2地域", "警報受信元: DM-D.S.S (2報)", "東京都２３区"],
			["緊急地震速報(12報)"],
			true),
		new("仮定震源要素", Forecast with
		{
			Hypocenter = Forecast.Hypocenter! with
			{
				Magnitude = 1.0f,
				Depth = 10,
				IsTemporary = true,
				Accuracy = new() { IsLocked = false, LocationAccuracy = 1, DepthAccuracy = 1, MagnitudeAccuracy = 0 },
			},
		},
			["規模未受信", "仮定震源要素", "未確定"],
			["深さ:", "km"],
			false),
		new("マグニチュード不明", Forecast with { Hypocenter = Forecast.Hypocenter! with { Magnitude = null } },
			["不明"],
			["規模未受信"],
			false),
		new("取消報", Forecast with { IsCancelled = true, IsTrueCancelled = true },
			["キャンセルされました"],
			["キャンセルまたは受信範囲外"],
			false),
		new("取消または受信範囲外", Forecast with { IsCancelled = true },
			["キャンセルまたは受信範囲外"],
			["キャンセルされました"],
			false),
	];

	[AvaloniaFact]
	public void 緊急地震速報の状態に応じて報数_震源_最大震度_警報_取消の表示が切り替わる()
	{
		foreach (var c in DisplayCases)
		{
			var panel = new EewPanel { DataContext = c.Eew };
			var window = new Window { Width = 360, Height = 640, Content = panel };
			try
			{
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var texts = Ui.VisibleTexts(panel);
				foreach (var text in c.Visible)
					if (!texts.Contains(text))
						throw new XunitException($"{c.Name}: 「{text}」が表示されていません。表示中: {string.Join(" / ", texts)}");
				foreach (var text in c.Hidden)
					if (texts.Contains(text))
						throw new XunitException($"{c.Name}: 「{text}」は表示されないはずです。");

				var root = Assert.IsType<Grid>(panel.Content);
				Assert.True(c.WarningStyle == root.Classes.Contains("warning"), $"{c.Name}: 警報の配色が電文と一致しません。");
				Assert.Equal(c.Eew.MaxIntensity, panel.GetVisualDescendants().OfType<IntensityIcon>().First().Intensity);
			}
			finally { window.Close(); }
		}
	}
}
