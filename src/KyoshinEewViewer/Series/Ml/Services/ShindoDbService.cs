using KyoshinMonitorLib;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Ml.Services;

/// <summary>
/// 気象庁震度データベースから地震情報を取得するサービス
/// </summary>
public class ShindoDbService
{
	private const string ApiUrl = "https://www.data.jma.go.jp/eqdb/data/shindo/api/";

	private HttpClient HttpClient { get; } = new()
	{
		Timeout = TimeSpan.FromSeconds(30)
	};

	/// <summary>
	/// 指定した期間の地震情報を取得する
	/// </summary>
	/// <param name="from">開始日時</param>
	/// <param name="to">終了日時</param>
	/// <returns>地震情報のリスト</returns>
	public async Task<ShindoDbEarthquake[]> SearchAsync(DateTime from, DateTime to)
	{
		from = from.AddMinutes(-5);
		to = to.AddSeconds(-1);
		// 複数の同名キーを送信するためのMultipart
		var content = new MultipartFormDataContent
		{
			{ new StringContent("search"), "mode" },
			{ new StringContent(from.ToString("yyyy-MM-dd")), "dateTimeF[]" },
			{ new StringContent(from.ToString("HH:mm")), "dateTimeF[]" },
			{ new StringContent(to.ToString("yyyy-MM-dd")), "dateTimeT[]" },
			{ new StringContent(to.ToString("HH:mm")), "dateTimeT[]" },
		};

		using var response = await HttpClient.PostAsync(ApiUrl, content);
		response.EnsureSuccessStatusCode();

		var json = await response.Content.ReadAsStringAsync();

		// JSONが空または無効な場合のチェック
		if (string.IsNullOrWhiteSpace(json))
			return [];

		// HTMLエラーページが返ってきた場合
		if (json.TrimStart().StartsWith('<'))
			throw new InvalidOperationException($"APIがHTMLを返しました: {json[..Math.Min(200, json.Length)]}");

		var result = JsonSerializer.Deserialize<ShindoDbResponse>(json);

		return result?.Res ?? [];
	}

	/// <summary>
	/// 震度に応じた表示時間（秒）を取得する
	/// </summary>
	/// <param name="intensity">最大震度（1～9）</param>
	/// <returns>表示時間（秒）</returns>
	/// <remarks>
	/// 震度1: 1.5分 (90秒)
	/// 震度2～3: 2分 (120秒)
	/// 震度4: 3分 (180秒)
	/// 震度5弱以上: 5分 (300秒)
	/// </remarks>
	public static int GetDisplayDurationSeconds(JmaIntensity? intensity)
		=> intensity switch
		{
			JmaIntensity.Int1 => 90,
			JmaIntensity.Int2 or JmaIntensity.Int3 or JmaIntensity.Int4 => 120,
			JmaIntensity.Int5Lower => 180,
			>= JmaIntensity.Int5Upper => 300,
			_ => 120
		};

	/// <summary>
	/// 時刻に対して有効な地震（震度に応じた表示時間内）を取得する
	/// </summary>
	/// <param name="earthquakes">地震リスト</param>
	/// <param name="currentTime">現在時刻</param>
	/// <returns>有効な地震リスト</returns>
	public static ShindoDbEarthquake[] GetActiveEarthquakes(IEnumerable<ShindoDbEarthquake> earthquakes, DateTime currentTime)
		=> earthquakes
			.Where(eq =>
			{
				if (eq.OccurrenceTime is not { } ot)
					return false;
				var elapsed = (currentTime - ot).TotalSeconds;
				var displayDuration = GetDisplayDurationSeconds(eq.MaxIntensity);
				return elapsed >= 0 && elapsed <= displayDuration;
			})
			.OrderByDescending(eq => eq.OccurrenceTime)
			.ToArray();
}

/// <summary>
/// 震度DBのAPIレスポンス
/// </summary>
public class ShindoDbResponse
{
	[JsonPropertyName("res")]
	[JsonConverter(typeof(ShindoDbEarthquakeArrayConverter))]
	public ShindoDbEarthquake[]? Res { get; set; }

	[JsonPropertyName("str")]
	public string[]? Str { get; set; }
}

/// <summary>
/// resフィールドが配列でない場合（空オブジェクトなど）に対応するコンバーター
/// </summary>
public class ShindoDbEarthquakeArrayConverter : JsonConverter<ShindoDbEarthquake[]?>
{
	public override ShindoDbEarthquake[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// 配列の場合は通常通りデシリアライズ
		if (reader.TokenType == JsonTokenType.StartArray)
			return JsonSerializer.Deserialize<ShindoDbEarthquake[]>(ref reader, options);

		// nullの場合
		if (reader.TokenType == JsonTokenType.Null)
			return null;

		// オブジェクトや他の型の場合はスキップして空配列を返す
		reader.Skip();
		return [];
	}

