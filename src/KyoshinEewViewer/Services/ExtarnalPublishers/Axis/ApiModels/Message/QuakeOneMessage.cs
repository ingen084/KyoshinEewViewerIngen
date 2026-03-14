using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。'required' 修飾子を追加するか、Null 許容として宣言することを検討してください。

public class QuakeOneMessage
{
	[JsonPropertyName("headline")]
	public string Headline { get; set; }
	[JsonPropertyName("info")]
	public QuakeOneEarthquakeInfo Info { get; set; }
	[JsonPropertyName("image")]
	public string Image { get; set; }
	[JsonPropertyName("largeScalePoints")]
	public string LargeScalePoints { get; set; }
	[JsonPropertyName("smallScalePoints")]
	public string SmallScalePoints { get; set; }
}

public class QuakeOneEarthquakeInfo
{
	public string EventID { get; set; }
	public DateTime ReportDateTime { get; set; }
	public DateTime OriginDateTime { get; set; }
	public string MaxInt { get; set; }
	public string Magnitude { get; set; }
	public QuakeOneHypocenter Hypocenter { get; set; }
	public string Comments { get; set; }
}

public class QuakeOneHypocenter
{
	public string Name { get; set; }
	public float[] Coordinate { get; set; }
	public int Depth { get; set; }
	public string Japanese { get; set; }
}

