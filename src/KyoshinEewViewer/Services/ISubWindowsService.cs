using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Views;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;
public interface ISubWindowsService
{
	SettingWindow? SettingWindow { get; }
	SetupWizardWindow? SetupWizardWindow { get; }

	Task ShowDialogSetupWizardWindow(Action<SetupWizardWindow> opened);
	void ShowSettingWindow();
	Task ShowDialogWindowThemeEditWindow(ThemeSelector.WindowTheme? theme);
	Task ShowDialogIntensityThemeEditWindow(ThemeSelector.IntensityTheme? theme);
}
