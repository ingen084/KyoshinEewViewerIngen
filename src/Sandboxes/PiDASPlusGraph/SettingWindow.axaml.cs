using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KyoshinEewViewer.Core;
using System;
using System.Threading.Tasks;

namespace PiDASPlusGraph;

public partial class SettingWindow : Window
{
	private static readonly string[] IntensityThresholdNames =
	[
		"震度0", "震度1", "震度2", "震度3", "震度4",
		"震度5弱", "震度5強", "震度6弱", "震度6強", "震度7",
	];

	private Func<Task<(int current, int max, int reset)?>> _queryDisplayConfig = null!;
	private Action<string> _sendCommand = null!;

	public SettingWindow()
	{
		InitializeComponent();
	}

	public SettingWindow(int currentSeconds, bool currentSplitAxes,
		Action<int> onSecondsChanged, Action<bool> onSplitAxesChanged,
		Func<Task<(int current, int max, int reset)?>> queryDisplayConfig,
		Action<string> sendCommand) : this()
	{
		_queryDisplayConfig = queryDisplayConfig;
		_sendCommand = sendCommand;

		// テーマ選択
		if (App.Selector is { } selector)
		{
			ThemeComboBox.ItemsSource = selector.WindowThemes;
			ThemeComboBox.SelectedItem = selector.SelectedWindowTheme;
			ThemeComboBox.SelectionChanged += (s, e) =>
			{
				if (ThemeComboBox.SelectedItem is ThemeSelector.WindowTheme theme)
					selector.SelectedWindowTheme = theme;
			};

			IntensityThemeComboBox.ItemsSource = selector.IntensityThemes;
			IntensityThemeComboBox.SelectedItem = selector.SelectedIntensityTheme;
			IntensityThemeComboBox.SelectionChanged += (s, e) =>
			{
				if (IntensityThemeComboBox.SelectedItem is ThemeSelector.IntensityTheme theme)
					selector.SelectedIntensityTheme = theme;
			};
		}

		// グラフ設定
		WaveformSecondsSlider.Value = currentSeconds;
		WaveformSecondsText.Text = $"{currentSeconds}秒";
		SplitAxesToggle.IsChecked = currentSplitAxes;

		WaveformSecondsSlider.PropertyChanged += (s, e) =>
		{
			if (e.Property != RangeBase.ValueProperty)
				return;
			var value = (int)WaveformSecondsSlider.Value;
			WaveformSecondsText.Text = $"{value}秒";
			onSecondsChanged(value);
		};

		SplitAxesToggle.IsCheckedChanged += (s, e) =>
		{
			onSplitAxesChanged(SplitAxesToggle.IsChecked == true);
		};

		// ボード表示設定
		CurrentThresholdComboBox.ItemsSource = IntensityThresholdNames;
		MaxThresholdComboBox.ItemsSource = IntensityThresholdNames;

		Opened += (_, _) => UpdateDisplayConfigState(true);

		SendDisplayConfigButton.Tapped += (_, _) =>
		{
			_sendCommand($"DSPCFG C {CurrentThresholdComboBox.SelectedIndex}");
			_sendCommand($"DSPCFG M {MaxThresholdComboBox.SelectedIndex}");
			_sendCommand($"DSPCFG R {(int)(ResetMinutesUpDown.Value ?? 10)}");
		};
	}

	public async void UpdateDisplayConfigState(bool isConnected)
	{
		if (!isConnected)
		{
			DisplayConfigExpander.IsEnabled = false;
			DisplayConfigExpander.Description = "未接続です";
			return;
		}

		DisplayConfigExpander.IsEnabled = false;
		DisplayConfigExpander.Description = "設定を取得中…";

		var result = await _queryDisplayConfig();
		if (result is not var (current, max, reset))
		{
			DisplayConfigExpander.Description = "この機能に対応していないボードです";
			return;
		}
		CurrentThresholdComboBox.SelectedIndex = Math.Clamp(current, 0, 9);
		MaxThresholdComboBox.SelectedIndex = Math.Clamp(max, 0, 9);
		ResetMinutesUpDown.Value = Math.Clamp(reset, 1, 1440);
		DisplayConfigExpander.Description = "ボードのLED・ディスプレイの表示設定を変更します";
		DisplayConfigExpander.IsEnabled = true;
	}
}
