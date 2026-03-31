using System;

namespace ReplayGenerator.Models;

public class ShakeState
{
	public string ShakeEventId { get; set; } = "";
	public DateTime StartTime { get; set; }
	public DateTime LastEventTime { get; set; }
	public string? EewJson { get; set; }
	public SessionStatus Status { get; set; } = SessionStatus.Tracking;
}
