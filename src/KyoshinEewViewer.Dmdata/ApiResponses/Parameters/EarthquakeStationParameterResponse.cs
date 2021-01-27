using KyoshinMonitorLib;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.ApiResponses.Parameters
{
	public class EarthquakeStationParameterResponse : DmdataParameterResponse
	{
		/// <summary>
		/// 震度観測点一覧
		/// </summary>
		[JsonPropertyName("items")]
		public Item[] Items { get; set; }

		/// <summary>
		/// 観測点を表す
		/// </summary>
		public class Item
		{
			/// <summary>
			/// 一次細分化地域
			/// </summary>
			[JsonPropertyName("region")]
			public Region Region { get; set; }
			/// <summary>
			/// 市区町村
			/// </summary>
			[JsonPropertyName("city")]
			public City City { get; set; }
			/// <summary>
			/// 観測点コード
			/// </summary>
			[JsonPropertyName("noCode")]
			public string NoCode { get; set; }
			/// <summary>
			/// 観測点コード（XML）
			/// </summary>
			[JsonPropertyName("code")]
			public string Code { get; set; }
			/// <summary>
			/// 観測点名
			/// </summary>
			[JsonPropertyName("name")]
			public string Name { get; set; }
			/// <summary>
			/// 観測点名（カナ）
			/// </summary>
			[JsonPropertyName("kana")]
			public string Kana { get; set; }
			/// <summary>
			/// データの運用状態
			/// <para>現: 運用中</para>
			/// <para>変更: 名称、住所、位置情報の修正</para>
			/// <para>新規: パラメータ変更時刻より運用開始</para>
			/// <para>廃止: パラメータ変更時刻をもって運用終了</para>
			/// </summary>
			[JsonPropertyName("status")]
			public string Status { get; set; }
			/// <summary>
			/// 所属する機関
			/// </summary>
			[JsonPropertyName("owner")]
			public string Owner { get; set; }
			/// <summary>
			/// 緯度
			/// </summary>
			[JsonPropertyName("latitude")]
			public string Latitude { get; set; }
			/// <summary>
			/// 緯度
			/// </summary>
			[JsonPropertyName("longitude")]
			public string Longitude { get; set; }

			/// <summary>
			/// 緯度経度をインスタンスとして取得します
			/// </summary>
			public Location Location
			{
				get
				{
					if (!float.TryParse(Latitude, out var lat) || !float.TryParse(Longitude, out var lng))
						return null;
					return new Location(lat, lng);
				}
			}
		}
		/// <summary>
		/// 一次細分化地域を表す
		/// </summary>
		public class Region
		{
			/// <summary>
			/// 一次細分化地域コード
			/// </summary>
			[JsonPropertyName("code")]
			public string Code { get; set; }
			/// <summary>
			/// 一次細分化地域名
			/// </summary>
			[JsonPropertyName("name")]
			public string Name { get; set; }
			/// <summary>
			/// 一次細分化地域名（カナ）
			/// </summary>
			[JsonPropertyName("kana")]
			public string Kana { get; set; }
		}
		/// <summary>
		/// 市区町村を表す
		/// </summary>
		public class City
		{
			/// <summary>
			/// 市区町村コード
			/// </summary>
			[JsonPropertyName("code")]
			public string Code { get; set; }
			/// <summary>
			/// 市区町村名
			/// </summary>
			[JsonPropertyName("name")]
			public string Name { get; set; }
			/// <summary>
			/// 市区町村名（カナ）
			/// </summary>
			[JsonPropertyName("kana")]
			public string Kana { get; set; }
		}
	}
}
