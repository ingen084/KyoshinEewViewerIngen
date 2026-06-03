using System;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

#pragma warning disable CS8618 // JSON DTOs are populated by deserialization.

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;

public class AxisJwtPayload
{
	[JsonPropertyName("user")]
	public AxisUser User { get; set; }

	[JsonPropertyName("channels")]
	public string[] Channels { get; set; }

	[JsonPropertyName("exp")]
	public int Exp { get; set; }

	[JsonIgnore]
	public DateTimeOffset ExpDateTime => DateTimeOffset.FromUnixTimeSeconds(Exp);

	public static AxisJwtPayload Parse(string jwt)
	{
		var parts = jwt.Split('.');
		if (parts.Length != 3)
			throw new Exception("JWT ではありません");
		var payload = parts[1];
		var padding = payload.Length % 4;
		if (padding > 0)
			payload = payload.PadRight(payload.Length + (4 - padding), '=');
		var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

		return JsonSerializer.Deserialize<AxisJwtPayload>(payloadJson) ?? throw new Exception("payload のパースに失敗しました");
	}
}

public class AxisUser
{
	[JsonPropertyName("id")]
	public string Id { get; set; }

	[JsonPropertyName("type")]
	public int Type { get; set; }

	[JsonPropertyName("connection")]
	public int Connection { get; set; }
}
