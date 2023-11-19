using KyoshinMonitorLib;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class ObservationIntensityGroup(JmaIntensity intensity)
{
	public JmaIntensity Intensity { get; } = intensity;
	public List<ObservationPrefectureArea> PrefectureAreas { get; } = [];
}

public static class ObservationIntensityGroupExtensions
{
	public static void AddArea(
		this List<ObservationIntensityGroup> groups,
		JmaIntensity intensity,
		string prefName, int prefCode,
		string areaName, int areaCode)
	{
		var group = groups.FirstOrDefault(g => g.Intensity == intensity);
		if (group == null)
			groups.Add(group = new(intensity));

		var pref = group.PrefectureAreas.FirstOrDefault(p => p.AreaCode == prefCode);
		if (pref == null)
			group.PrefectureAreas.Add(pref = new(prefName, prefCode));

		var area = pref.Areas.FirstOrDefault(m => m.AreaCode == areaCode);
		if (area == null)
			pref.Areas.Add(new ObservationMunicipalityArea(areaName, areaCode));
	}

	public static void AddStation(
		this List<ObservationIntensityGroup> groups,
		JmaIntensity intensity,
		string prefName, int prefCode,
		string cityName, int cityCode,
		string stationName, string stationCode)
	{
		var group = groups.FirstOrDefault(g => g.Intensity == intensity);
		if (group == null)
			groups.Add(group = new(intensity));

		var pref = group.PrefectureAreas.FirstOrDefault(p => p.AreaCode == prefCode);
		if (pref == null)
			group.PrefectureAreas.Add(pref = new(prefName, prefCode));

		if (pref.Areas.FirstOrDefault(p => p.AreaCode == cityCode) is not ObservationCityArea city)
			pref.Areas.Add(city = new ObservationCityArea(cityName, cityCode));

		var stat = city.Points.FirstOrDefault(p => p.Code == stationCode);
		if (stat == null)
			city.Points.Add(new(stationName, stationCode));
	}
}

public class ObservationPrefectureArea(string name, int areaCode)
{
	public string Name { get; } = name;
	public int AreaCode { get; } = areaCode;
	public List<ObservationDetailArea> Areas { get; } = [];
}


public abstract class ObservationDetailArea
{
	protected ObservationDetailArea(string name, int areaCode)
	{
		Name = name;
		AreaCode = areaCode;
	}

	public string Name { get; }
	public int AreaCode { get; }
}
public class ObservationMunicipalityArea(string name, int areaCode) : ObservationDetailArea(name, areaCode)
{
	public List<ObservationCityArea> CityAreas { get; } = [];
}

public class ObservationCityArea(string name, int areaCode) : ObservationDetailArea(name, areaCode)
{
	public List<ObservationPoint> Points { get; } = [];
}

public class ObservationPoint(string name, string code)
{
	public string Name { get; } = name;
	public string Code { get; } = code;
}
