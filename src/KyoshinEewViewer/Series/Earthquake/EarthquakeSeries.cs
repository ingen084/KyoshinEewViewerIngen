using Avalonia.Controls;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI.Fody.Helpers;
using System;

namespace KyoshinEewViewer.Series.Earthquake
{
	public class EarthquakeSeries : SeriesBase
	{
		public EarthquakeSeries() : base("地震情報β")
		{
			IsEnabled = false;
			Service = new EarthquakeWatchService();
			if (Design.IsDesignMode)
			{
				IsLoading = false;
				Service.Earthquakes.Add(new Models.Earthquake("a")
				{
					IsSokuhou = true,
					IsReportTime = true,
					IsHypocenterOnly = true,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Intensity = JmaIntensity.Int0,
					Magnitude = 3.1f,
					Place = "これはサンプルデータです",
				});
				Service.Earthquakes.Add(new Models.Earthquake("b")
				{
					OccurrenceTime = DateTime.Now,
					Depth = -1,
					Intensity = JmaIntensity.Int4,
					Magnitude = 6.1f,
					Place = "デザイナ",
					IsSelecting = true
				});
				Service.Earthquakes.Add(new Models.Earthquake("c")
				{
					OccurrenceTime = DateTime.Now,
					Depth = 60,
					Intensity = JmaIntensity.Int5Lower,
					Magnitude = 3.0f,
					Place = "サンプル"
				});
				Service.Earthquakes.Add(new Models.Earthquake("d")
				{
					OccurrenceTime = DateTime.Now,
					Depth = 90,
					Intensity = JmaIntensity.Int6Upper,
					Magnitude = 6.1f,
					Place = "ViewModel"
				});
				Service.Earthquakes.Add(new Models.Earthquake("e")
				{
					OccurrenceTime = DateTime.Now,
					Depth = 450,
					Intensity = JmaIntensity.Int7,
					Magnitude = 6.1f,
					Place = "です"
				});
			}
		}

		private EarthquakeView? control;
		public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

		public bool IsActivate { get; set; }

		public override async void Activating()
		{
			IsActivate = true;
			if (control != null)
				return;
			control = new EarthquakeView
			{
				DataContext = this
			};
			await Service.StartAsync();
			ProcessEarthquake(Service.Earthquakes[0]);
			IsLoading = false;
		}

		public override void Deactivated()
		{
			IsActivate = false;
		}


		public void EarthquakeClicked(Models.Earthquake eq) => ProcessEarthquake(eq);

		public async void ProcessEarthquake(Models.Earthquake eq)
		{
			if (eq.UsedModels.Count <= 0 || control == null || eq.IsSelecting)
				return;
			foreach (var e in Service.Earthquakes)
				e.IsSelecting = false;
			eq.IsSelecting = true;

			RenderObjects = await control.ProcessXml(await InformationProviderService.Default.FetchContentAsync(eq.UsedModels[eq.UsedModels.Count - 1]));
		}

		public EarthquakeWatchService Service { get; }

		[Reactive]
		public bool IsLoading { get; set; } = true;
	}
}
