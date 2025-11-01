using KyoshinMonitorLib;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Models;

public class ObservationPointEditorModel : ReactiveObject
{
	public ObservationPointEditorModel()
	{
		ObservationPoints = [];
		FilteredObservationPoints = [];
		
		// デフォルトフィルタ設定
		SearchText = "";
		ShowKiKNet = true;
		ShowKNet = true;
		ShowSuspended = true;
	}

	#region データプロパティ

	private ObservableCollection<CommonObservationPoint> _observationPoints = [];
	public ObservableCollection<CommonObservationPoint> ObservationPoints
	{
		get => _observationPoints;
		set => this.RaiseAndSetIfChanged(ref _observationPoints, value);
	}

	private ObservableCollection<CommonObservationPoint> _filteredObservationPoints = [];
	public ObservableCollection<CommonObservationPoint> FilteredObservationPoints
	{
		get => _filteredObservationPoints;
		set => this.RaiseAndSetIfChanged(ref _filteredObservationPoints, value);
	}

	private CommonObservationPoint? _selectedObservationPoint;
	public CommonObservationPoint? SelectedObservationPoint
	{
		get => _selectedObservationPoint;
		set => this.RaiseAndSetIfChanged(ref _selectedObservationPoint, value);
	}

	#endregion

	#region フィルタプロパティ

	private string _searchText = "";
	public string SearchText
	{
		get => _searchText;
		set => this.RaiseAndSetIfChanged(ref _searchText, value);
	}

	private bool _showKiKNet = true;
	public bool ShowKiKNet
	{
		get => _showKiKNet;
		set => this.RaiseAndSetIfChanged(ref _showKiKNet, value);
	}

	private bool _showKNet = true;
	public bool ShowKNet
	{
		get => _showKNet;
		set => this.RaiseAndSetIfChanged(ref _showKNet, value);
	}

	private bool _showSuspended = true;
	public bool ShowSuspended
	{
		get => _showSuspended;
		set => this.RaiseAndSetIfChanged(ref _showSuspended, value);
	}

	#endregion

	#region Undo/Redo機能

	private readonly Stack<ObservationPointChange> _undoStack = new();
	private readonly Stack<ObservationPointChange> _redoStack = new();

	/// <summary>
	/// Undoが可能かどうか
	/// </summary>
	public bool CanUndo => _undoStack.Count > 0;

	/// <summary>
	/// Redoが可能かどうか
	/// </summary>
	public bool CanRedo => _redoStack.Count > 0;

	/// <summary>
	/// 観測点の変更を記録してUndoスタックに追加
	/// </summary>
	/// <param name="point">変更された観測点</param>
	/// <param name="oldPoint">変更前の座標</param>
	/// <param name="newPoint">変更後の座標</param>
	public void RecordChange(CommonObservationPoint point, KyoshinImagePoint? oldPoint, KyoshinImagePoint? newPoint)
	{
		var change = new ObservationPointChange(point, oldPoint, newPoint);
		_undoStack.Push(change);
		
		// 新しい変更が記録されたらRedoスタックをクリア
		_redoStack.Clear();
		
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
		
		// Undoスタックのサイズ制限（メモリ対策）
		while (_undoStack.Count > 50)
		{
			var temp = new Stack<ObservationPointChange>();
			for (int i = 0; i < 49; i++)
			{
				temp.Push(_undoStack.Pop());
			}
			_undoStack.Clear();
			while (temp.Count > 0)
			{
				_undoStack.Push(temp.Pop());
			}
		}
	}

	/// <summary>
	/// 最後の変更をUndoする
	/// </summary>
	/// <returns>Undoが実行されたかどうか</returns>
	public bool Undo()
	{
		if (_undoStack.Count == 0)
			return false;

		var change = _undoStack.Pop();
		
		// Redoのために現在の状態を保存
		var redoChange = new ObservationPointChange(change.Point, change.NewPoint, change.OldPoint);
		_redoStack.Push(redoChange);
		
		// 変更を実行
		change.Point.Point = change.OldPoint;
		UpdateObservationPoint();
		
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
		return true;
	}

