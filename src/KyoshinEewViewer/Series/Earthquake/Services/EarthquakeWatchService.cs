using Avalonia.Controls;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.InformationProvider;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace KyoshinEewViewer.Series.Earthquake.Services
{
	/// <summary>
	/// 地震情報の更新を担う
	/// </summary>
	public class EarthquakeWatchService : ReactiveObject
	{
		private readonly string[] TargetTitles = { "震度速報", "震源に関する情報", "震源・震度に関する情報" };

		public ObservableCollection<Models.Earthquake> Earthquakes { get; } = new();
		public event Action<Models.Earthquake>? EarthquakeUpdated;

		private ILogger Logger { get; }

		public EarthquakeWatchService()
		{
			Logger = LoggingService.CreateLogger(this);
			if (Design.IsDesignMode)
				return;
			JmaXmlPullProvider.Default.NewFeedArrived += async h =>
			{
				(var id, var stream) = await h.GetBodyAsync();
				await ProcessInformationAsync(id, stream);
			};
		}

		public async Task StartAsync()
		{
			var histories = await JmaXmlPullProvider.Default.EnableAsync(TargetTitles);
			foreach (var h in histories.OrderBy(h => h.ArrivalTime))
			{
				(var id, var stream) = await h.GetBodyAsync();
				await ProcessInformationAsync(id, stream, hideNotice: true);
			}
			foreach (var eq in Earthquakes)
				EarthquakeUpdated?.Invoke(eq);
		}

		public async Task<Models.Earthquake?> ProcessInformationAsync(string id, Stream stream, bool dryRun = false, bool hideNotice = false)
		{
			XDocument document;
			XmlNamespaceManager nsManager;

			using (stream)
			using (var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true }))
			{
				document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
				nsManager = new XmlNamespaceManager(reader.NameTable);
			}
			nsManager.AddNamespace("jmx", "http://xml.kishou.go.jp/jmaxml1/");
			nsManager.AddNamespace("eb", "http://xml.kishou.go.jp/jmaxml1/body/seismology1/");
			nsManager.AddNamespace("jmx_eb", "http://xml.kishou.go.jp/jmaxml1/elementBasis1/");
			nsManager.AddNamespace("ib", "http://xml.kishou.go.jp/jmaxml1/informationBasis1/");

			var title = document.XPathSelectElement("/jmx:Report/jmx:Control/jmx:Title", nsManager)?.Value;

			if (!TargetTitles.Contains(title))
				return null;

			// TODO: もう少し綺麗にしたい
			try
			{
				var eventId = document.XPathSelectElement("/jmx:Report/ib:Head/ib:EventID", nsManager)?.Value ?? throw new Exception("EventIDを解析できませんでした");
				// TODO: EventIdの異なる電文に対応する
				var eq = Earthquakes.FirstOrDefault(e => e.Id == eventId);
				if (eq == null || dryRun)
				{
					eq = new Models.Earthquake(eventId)
					{
						IsSokuhou = true,
						IsHypocenterOnly = false,
						Intensity = JmaIntensity.Unknown
					};
					if (!dryRun)
						Earthquakes.Insert(0, eq);
				}
				eq.UsedModels.Add(id);

				switch (title)
				{
					case "震度速報":
						{
							// すでに他の情報が入ってきている場合更新を行わない
							if (!eq.IsSokuhou)
								break;
							eq.Intensity = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:MaxInt", nsManager)?.Value.ToJmaIntensity() ?? JmaIntensity.Unknown;

							// eq.IsHypocenterOnly = false;
							eq.IsSokuhou = true;
							eq.OccurrenceTime = DateTime.Parse(document.XPathSelectElement("/jmx:Report/ib:Head/ib:TargetDateTime", nsManager)?.Value ?? throw new Exception("TargetDateTimeを解析できませんでした"));
							eq.IsReportTime = true;

							eq.Place = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:Name", nsManager)?.Value;
							break;
						}
					case "震源に関する情報":
						{
							// すでに他の情報が入ってきている場合更新を行わない
							if (!eq.IsSokuhou)
								break;
							eq.IsHypocenterOnly = true;

							eq.OccurrenceTime = DateTime.Parse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:OriginTime", nsManager)?.Value ?? throw new Exception("OriginTimeを解析できませんでした"));
							eq.IsReportTime = false;

							eq.Place = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/eb:Name", nsManager)?.Value;
							eq.Magnitude = float.Parse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/jmx_eb:Magnitude", nsManager)?.Value ?? throw new Exception("Magnitudeを解析できませんでした"));
							if (float.IsNaN(eq.Magnitude))
								eq.MagnitudeAlternativeText = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/jmx_eb:Magnitude", nsManager)?.Attribute("description")?.Value;
							eq.Depth = CoordinateConverter.GetDepth(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value) ?? -1;

							eq.Comment = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Comments/eb:ForecastComment/eb:Text", nsManager)?.Value;
							break;
						}
					case "震源・震度に関する情報":
						{
							eq.IsSokuhou = false;
							eq.IsHypocenterOnly = false;
							eq.OccurrenceTime = DateTime.Parse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:OriginTime", nsManager)?.Value ?? throw new Exception("OriginTimeを解析できませんでした"));
							eq.IsReportTime = false;

							eq.Intensity = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:MaxInt", nsManager)?.Value.ToJmaIntensity() ?? JmaIntensity.Unknown;
							eq.Place = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/eb:Name", nsManager)?.Value;
							eq.Magnitude = float.Parse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/jmx_eb:Magnitude", nsManager)?.Value ?? throw new Exception("Magnitudeを解析できませんでした"));
							if (float.IsNaN(eq.Magnitude))
								eq.MagnitudeAlternativeText = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/jmx_eb:Magnitude", nsManager)?.Attribute("description")?.Value;
							eq.Depth = CoordinateConverter.GetDepth(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value) ?? -1;

							eq.Comment = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Comments/eb:ForecastComment/eb:Text", nsManager)?.Value;
							break;
						}
					default:
						Logger.LogError("不明なTitleをパースしました。: " + title);
						break;
				}
				if (!hideNotice)
					EarthquakeUpdated?.Invoke(eq);
				return eq;
			}
			catch (Exception ex)
			{
				Logger.LogError("デシリアライズ時に例外が発生しました。 " + ex);
				return null;
			}
		}
	}
}