	public override void Write(Utf8JsonWriter writer, ShindoDbEarthquake[]? value, JsonSerializerOptions options)
	{
		JsonSerializer.Serialize(writer, value, options);
	}
}

/// <summary>
/// 震度DBの地震情報
/// </summary>
public class ShindoDbEarthquake
{
	/// <summary>
	/// 地震ID (例: "20251127014356")
	/// </summary>
	[JsonPropertyName("id")]
	public string? Id { get; set; }

	/// <summary>
	/// 発生時刻 (例: "2025/11/27 01:43:56.9")
	/// </summary>
	[JsonPropertyName("ot")]
	public string? Ot { get; set; }

	/// <summary>
	/// 震央地名
	/// </summary>
	[JsonPropertyName("name")]
	public string? Name { get; set; }

	/// <summary>
	/// 緯度文字列 (例: "42°27.6′N")
	/// </summary>
	[JsonPropertyName("latS")]
	public string? LatS { get; set; }

	/// <summary>
	/// 経度文字列 (例: "144°19.4′E")
	/// </summary>
	[JsonPropertyName("lonS")]
	public string? LonS { get; set; }

	/// <summary>
	/// 緯度 (例: "42.4600")
	/// </summary>
	[JsonPropertyName("lat")]
	public string? Lat { get; set; }

	/// <summary>
	/// 経度 (例: "144.3230")
	/// </summary>
	[JsonPropertyName("lon")]
	public string? Lon { get; set; }

	/// <summary>
	/// 深さ (例: "74 km")
	/// </summary>
	[JsonPropertyName("dep")]
	public string? Dep { get; set; }

	/// <summary>
	/// マグニチュード (例: "4.6")
	/// </summary>
	[JsonPropertyName("mag")]
	public string? Mag { get; set; }

	/// <summary>
	/// 最大震度 (例: "震度２")
	/// </summary>
	[JsonPropertyName("maxI")]
	public string? MaxI { get; set; }

	/// <summary>
	/// 最大震度のCSSクラス
	/// </summary>
	[JsonPropertyName("maxIcls")]
	public string? MaxIcls { get; set; }

	/// <summary>
	/// 最大震度 (例: "震度２")
	/// </summary>
	[JsonPropertyName("maxS")]
	public string? MaxS { get; set; }

	/// <summary>
	/// 最大震度のCSSクラス
	/// </summary>
	[JsonPropertyName("maxScls")]
	public string? MaxScls { get; set; }

	/// <summary>
	/// 発生時刻（パース済み）
	/// </summary>
	[JsonIgnore]
	public DateTime? OccurrenceTime
	{
		get
		{
			if (string.IsNullOrEmpty(Ot))
				return null;
			// "2025/11/27 01:43:56.9" 形式をパース
			if (DateTime.TryParse(Ot, out var dt))
				return dt;
			return null;
		}
	}

	/// <summary>
	/// 緯度（数値）
	/// </summary>
	[JsonIgnore]
	public float? Latitude
	{
		get
		{
			if (string.IsNullOrEmpty(Lat))
				return null;
			if (float.TryParse(Lat, out var lat))
				return lat;
			return null;
		}
	}

	/// <summary>
	/// 経度（数値）
	/// </summary>
	[JsonIgnore]
	public float? Longitude
	{
		get
		{
			if (string.IsNullOrEmpty(Lon))
				return null;
			if (float.TryParse(Lon, out var lon))
				return lon;
			return null;
		}
	}

	/// <summary>
	/// 深さ（数値、km）
	/// </summary>
	[JsonIgnore]
	public int? Depth
	{
		get
		{
			if (string.IsNullOrEmpty(Dep))
				return null;
			// "74 km" から数値を抽出
			var num = Dep.Replace("km", "").Trim();
			if (int.TryParse(num, out var depth))
				return depth;
			return null;
		}
	}

	/// <summary>
	/// マグニチュード（数値）
	/// </summary>
	[JsonIgnore]
	public float? Magnitude
	{
		get
		{
			if (string.IsNullOrEmpty(Mag))
				return null;
			if (float.TryParse(Mag, out var mag))
				return mag;
			return null;
		}
	}

	/// <summary>
	/// Location オブジェクト
	/// </summary>
	[JsonIgnore]
	public Location? Location
	{
		get
		{
			if (Latitude is not { } lat || Longitude is not { } lon)
				return null;
			return new Location(lat, lon);
		}
	}

	/// <summary>
	/// 最大震度（数値: 1=震度1, 2=震度2, 3=震度3, 4=震度4, 5=震度5弱, 6=震度5強, 7=震度6弱, 8=震度6強, 9=震度7）
	/// </summary>
	[JsonIgnore]
	public JmaIntensity? MaxIntensity
	{
		get
		{
			if (string.IsNullOrEmpty(MaxI))
				return null;

			return MaxI.ToJmaIntensity();
		}
	}
}
