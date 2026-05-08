using KyoshinEewViewer.Series.Earthquake.Models;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Services;

internal static class ObservationEqualityHelper
{
	public static bool AreEqual(
		IReadOnlyList<PrefectureIntensitySnapshot>? a,
		IReadOnlyList<PrefectureIntensitySnapshot>? b)
	{
		if (ReferenceEquals(a, b)) return true;
		if (a is null || b is null) return a is null && b is null;
		if (a.Count != b.Count) return false;

		var bByCode = b.ToDictionary(p => p.Code);
		foreach (var pa in a)
		{
			if (!bByCode.TryGetValue(pa.Code, out var pb)) return false;
			if (!ArePrefectureEqual(pa, pb)) return false;
		}
		return true;
	}

	private static bool ArePrefectureEqual(PrefectureIntensitySnapshot a, PrefectureIntensitySnapshot b)
	{
		if (a.Name != b.Name) return false;
		if (a.MaxIntensity != b.MaxIntensity) return false;
		if (a.MaxLpgmIntensity != b.MaxLpgmIntensity) return false;
		return AreAreasEqual(a.Areas, b.Areas);
	}

	private static bool AreAreasEqual(IReadOnlyList<AreaIntensitySnapshot> a, IReadOnlyList<AreaIntensitySnapshot> b)
	{
		if (a.Count != b.Count) return false;
		var bByCode = b.ToDictionary(x => x.Code);
		foreach (var aa in a)
		{
			if (!bByCode.TryGetValue(aa.Code, out var ba)) return false;
			if (aa.Name != ba.Name) return false;
			if (aa.MaxIntensity != ba.MaxIntensity) return false;
			if (aa.MaxLpgmIntensity != ba.MaxLpgmIntensity) return false;
			if (!AreCitiesEqual(aa.Cities, ba.Cities)) return false;
		}
		return true;
	}

	private static bool AreCitiesEqual(IReadOnlyList<CityIntensitySnapshot> a, IReadOnlyList<CityIntensitySnapshot> b)
	{
		if (a.Count != b.Count) return false;
		var bByCode = b.ToDictionary(x => x.Code);
		foreach (var ac in a)
		{
			if (!bByCode.TryGetValue(ac.Code, out var bc)) return false;
			if (ac.Name != bc.Name) return false;
			if (ac.MaxIntensity != bc.MaxIntensity) return false;
			if (ac.MaxLpgmIntensity != bc.MaxLpgmIntensity) return false;
			if (!AreStationsEqual(ac.Stations, bc.Stations)) return false;
		}
		return true;
	}

	private static bool AreStationsEqual(IReadOnlyList<StationIntensitySnapshot> a, IReadOnlyList<StationIntensitySnapshot> b)
	{
		if (a.Count != b.Count) return false;
		var bByCode = b.ToDictionary(x => x.Code);
		foreach (var astn in a)
		{
			if (!bByCode.TryGetValue(astn.Code, out var bstn)) return false;
			if (astn.Name != bstn.Name) return false;
			if (astn.Intensity != bstn.Intensity) return false;
			if (astn.MaxLpgmIntensity != bstn.MaxLpgmIntensity) return false;
			if (!AreLpgmByPeriodEqual(astn.LpgmByPeriod, bstn.LpgmByPeriod)) return false;
		}
		return true;
	}

	private static bool AreLpgmByPeriodEqual(IReadOnlyList<LgIntByPeriod>? a, IReadOnlyList<LgIntByPeriod>? b)
	{
		if (a is null && b is null) return true;
		if (a is null || b is null) return false;
		if (a.Count != b.Count) return false;
		var bByBand = b.ToDictionary(x => x.PeriodicBand);
		foreach (var ap in a)
		{
			if (!bByBand.TryGetValue(ap.PeriodicBand, out var bp)) return false;
			if (ap.Intensity != bp.Intensity) return false;
		}
		return true;
	}
}
