using Avalonia.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.RenderObjects;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Typhoon;

internal class TyphoonSeries : SeriesBase
{
	public TyphoonSeries() : base("台風情報α")
	{
		Logger = LoggingService.CreateLogger(this);
	}

	private ILogger Logger { get; }

	private TyphoonView? control;
	public override Control DisplayControl => control ?? throw new Exception();

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
			await ProcessXml(File.OpenRead(file));
		}
		catch (Exception ex)
		{
			Logger.LogError("外部XMLの読み込みに失敗しました {ex}", ex);
		}
	}

	// 受け取った stream はこの中でdisposeします ちゅうい
	private async Task ProcessXml(FileStream body)
	{
		using (body)
		{
			XDocument document;
			XmlNamespaceManager nsManager;

			using (var reader = XmlReader.Create(body, new XmlReaderSettings { Async = true }))
			{
				document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
				nsManager = new XmlNamespaceManager(reader.NameTable);
			}
			nsManager.AddNamespace("jmx", "http://xml.kishou.go.jp/jmaxml1/");
			nsManager.AddNamespace("eb", "http://xml.kishou.go.jp/jmaxml1/body/meteorology1/");
			nsManager.AddNamespace("jmx_eb", "http://xml.kishou.go.jp/jmaxml1/elementBasis1/");

			var obj = new List<IRenderObject>();
			var circles = new List<(TyphoonCircle?, TyphoonCircle?)>();
			TyphoonCircle? currentStormCircles = null;
			Location? currentLocation = null;

			// 引数として jmx_eb:Axis をとる
			(Direction d, int l)? ParseAxis(XElement element)
			{
				var direction = CoordinateConverter.GetDirection(element.XPathSelectElement("jmx_eb:Direction", nsManager)?.Value) ?? Direction.None;
				// サイズが取得できないときはデータがないという扱いにする
				var re = element.XPathSelectElement("jmx_eb:Radius[@unit='km']", nsManager);
				if (string.IsNullOrWhiteSpace(re?.Value))
					return null;
				if (!int.TryParse(re.Value, out var radius))
					throw new Exception("予報円のサイズが取得できませんでした");
				return (direction, radius);
			}

			// 引数として jmx_eb:Circle をとる
			TyphoonCircle? ParseCircleElement(Location center, XElement element)
			{
				// 取得できる方向要素を取得
#pragma warning disable CS8629
				var axes = element.XPathSelectElements("jmx_eb:Axes/jmx_eb:Axis", nsManager).Select(ParseAxis).Where(x => x is not null).Select(x => x.Value).ToArray();
#pragma warning restore CS8629
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
				return new TyphoonCircle(center, range, rawCenter);
			}

			// 引数として MeteorologicalInfo をとる
			(Location location, TyphoonCircle? strongCircle, TyphoonCircle? stormCircle) ProcessNowTyphoonCircle(XElement element)
			{
				TyphoonCircle? strongCircle = null;
				TyphoonCircle? stormCircle = null;

				var centerPart = element.XPathSelectElement("eb:Item/eb:Kind/eb:Property/eb:CenterPart", nsManager) ?? throw new Exception("CenterPartが取得できません");
				var center = CoordinateConverter.GetLocation(centerPart.XPathSelectElement("jmx_eb:Coordinate[@type='中心位置（度）']", nsManager)?.Value) ?? throw new Exception("現在の中心座標が取得できません");

				var currentStrongWindKind = element.XPathSelectElement("eb:Item/eb:Kind/eb:Property/eb:WarningAreaPart[@type='強風域']", nsManager);
				if (currentStrongWindKind != null)
					strongCircle = ParseCircleElement(center, currentStrongWindKind.XPathSelectElement("jmx_eb:Circle", nsManager) ?? throw new Exception("強風域の円が取得できません"));

				var currentStormWindKind = element.XPathSelectElement("eb:Item/eb:Kind/eb:Property/eb:WarningAreaPart[@type='暴風域']", nsManager);
				if (currentStormWindKind != null)
					stormCircle = ParseCircleElement(center, currentStormWindKind.XPathSelectElement("jmx_eb:Circle", nsManager) ?? throw new Exception("暴風域の円が取得できません"));

				return (center, strongCircle, stormCircle);
			}

			// 引数として MeteorologicalInfo をとる
			(Location location, TyphoonCircle? forecastCircle, TyphoonCircle? stormForecastCircle) ProcessForecastTyphoonCircle(XElement element)
			{
				TyphoonCircle? forecastCircle = null, forecastStormCircle = null;

				var area = element.XPathSelectElement("eb:Item/eb:Area", nsManager) ?? throw new Exception("Areaが取得できません");
				var center = CoordinateConverter.GetLocation(area.XPathSelectElement("jmx_eb:Circle/jmx_eb:BasePoint[@type='中心位置（度）']", nsManager)?.Value)
					?? throw new Exception("中心座標が取得できませんでした");

				forecastCircle = ParseCircleElement(center, area.XPathSelectElement("jmx_eb:Circle", nsManager) ?? throw new Exception("予報円が取得できません"));

				var forecastStormWindKind = element.XPathSelectElement("eb:Item/eb:Kind/eb:Property/eb:WarningAreaPart[@type='暴風警戒域']", nsManager);
				if (forecastStormWindKind != null)
					forecastStormCircle = ParseCircleElement(center, forecastStormWindKind.XPathSelectElement("jmx_eb:Circle", nsManager) ?? throw new Exception("暴風警戒域の円が取得できません"));

				return (center, forecastCircle, forecastStormCircle);
			}

			foreach (var info in document.XPathSelectElements("/jmx:Report/eb:Body/eb:MeteorologicalInfos/eb:MeteorologicalInfo", nsManager))
			{
				// 現況
				if (info.XPathSelectElement("eb:DateTime", nsManager)?.Attribute("type")?.Value == "実況")
				{
					(currentLocation, var strongCircle, currentStormCircles) = ProcessNowTyphoonCircle(info);
					obj.Add(new TyphoonBodyRenderObject(currentLocation, strongCircle, currentStormCircles));
					continue;
				}
				// 推定
				if (info.XPathSelectElement("eb:DateTime", nsManager)?.Attribute("type")?.Value?.StartsWith("推定") ?? false)
				{
					(var location, var strongCircle, var stormCircle) = ProcessNowTyphoonCircle(info);
					foreach (var o in obj.OfType<TyphoonBodyRenderObject>())
						o.IsBaseMode = true;
					obj.Add(new TyphoonBodyRenderObject(location, strongCircle, stormCircle));
					continue;
				}

				// 予報
				if (currentLocation == null)
					throw new Exception("台風の現在位置が特定できていません");

				var (forecastLocation, featureStrongCircle, featureStormCircle) = ProcessForecastTyphoonCircle(info);
				circles.Add((featureStrongCircle, featureStormCircle));
			}

			if (currentLocation == null)
				throw new Exception("台風の現在位置が特定できていません");
			obj.Add(new TyphoonForecastRenderObject(currentLocation, currentStormCircles, circles.ToArray()));

			//RenderObjects = obj.ToArray();
		}
	}
}
