using DmdataSharp.WebSocketMessages.V2;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

/// <summary>
/// dmdata WebSocket統計情報
/// </summary>
public class DmdataStatistics : ReactiveObject
{
	private readonly ConcurrentQueue<MessageFlowInfo> _recentMessages = new();
	private readonly object _statsLock = new();

	// 基本統計
	private long _totalMessages = 0;
	public long TotalMessages
	{
		get => _totalMessages;
		private set => this.RaiseAndSetIfChanged(ref _totalMessages, value);
	}

	private long _duplicateMessages = 0;
	public long DuplicateMessages
	{
		get => _duplicateMessages;
		private set => this.RaiseAndSetIfChanged(ref _duplicateMessages, value);
	}

	private DateTime? _lastMessageTime;
	public DateTime? LastMessageTime
	{
		get => _lastMessageTime;
		private set => this.RaiseAndSetIfChanged(ref _lastMessageTime, value);
	}

	// 処理時間統計
	private double _averageProcessingTime = 0;
	public double AverageProcessingTime
	{
		get => _averageProcessingTime;
		private set => this.RaiseAndSetIfChanged(ref _averageProcessingTime, value);
	}

	private double _maxProcessingTime = 0;
	public double MaxProcessingTime
	{
		get => _maxProcessingTime;
		private set => this.RaiseAndSetIfChanged(ref _maxProcessingTime, value);
	}

	// フロー統計
	private Dictionary<string, long> _flowStepCounts = new();
	public Dictionary<string, long> FlowStepCounts
	{
		get => _flowStepCounts;
		private set => this.RaiseAndSetIfChanged(ref _flowStepCounts, value);
	}

	private double _averageFlowTime = 0;
	public double AverageFlowTime
	{
		get => _averageFlowTime;
		private set => this.RaiseAndSetIfChanged(ref _averageFlowTime, value);
	}

	// エンドポイント統計
	private Dictionary<string, EndpointStats> _endpointStats = new();
	public Dictionary<string, EndpointStats> EndpointStats
	{
		get => _endpointStats;
		private set => this.RaiseAndSetIfChanged(ref _endpointStats, value);
	}

	// 重複率（計算プロパティ）
	public double DuplicationRate => TotalMessages > 0 ? (double)DuplicateMessages / TotalMessages * 100 : 0;

	/// <summary>
	/// メッセージ受信時の統計更新
	/// </summary>
	public void OnMessageReceived(DataWebSocketMessage? message, string endpointName, bool isDuplicate)
	{
		if (message == null || string.IsNullOrEmpty(endpointName))
			return;
		lock (_statsLock)
		{
			// 基本統計更新
			TotalMessages++;
			if (isDuplicate)
				DuplicateMessages++;

			LastMessageTime = DateTime.Now;

			// フロー情報を解析
			var flowInfo = AnalyzeMessageFlow(message, endpointName, isDuplicate);

			// 最新メッセージ情報を保存（最大1000件）
			_recentMessages.Enqueue(flowInfo);
			while (_recentMessages.Count > 1000)
				_recentMessages.TryDequeue(out _);

			// エンドポイント統計更新
			UpdateEndpointStats(endpointName, flowInfo);

			// 処理時間統計更新
			UpdateProcessingTimeStats();

			// フロー統計更新
			UpdateFlowStats();

			// ReactiveUIプロパティ変更通知
			this.RaisePropertyChanged(nameof(DuplicationRate));
		}
	}

	/// <summary>
	/// WebSocketメッセージからフロー情報を解析
	/// </summary>
	private MessageFlowInfo AnalyzeMessageFlow(DataWebSocketMessage message, string endpointName, bool isDuplicate)
	{
		var flowInfo = new MessageFlowInfo
		{
			MessageId = message.Id,
			EndpointName = endpointName,
			MessageType = message.Head.Type,
			ReceivedTime = DateTime.Now,
			IsDuplicate = isDuplicate,
			FlowSteps = new List<FlowStepInfo>()
		};

		// Passing情報からフロー解析
		DateTime? firstTime = null;
		DateTime? lastTime = null;

		foreach (var passing in message.Passing)
		{
			var stepInfo = new FlowStepInfo
			{
				StepName = passing.Name,
				Timestamp = passing.Time,
				ProcessingTime = 0 // 次のステップとの差分で計算
			};

			if (firstTime == null)
				firstTime = passing.Time;
			lastTime = passing.Time;

			flowInfo.FlowSteps.Add(stepInfo);
		}

		// 各ステップの処理時間を計算
		for (int i = 0; i < flowInfo.FlowSteps.Count - 1; i++)
		{
			var current = flowInfo.FlowSteps[i];
			var next = flowInfo.FlowSteps[i + 1];
			current.ProcessingTime = (next.Timestamp - current.Timestamp).TotalMilliseconds;
		}

		// 全体の処理時間
		if (firstTime.HasValue && lastTime.HasValue)
		{
			flowInfo.TotalProcessingTime = (lastTime.Value - firstTime.Value).TotalMilliseconds;
		}

		return flowInfo;
	}

