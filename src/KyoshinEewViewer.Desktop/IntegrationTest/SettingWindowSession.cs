#if INTEGRATION_TEST
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// シナリオの間で共有する設定ウィンドウ
/// </summary>
internal sealed class SettingWindowSession(ISubWindowsService subWindows, Window window, SettingWindowViewModel viewModel)
{
	public Window Window => window;
	public SettingWindowViewModel ViewModel => viewModel;

	public static async Task<SettingWindowSession> OpenAsync()
	{
		UiTestDriver.Step("設定ウィンドウを開く");
		var subWindows = ServiceLocator.Current.RequireService<ISubWindowsService>();
		StrongReferenceMessenger.Default.Send(new ShowSettingWindowRequested());
		var window = await WaitForAsync(() => subWindows.SettingWindow is { IsVisible: true } w ? w : null, "設定ウィンドウ");
		return new(subWindows, window, (SettingWindowViewModel)window.DataContext!);
	}

	public async Task<T> ShowPageAsync<T>(string title) where T : Visual
	{
		viewModel.SelectedSettingPage = viewModel.SettingPages.Single(p => p.Title == title);
		return await WaitForControlAsync<T>(window);
	}

	public async Task CloseAsync()
	{
		UiTestDriver.Step("設定ウィンドウを閉じる");
		window.Close();
		await WaitUntilAsync(() => subWindows.SettingWindow == null, "設定ウィンドウが閉じる");
	}
}
#endif
