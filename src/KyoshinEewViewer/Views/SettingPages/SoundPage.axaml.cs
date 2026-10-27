using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.ViewModels;

namespace KyoshinEewViewer.Views.SettingPages;
public partial class SoundPage : UserControl
{
	public SoundPage()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		// デバイスは抜き差しされるため、ページを開くたびに一覧を読み込み直す
		if (DataContext is SettingWindowViewModel vm)
			vm.RefreshAudioDevices();
	}
}
