using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Voicevox.Model;

public class SpeakerStyle
{
	[JsonPropertyName("name")]
	public string? Name { get; set; }
	[JsonPropertyName("id")]
	public int Id { get; set; }
	[JsonPropertyName("type")]
	public string? Type { get; set; }
}
