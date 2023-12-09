using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core;
using Splat;

namespace KyoshinEewViewer;

public static class KyoshinEewViewerApp
{
	public static Application? Application { get; set; }
	public static TopLevel? TopLevelControl { get; set; }
	public static ThemeSelector? Selector { get; set; }

	public static void SetupIOC(IDependencyResolver resolver)
		=> SplatRegistrations.SetupIOC(resolver);
}
