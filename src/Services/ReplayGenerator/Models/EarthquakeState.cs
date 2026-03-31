using System;

namespace ReplayGenerator.Models;

public class EarthquakeState
{
	public string EventId { get; set; } = "";
	public DateTime? OriginTime { get; set; }
	public DateTime ReportTime { get; set; }
	public string? HypocenterJson { get; set; }
	public SessionStatus Status { get; set; } = SessionStatus.Tracking;
}
