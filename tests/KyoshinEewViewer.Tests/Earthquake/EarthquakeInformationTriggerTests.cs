using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Earthquake;

public class EarthquakeInformationTriggerTests
{
	private static EarthquakeInformationEvent CreateEvent(
		string infoName = "震度速報",
		JmaIntensity intensity = JmaIntensity.Int3,
		JmaIntensity? previousIntensity = null,
		bool isRegionUpdated = false,
		ObservationDiff? regionDiff = null)
	{
		return new EarthquakeInformationEvent(null)
		{
			LatestInformationName = infoName,
			EarthquakeId = "test",
			MaxIntensity = intensity,
			PreviousMaxIntensity = previousIntensity,
			IsRegionUpdated = isRegionUpdated,
			RegionDiff = regionDiff,
		};
	}

	[Fact(DisplayName = "地域更新フィルタON＋地域更新ありでトリガー発火")]
	public void RegionUpdateFilter_WithUpdate_Triggers()
	{
		var trigger = new EarthquakeInformationTrigger
		{
			Intensity = JmaIntensity.Unknown,
			IncludeGreaterIntensity = true,
			EnableRegionUpdateFilter = true,
			EnableSokuhou = true,
		};
		var evt = CreateEvent(isRegionUpdated: true);

		Assert.True(trigger.CheckTrigger(evt));
	}

	[Fact(DisplayName = "地域更新フィルタON＋地域更新なしでトリガー不発火")]
	public void RegionUpdateFilter_WithoutUpdate_DoesNotTrigger()
	{
		var trigger = new EarthquakeInformationTrigger
		{
			Intensity = JmaIntensity.Unknown,
			IncludeGreaterIntensity = true,
			EnableRegionUpdateFilter = true,
			EnableSokuhou = true,
		};
		var evt = CreateEvent(isRegionUpdated: false);

		Assert.False(trigger.CheckTrigger(evt));
	}

	[Fact(DisplayName = "地域更新フィルタOFFでは地域更新の有無に関係なくトリガー発火")]
	public void RegionUpdateFilter_Disabled_TriggersRegardless()
	{
		var trigger = new EarthquakeInformationTrigger
		{
			Intensity = JmaIntensity.Unknown,
			IncludeGreaterIntensity = true,
			EnableRegionUpdateFilter = false,
			EnableSokuhou = true,
		};
		var evtNoUpdate = CreateEvent(isRegionUpdated: false);
		var evtWithUpdate = CreateEvent(isRegionUpdated: true);

		Assert.True(trigger.CheckTrigger(evtNoUpdate));
		Assert.True(trigger.CheckTrigger(evtWithUpdate));
	}

	[Fact(DisplayName = "地域更新フィルタと最大震度変更時フィルタの組み合わせ")]
	public void RegionUpdateFilter_WithIntensityChangeOnly()
	{
		var trigger = new EarthquakeInformationTrigger
		{
			Intensity = JmaIntensity.Unknown,
			IncludeGreaterIntensity = true,
			IsIntensityChangeOnly = true,
			IsIntensityIncreaseOnly = true,
			EnableRegionUpdateFilter = true,
		};

		// 震度上昇あり＋地域更新あり → 発火
		var evt1 = CreateEvent(intensity: JmaIntensity.Int4, previousIntensity: JmaIntensity.Int3, isRegionUpdated: true);
		Assert.True(trigger.CheckTrigger(evt1));

		// 震度上昇あり＋地域更新なし → 不発火（地域更新フィルタでブロック）
		var evt2 = CreateEvent(intensity: JmaIntensity.Int4, previousIntensity: JmaIntensity.Int3, isRegionUpdated: false);
		Assert.False(trigger.CheckTrigger(evt2));

		// 震度変更なし＋地域更新あり → 不発火（震度変更フィルタでブロック）
		var evt3 = CreateEvent(intensity: JmaIntensity.Int3, previousIntensity: JmaIntensity.Int3, isRegionUpdated: true);
		Assert.False(trigger.CheckTrigger(evt3));
	}

	[Theory(DisplayName = "各情報種別と地域更新フィルタの組み合わせ")]
	[InlineData("震度速報", true)]
	[InlineData("震源に関する情報", true)]
	[InlineData("震源・震度に関する情報", true)]
	[InlineData("長周期地震動に関する観測情報", true)]
	public void RegionUpdateFilter_WithInfoTypes(string infoName, bool enableType)
	{
		var trigger = new EarthquakeInformationTrigger
		{
			Intensity = JmaIntensity.Unknown,
			IncludeGreaterIntensity = true,
			EnableRegionUpdateFilter = true,
			EnableSokuhou = enableType,
			EnableEpicenter = enableType,
			EnableDetail = enableType,
			EnableLpgm = enableType,
		};
		var evt = CreateEvent(infoName: infoName, isRegionUpdated: true);

		Assert.True(trigger.CheckTrigger(evt));
	}
}
