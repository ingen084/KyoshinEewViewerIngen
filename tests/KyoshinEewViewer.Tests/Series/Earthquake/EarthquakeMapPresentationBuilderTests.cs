using KyoshinEewViewer.Core;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;
using System;
using Xunit;
using static KyoshinEewViewer.Core.Models.KyoshinEewViewerConfiguration;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

public class EarthquakeMapPresentationBuilderTests
{
	// テストでは FixedObjectRenderer.IntensityPaintCache の初期化を伴わないので、Fill 系を無効にする
	private static EarthquakeConfig DefaultConfig() => new() { FillSokuhou = false, FillDetail = false };

	private static EarthquakeHypocenterSnapshot Hypocenter(string place = "テスト", float mag = 5.0f, bool isVolcano = false, bool isForeign = false)
		=> new(
			OriginTime: new DateTime(2026, 4, 30, 14, 30, 0),
			Place: place,
			Location: new Location(38.0f, 142.0f),
			LocationError: null,
			Magnitude: mag,
			MagnitudeAlternativeText: null,
			Depth: 50,
			DepthError: null,
			IsForeign: isForeign,
			IsVolcano: isVolcano,
			VolcanoName: null);

	private static EarthquakeSnapshot Snapshot(
		EarthquakeIntensityScope scope,
		EarthquakeHypocenterSnapshot? hypocenter,
		JmaIntensity maxIntensity,
		LpgmIntensity? maxLpgmIntensity,
		IReadOnlyList<PrefectureIntensitySnapshot> prefectures)
		=> new(
			Scope: scope,
			Hypocenter: hypocenter,
			MaxIntensity: maxIntensity,
			MaxLpgmIntensity: maxLpgmIntensity,
			DetectionTime: null,
			SokuhouPlace: null,
			IsSingleArea: true,
			Comment: null,
			FreeFormComment: null,
			UpdatedTime: new DateTime(2026, 4, 30, 14, 30, 0),
			Prefectures: prefectures);

	[Fact(DisplayName = "snapshot=null で空のPresentationを返す")]
	public void NullSnapshot_ReturnsEmptyPresentation()
	{
		var presentation = EarthquakeMapPresentationBuilder.Build(null, null, DefaultConfig());

		Assert.Empty(presentation.Hypocenters);
		Assert.Empty(presentation.AreaItems);
		Assert.Null(presentation.CityItems);
		Assert.Null(presentation.StationItems);
		Assert.Null(presentation.AutoZoom);
		Assert.Empty(presentation.Groups);
	}

