using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Series;

public abstract class SeriesBase(SeriesMeta meta) : ReactiveObject, IDisposable
{
	public SeriesMeta Meta { get; } = meta;

	private bool _isActivated;
	public bool IsActivated
	{
		get => _isActivated;
		internal set => this.RaiseAndSetIfChanged(ref _isActivated, value);
	}

	private bool _isSeparated;
	/// <summary>
	/// 別ウィンドウに分離されているかどうか
	/// </summary>
	public bool IsSeparated
	{
		get => _isSeparated;
		internal set => this.RaiseAndSetIfChanged(ref _isSeparated, value);
	}

	/// <summary>
	/// タブ内部に表示させるコントロール
	/// </summary>
	public abstract Control DisplayControl { get; }

	/// <summary>
	/// 設定画面のページ
	/// </summary>
	public abstract ISettingPage[] SettingPages { get; }

	private MapNavigationRequest? _mapNavigationRequest;
	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	public MapNavigationRequest? MapNavigationRequest
	{
		get => _mapNavigationRequest;
		protected set => this.RaiseAndSetIfChanged(ref _mapNavigationRequest, value);
	}

	private MapDisplayParameter _mapDisplayParameter;
	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	public MapDisplayParameter MapDisplayParameter
	{
		get => _mapDisplayParameter;
		protected set => this.RaiseAndSetIfChanged(ref _mapDisplayParameter, value);
	}

	public virtual void Initialize() { }

	/// <summary>
	/// DisplayControlを作成または再作成する
	/// 初期化時および分離ウィンドウへの移動時や復帰時に呼び出される
	/// </summary>
	public abstract void RecreateDisplayControl();

	public virtual void Dispose()
		=> GC.SuppressFinalize(this);
}