	/// <summary>
	/// 最後にUndoした変更をRedoする
	/// </summary>
	/// <returns>Redoが実行されたかどうか</returns>
	public bool Redo()
	{
		if (_redoStack.Count == 0)
			return false;

		var change = _redoStack.Pop();
		
		// Undoのために現在の状態を保存
		var undoChange = new ObservationPointChange(change.Point, change.NewPoint, change.OldPoint);
		_undoStack.Push(undoChange);
		
		// 変更を実行
		change.Point.Point = change.NewPoint;
		UpdateObservationPoint();
		
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
		return true;
	}

	/// <summary>
	/// Undo/Redoスタックをクリアする
	/// </summary>
	public void ClearUndoRedoStack()
	{
		_undoStack.Clear();
		_redoStack.Clear();
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
	}

	#endregion

	#region 編集状態プロパティ

	private bool _isModified;
	public bool IsModified
	{
		get => _isModified;
		set => this.RaiseAndSetIfChanged(ref _isModified, value);
	}

	private string? _currentFilePath;
	public string? CurrentFilePath
	{
		get => _currentFilePath;
		set => this.RaiseAndSetIfChanged(ref _currentFilePath, value);
	}

	#endregion

	#region データ管理メソッド

	/// <summary>
	/// 観測点データを設定する
	/// </summary>
	/// <param name="points">観測点データ</param>
	public void SetObservationPoints(CommonObservationPoint[] points)
	{
		ObservationPoints.Clear();
		foreach (var point in points)
			ObservationPoints.Add(point);
		
		ApplyFilter();
		IsModified = false;
		
		// TotalCount の変更通知
		this.RaisePropertyChanged(nameof(TotalCount));
	}

	/// <summary>
	/// 観測点を追加する
	/// </summary>
	/// <param name="point">追加する観測点</param>
	public void AddObservationPoint(CommonObservationPoint point)
	{
		ObservationPoints.Add(point);
		ApplyFilter();
		IsModified = true;
		
		// TotalCount の変更通知
		this.RaisePropertyChanged(nameof(TotalCount));
	}

	/// <summary>
	/// 観測点を削除する
	/// </summary>
	/// <param name="point">削除する観測点</param>
	/// <returns>削除に成功した場合true</returns>
	public bool RemoveObservationPoint(CommonObservationPoint point)
	{
		var removed = ObservationPoints.Remove(point);
		if (removed)
		{
			ApplyFilter();
			IsModified = true;
			
			// 選択中の観測点が削除された場合はクリア
			if (SelectedObservationPoint == point)
				SelectedObservationPoint = null;
			
			// TotalCount の変更通知
			this.RaisePropertyChanged(nameof(TotalCount));
		}
		return removed;
	}

	/// <summary>
	/// 観測点データを更新する
	/// </summary>
	public void UpdateObservationPoint()
	{
		IsModified = true;
		// 座標変更の場合はフィルタを再適用しない
		// ApplyFilter()を呼ぶとFilteredObservationPointsが再構築され、DataGridの参照が変わってしまう
		// この場合は単純にコレクションの更新通知を送信
		this.RaisePropertyChanged(nameof(FilteredObservationPoints));
	}

	/// <summary>
	/// 新規観測点を作成する
	/// </summary>
	/// <returns>新規観測点</returns>
	public CommonObservationPoint CreateNewObservationPoint()
	{
		// 新しいコードを生成（既存のコードと重複しないように）
		var existingCodes = ObservationPoints.Select(p => p.Code).ToHashSet();
		var newCode = "NEW001";
		var counter = 1;

		while (existingCodes.Contains(newCode))
		{
			counter++;
			newCode = $"NEW{counter:D3}";
		}

		return new CommonObservationPoint
		{
			Type = ObservationPointType.K_NET,
			Code = newCode,
			Name = "新規観測点",
			Region = "未設定",
			Location = new Location(35.0f, 139.0f), // 東京駅付近
			Point = null,
			IsSuspended = false
		};
	}

	#endregion

	#region フィルタリングメソッド

	/// <summary>
	/// フィルタを適用する
	/// </summary>
	public void ApplyFilter()
	{
		var filtered = ObservationPoints.AsEnumerable();

		// 検索テキストでフィルタ
		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			var search = SearchText.Trim().ToLowerInvariant();
			filtered = filtered.Where(p => 
				p.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				p.Region.Contains(search, StringComparison.OrdinalIgnoreCase));
		}

		// 観測点種類でフィルタ
		filtered = filtered.Where(p => p.Type switch
		{
			ObservationPointType.KiK_net => ShowKiKNet,
			ObservationPointType.K_NET => ShowKNet,
			_ => true
		});

