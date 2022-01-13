using KyoshinMonitorLib;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class ObservationIntensityGroup
{
	public ObservationIntensityGroup(JmaIntensity intensity)
	{
		Intensity = intensity;
	}

	public JmaIntensity Intensity { get; }
	public List<ObservationPrefectureArea> PrefectureAreas { get; } = new();
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

		var area = pref.MunicipalityAreas.FirstOrDefault(m => m.AreaCode == areaCode);
		if (area == null)
			pref.MunicipalityAreas.Add(new(areaName, areaCode));
	}

	public static void AddStation(
		this List<ObservationIntensityGroup> groups,
		JmaIntensity intensity,
		string prefName, int prefCode,
		string areaName, int areaCode,
		string cityName, int cityCode,
		string stationName, int stationCode)
	{
		var group = groups.FirstOrDefault(g => g.Intensity == intensity);
		if (group == null)
			groups.Add(group = new(intensity));

		var pref = group.PrefectureAreas.FirstOrDefault(p => p.AreaCode == prefCode);
		if (pref == null)
			group.PrefectureAreas.Add(pref = new(prefName, prefCode));

		var area = pref.MunicipalityAreas.FirstOrDefault(m => m.AreaCode == areaCode);
		if (area == null)
			pref.MunicipalityAreas.Add(area = new(areaName, areaCode));

		var city = area.CityAreas.FirstOrDefault(p => p.AreaCode == cityCode);
		if (city == null)
			area.CityAreas.Add(city = new(cityName, cityCode));

		var stat = city.Points.FirstOrDefault(p => p.Code == stationCode);
		if (stat == null)
			city.Points.Add(new(stationName, stationCode));
	}
}

public class ObservationPrefectureArea
{
	public ObservationPrefectureArea(string name, int areaCode)
	{
		Name = name;
		AreaCode = areaCode;
	}

	public string Name { get; }
	public int AreaCode { get; }
	public List<ObservationMunicipalityArea> MunicipalityAreas { get; set; } = new();
}

public class ObservationMunicipalityArea
{
	public ObservationMunicipalityArea(string name, int areaCode)
	{
		Name = name;
		AreaCode = areaCode;
	}

	public string Name { get; }
	public int AreaCode { get; }
	public List<ObservationCityArea> CityAreas { get; } = new();
}
public class ObservationCityArea
{
	public ObservationCityArea(string name, int areaCode)
	{
		Name = name;
		AreaCode = areaCode;
	}

	public string Name { get; }
	public int AreaCode { get; }
	public List<ObservationPoint> Points { get; } = new();

	public class ObservationPoint
	{
		public ObservationPoint(string name, int code)
		{
			Name = name;
			Code = code;
		}

		public string Name { get; }
		public int Code { get; }
	}
}
