using FluentAvalonia.UI.Controls;
using System;

namespace KyoshinEewViewer.Series;

public class SeriesMeta
{
	public SeriesMeta(Type type, string key, string name, IconSource icon, bool isDefaultEnabled, string detail = "")
	{
		Type = type;
		Key = key;
		Name = name;
		Icon = icon;
		IsDefaultEnabled = isDefaultEnabled;
		Detail = detail;
	}

	public Type Type { get; }

	/// <summary>
	/// 設定ファイルなどで使用するキー名
	/// </summary>
	public string Key { get; }

	/// <summary>
	/// 表示名
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// アイコン
	/// </summary>
	public IconSource Icon { get; }

	/// <summary>
	/// デフォルトで有効な状態にするか
	/// </summary>
	public bool IsDefaultEnabled { get; }

	/// <summary>
	/// 機能についての詳細
	/// </summary>
	public string Detail { get; }
}
