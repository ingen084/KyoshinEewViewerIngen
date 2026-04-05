using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。'required' 修飾子を追加するか、Null 許容として宣言することを検討してください。

public class EewMessage
{
	public string? Title { get; set; }
	public DateTime OriginDateTime { get; set; }
	public DateTime ReportDateTime { get; set; }
	public string? EventID { get; set; }
	public int Serial { get; set; }
	public EewHypocenter Hypocenter { get; set; }
	public string? Intensity { get; set; }
	public string? Magnitude { get; set; }
	public EewFlag? Flag { get; set; }
	public EewForecast[]? Forecast { get; set; }
	public string? Text { get; set; }
}

public class EewHypocenter
{
	public int Code { get; set; }
	public string? Name { get; set; }
	public float[]? Coordinate { get; set; }
	public string? Depth { get; set; }
	public string? Description { get; set; }
}

public class EewFlag
{
	[JsonPropertyName("is_final")]
	public bool IsFinal { get; set; }
	[JsonPropertyName("is_cancel")]
	public bool IsCancel { get; set; }
	[JsonPropertyName("is_training")]
	public bool IsTraining { get; set; }
}

public class EewForecast
{
	public int Code { get; set; }
	public string? Name { get; set; }
	public EewIntensity? Intensity { get; set; }
}

public class EewIntensity
{
	public string? From { get; set; }
	public string? To { get; set; }
	public string? Description { get; set; }
}

