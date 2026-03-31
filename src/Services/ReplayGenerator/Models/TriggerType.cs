namespace ReplayGenerator.Models;

public enum TriggerType
{
	ShakeDetection,
	Earthquake,
}

public enum SessionStatus
{
	Tracking,
	Waiting,
	Generating,
	Done,
}
