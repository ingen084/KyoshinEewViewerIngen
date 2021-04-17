using Avalonia.Controls;
using System;

namespace KyoshinEewViewer.Series.Earthquake
{
	public class EarthquakeSeries : SeriesBase
	{
		public EarthquakeSeries() : base("地震情報")
		{
			IsEnabled = false;
		}

		private EarthquakeView? control;
		public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

		public bool IsActivate { get; set; }

		public override void Activating()
		{
			IsActivate = true;
			if (control != null)
				return;
			control = new EarthquakeView
			{
				DataContext = this
			};
		}

		public override void Deactivated()
		{
			IsActivate = false;
		}
	}
}
