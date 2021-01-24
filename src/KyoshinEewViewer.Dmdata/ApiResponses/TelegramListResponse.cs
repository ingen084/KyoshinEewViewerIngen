using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.ApiResponses
{
	/// <summary>
	/// 電文リストのレスポンス
	/// </summary>
	public class TelegramListResponse : DmdataResponse
	{
		/// <summary>
		/// 電文情報
		/// </summary>
		[JsonPropertyName("items")]
		public TelegramItem[] Items { get; set; }
		/// <summary>
		/// 次回以降のクエリパラメーターに入れて新しい情報のみを表示させるための値
		/// </summary>
		[JsonPropertyName("newCatch")]
		public int NewCatch { get; set; }
	}

	/// <summary>
	/// 電文情報
	/// </summary>
	public class TelegramItem
	{
		/// <summary>
		/// 配信区分
		/// </summary>
		[JsonPropertyName("classification")]
		public string Classification { get; set; }
		/// <summary>
		/// 配信データを区別するハッシュ
		/// </summary>
		[JsonPropertyName("key")]
		public string Key { get; set; }
		/// <summary>
		/// 電文ヘッダ情報
		/// </summary>
		[JsonPropertyName("data")]
		public TelegramData Data { get; set; }
		/// <summary>
		/// 電文本体があるURL
		/// </summary>
		[JsonPropertyName("url")]
		public string Url { get; set; }
		/// <summary>
		/// XML電文におけるHead/Controlの情報
		/// </summary>
		[JsonPropertyName("xmlData")]
		public TelegramXmldata XmlData { get; set; }
	}

	/// <summary>
	/// 電文ヘッダ情報
	/// </summary>
	public class TelegramData
	{
		/// <summary>
		/// データ種類コード
		/// </summary>
		[JsonPropertyName("type")]
		public string Type { get; set; }
		/// <summary>
		/// 発表英字官署名
		/// </summary>
		[JsonPropertyName("author")]
		public string Author { get; set; }
		/// <summary>
		/// 基点時刻
		/// </summary>
		[JsonPropertyName("time")]
		public DateTime Time { get; set; }
		/// <summary>
		/// 訓練･試験等のテスト等電文か
		/// <para>注意：XML電文以外のテスト配信は常にfalseになります。本文中を参照するようにしてください。</para>
		/// </summary>
		[JsonPropertyName("test")]
		public bool Test { get; set; }
		/// <summary>
		/// XML電文か
		/// </summary>
		[JsonPropertyName("xml")]
		public bool Xml { get; set; }
		/// <summary>
		/// 気象業務支援センター電文生成時刻
		/// </summary>
		[JsonPropertyName("createTime")]
		public DateTime CreateTime { get; set; }
		/// <summary>
		/// 気象業務支援センター付与通過番号
		/// </summary>
		[JsonPropertyName("sendNumber")]
		public int SendNumber { get; set; }
	}

	/// <summary>
	/// XML電文のControl/Head情報
	/// </summary>
	public class TelegramXmldata
	{
		[JsonPropertyName("control")]
		public TelegramXmlControl Control { get; set; }
		[JsonPropertyName("head")]
		public TelegramXmlHead Head { get; set; }
	}

	/// <summary>
	/// XML電文のControl情報
	/// </summary>
	public class TelegramXmlControl
	{
		/// <summary>
		/// 情報名称
		/// </summary>
		[JsonPropertyName("title")]
		public string Title { get; set; }
		/// <summary>
		/// 発表時刻
		/// </summary>
		[JsonPropertyName("dateTime")]
		public DateTime DateTime { get; set; }
		/// <summary>
		/// 運用種別
		/// <para>"通常" 以外利用してはいけない</para>
		/// <para>通常/試験/訓練</para>
		/// </summary>
		[JsonPropertyName("status")]
		public string Status { get; set; }
		/// <summary>
		/// 編集官署名
		/// </summary>
		[JsonPropertyName("editorialOffice")]
		public string EditorialOffice { get; set; }
		/// <summary>
		/// 発表官署名
		/// </summary>
		[JsonPropertyName("publishingOffice")]
		public string PublishingOffice { get; set; }
	}

	/// <summary>
	/// XML電文のHead情報
	/// </summary>
	public class TelegramXmlHead
	{
		/// <summary>
		/// 情報表題
		/// </summary>
		[JsonPropertyName("title")]
		public string Title { get; set; }
		/// <summary>
		/// 公式な発表時刻
		/// </summary>
		[JsonPropertyName("reportDateTime")]
		public DateTime ReportDateTime { get; set; }
		/// <summary>
		/// 基点時刻
		/// </summary>
		[JsonPropertyName("targetDateTime")]
		public DateTime TargetDateTime { get; set; }
		/// <summary>
		/// 基点時刻のあいまいさ（頃、など）
		/// </summary>
		[JsonPropertyName("targetDateTimeDubious")]
		public string TargetDateTimeDubious { get; set; }
		/// <summary>
		/// 予報期間
		/// </summary>
		[JsonPropertyName("targetDuration")]
		public string TargetDuration { get; set; }
		/// <summary>
		/// 情報の失効時刻
		/// </summary>
		[JsonPropertyName("validDateTime")]
		public DateTime? ValidDateTime { get; set; }
		/// <summary>
		/// 電文識別情報
		/// </summary>
		[JsonPropertyName("eventId")]
		public string EventID { get; set; }
		/// <summary>
		/// 電文情報番号
		/// </summary>
		[JsonPropertyName("serial")]
		public string Serial { get; set; }
		/// <summary>
		/// 電文発表形態
		/// <para>発表/訂正/遅延/取消</para>
		/// </summary>
		[JsonPropertyName("infoType")]
		public string InfoType { get; set; }
		/// <summary>
		/// XML電文スキーマの運用種別情報
		/// </summary>
		[JsonPropertyName("infoKind")]
		public string InfoKind { get; set; }
		/// <summary>
		/// XML電文スキーマの運用種別情報のバージョン
		/// </summary>
		[JsonPropertyName("infoKindVersion")]
		public string InfoKindVersion { get; set; }
		/// <summary>
		/// 見出し文
		/// </summary>
		[JsonPropertyName("headline")]
		public string Headline { get; set; }
	}
}
