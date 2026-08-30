using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Series.Qzss.Layers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Models;

public record NPTsunamiArea(byte Code, string Status, string Height);

/// <summary>
/// 地図に表示する予報地点
/// </summary>
/// <param name="Area">一覧に表示している予報地点の情報</param>
/// <param name="Location">予報地点の位置</param>
/// <param name="Height">電文上の津波の高さ区分</param>
public record NPTsunamiPoint(NPTsunamiArea Area, Location Location, int Height);

public partial class NorthwestPacificTsunamiReportGroup : DCReportGroup
{
	public static readonly string TYPE = "NorthwestPacificTsunami";
	public override string Type => TYPE;

	private List<NorthwestPacificTsunamiReport> Reports { get; } = [];
	private NorthwestPacificTsunamiLayer Layer { get; } = new();

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	[ObservableProperty]
	public partial byte TsunamigenicPotential { get; set; }

	public ObservableCollection<NPTsunamiArea> Areas { get; } = [];

	public NorthwestPacificTsunamiReportGroup(NorthwestPacificTsunamiReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		TsunamigenicPotential = report.TsunamigenicPotential;

		Reports.Add(report);
		UpdateDetails();
	}

	public override bool CheckDuplicate(DCReport report) => report is NorthwestPacificTsunamiReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not NorthwestPacificTsunamiReport n || ApplyTimezoneOffset(n.ReportTime) != ReportTime || n.TsunamigenicPotential != TsunamigenicPotential)
			return false;

		Reports.Add(n);
		ReportCount++;
		TotalAreaCount += n.Regions.Count(a => a.Region != 0);

		UpdateDetails();
		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new NorthwestPacificTsunamiReportControl { DataContext = this };

	private static string GetTsunamiHeightString(int height)
		=> height switch
		{
			1 => "0.3m~1m",
			2 => "1m~3m",
			3 => "3m~5m",
			4 => "5m~10m",
			508 => "10m超",
			509 => "巨大",
			510 => "高い",
			511 => "不明",
			_ => $"不明({height})",
		};
	public void UpdateDetails()
	{
		Areas.Clear();
		// 位置が分かっている予報地点のみ地図に表示する
		var points = new Dictionary<byte, NPTsunamiPoint>();
		foreach (var report in Reports)
		{
			foreach (var region in report.Regions)
			{
				if (region.Region == 0)
					continue;
				var area = new NPTsunamiArea(region.Region, region.IsArrived ? "到達" : ApplyTimezoneOffset(region.ArrivalTime).ToString("HH:mm 到達見込み"), GetTsunamiHeightString(region.Height));
				Areas.Add(area);

				if (!CsvDictionary.DCRNorthwestPacificTsunamiLocation.TryGetValue(region.Region, out var location))
					continue;
				// 同じ地点が複数の電文に含まれる場合は高いほうを採用する
				if (points.TryGetValue(region.Region, out var exist) && NorthwestPacificTsunamiLayer.GetHeightRank(exist.Height) >= NorthwestPacificTsunamiLayer.GetHeightRank(region.Height))
					continue;
				points[region.Region] = new(area, new Location(location.Latitude, location.Longitude), region.Height);
			}
		}

		Layer.Points = [.. points.Values];

		MapDisplayParameter = new()
		{
			// 左側に表示する予報地点の一覧と地図が重ならないようにする
			Padding = new(355, 0, 0, 0),
			OverlayLayers = [Layer],
		};

		if (points.Count <= 0)
		{
			MapNavigationRequest = null;
			return;
		}

		// 予報地点は北西太平洋全域に散らばるため、対象の地点がすべて入る範囲を表示する
		var padding = 4;
		MapNavigationRequest = new(new(
			new Point(points.Values.Min(p => p.Location.Latitude) - padding, points.Values.Min(p => p.Location.Longitude) - padding),
			new Point(points.Values.Max(p => p.Location.Latitude) + padding, points.Values.Max(p => p.Location.Longitude) + padding)
		));
	}
}