		// 運用状態でフィルタ
		if (!ShowSuspended)
			filtered = filtered.Where(p => !p.IsSuspended);

		// フィルタ結果を更新
		FilteredObservationPoints.Clear();
		foreach (var point in filtered.OrderBy(p => p.Code))
			FilteredObservationPoints.Add(point);
		
		// FilteredCount の変更通知
		this.RaisePropertyChanged(nameof(FilteredCount));
	}

	#endregion

	#region 統計情報プロパティ

	/// <summary>
	/// 総観測点数
	/// </summary>
	public int TotalCount => ObservationPoints.Count;

	/// <summary>
	/// 表示中の観測点数
	/// </summary>
	public int FilteredCount => FilteredObservationPoints.Count;
	#endregion

	#region 重複統合メソッド

	/// <summary>
	/// 重複している観測点を統合する
	/// </summary>
	/// <returns>統合結果の情報</returns>
	public DuplicateConsolidationResult ConsolidateDuplicateObservationPoints()
	{
		var result = new DuplicateConsolidationResult();
		
		// 観測点コードでグループ化
		var duplicateGroups = ObservationPoints
			.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
			.Where(g => g.Count() > 1)
			.ToList();

		if (duplicateGroups.Count == 0)
		{
			return result; // 重複なし
		}

		var pointsToRemove = new List<CommonObservationPoint>();
		
		foreach (var group in duplicateGroups)
		{
			var duplicates = group.ToList();
			result.ProcessedGroups.Add(new DuplicateGroup(group.Key, duplicates.Count));
			
			// 優先順位に基づいて統合対象を決定
			var prioritized = duplicates.OrderByDescending(GetPriorityScore).ToList();
			var keepPoint = prioritized.First(); // 最も優先度の高い観測点を保持
			var removePoints = prioritized.Skip(1).ToList(); // 残りを削除対象に
			
			// 統合詳細を記録
			foreach (var removePoint in removePoints)
			{
				result.ConsolidatedPoints.Add(new ConsolidatedPointInfo(
					removePoint.Code,
					removePoint.Name,
					removePoint.Location?.ToString() ?? "座標なし",
					removePoint.Point?.ToString() ?? "Point座標なし",
					keepPoint.Point?.ToString() ?? "Point座標なし",
					GetPriorityReason(removePoint, keepPoint)
				));
				
				pointsToRemove.Add(removePoint);
			}
		}
		
		// 重複観測点を削除
		foreach (var point in pointsToRemove)
		{
			ObservationPoints.Remove(point);
		}
		
		// 統計情報を更新
		result.RemovedCount = pointsToRemove.Count;
		result.GroupCount = duplicateGroups.Count;
		
		// フィルタを再適用
		ApplyFilter();
		IsModified = true;
		
		// TotalCount の変更通知
		this.RaisePropertyChanged(nameof(TotalCount));
		
		return result;
	}

	/// <summary>
	/// 観測点の優先度スコアを計算する
	/// </summary>
	private static int GetPriorityScore(CommonObservationPoint point)
	{
		var score = 0;
		
		// Point座標があるかどうか（最重要）
		if (point.Point != null)
			score += 1000;
		
		// 運用中かどうか
		if (!point.IsSuspended)
			score += 100;
		
		// Location座標があるかどうか
		if (point.Location != null)
			score += 10;
		
		// 名前が設定されているかどうか
		if (!string.IsNullOrWhiteSpace(point.Name))
			score += 1;
		
		return score;
	}

	/// <summary>
	/// 優先理由を取得する
	/// </summary>
	private static string GetPriorityReason(CommonObservationPoint removed, CommonObservationPoint kept)
	{
		var reasons = new List<string>();

		if (kept.Point != null && removed.Point == null)
			reasons.Add("強震モニタ座標あり");
		
		if (!kept.IsSuspended && removed.IsSuspended)
			reasons.Add("運用中");
		
		if (kept.Location != null && removed.Location == null)
			reasons.Add("地理座標あり");
		
		if (!string.IsNullOrWhiteSpace(kept.Name) && string.IsNullOrWhiteSpace(removed.Name))
			reasons.Add("名前設定済み");
		
		return reasons.Count > 0 ? string.Join(", ", reasons) : "より詳細なデータ";
	}

	#endregion

	#region 検索・選択メソッド

	/// <summary>
	/// 観測点コードで検索する
	/// </summary>
	/// <param name="code">観測点コード</param>
	/// <returns>見つかった観測点（存在しない場合はnull）</returns>
	public CommonObservationPoint? FindByCode(string code)
	{
		return ObservationPoints.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// 座標で最も近い観測点を検索する
	/// </summary>
	/// <param name="location">検索する座標</param>
	/// <param name="maxDistance">最大距離（度）</param>
	/// <returns>見つかった観測点（存在しない場合はnull）</returns>
	public CommonObservationPoint? FindNearestByLocation(Location location, float maxDistance = 0.01f)
	{
		return ObservationPoints
			.Where(p => p.Location != null)
			.Select(p => new { Point = p, Distance = CalculateDistance(location, p.Location) })
			.Where(x => x.Distance <= maxDistance)
			.OrderBy(x => x.Distance)
			.FirstOrDefault()?.Point;
	}

	/// <summary>
	/// ピクセル座標で最も近い観測点を検索する
	/// </summary>
	/// <param name="pixelPoint">検索するピクセル座標</param>
	/// <param name="maxDistance">最大距離（ピクセル）</param>
	/// <returns>見つかった観測点（存在しない場合はnull）</returns>
	public CommonObservationPoint? FindNearestByPixel(Point2 pixelPoint, int maxDistance = 10)
	{
		return ObservationPoints
			.Where(p => p.Point != null)
			.Select(p => new {
				Point = p,
				Distance = CalculatePixelDistance(pixelPoint,
					new Point2(p.Point!.Center.X + p.Point.Offset.X, p.Point.Center.Y + p.Point.Offset.Y))
			})
			.Where(x => x.Distance <= maxDistance)
			.OrderBy(x => x.Distance)
			.FirstOrDefault()?.Point;
	}

	/// <summary>
	/// 地理座標間の距離を計算する（簡易版）
	/// </summary>
	private static float CalculateDistance(Location loc1, Location loc2)
	{
		var deltaLat = loc1.Latitude - loc2.Latitude;
		var deltaLng = loc1.Longitude - loc2.Longitude;
		return (float)Math.Sqrt(deltaLat * deltaLat + deltaLng * deltaLng);
	}

	/// <summary>
	/// ピクセル座標間の距離を計算する
	/// </summary>
	private static double CalculatePixelDistance(Point2 point1, Point2 point2)
	{
		var deltaX = point1.X - point2.X;
		var deltaY = point1.Y - point2.Y;
		return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
	}

	#endregion
}

