using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;

namespace KyoshinEewViewer.Series.Qzss.Models;

public abstract class DCReportGroup : ReactiveObject
{
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
}
