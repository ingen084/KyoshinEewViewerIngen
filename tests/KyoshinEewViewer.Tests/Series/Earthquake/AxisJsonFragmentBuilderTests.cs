using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using System;
using System.Linq;
using Xunit;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

public class AxisJsonFragmentBuilderTests
{
	private static EarthquakeMessage CreateBaseMessage(string title, string status = "通常")
		=> new()
		{
			Control = new JmaXmlControl
			{
				Title = title,
				Status = status,
				DateTime = new DateTime(2026, 5, 5, 12, 30, 0),
			},
			Head = new JmaXmlHead
			{
				EventID = "20260505123000",
				Title = title,
				ReportDateTime = new DateTime(2026, 5, 5, 12, 30, 5),
				TargetDateTime = new DateTime(2026, 5, 5, 12, 29, 50),
				InfoType = "発表",
				Serial = "1",
			},
			Body = new EarthquakeBody(),
			Uuid = Guid.NewGuid().ToString(),
		};

	[Fact(DisplayName = "震度速報のJSONからIntensityInformationFragmentが構築される")]
	public void IntensityReport_BuildsIntensityFragment()
	{
		var msg = CreateBaseMessage("震度速報");
		msg.Body.Intensity = new Intensity
		{
			Observation = new Observation
			{
				MaxInt = "4",
				Pref =
				[
					new Pref
					{
						Code = "200",
						MaxInt = "4",
						Name = "宮城県",
						Area =
						[
							new Area
							{
								Code = "211",
								MaxInt = "4",
								Name = "宮城県北部",
							},
						],
					},
				],
			},
		};

		var fragment = EarthquakeInformationFragment.CreateFromAxisJson(msg);

		var intensity = Assert.IsType<IntensityInformationFragment>(fragment);
		Assert.Equal("震度速報", intensity.Title);
		Assert.Equal(JmaIntensity.Int4, intensity.MaxIntensity);
		Assert.Equal("宮城県北部", intensity.Place);
		Assert.True(intensity.IsSingleArea);
		Assert.False(intensity.IsTraining);
		Assert.False(intensity.IsTest);
	}

	[Fact(DisplayName = "震源情報のJSONからHypocenterInformationFragmentが構築される")]
	public void HypocenterReport_BuildsHypocenterFragment()
	{
		var msg = CreateBaseMessage("震源に関する情報");
		msg.Body.Earthquake =
		[
			new global::KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message.Earthquake
			{
				OriginTime = new DateTime(2026, 5, 5, 12, 29, 30),
				ArrivalTime = new DateTime(2026, 5, 5, 12, 29, 50),
				Hypocenter = new Hypocenter
				{
					Area = new HypocenterArea
					{
						Name = "宮城県沖",
						Coordinate =
						[
							new PhysicalQuantity
							{
								Type = "震源位置",
								ValueOf = "+38.5+142.1-50000/",
								Description = "北緯38.5度 東経142.1度 深さ50km",
							},
						],
					},
				},
				Magnitude =
				[
					new PhysicalQuantity
					{
						Type = "Mj",
						ValueOf = "6.8",
					},
				],
			},
		];

		var fragment = EarthquakeInformationFragment.CreateFromAxisJson(msg);

		var hypo = Assert.IsType<HypocenterInformationFragment>(fragment);
		Assert.Equal("震源に関する情報", hypo.Title);
		Assert.Equal("宮城県沖", hypo.Place);
		Assert.Equal(6.8f, hypo.Magnitude);
		Assert.Equal(50, hypo.Depth);
		Assert.Equal(new DateTime(2026, 5, 5, 12, 29, 30), hypo.OccurrenceTime);
	}

	[Fact(DisplayName = "サポート外Titleはnullを返す")]
	public void UnsupportedTitle_ReturnsNull()
	{
		var msg = CreateBaseMessage("サポート外の情報");
		var fragment = EarthquakeInformationFragment.CreateFromAxisJson(msg);
		Assert.Null(fragment);
	}

	[Fact(DisplayName = "[診断] 同一データから生成した Fragment 同士が等価判定で true")]
	public void Diagnostic_TwoFragmentsFromSameMessage_AreEquivalent()
	{
		var msg1 = CreateBaseMessage("震度速報");
		msg1.Body.Intensity = new Intensity
		{
			Observation = new Observation
			{
				MaxInt = "4",
				Pref = [new Pref { Code = "200", MaxInt = "4", Name = "宮城県", Area = [new Area { Code = "211", MaxInt = "4", Name = "宮城県北部" }] }],
			},
		};
		var msg2 = CreateBaseMessage("震度速報");
		msg2.Body.Intensity = new Intensity
		{
			Observation = new Observation
			{
				MaxInt = "4",
				Pref = [new Pref { Code = "200", MaxInt = "4", Name = "宮城県", Area = [new Area { Code = "211", MaxInt = "4", Name = "宮城県北部" }] }],
			},
		};
		var f1 = (IntensityInformationFragment)EarthquakeInformationFragment.CreateFromAxisJson(msg1)!;
		var f2 = (IntensityInformationFragment)EarthquakeInformationFragment.CreateFromAxisJson(msg2)!;

		Assert.Equal(f1.Title, f2.Title);
		Assert.Equal(f1.IsTraining, f2.IsTraining);
		Assert.Equal(f1.IsTest, f2.IsTest);
		Assert.Equal(f1.IsCancelled, f2.IsCancelled);
		Assert.Equal(f1.IsCorrected, f2.IsCorrected);
		Assert.Equal(f1.Place, f2.Place);
		Assert.Equal(f1.DetectionTime, f2.DetectionTime);
		Assert.Equal(f1.MaxIntensity, f2.MaxIntensity);
		Assert.Equal(f1.IsSingleArea, f2.IsSingleArea);
		Assert.Equal(f1.Comment, f2.Comment);
		Assert.Equal(f1.FreeFormComment, f2.FreeFormComment);
		Assert.Equal(f1.Observation.Count, f2.Observation.Count);
		Assert.True(f1.IsEquivalentContent(f2));
	}

	[Fact(DisplayName = "訓練の Status を持つ電文では IsTraining フラグが立つ")]
	public void TrainingStatus_SetsIsTrainingFlag()
	{
		var msg = CreateBaseMessage("震度速報", status: "訓練");
		msg.Body.Intensity = new Intensity
		{
			Observation = new Observation
			{
				MaxInt = "3",
				Pref =
				[
					new Pref
					{
						Code = "100",
						MaxInt = "3",
						Name = "北海道",
						Area =
						[
							new Area { Code = "100", MaxInt = "3", Name = "北海道訓練地域" },
						],
					},
				],
			},
		};

		var fragment = EarthquakeInformationFragment.CreateFromAxisJson(msg);

		var intensity = Assert.IsType<IntensityInformationFragment>(fragment);
		Assert.True(intensity.IsTraining);
	}
}
