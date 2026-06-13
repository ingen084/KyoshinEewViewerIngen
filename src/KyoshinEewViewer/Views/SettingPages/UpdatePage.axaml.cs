using Avalonia.Controls;
using LiveMarkdown.Avalonia;

namespace KyoshinEewViewer.Views.SettingPages;
public partial class UpdatePage : UserControl
{
	public UpdatePage()
	{
		InitializeComponent();
		AddHandler(MarkdownTextBlock.LinkClickEvent, (_, e) => UrlOpener.OpenUrl(e.HRef.ToString()));
	}
}
