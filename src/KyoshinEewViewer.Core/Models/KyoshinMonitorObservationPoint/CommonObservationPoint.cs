using KyoshinMonitorLib;
using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;

public class CommonObservationPoint : IComparable
{
#nullable disable
	public CommonObservationPoint()
	{
	}
#nullable restore

	/// <summary>
	/// 観測地点のネットワークの種類
	/// </summary>
	[JsonPropertyName("type")]
	public ObservationPointType Type { get; set; }

	/// <summary>
	/// 観測点コード
	/// </summary>
	[JsonPropertyName("code")]
	public string Code { get; set; }

	/// <summary>
	/// 観測点名
	/// </summary>
	[JsonPropertyName("name")]
	public string Name { get; set; }

	/// <summary>
	/// 観測点広域名
	/// </summary>
	[JsonPropertyName("region")]
	public string Region { get; set; }

	/// <summary>
	/// 観測地点が休止状態(無効)かどうか
	/// </summary>
	[JsonPropertyName("is_suspended")]
	public bool IsSuspended { get; set; }

	/// <summary>
	/// 地理座標
	/// </summary>
	[JsonPropertyName("location")]
	public Location Location { get; set; }

	/// <summary>
	/// 地理座標(日本座標系)
	/// </summary>
	[JsonPropertyName("japanese_coordinate_system_location")]
	public Location? OldLocation { get; set; }

	/// <summary>
	/// 強震モニタ画像上での座標
	/// </summary>
	[JsonPropertyName("point")]
	public KyoshinImagePoint? Point { get; set; }

	/// <summary>
	/// ObservationPoint同士を比較します。
	/// </summary>
	/// <param name="obj">比較対象のObservationPoint</param>
	/// <returns></returns>
	public int CompareTo(object? obj)
	{
		if (obj is not CommonObservationPoint ins)
			throw new ArgumentException("比較対象はObservationPointにキャストできる型でなければなりません。");
		return Code.CompareTo(ins.Code);
	}

	public ObservationPointV2 ToV2()
		=> new()
		{
			Type = Type,
			Code = Code,
			Name = Name,
			Region = Region,
			IsSuspended = IsSuspended,
			Location = Location,
			Point = Point,
		};

}
