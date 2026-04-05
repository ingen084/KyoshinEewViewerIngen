using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Earthquake;

public class ObservationDiffCalculatorTests
{
	private readonly IObservationDiffCalculator _calculator = new ObservationDiffCalculator();

	private static EarthquakeObservationPref[] CreatePrefs(params (int Code, string Name, JmaIntensity Intensity)[] areas)
	{
		return [new EarthquakeObservationPref
		{
			Name = "テスト県",
			Code = 1,
			Areas = areas.Select(a => new EarthquakeObservationArea
			{
				Code = a.Code,
				Name = a.Name,
				MaxIntensity = a.Intensity,
			}).ToArray(),
		}];
	}

	[Fact(DisplayName = "両方nullの場合は変更なし")]
	public void ComputeFromPrefs_BothNull_NoChanges()
	{
		var result = _calculator.ComputeFromPrefs(null, null);

		Assert.False(result.HasChanges);
		Assert.Empty(result.AddedAreas);
		Assert.Empty(result.RemovedAreas);
		Assert.Empty(result.IntensityChangedAreas);
	}

	[Fact(DisplayName = "前回nullで今回ありの場合は全地域が追加")]
	public void ComputeFromPrefs_PreviousNull_AllAdded()
	{
		var current = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3),
			(351, "東京都多摩東部", JmaIntensity.Int2)
		);

		var result = _calculator.ComputeFromPrefs(null, current);

		Assert.True(result.HasChanges);
		Assert.Equal(2, result.AddedAreas.Length);
		Assert.Empty(result.RemovedAreas);
		Assert.Empty(result.IntensityChangedAreas);
		Assert.Contains(result.AddedAreas, a => a.Code == 350 && a.Intensity == JmaIntensity.Int3);
		Assert.Contains(result.AddedAreas, a => a.Code == 351 && a.Intensity == JmaIntensity.Int2);
	}

	[Fact(DisplayName = "前回ありで今回nullの場合は全地域が削除")]
	public void ComputeFromPrefs_CurrentNull_AllRemoved()
	{
		var previous = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3)
		);

		var result = _calculator.ComputeFromPrefs(previous, null);

		Assert.True(result.HasChanges);
		Assert.Empty(result.AddedAreas);
		Assert.Single(result.RemovedAreas);
		Assert.Empty(result.IntensityChangedAreas);
		Assert.Equal(350, result.RemovedAreas[0].Code);
	}

	[Fact(DisplayName = "同一データの場合は変更なし")]
	public void ComputeFromPrefs_SameData_NoChanges()
	{
		var prefs = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3),
			(351, "東京都多摩東部", JmaIntensity.Int2)
		);

		var result = _calculator.ComputeFromPrefs(prefs, prefs);

		Assert.False(result.HasChanges);
	}

	[Fact(DisplayName = "地域追加の検出")]
	public void ComputeFromPrefs_AreaAdded()
	{
		var previous = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3)
		);
		var current = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3),
			(351, "東京都多摩東部", JmaIntensity.Int2)
		);

		var result = _calculator.ComputeFromPrefs(previous, current);

		Assert.True(result.HasChanges);
		Assert.Single(result.AddedAreas);
		Assert.Equal(351, result.AddedAreas[0].Code);
		Assert.Equal("東京都多摩東部", result.AddedAreas[0].Name);
		Assert.Empty(result.RemovedAreas);
		Assert.Empty(result.IntensityChangedAreas);
	}

	[Fact(DisplayName = "地域削除の検出")]
	public void ComputeFromPrefs_AreaRemoved()
	{
		var previous = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3),
			(351, "東京都多摩東部", JmaIntensity.Int2)
		);
		var current = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3)
		);

		var result = _calculator.ComputeFromPrefs(previous, current);

		Assert.True(result.HasChanges);
		Assert.Empty(result.AddedAreas);
		Assert.Single(result.RemovedAreas);
		Assert.Equal(351, result.RemovedAreas[0].Code);
		Assert.Empty(result.IntensityChangedAreas);
	}

	[Fact(DisplayName = "震度変化の検出")]
	public void ComputeFromPrefs_IntensityChanged()
	{
		var previous = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3)
		);
		var current = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int4)
		);

		var result = _calculator.ComputeFromPrefs(previous, current);

		Assert.True(result.HasChanges);
		Assert.Empty(result.AddedAreas);
		Assert.Empty(result.RemovedAreas);
		Assert.Single(result.IntensityChangedAreas);
		Assert.Equal(350, result.IntensityChangedAreas[0].Code);
		Assert.Equal(JmaIntensity.Int3, result.IntensityChangedAreas[0].PreviousIntensity);
		Assert.Equal(JmaIntensity.Int4, result.IntensityChangedAreas[0].CurrentIntensity);
	}

	[Fact(DisplayName = "追加・削除・震度変化が同時に発生する複合変化の検出")]
	public void ComputeFromPrefs_CompoundChanges()
	{
		var previous = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int3),
			(352, "神奈川県東部", JmaIntensity.Int2)
		);
		var current = CreatePrefs(
			(350, "東京都23区", JmaIntensity.Int4),
			(351, "東京都多摩東部", JmaIntensity.Int2)
		);

		var result = _calculator.ComputeFromPrefs(previous, current);

		Assert.True(result.HasChanges);
		Assert.Single(result.AddedAreas);
		Assert.Equal(351, result.AddedAreas[0].Code);
		Assert.Single(result.RemovedAreas);
		Assert.Equal(352, result.RemovedAreas[0].Code);
		Assert.Single(result.IntensityChangedAreas);
		Assert.Equal(350, result.IntensityChangedAreas[0].Code);
	}

	[Fact(DisplayName = "複数都道府県にまたがる地域変化の検出")]
	public void ComputeFromPrefs_MultiPrefecture()
	{
		var previous = new EarthquakeObservationPref[]
		{
			new() { Name = "東京都", Code = 13, Areas = [
				new() { Code = 350, Name = "東京都23区", MaxIntensity = JmaIntensity.Int3 },
			]},
		};
		var current = new EarthquakeObservationPref[]
		{
			new() { Name = "東京都", Code = 13, Areas = [
				new() { Code = 350, Name = "東京都23区", MaxIntensity = JmaIntensity.Int3 },
			]},
			new() { Name = "神奈川県", Code = 14, Areas = [
				new() { Code = 360, Name = "神奈川県東部", MaxIntensity = JmaIntensity.Int2 },
			]},
		};

		var result = _calculator.ComputeFromPrefs(previous, current);

		Assert.True(result.HasChanges);
		Assert.Single(result.AddedAreas);
		Assert.Equal(360, result.AddedAreas[0].Code);
		Assert.Equal("神奈川県東部", result.AddedAreas[0].Name);
	}

	[Fact(DisplayName = "FlatPoints: 両方nullの場合は変更なし")]
	public void ComputeFromFlatPoints_BothNull_NoChanges()
	{
		var result = _calculator.ComputeFromFlatPoints(null, null);
		Assert.False(result.HasChanges);
	}

	[Fact(DisplayName = "FlatPoints: ポイント追加・削除・震度変化の検出")]
	public void ComputeFromFlatPoints_Changes()
	{
		var previous = new EarthquakeObservationFlatPoint[]
		{
			new() { PrefName = "東京都", Address = "東京都千代田区", Intensity = JmaIntensity.Int3, IsArea = false },
			new() { PrefName = "東京都", Address = "東京都港区", Intensity = JmaIntensity.Int2, IsArea = false },
		};
		var current = new EarthquakeObservationFlatPoint[]
		{
			new() { PrefName = "東京都", Address = "東京都千代田区", Intensity = JmaIntensity.Int4, IsArea = false },
			new() { PrefName = "神奈川県", Address = "横浜市中区", Intensity = JmaIntensity.Int2, IsArea = false },
		};

		var result = _calculator.ComputeFromFlatPoints(previous, current);

		Assert.True(result.HasChanges);
		Assert.Single(result.AddedAreas);
		Assert.Equal("横浜市中区", result.AddedAreas[0].Name);
		Assert.Single(result.RemovedAreas);
		Assert.Equal("東京都港区", result.RemovedAreas[0].Name);
		Assert.Single(result.IntensityChangedAreas);
		Assert.Equal("東京都千代田区", result.IntensityChangedAreas[0].Name);
		Assert.Equal(JmaIntensity.Int3, result.IntensityChangedAreas[0].PreviousIntensity);
		Assert.Equal(JmaIntensity.Int4, result.IntensityChangedAreas[0].CurrentIntensity);
	}
}
