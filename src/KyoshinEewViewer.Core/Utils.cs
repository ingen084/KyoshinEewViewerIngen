using System.Reflection;

namespace KyoshinEewViewer.Core;

public static class Utils
{
	public static string Version
	{
		get {
			var ver = Assembly.GetExecutingAssembly().GetName().Version;
			if (ver == null)
				return "Unknown";
			// 0.1.1.X は手元ビルドかリリース外のビルド
			if (ver.Major == 0 && ver.Minor == 1 && ver.Build == 1)
			{
				if (ver.Revision != 0)
					return "EXPERIMENTAL-" + ver.Revision;
				return "DEBUG";
			}
			return ver.ToString();
		}
	}
}
