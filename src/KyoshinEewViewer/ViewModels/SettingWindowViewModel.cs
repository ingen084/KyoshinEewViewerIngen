﻿using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Prism.Commands;
using Prism.Events;
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
			Config = new Configuration();
			Config.Timer.Offset = 2500;
			Config.Theme.WindowThemeName = "Light";
			Config.Theme.IntensityThemeName = "Standard";

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

		private ICommand _registMapPositionCommand;
		public ICommand RegistMapPositionCommand => _registMapPositionCommand ?? (_registMapPositionCommand = new DelegateCommand(() => Aggregator.GetEvent<Events.RegistMapPositionRequested>().Publish()));

		private ICommand _resetMapPositionCommand;
		public ICommand ResetMapPositionCommand => _resetMapPositionCommand ?? (_resetMapPositionCommand = new DelegateCommand(() =>
		{
			Config.Map.Location1 = new Location(24.058240f, 123.046875f);
			Config.Map.Location2 = new Location(45.706479f, 146.293945f);
		}));

		private IEventAggregator Aggregator { get; }
		public SettingWindowViewModel(ThemeService service, ConfigurationService configService, IEventAggregator aggregator)
		{
			ThemeService = service;
			Aggregator = aggregator;
			ConfigService = configService;
			Config = ConfigService.Configuration;

			SelectedWindowTheme = ThemeService.WindowThemes.FirstOrDefault(t => t.Value == Config.Theme.WindowThemeName);
			SelectedIntensityTheme = ThemeService.IntensityThemes.FirstOrDefault(t => t.Value == Config.Theme.IntensityThemeName);

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