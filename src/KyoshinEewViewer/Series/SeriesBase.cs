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

	/// <summary>
	/// タブ内部に表示させるコントロール
	/// </summary>
	public abstract Control DisplayControl { get; }

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

	public abstract void Activating();
	public abstract void Deactivated();

	public virtual void Dispose()
		=> GC.SuppressFinalize(this);
}
