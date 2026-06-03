using System;

#pragma warning disable CS8618 // JSON DTOs are populated by deserialization.

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;

public class BreakingNewsMessage
{
	public string Title { get; set; }
	public string[] Text { get; set; }
	public DateTime ReportDateTime { get; set; }
}

