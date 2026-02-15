using EewModels = KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Tests.Services.Eew;

/// <summary>
/// EewControllerテストの共通基底クラス
/// </summary>
public abstract class EewControllerTestBase : IDisposable
{
	protected EewController Controller { get; }
	protected KyoshinEewViewerConfiguration Config { get; }
	protected List<(DateTime UpdatedTime, EewModels.Eew[] Eews)> EewUpdatedCalls { get; }

	protected EewControllerTestBase()
	{
		var logManager = new DefaultLogManager();
		Config = new KyoshinEewViewerConfiguration();
		var soundPlayer = new SoundPlayerService(Config, logManager);
		var workflowService = new WorkflowService(logManager);

		Controller = new EewController(
			logManager,
			null!,
			Config,
			soundPlayer,
			workflowService);

		EewUpdatedCalls = [];
		Controller.EewUpdated += (time, eews) => EewUpdatedCalls.Add((time, eews));
	}

	protected static EewModels.Eew CreateEew(
		string id = "test-event-1",
		EewSource source = EewSource.KyoshinMonitor,
		int serialNo = 1,
		bool isFinal = false,
		bool isCancelled = false,
		bool isTrueCancelled = false,
		bool isWarning = false,
		JmaIntensity maxIntensity = JmaIntensity.Int3,
		DateTime? receiveTime = null,
		EewHypocenter? hypocenter = null)
		=> new()
		{
			Id = id,
			Source = source,
			DisplaySource = source.ToString(),
			SerialNo = serialNo,
			IsFinal = isFinal,
			IsCancelled = isCancelled,
			IsTrueCancelled = isTrueCancelled,
			IsWarning = isWarning,
			MaxIntensity = maxIntensity,
			ReceiveTime = receiveTime ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			Hypocenter = hypocenter ?? CreateDefaultHypocenter(),
		};

	protected static EewHypocenter CreateDefaultHypocenter(
		int locationAccuracy = 3,
		int depthAccuracy = 3)
		=> new()
		{
			OccurrenceTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			Place = "テスト震源地",
			Location = new Location(35.0f, 139.0f),
			Magnitude = 5.0f,
			Depth = 10,
			IsTemporary = false,
			Accuracy = new EewHypocenterAccuracy
			{
				IsLocked = false,
				LocationAccuracy = locationAccuracy,
				DepthAccuracy = depthAccuracy,
				MagnitudeAccuracy = 3,
			},
		};

	public void Dispose()
	{
		Controller.Clear();
		GC.SuppressFinalize(this);
	}
}
