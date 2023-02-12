using CommandLine;

namespace KyoshinEewViewer;

public class StartupOptions
{
	public static StartupOptions? Current { get; set; }

	[Option('c', "CurrentDirectory", Required = false)]
	public string? CurrentDirectory { get; set; }
	[Option('s', "Standalone", Required = false)]
	public string? StandaloneSeriesName { get; set; }
}
