using System.Reflection;

namespace KyoshinEewViewer.Core;

public static class Utils
{
	public static string? OverrideVersion { get; set; }
	public static string Version
	{
		get {
			if (OverrideVersion != null)
				return OverrideVersion;
			var ver = Assembly.GetExecutingAssembly().GetName().Version;
			if (ver == null)
				return "不明";
			// 0.1.1.X は手元ビルドかリリース外のビルド
			if (ver.Major == 0 && ver.Minor == 1 && ver.Build == 1)
			{
				if (ver.Revision != 0)
					return "実験" + ver.Revision;
				return "DEBUG";
			}
			return ver.ToString();
		}
	}
}
