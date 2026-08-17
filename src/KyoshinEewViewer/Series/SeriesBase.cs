using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models.Events;
using System;

namespace KyoshinEewViewer.Series;

public abstract partial class SeriesBase(SeriesMeta meta) : ObservableObject, IDisposable
{
	public SeriesMeta Meta { get; } = meta;

	[ObservableProperty]
	public partial bool IsActivated { get; internal set; }

	/// <summary>
	/// 別ウィンドウに分離されているかどうか
	/// </summary>
	[ObservableProperty]
	public partial bool IsSeparated { get; internal set; }

	/// <summary>
	/// DisplayControl の最小表示サイズ。
	/// 表示領域がこのサイズを下回るとスケーリングが開始される。
	/// </summary>
	public virtual Size MinViewSize { get; } = default;

	/// <summary>
	/// タブ内部に表示させるコントロール
	/// </summary>
	public abstract Control DisplayControl { get; }

	/// <summary>
	/// 設定画面のページ
	/// </summary>
	public abstract ISettingPage[] SettingPages { get; }

	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	[ObservableProperty]
	public partial MapNavigationRequest? MapNavigationRequest { get; protected set; }

	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	[ObservableProperty]
	public partial MapDisplayParameter MapDisplayParameter { get; protected set; }

	public virtual void Initialize() { }

	/// <summary>
	/// DisplayControlを作成または再作成する
	/// 初期化時および分離ウィンドウへの移動時や復帰時に呼び出される
	/// </summary>
	public abstract void RecreateDisplayControl();

	public virtual void Dispose()
		=> GC.SuppressFinalize(this);
}