	/// <summary>
	/// エンドポイント統計更新
	/// </summary>
	private void UpdateEndpointStats(string endpointName, MessageFlowInfo flowInfo)
	{
		if (!_endpointStats.ContainsKey(endpointName))
		{
			_endpointStats[endpointName] = new EndpointStats { EndpointName = endpointName };
		}

		var stats = _endpointStats[endpointName];
		stats.MessageCount++;
		if (flowInfo.IsDuplicate)
			stats.DuplicateCount++;

		if (flowInfo.TotalProcessingTime > 0)
		{
			stats.TotalProcessingTime += flowInfo.TotalProcessingTime;
			stats.AverageProcessingTime = stats.TotalProcessingTime / stats.MessageCount;

			if (flowInfo.TotalProcessingTime > stats.MaxProcessingTime)
				stats.MaxProcessingTime = flowInfo.TotalProcessingTime;
		}

		stats.LastMessageTime = flowInfo.ReceivedTime;

		// 辞書全体を新しいインスタンスとして設定（ReactiveUI通知のため）
		EndpointStats = new Dictionary<string, EndpointStats>(_endpointStats);
	}

	/// <summary>
	/// 処理時間統計更新
	/// </summary>
	private void UpdateProcessingTimeStats()
	{
		var recentMessages = _recentMessages.Where(m => m.TotalProcessingTime > 0).ToArray();
		if (recentMessages.Length > 0)
		{
			AverageProcessingTime = recentMessages.Average(m => m.TotalProcessingTime);
			MaxProcessingTime = recentMessages.Max(m => m.TotalProcessingTime);
		}
	}

	/// <summary>
	/// フロー統計更新
	/// </summary>
	private void UpdateFlowStats()
	{
		var stepCounts = new Dictionary<string, long>();
		var flowTimes = new List<double>();

		foreach (var message in _recentMessages)
		{
			if (message.TotalProcessingTime > 0)
				flowTimes.Add(message.TotalProcessingTime);

			foreach (var step in message.FlowSteps)
			{
				stepCounts[step.StepName] = stepCounts.GetValueOrDefault(step.StepName, 0) + 1;
			}
		}

		FlowStepCounts = stepCounts;

		if (flowTimes.Count > 0)
			AverageFlowTime = flowTimes.Average();
	}

	/// <summary>
	/// 統計リセット
	/// </summary>
	public void Reset()
	{
		lock (_statsLock)
		{
			TotalMessages = 0;
			DuplicateMessages = 0;
			LastMessageTime = null;
			AverageProcessingTime = 0;
			MaxProcessingTime = 0;
			AverageFlowTime = 0;
			FlowStepCounts = new Dictionary<string, long>();
			EndpointStats = new Dictionary<string, EndpointStats>();

			while (_recentMessages.TryDequeue(out _)) { }

			this.RaisePropertyChanged(nameof(DuplicationRate));
		}
	}
}

/// <summary>
/// メッセージフロー情報
/// </summary>
public class MessageFlowInfo
{
	public string MessageId { get; set; } = "";
	public string EndpointName { get; set; } = "";
	public string MessageType { get; set; } = "";
	public DateTime ReceivedTime { get; set; }
	public bool IsDuplicate { get; set; }
	public double TotalProcessingTime { get; set; }
	public List<FlowStepInfo> FlowSteps { get; set; } = [];
}

/// <summary>
/// フローステップ情報
/// </summary>
public class FlowStepInfo
{
	public string StepName { get; set; } = "";
	public DateTime Timestamp { get; set; }
	public double ProcessingTime { get; set; }
}

/// <summary>
/// エンドポイント統計情報
/// </summary>
public class EndpointStats : ReactiveObject
{
	public string EndpointName { get; set; } = "";
	public long MessageCount { get; set; }
	public long DuplicateCount { get; set; }
	public double TotalProcessingTime { get; set; }
	public double AverageProcessingTime { get; set; }
	public double MaxProcessingTime { get; set; }
	public DateTime? LastMessageTime { get; set; }

	public double DuplicationRate => MessageCount > 0 ? (double)DuplicateCount / MessageCount * 100 : 0;
}