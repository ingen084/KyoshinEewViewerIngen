using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Typhoon;

internal class TyphoonSeries : SeriesBase
{
	public TyphoonSeries() : base("台風情報α", new FontIcon { Glyph = "\xf751", FontFamily = new("IconFont") })
	{
		Logger = LoggingService.CreateLogger(this);
		if (Design.IsDesignMode)
			return;
		OverlayLayers = new[] { TyphoonLayer };
	}

	private ILogger Logger { get; }

	private TyphoonView? control;
	public override Control DisplayControl => control ?? throw new Exception();

	private TyphoonLayer TyphoonLayer { get; } = new();

	public override void Activating()
	{
		if (control != null)
			return;
		control = new TyphoonView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }

	public async Task OpenXML()
	{
		if (App.MainWindow == null)
			return;

		try
		{
			var ofd = new OpenFileDialog();
			ofd.Filters.Add(new FileDialogFilter
			{
				Name = "防災情報XML",
				Extensions = new List<string>
				{
					"xml"
				},
			});
			ofd.AllowMultiple = false;
			var files = await ofd.ShowAsync(App.MainWindow);
			var file = files?.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(file))
				return;
			if (!File.Exists(file))
				return;
			ProcessXml(File.OpenRead(file));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}

	// 受け取った stream はこの中でdisposeします ちゅうい
	private void ProcessXml(FileStream body)
	{
		using (body)
		{
			using var document = new JmaXmlDocument(body);

			var forecastPlaces = new List<TyphoonPlace>();
			TyphoonPlace? currentPlace = null;

			// 引数として jmx_eb:Axis をとる
			(Direction d, int l)? ParseAxis(Axis axis)
			{
				var direction = CoordinateConverter.GetDirection(axis.Direction.Value) ?? Direction.None;
				// サイズが取得できないときはデータがないという扱いにする
				var re = axis.Radiuses.Where(r => r.Unit == "km");
				if (!re.Any() || string.IsNullOrEmpty(re.First().Value))
					return null;
				if (!int.TryParse(re.First().Value, out var radius))
					throw new Exception("予報円のサイズが取得できませんでした");
				return (direction, radius);
			}

			// 引数として jmx_eb:Circle をとる
			TyphoonRenderCircle? ParseCircleElement(Location center, TyphoonCircle circle)
			{
				// 取得できる方向要素を取得
				var axes = circle.Axes.Select(ParseAxis).Where(x => x is not null).Select(x => x!.Value).ToArray();
				// 取得できなければnull
				if (axes.Length <= 0)
					return null;

				var range = axes[0].l;
				// 中心からの移動量
				var moveLength = new PointD();
				if (axes[0].d != Direction.None)
				{
					range = (axes[0].l + axes[1].l) / 2;
					moveLength = ((axes[0].d.GetVector() * axes[0].l) + (axes[1].d.GetVector() * axes[1].l)) / 2;
				}

				var rawCenter = center.MoveTo(moveLength.Direction + 90, moveLength.Length * 1000);
				return new TyphoonRenderCircle(center, range, rawCenter);
			}

			// 引数として MeteorologicalInfo をとる
			TyphoonPlace ProcessNowTyphoonCircle(MeteorologicalInfo info)
			{
				TyphoonRenderCircle? strongCircle = null;
				TyphoonRenderCircle? stormCircle = null;

				var centerPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "中心");
				var center = CoordinateConverter.GetLocation(centerPart.CenterPart.Coordinates.First(c => c.Type == "中心位置（度）").Value) ?? throw new Exception("現在の中心座標が取得できません");

				var windPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "風");
				strongCircle = ParseCircleElement(center, windPart.WarningAreaParts.First(a => a.Type == "強風域").Circle);
				stormCircle = ParseCircleElement(center, windPart.WarningAreaParts.First(a => a.Type == "暴風域").Circle);

				return new(center, strongCircle, stormCircle);
			}

			// 引数として MeteorologicalInfo をとる
			TyphoonPlace ProcessForecastTyphoonCircle(MeteorologicalInfo info)
			{
				TyphoonRenderCircle? forecastCircle = null, forecastStormCircle = null;

				var area = info.TyphoonCircles.First(c => c.Type == "予報円");
				var center = CoordinateConverter.GetLocation(area.BasePoints.First(p => p.Type == "中心位置（度）").Value) ?? throw new Exception("中心座標が取得できませんでした");
				forecastCircle = ParseCircleElement(center, area);

				var windPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "風");
				forecastStormCircle = ParseCircleElement(center, windPart.WarningAreaParts.First(a => a.Type == "暴風警戒域").Circle);

				return new(center, forecastCircle, forecastStormCircle);
			}

			foreach (var info in document.MeteorologicalBody.MeteorologicalInfos)
			{
				// 現況
				if (info.DateTimeType == "実況")
				{
					currentPlace = ProcessNowTyphoonCircle(info);
					continue;
				}
				// 推定
				if (info.DateTimeType?.StartsWith("推定") ?? false)
				{
					//forecastPlaces.Add(ProcessNowTyphoonCircle(info));
					continue;
				}

				// 予報
				forecastPlaces.Add(ProcessForecastTyphoonCircle(info));
			}

			if (currentPlace == null)
				throw new Exception("台風の現在位置が特定できていません");
			TyphoonLayer.TyphoonItems = new[] { new TyphoonItem("", currentPlace, forecastPlaces.ToArray()) };
		}
	}
}
