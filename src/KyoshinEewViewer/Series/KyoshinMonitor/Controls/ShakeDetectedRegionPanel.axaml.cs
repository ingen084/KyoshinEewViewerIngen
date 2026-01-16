using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Controls;

public partial class ShakeDetectedRegionPanel : UserControl
{
	public ShakeDetectedRegionPanel()
	{
		InitializeComponent();
	}

	public static readonly StyledProperty<ShakeDetectedRegion[]> RegionsProperty =
		AvaloniaProperty.Register<ShakeDetectedRegionPanel, ShakeDetectedRegion[]>(
			nameof(Regions),
			defaultValue: []
		);

	public ShakeDetectedRegion[] Regions
	{
		get => GetValue(RegionsProperty);
		set => SetValue(RegionsProperty, value);
	}

	public static readonly StyledProperty<KyoshinEventLevel> LevelProperty =
		AvaloniaProperty.Register<ShakeDetectedRegionPanel, KyoshinEventLevel>(
			nameof(Level),
			defaultValue: KyoshinEventLevel.Weaker
		);

	public KyoshinEventLevel Level
	{
		get => GetValue(LevelProperty);
		set => SetValue(LevelProperty, value);
	}

	public string Title => GetTitle(Level);

	/// <summary>
	/// KyoshinEventLevel に応じたタイトルを取得
	/// </summary>
	public static string GetTitle(KyoshinEventLevel level)
		=> level switch
		{
			KyoshinEventLevel.Stronger => "非常に強い揺れを検知",
			KyoshinEventLevel.Strong => "強い揺れを検知",
			KyoshinEventLevel.Medium => "揺れを検知",
			KyoshinEventLevel.Weak => "弱い揺れを検知",
			_ => "微弱な揺れを検知",
		};

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == LevelProperty)
		{
			var oldTitle = change.OldValue is KyoshinEventLevel oldLevel ? GetTitle(oldLevel) : GetTitle(KyoshinEventLevel.Weaker);
			RaisePropertyChanged(TitleProperty, oldTitle, Title);
		}
	}

	private static readonly DirectProperty<ShakeDetectedRegionPanel, string> TitleProperty =
		AvaloniaProperty.RegisterDirect<ShakeDetectedRegionPanel, string>(
			nameof(Title),
			o => o.Title
		);
}
