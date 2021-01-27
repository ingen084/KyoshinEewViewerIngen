using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.ApiResponses.Parameters
{
	public abstract class DmdataParameterResponse : DmdataResponse
	{
		[JsonPropertyName("changeTime")]
		public DateTime ChangeTime { get; set; }
		[JsonPropertyName("version")]
		public string Version { get; set; }
	}
}
