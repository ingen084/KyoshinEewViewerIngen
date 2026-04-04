using KyoshinEewViewer.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiDASPlusGraph;

public class PpgConfig
{
	public ThemeMeta WindowTheme { get; set; } = new(ThemeType.BuiltIn, "Dark");
	public ThemeMeta IntensityTheme { get; set; } = new(ThemeType.BuiltIn, "Standard");
	public int WaveformSeconds { get; set; } = 10;
	public bool SplitAxes { get; set; }
	public string? SelectedPort { get; set; }
	public int WindowState { get; set; }
	public double WindowX { get; set; } = double.NaN;
	public double WindowY { get; set; } = double.NaN;
	public double WindowWidth { get; set; } = double.NaN;
	public double WindowHeight { get; set; } = double.NaN;
}

[JsonSerializable(typeof(PpgConfig))]
[JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
internal partial class PpgConfigSerializerContext : JsonSerializerContext;
