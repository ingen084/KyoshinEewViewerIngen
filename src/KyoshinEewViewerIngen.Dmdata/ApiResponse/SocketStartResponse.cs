using System.Text.Json.Serialization;

namespace KyoshinEewViewerIngen.Dmdata.ApiResponse
{
	/// <summary>
	/// SocketStartのレスポンス
	/// </summary>
	public class SocketStartResponse : DmdataResponse
	{
		/// <summary>
		/// WSエンドポイントに接続するためのKey
		/// </summary>
		[JsonPropertyName("key")]
		public string Key { get; set; }
		/// <summary>
		/// WSエンドポイントのURL(Keyつき)
		/// </summary>
		[JsonPropertyName("url")]
		public string Url { get; set; }
		/// <summary>
		/// WebSocketProtocol
		/// </summary>
		[JsonPropertyName("protocol")]
		public string[] Protocol { get; set; }
		/// <summary>
		/// 取得できる配信区分
		/// </summary>
		[JsonPropertyName("classification")]
		public string[] Classification { get; set; }
		/// <summary>
		/// キーの有効時間(
		/// </summary>
		[JsonPropertyName("expiration")]
		public int Expiration { get; set; }
	}

}
