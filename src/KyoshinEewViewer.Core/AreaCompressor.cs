using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Core;

/// <summary>
/// 地域をグループ化してまとめるためのデータ
/// </summary>
/// <param name="Name">グループ名</param>
/// <param name="Areas">グループに含まれる地域名の配列</param>
public record AreaGroup(string Name, string[] Areas);

/// <summary>
/// 地域をグループ定義に基づいてまとめるクラス
/// </summary>
/// <param name="groups">地域グループの定義</param>
public class AreaCompressor(IEnumerable<AreaGroup> groups)
{
	private readonly AreaGroup[] _groups = groups.ToArray();

	/// <summary>
	/// 地域の配列を圧縮する
	/// グループに含まれるすべての地域が揃っている場合、グループ名に置き換える
	/// </summary>
	/// <param name="areas">圧縮する地域名の配列</param>
	/// <returns>圧縮された地域名の配列</returns>
	public string[] Compress(string[] areas)
	{
		var result = areas.ToList();
		foreach (var group in _groups)
		{
			var groupAreas = group.Areas;
			if (groupAreas.All(result.Contains))
			{
				result.RemoveAll(a => groupAreas.Contains(a));
				result.Insert(0, group.Name);
			}
		}
		return result.ToArray();
	}
}
