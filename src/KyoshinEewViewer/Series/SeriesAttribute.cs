using FluentAvalonia.UI.Controls;
using System;

namespace KyoshinEewViewer.Series;

public class SeriesMeta(Type type, string key, string name, IconSource icon, bool isDefaultEnabled, string detail = "")
{
	public Type Type { get; } = type;

	/// <summary>
	/// 設定ファイルなどで使用するキー名
	/// </summary>
	public string Key { get; } = key;

	/// <summary>
	/// 表示名
	/// </summary>
	public string Name { get; } = name;

	/// <summary>
	/// アイコン
	/// </summary>
	public IconSource Icon { get; } = icon;

	/// <summary>
	/// デフォルトで有効な状態にするか
	/// </summary>
	public bool IsDefaultEnabled { get; } = isDefaultEnabled;

	/// <summary>
	/// 機能についての詳細
	/// </summary>
	public string Detail { get; } = detail;
}
