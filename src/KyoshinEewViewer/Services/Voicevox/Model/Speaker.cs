using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Voicevox.Model;
public class Speaker
{
	[JsonPropertyName("name")]
	public string? Name { get; set; }
	[JsonPropertyName("speaker_uuid")]
	public Guid SpeakerUuid { get; set; }
	[JsonPropertyName("styles")]
	public SpeakerStyle[]? Styles { get; set; }
	[JsonPropertyName("version")]
	public string? Version { get; set; }
}
