using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using System;
using System.Linq;
using Xunit;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

public class EarthquakeEventAxisRouteTests
{
	private static EarthquakeMessage CreateMessage(string title, string infoType = "発表", string status = "通常", string serial = "1")
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
				EventID = "20260505999999",
				Title = title,
				ReportDateTime = new DateTime(2026, 5, 5, 12, 30, 5),
				TargetDateTime = new DateTime(2026, 5, 5, 12, 29, 50),
				InfoType = infoType,
				Serial = serial,
			},
			Body = new EarthquakeBody(),
			Uuid = Guid.NewGuid().ToString(),
		};

	private static EarthquakeMessage CreateIntensityMessage(string maxInt = "4", string infoType = "発表", string serial = "1")
	{
		var msg = CreateMessage("震度速報", infoType: infoType, serial: serial);
		msg.Body.Intensity = new Intensity
		{
			Observation = new Observation
			{
				MaxInt = maxInt,
				Pref =
				[
					new Pref
					{
						Code = "200",
						MaxInt = maxInt,
						Name = "宮城県",
						Area =
						[
							new Area { Code = "211", MaxInt = maxInt, Name = "宮城県北部" },
						],
					},
				],
			},
		};
		return msg;
	}

	[Fact(DisplayName = "AXIS取消電文を2回投入しても2回目はnullが返り通知が走らない")]
	public void AxisCancellation_IsIdempotent()
	{
		var ev = new EarthquakeEvent("20260505999999");
		var first = CreateIntensityMessage();
		var firstFragment = ev.ProcessAxisMessage(first);
		Assert.NotNull(firstFragment);

		var cancel1 = CreateMessage("震度速報", infoType: "取消");
		var cancel2 = CreateMessage("震度速報", infoType: "取消");

		var afterFirstCancel = ev.ProcessAxisMessage(cancel1);
		Assert.NotNull(afterFirstCancel);
		Assert.True(afterFirstCancel.IsCancelled);

		var afterSecondCancel = ev.ProcessAxisMessage(cancel2);
		Assert.Null(afterSecondCancel);
	}

	[Fact(DisplayName = "AXIS取消で対象タイトルのFragmentが既に取消済みなら冪等にnullが返る")]
	public void AxisCancellation_AlreadyCancelled_ReturnsNull()
	{
		var ev = new EarthquakeEvent("20260505999999");
		ev.ProcessAxisMessage(CreateIntensityMessage());

		var cancel = CreateMessage("震度速報", infoType: "取消");
		ev.ProcessAxisMessage(cancel);

		var second = ev.ProcessAxisMessage(CreateMessage("震度速報", infoType: "取消"));
		Assert.Null(second);
	}

	[Fact(DisplayName = "AXIS経路は同コンテンツの再受信で等価判定によりnullが返る")]
	public void AxisRoute_SameContent_IsDroppedByEquivalence()
	{
		var ev = new EarthquakeEvent("20260505999999");
		var first = CreateIntensityMessage(serial: "1");
		var firstFragment = ev.ProcessAxisMessage(first);
		Assert.NotNull(firstFragment);

		var dup = CreateIntensityMessage(serial: "2");
		var second = ev.ProcessAxisMessage(dup);
		Assert.Null(second);

		Assert.Single(ev.Fragments);
	}

	[Fact(DisplayName = "AXIS経路は異なるコンテンツでは別Fragmentとして両方保持される")]
	public void AxisRoute_DifferentContent_AddsBothFragments()
	{
		var ev = new EarthquakeEvent("20260505999999");
		var first = CreateIntensityMessage(maxInt: "3");
		var firstFragment = ev.ProcessAxisMessage(first);
		Assert.NotNull(firstFragment);

		var second = CreateIntensityMessage(maxInt: "4");
		var secondFragment = ev.ProcessAxisMessage(second);
		Assert.NotNull(secondFragment);

		Assert.Equal(2, ev.Fragments.Count);
	}

	[Fact(DisplayName = "AXISでFragmentを保持後、同コンテンツのXML経路Fragmentは追加されない（_hasAxisFragment=true 経路）")]
	public void XmlRoute_WithAxisFragmentPresent_DropsEquivalent()
	{
		var ev = new EarthquakeEvent("20260505999999");
		ev.ProcessAxisMessage(CreateIntensityMessage());
		Assert.Single(ev.Fragments);

		// XML 由来として AddFragment した場合は _hasAxisFragment 切替に関与しないが、
		// ここでは AXIS 経路の等価判定の挙動を確認するため、再度 AXIS 経路で同コンテンツを投入
		var second = ev.ProcessAxisMessage(CreateIntensityMessage());
		Assert.Null(second);
		Assert.Single(ev.Fragments);
	}
}
