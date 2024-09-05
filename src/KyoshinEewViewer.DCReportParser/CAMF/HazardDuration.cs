namespace KyoshinEewViewer.DCReportParser.CAMF;

public enum HazardDuration
{
	// 不明
	Unknown = 0b00,
	// 6時間未満
	Within6Hours = 0b01,
	// 6時間以上12時間未満
	Within12Hours = 0b10,
	// 12時間以上24時間未満
	Within24Hours = 0b11,
}
