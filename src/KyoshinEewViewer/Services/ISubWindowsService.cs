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
}
