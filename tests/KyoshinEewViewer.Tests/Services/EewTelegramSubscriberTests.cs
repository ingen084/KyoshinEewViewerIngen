using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Tests.Services.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace KyoshinEewViewer.Tests.Services;

public class EewTelegramSubscriberTests
{
	private const string EventId = "20261108120000";

	[Fact(DisplayName = "dmdata の訓練電文は訓練の EEW として受信し、試験電文は扱わず、訓練の取消は訓練の EEW だけに適用する")]
	public async Task 訓練電文を訓練のEEWとして受信する()
	{
		var config = new KyoshinEewViewerConfiguration();
		var controller = new EewController(
			NullLogger<EewController>.Instance,
			null!,
			config,
			new SoundPlayerService(config, NullLogger<SoundPlayerService>.Instance),
			new WorkflowService(NullLogger<WorkflowService>.Instance));
		var publisher = new MockTelegramPublisher { SupportedCategories = [InformationCategory.EewForecast, InformationCategory.EewWarning] };
		var serviceProvider = new Mock<IServiceProvider>();
		serviceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher))).Returns(publisher);
		var telegramProvider = new TelegramProvideService(NullLogger<TelegramProvideService>.Instance, serviceProvider.Object);
		_ = new EewTelegramSubscriber(NullLogger<EewTelegramSubscriber>.Instance, controller, telegramProvider, new TimerService(NullLogger<TimerService>.Instance, config));
		await telegramProvider.StartAsync([typeof(MockTelegramPublisher)]);
		var trainingKey = Eew.GetKey(EventId, true);

		await ReceiveAsync(publisher, "訓練", "発表", 1, () => controller.TryGetEew(trainingKey, out _));
		Assert.True(controller.TryGetEew(trainingKey, out var training));
		Assert.True(training!.IsTraining);
		Assert.Equal(EventId, training.Id);
		Assert.False(controller.TryGetEew(EventId, out _));

		publisher.SimulateTelegramArrival(InformationCategory.EewForecast, CreateTelegram("試験", "発表", 2));
		await Task.Delay(100);
		Assert.True(controller.TryGetEew(trainingKey, out training));
		Assert.Equal(1, training!.SerialNo);
		Assert.False(controller.TryGetEew(EventId, out _));

		await ReceiveAsync(publisher, "訓練", "取消", 3, () => controller.TryGetEew(trainingKey, out var e) && e!.IsTrueCancelled);

		await ReceiveAsync(publisher, "通常", "発表", 1, () => controller.TryGetEew(EventId, out _));
		Assert.True(controller.TryGetEew(EventId, out var real));
		Assert.False(real!.IsTraining);
		Assert.False(controller.TryGetEew(trainingKey, out _));
	}

	private static async Task ReceiveAsync(MockTelegramPublisher publisher, string status, string infoType, int serial, Func<bool> condition)
	{
		publisher.SimulateTelegramArrival(InformationCategory.EewForecast, CreateTelegram(status, infoType, serial));
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		while (!condition())
		{
			if (timeout.IsCancellationRequested)
				throw new TimeoutException($"{status}の{infoType}の電文の処理を待っている間に時間切れになりました");
			await Task.Delay(10, CancellationToken.None);
		}
	}

	private static MockTelegram CreateTelegram(string status, string infoType, int serial)
		=> new($"{status}-{infoType}-{serial}", "緊急地震速報（地震動予報）", $"{status}-{infoType}-{serial}", new DateTime(2026, 11, 8, 12, 0, serial), Encoding.UTF8.GetBytes(EewXml(status, infoType, serial)));

	private static string EewXml(string status, string infoType, int serial)
		=> $"""
			<?xml version="1.0" encoding="UTF-8"?>
			<Report xmlns="http://xml.kishou.go.jp/jmaxml1/" xmlns:jmx="http://xml.kishou.go.jp/jmaxml1/">
			  <Control>
			    <Title>緊急地震速報（地震動予報）</Title>
			    <DateTime>2026-11-08T03:00:0{serial}Z</DateTime>
			    <Status>{status}</Status>
			    <EditorialOffice>気象庁本庁</EditorialOffice>
			    <PublishingOffice>気象庁</PublishingOffice>
			  </Control>
			  <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
			    <Title>緊急地震速報（地震動予報）</Title>
			    <ReportDateTime>2026-11-08T12:00:0{serial}+09:00</ReportDateTime>
			    <TargetDateTime>2026-11-08T12:00:0{serial}+09:00</TargetDateTime>
			    <EventID>{EventId}</EventID>
			    <InfoType>{infoType}</InfoType>
			    <Serial>{serial}</Serial>
			    <InfoKind>緊急地震速報</InfoKind>
			    <InfoKindVersion>1.0_0</InfoKindVersion>
			    <Headline>
			      <Text />
			    </Headline>
			  </Head>
			  <Body xmlns="http://xml.kishou.go.jp/jmaxml1/body/seismology1/" xmlns:jmx_eb="http://xml.kishou.go.jp/jmaxml1/elementBasis1/">
			    <Earthquake>
			      <OriginTime>2026-11-08T12:00:00+09:00</OriginTime>
			      <ArrivalTime>2026-11-08T12:00:00+09:00</ArrivalTime>
			      <Hypocenter>
			        <Area>
			          <Name>千葉県東方沖</Name>
			          <Code type="震央地名">350</Code>
			          <jmx_eb:Coordinate description="北緯３５．７度　東経１４０．８度　深さ　３０ｋｍ" datum="日本測地系">+35.7+140.8-30000/</jmx_eb:Coordinate>
			        </Area>
			        <Accuracy>
			          <Epicenter rank="4" rank2="4">4</Epicenter>
			          <Depth rank="4">4</Depth>
			          <MagnitudeCalculation rank="4">4</MagnitudeCalculation>
			          <NumberOfMagnitudeCalculation>4</NumberOfMagnitudeCalculation>
			        </Accuracy>
			      </Hypocenter>
			      <jmx_eb:Magnitude type="Mj" description="Ｍ４．５">4.5</jmx_eb:Magnitude>
			    </Earthquake>
			    <Intensity>
			      <Forecast>
			        <CodeDefine>
			          <Type xpath="Pref/Code">地震情報／都道府県等</Type>
			          <Type xpath="Pref/Area/Code">緊急地震速報／府県予報区</Type>
			        </CodeDefine>
			        <ForecastInt>
			          <From>3</From>
			          <To>3</To>
			        </ForecastInt>
			      </Forecast>
			    </Intensity>
			  </Body>
			</Report>
			""";
}
