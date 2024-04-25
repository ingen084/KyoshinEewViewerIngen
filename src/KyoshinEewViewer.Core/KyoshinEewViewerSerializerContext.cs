using KyoshinEewViewer.Core.Models;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core;

[JsonSourceGenerationOptions(WriteIndented = false, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(KyoshinEewViewerConfiguration))]
[JsonSerializable(typeof(GitHubRelease[]))]
[JsonSerializable(typeof(UpdaterStore))]
[JsonSerializable(typeof(WindowTheme))]
[JsonSerializable(typeof(IntensityTheme))]
public partial class KyoshinEewViewerSerializerContext : JsonSerializerContext
{
}
