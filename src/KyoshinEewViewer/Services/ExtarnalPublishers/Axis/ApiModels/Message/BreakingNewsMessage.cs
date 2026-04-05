using System;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。'required' 修飾子を追加するか、Null 許容として宣言することを検討してください。

public class BreakingNewsMessage
{
	public string Title { get; set; }
	public string[] Text { get; set; }
	public DateTime ReportDateTime { get; set; }
}