/// <summary>
/// 観測点の変更履歴を記録するレコード
/// </summary>
/// <param name="Point">変更された観測点</param>
/// <param name="OldPoint">変更前の座標</param>
/// <param name="NewPoint">変更後の座標</param>
public record ObservationPointChange(CommonObservationPoint Point, KyoshinImagePoint? OldPoint, KyoshinImagePoint? NewPoint);

/// <summary>
/// 重複統合処理の結果
/// </summary>
public class DuplicateConsolidationResult
{
	/// <summary>
	/// 処理された重複グループ
	/// </summary>
	public List<DuplicateGroup> ProcessedGroups { get; } = [];

	/// <summary>
	/// 統合された観測点の詳細情報
	/// </summary>
	public List<ConsolidatedPointInfo> ConsolidatedPoints { get; } = [];

	/// <summary>
	/// 削除された観測点数
	/// </summary>
	public int RemovedCount { get; set; }

	/// <summary>
	/// 重複グループ数
	/// </summary>
	public int GroupCount { get; set; }

	/// <summary>
	/// 重複が存在したかどうか
	/// </summary>
	public bool HasDuplicates => GroupCount > 0;
}

/// <summary>
/// 重複グループ情報
/// </summary>
/// <param name="Code">観測点コード</param>
/// <param name="Count">重複数</param>
public record DuplicateGroup(string Code, int Count);

/// <summary>
/// 統合された観測点の情報
/// </summary>
/// <param name="Code">観測点コード</param>
/// <param name="Name">観測点名</param>
/// <param name="Location">地理座標</param>
/// <param name="RemovedPoint">削除された強震モニタ座標</param>
/// <param name="KeptPoint">保持された強震モニタ座標</param>
/// <param name="Reason">優先理由</param>
public record ConsolidatedPointInfo(
	string Code,
	string Name,
	string Location,
	string RemovedPoint,
	string KeptPoint,
	string Reason
);
