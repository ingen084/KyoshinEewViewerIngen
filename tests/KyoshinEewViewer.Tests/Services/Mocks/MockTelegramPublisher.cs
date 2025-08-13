using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Tests.Services.Mocks;

/// <summary>
/// テスト用のモックTelegramPublisher
/// </summary>
public class MockTelegramPublisher : TelegramPublisher
{
	public bool IsInitialized { get; private set; }
	public List<InformationCategory> SupportedCategories { get; set; } = [];
	public List<InformationCategory> StartedCategories { get; set; } = [];
	public bool ShouldFailOnInit { get; set; }
	public bool ShouldFailOnGetCategories { get; set; }
	public string Name { get; set; } = "MockPublisher";
	public int StartCallCount { get; private set; } = 0;

	public override Task InitializeAsync()
	{
		if (ShouldFailOnInit)
			throw new Exception($"{Name} の初期化に失敗しました");
		IsInitialized = true;
		return Task.CompletedTask;
	}

	public override Task<InformationCategory[]> GetSupportedCategoriesAsync()
	{
		if (ShouldFailOnGetCategories)
			throw new Exception($"{Name} のカテゴリ取得に失敗しました");
		return Task.FromResult(SupportedCategories.ToArray());
	}

	public override void Start(InformationCategory[] categories)
	{
		StartCallCount++;
		StartedCategories.AddRange(categories);
	}

	public override void Stop(InformationCategory[] categories)
	{
		StartedCategories.RemoveAll(categories.Contains);
	}

	// テスト用のイベント発火メソッド
	public void SimulateFailure(InformationCategory[] categories, bool isRestorable)
	{
		OnFailed(categories, isRestorable);
	}

	public void SimulateRecovery()
	{
		OnInformationCategoryUpdated();
	}

	public void SimulateTelegramArrival(InformationCategory category, Telegram telegram)
	{
		OnTelegramArrived(category, telegram);
	}

	public void SimulateHistoryTelegramArrival(string name, InformationCategory category, Telegram[] telegrams)
	{
		OnHistoryTelegramArrived(name, category, telegrams);
	}
}