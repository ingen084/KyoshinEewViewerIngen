using KyoshinEewViewer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KyoshinEewViewer.Services
{
	/// <summary>
	/// 震度アイコン･ウィンドウテーマ管理
	/// </summary>
	public class ThemeService
	{
		private static readonly Dictionary<string, string>
			windowThemes = new Dictionary<string, string>
		{
			{ "濃色", "Dark" },
			{ "淡色", "Light" },
			{ "青色", "Blue" },
		};

		public static IReadOnlyDictionary<string, string> WindowThemes => windowThemes;

		private static readonly Dictionary<string, string>
			intensityThemes = new Dictionary<string, string>
		{
			{ "通常", "Standard" },
			{ "ビビッド", "Vivid" },
		};

		public static IReadOnlyDictionary<string, string> IntensityThemes => intensityThemes;

		private ConfigurationService ConfigService { get; }
		private KyoshinEewViewerConfiguration.ThemeConfig Config { get; }

		public ThemeService(ConfigurationService configService)
		{
			ConfigService = configService;
			Config = ConfigService.Configuration.Theme;
			ApplyWindowTheme(Config.WindowThemeName, Config.IntensityThemeName);
		}

		private string windowThemeId = "Dark";

		public string WindowThemeId
		{
			get => windowThemeId;
			set
			{
				if (windowThemeId == value)
					return;
				Config.WindowThemeName = value;
				Application.Current.Resources.MergedDictionaries.Clear();
				Application.Current.Resources.MergedDictionaries.Add(Application.LoadComponent(new Uri($"/Themes/{value}.xaml", UriKind.Relative)) as ResourceDictionary);
				Application.Current.Resources.MergedDictionaries.Add(Application.LoadComponent(new Uri($"/IntensityThemes/{IntensityThemeId}.xaml", UriKind.Relative)) as ResourceDictionary);
				windowThemeId = value;
			}
		}

		private string intensityThemeId = "Standard";

		public string IntensityThemeId
		{
			get => intensityThemeId;
			set
			{
				if (intensityThemeId == value)
					return;
				Config.IntensityThemeName = value;
				Application.Current.Resources.MergedDictionaries.Clear();
				Application.Current.Resources.MergedDictionaries.Add(Application.LoadComponent(new Uri($"/Themes/{WindowThemeId}.xaml", UriKind.Relative)) as ResourceDictionary);
				Application.Current.Resources.MergedDictionaries.Add(Application.LoadComponent(new Uri($"/IntensityThemes/{value}.xaml", UriKind.Relative)) as ResourceDictionary);
				intensityThemeId = value;
			}
		}

		/// <summary>
		/// テーマを適用します(初期設定用)
		/// </summary>
		/// <param name="window">ウィンドウテーマのID(Value)</param>
		/// <param name="intensity">震度テーマのID(Value)</param>
		private void ApplyWindowTheme(string window, string intensity)
		{
			windowThemeId = window;
			intensityThemeId = intensity;
			if (!windowThemes.ContainsValue(windowThemeId))
				windowThemeId = WindowThemes.Values.First();
			if (!intensityThemes.ContainsValue(intensityThemeId))
				intensityThemeId = IntensityThemes.Values.First();

			Application.Current.Resources.MergedDictionaries.Clear();
			Application.Current.Resources.MergedDictionaries.Add(Application.LoadComponent(new Uri($"/Themes/{WindowThemeId}.xaml", UriKind.Relative)) as ResourceDictionary);
			Application.Current.Resources.MergedDictionaries.Add(Application.LoadComponent(new Uri($"/IntensityThemes/{IntensityThemeId}.xaml", UriKind.Relative)) as ResourceDictionary);
		}
	}
}