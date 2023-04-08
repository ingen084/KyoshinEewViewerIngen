using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

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
				return "?";
			// 0.1.1.X は手元ビルドかリリース外のビルド
			if (ver.Major == 0 && ver.Minor == 1 && ver.Build == 1)
			{
				if (ver.Revision != 0)
					return "EXP-" + ver.Revision;
				return "DEBUG";
			}
			return ver.ToString();
		}
	}

	/// <summary>
	/// 全角文字を半角に変換する
	/// </summary>
	/// <param name="str"></param>
	/// <returns></returns>
	public static string ConvertToShortWidthString(string str)
		=> new StringBuilder(str)
		.Replace('０', '0')
		.Replace('１', '1')
		.Replace('２', '2')
		.Replace('３', '3')
		.Replace('４', '4')
		.Replace('５', '5')
		.Replace('６', '6')
		.Replace('７', '7')
		.Replace('８', '8')
		.Replace('９', '9')
		.Replace('ｍ', 'm')
		.Replace('．', '.')
		.ToString();

	public static bool IsAppRunning => Process.GetProcessesByName("KyoshinEewViewer").Count(p => p.Responding) > 1;
}
