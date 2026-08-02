using System;

#pragma warning disable CS8618 // JSON DTOs are populated by deserialization.

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;

public class BreakingNewsMessage
{
	public string Title { get; set; }
	public string[] Text { get; set; }
	// DateTime で受けるとオフセット付きの値がマシンのローカル時刻へ変換されてしまうため、DateTimeOffset で受ける
	public DateTimeOffset ReportDateTime { get; set; }
}

