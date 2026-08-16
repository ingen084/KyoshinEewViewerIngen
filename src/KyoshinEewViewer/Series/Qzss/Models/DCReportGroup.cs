using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public abstract class DCReportGroup : DisasterCrisisInformation
{
	/// <summary>
	/// タイムゾーンオフセット(時間)
	/// </summary>
	public static int TimezoneOffset { get; set; } = -9;

	/// <summary>
	/// DateTimeOffsetをタイムゾーンオフセットを適用したDateTimeに変換する
	/// </summary>
	protected static DateTime ApplyTimezoneOffset(DateTimeOffset dateTimeOffset)
		=> dateTimeOffset.UtcDateTime.AddHours(-TimezoneOffset);

	public bool IsTestOrDrill => Classification == ReportClassification.TrainingOrTest;

	private ReportClassification _classification;
	public ReportClassification Classification
	{
		get => _classification;
		set => SetProperty(ref _classification, value);
	}

	private InformationType? _informationType;
	public InformationType? InformationType
	{
		get => _informationType;
		set => SetProperty(ref _informationType, value);
	}

	private int _reportCount = 1;
	public int ReportCount
	{
		get => _reportCount;
		set => SetProperty(ref _reportCount, value);
	}

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => SetProperty(ref _reportTime, value);
	}

	public abstract bool CheckDuplicate(DCReport report);
	public abstract bool TryProcess(DCReport report);

	[JsonIgnore]
	public abstract Control? DetailDisplayControl { get; }

	private MapNavigationRequest? _mapNavigationRequest;
	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	[JsonIgnore]
	public MapNavigationRequest? MapNavigationRequest
	{
		get => _mapNavigationRequest;
		protected set => SetProperty(ref _mapNavigationRequest, value);
	}

	private MapDisplayParameter _mapDisplayParameter;
	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	[JsonIgnore]
	public MapDisplayParameter MapDisplayParameter
	{
		get => _mapDisplayParameter;
		protected set => SetProperty(ref _mapDisplayParameter, value);
	}
}
