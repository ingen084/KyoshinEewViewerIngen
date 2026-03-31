using System.Text.Json;

namespace ReplayGenerator.Services;

/// <summary>
/// 揺れ検知後の追加待機秒数（EEW スナップショットの深さ・M に応じて変える）
/// </summary>
public static class ShakeWaitPolicy
{
	public static int DetermineWaitSeconds(string? snapshotJson)
	{
		if (snapshotJson == null) return 30;

		try
		{
			using var doc = JsonDocument.Parse(snapshotJson);
			if (!doc.RootElement.TryGetProperty("eews", out var eews))
				return 30;

			foreach (var eew in eews.EnumerateArray())
			{
				if (eew.TryGetProperty("hypocenter", out var hypo) && hypo.TryGetProperty("depth", out var depthProp))
				{
					var depth = depthProp.GetInt32();
					var magnitude = eew.TryGetProperty("magnitude", out var magProp) ? magProp.GetDouble() : 0;

					if (depth <= 150 && magnitude <= 5)
						return 60;
					return 180;
				}
			}
		}
		catch
		{
			// 不正 JSON はデフォルト待機
		}

		return 30;
	}
}
