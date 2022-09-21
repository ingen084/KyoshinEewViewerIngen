using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.Typhoon.Models;

// 予報を含めた台風
public record TyphoonItem(
	string Id,
	string? Name,
	bool IsEliminated,
	TyphoonPlace Current,
	TyphoonPlace? Estimate)
{
	public Location[]? LocationHistory { get; set; }
	public TyphoonPlace[]? ForecastPlaces { get; set; }
}

// 台風の位置
public record TyphoonPlace(
	string? AreaClass,
	string? IntensityClass,
	DateTime TargetDateTime,
	string? TargetDateType,
	string? CenterPosition,
	int CentralPressure,
	int? MaximumWindSpeed,
	bool IsMaximumWindSpeedIsCenterNear,
	int? MaximumInstantaneousWindSpeed,
	Location Center,
	TyphoonRenderCircle? Strong,
	TyphoonRenderCircle? Storm);

// 台風の円
public record TyphoonRenderCircle(
	Location Center,
	double RangeKilometer,
	Location RawCenter);
