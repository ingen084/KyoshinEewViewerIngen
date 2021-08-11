using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Radar
{
	public class RadarSeries : SeriesBase
	{
		public static HttpClient Client { get; } = new();

		[Reactive]
		public DateTime CurrentDateTime { get; set; }

		private int timeSliderValue;
		public int TimeSliderValue
		{
			get => timeSliderValue;
			set {
				if (timeSliderValue == value)
					return;
				this.RaiseAndSetIfChanged(ref timeSliderValue, value);
				if (JmaRadarTimes == null || JmaRadarTimes.Length <= timeSliderValue)
					return;

				var val = JmaRadarTimes[timeSliderValue];
				if (val is null)
					return;
				CurrentDateTime = val.ValidDateTime?.AddHours(9) ?? throw new Exception("ValidTime が取得できません");
				var oldLayer = ImageTileProviders?.FirstOrDefault();
				ImageTileProviders = new Map.Layers.ImageTile.ImageTileProvider[]
				{
					new RadarImageTileProvider(
						val.BaseDateTime ?? throw new Exception("BaseTime が取得できません"),
						val.ValidDateTime ?? throw new Exception("ValidTime が取得できません"))
				};
				if (oldLayer is RadarImageTileProvider ol)
					ol.Dispose();
			}
		}
		[Reactive]
		public int TimeSliderSize { get; set; } = 1;

		[Reactive]
		public JmaRadarTime[]? JmaRadarTimes { get; set; }

		public RadarSeries() : base("レーダー･ナウキャスト")
		{
			CurrentDateTime = DateTime.Now;
		}

		private RadarView? control;
		public override Control DisplayControl => control ?? throw new Exception("初期化前にコントロールが呼ばれています");

		public override async void Activating()
		{
			if (control != null)
				return;
			control = new RadarView
			{
				DataContext = this,
			};
			JmaRadarTimes = (await JsonSerializer.DeserializeAsync<JmaRadarTime[]>(await Client.GetStreamAsync("https://www.jma.go.jp/bosai/jmatile/data/nowc/targetTimes_N1.json")))?.OrderBy(j => j.BaseDateTime).ToArray();
			TimeSliderSize = JmaRadarTimes?.Length - 1 ?? 0;
			TimeSliderValue = TimeSliderSize;
		}
		public override void Deactivated() { }
	}

	public class JmaRadarTime
	{
		[JsonPropertyName("basetime")]
		public string? BaseTime { get; set; }
		[JsonIgnore]
		public DateTime? BaseDateTime => DateTime.TryParseExact(BaseTime, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var time)
					? time
					: null;
		[JsonPropertyName("validtime")]
		public string? ValidTime { get; set; }
		[JsonIgnore]
		public DateTime? ValidDateTime => DateTime.TryParseExact(BaseTime, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var time)
					? time
					: null;
		[JsonPropertyName("elements")]
		public string[]? Elements { get; set; }
	}

}
