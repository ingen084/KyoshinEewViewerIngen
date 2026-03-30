using CommandLine;

namespace KyoshinEewViewer.Core;

public class StartupOptions
{
	public static StartupOptions? Current { get; set; }

	[Option('c', "CurrentDirectory", Required = false)]
	public string? CurrentDirectory { get; set; }

	[Option('s', "Standalone", Required = false)]
	public string? StandaloneSeriesName { get; set; }

	[Option('l', "console-log", Required = false)]
	public bool ConsoleLog { get; set; }

	[Option('d', "debug", Required = false)]
	public bool DebugLog { get; set; }

	[Option('n', "no-logo", Required = false)]
	public bool NoSplash { get; set; }
}
