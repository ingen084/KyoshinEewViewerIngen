using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using ReactiveUI;

namespace KyoshinEewViewer.Series.Qzss.Models;

public abstract class DCReportGroup : ReactiveObject
{
	public bool IsTestOrDrill => Classification == ReportClassification.TrainingOrTest;

	private ReportClassification _classification;
	public ReportClassification Classification
	{
		get => _classification;
		set => this.RaiseAndSetIfChanged(ref _classification, value);
	}

	private InformationType? _informationType;
	public InformationType? InformationType
	{
		get => _informationType;
		set => this.RaiseAndSetIfChanged(ref _informationType, value);
	}

	private int _reportCount = 1;
	public int ReportCount
	{
		get => _reportCount;
		set => this.RaiseAndSetIfChanged(ref _reportCount, value);
	}

	public abstract bool CheckDuplicate(DCReport report);
	public abstract bool TryProcess(DCReport report);

	public abstract Control? DetailDisplayControl { get; }

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
}
