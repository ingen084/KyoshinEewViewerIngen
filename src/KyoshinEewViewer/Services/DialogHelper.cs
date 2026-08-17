using FluentAvalonia.UI.Controls;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Services;

public static class DialogHelper
{
	/// <summary>
	/// 確認ダイアログを表示します
	/// </summary>
	/// <param name="title">ダイアログのタイトル</param>
	/// <param name="message">確認メッセージ</param>
	/// <returns>「はい」が選択された場合true、それ以外はfalse</returns>
	public static async Task<bool> ShowSettingWindowConfirmationDialogAsync(string title, string message)
	{
		var windowService = ServiceLocator.Current.GetService<ISubWindowsService>();
		if (windowService == null)
			return false;

		var dialog = new FAContentDialog()
		{
			Title = title,
			Content = message,
			PrimaryButtonText = "はい",
			SecondaryButtonText = "いいえ",
			DefaultButton = FAContentDialogButton.Secondary
		};

		var result = await dialog.ShowAsync(windowService.SettingWindow);
		return result == FAContentDialogResult.Primary;
	}
}