	[Fact(DisplayName = "Scope=HypocenterOnly でHypocentersのみ、観測点系dictは空/null")]
	public void HypocenterOnly_PopulatesHypocentersOnly()
	{
		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.HypocenterOnly,
			hypocenter: Hypocenter(),
			maxIntensity: JmaIntensity.Unknown,
			maxLpgmIntensity: null,
			prefectures: []);

		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, DefaultConfig());

		Assert.Single(presentation.Hypocenters);
		Assert.Empty(presentation.AreaItems);
		Assert.Null(presentation.CityItems);
		Assert.Null(presentation.StationItems);
		Assert.Empty(presentation.Groups);
		Assert.NotNull(presentation.AutoZoom);
	}

	[Fact(DisplayName = "Scope=Detail で観測点ツリーを含むPresentationを生成")]
	public void Detail_BuildsObservationItems()
	{
		var station = new StationIntensitySnapshot(
			Name: "栗原市築館",
			Code: 410500,
			Intensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			LpgmByPeriod: null,
			Location: new Location(38.7f, 140.9f));

		var city = new CityIntensitySnapshot(
			Name: "栗原市",
			Code: 4105,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			CenterLocation: new Location(38.7f, 140.95f),
			Stations: [station]);

		var area = new AreaIntensitySnapshot(
			Name: "宮城県北部",
			Code: 410,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			CenterLocation: new Location(38.7f, 141.0f),
			Cities: [city]);

		var pref = new PrefectureIntensitySnapshot(
			Name: "宮城県",
			Code: 4,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			Areas: [area]);

		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.Detail,
			hypocenter: Hypocenter(),
			maxIntensity: JmaIntensity.Int4,
			maxLpgmIntensity: null,
			prefectures: [pref]);

		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, DefaultConfig());

		Assert.NotNull(presentation.StationItems);
		Assert.NotNull(presentation.CityItems);
		Assert.True(presentation.StationItems!.ContainsKey(JmaIntensity.Int4));
		Assert.True(presentation.CityItems!.ContainsKey(JmaIntensity.Int4));
		Assert.NotEmpty(presentation.Groups);
		Assert.NotNull(presentation.AutoZoom);
	}

	[Fact(DisplayName = "Scope=AreaOnly でAreaItems が震度別に集約される")]
	public void AreaOnly_BuildsAreaItems()
	{
		var area1 = new AreaIntensitySnapshot(
			Name: "宮城県北部",
			Code: 410,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			CenterLocation: new Location(38.7f, 141.0f),
			Cities: []);
		var area2 = new AreaIntensitySnapshot(
			Name: "岩手県内陸南部",
			Code: 320,
			MaxIntensity: JmaIntensity.Int3,
			MaxLpgmIntensity: null,
			CenterLocation: new Location(39.5f, 141.2f),
			Cities: []);

		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.AreaOnly,
			hypocenter: null,
			maxIntensity: JmaIntensity.Int4,
			maxLpgmIntensity: null,
			prefectures:
			[
				new PrefectureIntensitySnapshot("宮城県", 4, JmaIntensity.Int4, null, [area1]),
				new PrefectureIntensitySnapshot("岩手県", 3, JmaIntensity.Int3, null, [area2]),
			]);

		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, DefaultConfig());

		Assert.Equal(2, presentation.AreaItems.Count);
		Assert.True(presentation.AreaItems.ContainsKey(JmaIntensity.Int4));
		Assert.True(presentation.AreaItems.ContainsKey(JmaIntensity.Int3));
		Assert.Equal(2, presentation.Groups.Length);
	}

	[Fact(DisplayName = "MapData=null でも CenterLocation ベースの zoomPoints が動く")]
	public void NullMapData_FallsBackToCenterLocation()
	{
		var area = new AreaIntensitySnapshot(
			Name: "宮城県北部",
			Code: 410,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			CenterLocation: new Location(38.7f, 141.0f),
			Cities: []);

		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.AreaOnly,
			hypocenter: null,
			maxIntensity: JmaIntensity.Int4,
			maxLpgmIntensity: null,
			prefectures:
			[
				new PrefectureIntensitySnapshot("宮城県", 4, JmaIntensity.Int4, null, [area]),
			]);

		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, DefaultConfig());

		Assert.NotNull(presentation.AutoZoom);
	}

	[Fact(DisplayName = "Config.FillSokuhou=false で AreaOnly でも colorMap[Subdivision] が空")]
	public void FillSokuhouFalse_LeavesColorMapEmpty()
	{
		var area = new AreaIntensitySnapshot(
			Name: "宮城県北部",
			Code: 410,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: null,
			CenterLocation: new Location(38.7f, 141.0f),
			Cities: []);

		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.AreaOnly,
			hypocenter: null,
			maxIntensity: JmaIntensity.Int4,
			maxLpgmIntensity: null,
			prefectures:
			[
				new PrefectureIntensitySnapshot("宮城県", 4, JmaIntensity.Int4, null, [area]),
			]);

		var config = new EarthquakeConfig { FillSokuhou = false };
		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, config);

		Assert.True(presentation.ColorMap[KyoshinEewViewer.Map.LandLayerType.EarthquakeInformationSubdivisionArea].Count == 0);
	}

	[Fact(DisplayName = "Scope=Lpgm で LpgmGroups が非空・LpgmColorMap が非null")]
	public void Lpgm_Scope_BuildsLpgmGroups()
	{
		var station = new StationIntensitySnapshot(
			Name: "東京千代田区",
			Code: 130100,
			Intensity: JmaIntensity.Int4,
			MaxLpgmIntensity: LpgmIntensity.LpgmInt2,
			LpgmByPeriod: null,
			Location: new Location(35.69f, 139.75f));

		var city = new CityIntensitySnapshot(
			Name: "千代田区",
			Code: 13101,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: LpgmIntensity.LpgmInt2,
			CenterLocation: new Location(35.69f, 139.75f),
			Stations: [station]);

		var area = new AreaIntensitySnapshot(
			Name: "東京都23区",
			Code: 350,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: LpgmIntensity.LpgmInt2,
			CenterLocation: new Location(35.69f, 139.75f),
			Cities: [city]);

		var pref = new PrefectureIntensitySnapshot(
			Name: "東京都",
			Code: 13,
			MaxIntensity: JmaIntensity.Int4,
			MaxLpgmIntensity: LpgmIntensity.LpgmInt2,
			Areas: [area]);

		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.Lpgm,
			hypocenter: Hypocenter(),
			maxIntensity: JmaIntensity.Int4,
			maxLpgmIntensity: LpgmIntensity.LpgmInt2,
			prefectures: [pref]);

		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, DefaultConfig());

		Assert.NotEmpty(presentation.LpgmGroups);
		Assert.Contains(presentation.LpgmGroups, g => g.Intensity == LpgmIntensity.LpgmInt2);
		Assert.NotNull(presentation.LpgmColorMap);
	}

	[Fact(DisplayName = "Scope!=Lpgm で LpgmGroups が空・LpgmColorMap が null")]
	public void NonLpgm_Scope_LeavesLpgmGroupsEmpty()
	{
		var snapshot = Snapshot(
			scope: EarthquakeIntensityScope.Detail,
			hypocenter: Hypocenter(),
			maxIntensity: JmaIntensity.Int4,
			maxLpgmIntensity: null,
			prefectures: []);

		var presentation = EarthquakeMapPresentationBuilder.Build(snapshot, null, DefaultConfig());

		Assert.Empty(presentation.LpgmGroups);
		Assert.Null(presentation.LpgmColorMap);
	}
}
