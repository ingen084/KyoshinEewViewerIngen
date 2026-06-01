using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Tests.Services.Mocks;
using KyoshinMonitorLib;
using Moq;
using Splat;
using System;
using System.Text;
using Xunit;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

public class EarthquakeWatchServiceDryRunTests
{
	[Fact(DisplayName = "dryRunは同一EventIDの既存イベントを変更しない")]
	public async Task ProcessInformation_DryRun_DoesNotMutateExistingEvent()
	{
		var service = CreateService();

		var first = CreateTelegram("first", "3");
		var existing = await service.ProcessInformation(first);

		Assert.NotNull(existing);
		Assert.Single(existing.Fragments);
		Assert.Single(service.Earthquakes);
		Assert.Equal("テスト地域A", existing.Place);

		var dryRun = CreateTelegram("dry-run", "5弱");
		var preview = await service.ProcessInformation(dryRun, dryRun: true);

		Assert.NotNull(preview);
		Assert.NotSame(existing, preview);
		Assert.Equal(JmaIntensity.Int5Lower, preview.Intensity);
		Assert.Single(service.Earthquakes);
		Assert.Single(existing.Fragments);
		Assert.Equal(JmaIntensity.Int3, existing.Intensity);
	}

	private static EarthquakeWatchService CreateService()
	{
		var logManager = CreateLogManager();
		var config = new KyoshinEewViewerConfiguration();
		var cacheService = new InformationCacheService(logManager);
		var telegramProvider = new TelegramProvideService(logManager);
		var dmdata = new DmdataRedundantTelegramPublisher(logManager, config, cacheService);
		var axis = new AxisInformationProvider(config, logManager);

		return new EarthquakeWatchService(logManager, config, telegramProvider, dmdata, axis);
	}

	private static ILogManager CreateLogManager()
	{
		var logger = new Mock<IFullLogger>();
		var logManager = new Mock<ILogManager>();
		logManager.Setup(x => x.GetLogger(It.IsAny<Type>())).Returns(logger.Object);
		return logManager.Object;
	}

	private static MockTelegram CreateTelegram(string key, string maxIntensity)
		=> new(
			key,
			"震度速報",
			"VXSE51",
			new DateTime(2026, 5, 5, 12, 30, 5),
			Encoding.UTF8.GetBytes(CreateIntensityXml(maxIntensity)));

	private static string CreateIntensityXml(string maxIntensity)
		=> $$"""
		<?xml version="1.0" encoding="UTF-8"?>
		<Report>
			<Control>
				<Title>震度速報</Title>
				<DateTime>2026-05-05T12:30:05+09:00</DateTime>
				<Status>通常</Status>
				<EditorialOffice>気象庁本庁</EditorialOffice>
				<PublishingOffice>気象庁</PublishingOffice>
			</Control>
			<Head>
				<Title>震度速報</Title>
				<ReportDateTime>2026-05-05T12:30:05+09:00</ReportDateTime>
				<TargetDateTime>2026-05-05T12:29:50+09:00</TargetDateTime>
				<EventID>20260505999999</EventID>
				<InfoType>発表</InfoType>
				<Serial>1</Serial>
				<InfoKind>震度速報</InfoKind>
				<InfoKindVersion>1.0_0</InfoKindVersion>
			</Head>
			<Body>
				<Intensity>
					<Observation>
						<MaxInt>{{maxIntensity}}</MaxInt>
						<Pref>
							<Name>テスト県</Name>
							<Code>99</Code>
							<MaxInt>{{maxIntensity}}</MaxInt>
							<Area>
								<Name>テスト地域A</Name>
								<Code>999</Code>
								<MaxInt>{{maxIntensity}}</MaxInt>
							</Area>
						</Pref>
					</Observation>
				</Intensity>
			</Body>
		</Report>
		""";
}
