using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace KyoshinEewViewer.ViewModels
{
	public class SettingWindowViewModel : BindableBase
	{
		private ThemeService ThemeService { get; }
		private ConfigurationService ConfigService { get; }
		public Configuration Config { get; }

		public SettingWindowViewModel()
		{
			Config = new Configuration
			{
				Offset = 2500,
				WindowThemeId = "Light",
				IntensityThemeId = "Standard"
			};

			Ints = new List<JmaIntensity> {
				JmaIntensity.Unknown,
				JmaIntensity.Int0,
				JmaIntensity.Int1,
				JmaIntensity.Int2,
				JmaIntensity.Int3,
				JmaIntensity.Int4,
				JmaIntensity.Int5Lower,
				JmaIntensity.Int5Upper,
				JmaIntensity.Int6Lower,
				JmaIntensity.Int6Upper,
				JmaIntensity.Int7,
				JmaIntensity.Error,
			};
		}

		public List<JmaIntensity> Ints { get; }

		public SettingWindowViewModel(ThemeService service, ConfigurationService configService)
		{
			ThemeService = service;
			ConfigService = configService;
			Config = ConfigService.Configuration;
			Config.ConfigurationUpdated += c => RaisePropertyChanged(nameof(Config));

			SelectedWindowTheme = ThemeService.WindowThemes.FirstOrDefault(t => t.Value == Config.WindowThemeId);
			SelectedIntensityTheme = ThemeService.IntensityThemes.FirstOrDefault(t => t.Value == Config.IntensityThemeId);

			Ints = new List<JmaIntensity> {
				JmaIntensity.Unknown,
				JmaIntensity.Int0,
				JmaIntensity.Int1,
				JmaIntensity.Int2,
				JmaIntensity.Int3,
				JmaIntensity.Int4,
				JmaIntensity.Int5Lower,
				JmaIntensity.Int5Upper,
				JmaIntensity.Int6Lower,
				JmaIntensity.Int6Upper,
				JmaIntensity.Int7,
			};
		}

		public IReadOnlyDictionary<string, string> WindowThemes => ThemeService.WindowThemes;
		public IReadOnlyDictionary<string, string> IntensityThemes => ThemeService.IntensityThemes;

		private KeyValuePair<string, string> selectedWindowTheme;

		public KeyValuePair<string, string> SelectedWindowTheme
		{
			get => selectedWindowTheme;
			set
			{
				if (SelectedWindowTheme.Value != value.Value)
					ThemeService.WindowThemeId = value.Value;
				SetProperty(ref selectedWindowTheme, value);
			}
		}

		private KeyValuePair<string, string> selectedIntensityTheme;

		public KeyValuePair<string, string> SelectedIntensityTheme
		{
			get => selectedIntensityTheme;
			set
			{
				if (SelectedIntensityTheme.Value != value.Value)
					ThemeService.IntensityThemeId = value.Value;
				SetProperty(ref selectedIntensityTheme, value);
			}
		}

		private ICommand _openUrl;
		public ICommand OpenUrl => _openUrl ?? (_openUrl = new DelegateCommand<string>(u => Process.Start(new ProcessStartInfo("cmd", $"/c start {u.Replace("&", "^&")}") { CreateNoWindow = true })));
	}
}