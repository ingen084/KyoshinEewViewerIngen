using KyoshinEewViewer.Core.Models;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core;

[JsonSerializable(typeof(KyoshinEewViewerConfiguration))]
[JsonSerializable(typeof(GitHubRelease[]))]
[JsonSerializable(typeof(UpdaterStore))]
[JsonSerializable(typeof(WindowTheme))]
[JsonSerializable(typeof(IntensityTheme))]
public partial class KyoshinEewViewerSerializerContext : JsonSerializerContext
{
}
