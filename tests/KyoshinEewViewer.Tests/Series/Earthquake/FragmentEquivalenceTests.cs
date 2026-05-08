using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;
using System;
using Xunit;

namespace KyoshinEewViewer.Tests.Series.Earthquake;

public class FragmentEquivalenceTests
{
	private static IntensityInformationFragment CreateSokuhou(
		JmaIntensity max = JmaIntensity.Int4,
		string place = "宮城県北部",
		bool isSingleArea = false,
		bool isTraining = false,
		bool isCancelled = false)
	{
		var time = new DateTime(2026, 5, 5, 12, 0, 0);
		return new IntensityInformationFragment
		{
			ArrivedTime = time,
			Title = "震度速報",
			IsTraining = isTraining,
			IsTest = false,
			Place = place,
			DetectionTime = time,
			MaxIntensity = max,
			IsSingleArea = isSingleArea,
			Observation = [],
			IsCancelled = isCancelled,
		};
	}

	private static HypocenterInformationFragment CreateHypo(
		float magnitude = 6.8f,
		int depth = 50,
		string place = "宮城県沖")
	{
		var time = new DateTime(2026, 5, 5, 12, 0, 0);
		return new HypocenterInformationFragment
		{
			ArrivedTime = time,
			Title = "震源に関する情報",
			IsTraining = false,
			IsTest = false,
			OccurrenceTime = time,
			Place = place,
			Location = new Location(38.5f, 142.1f),
			LocationError = null,
			Magnitude = magnitude,
			MagnitudeAlternativeText = null,
			Depth = depth,
			DepthError = null,
		};
	}

	[Fact(DisplayName = "同一の震度速報Fragmentは等価判定でtrue")]
	public void IntensityFragment_SameContent_IsEquivalent()
	{
		var a = CreateSokuhou();
		var b = CreateSokuhou();
		Assert.True(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "最大震度が異なる震度速報Fragmentは等価判定でfalse")]
	public void IntensityFragment_DifferentMaxIntensity_IsNotEquivalent()
	{
		var a = CreateSokuhou(JmaIntensity.Int3);
		var b = CreateSokuhou(JmaIntensity.Int4);
		Assert.False(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "Placeが異なる震度速報Fragmentは等価判定でfalse")]
	public void IntensityFragment_DifferentPlace_IsNotEquivalent()
	{
		var a = CreateSokuhou(place: "宮城県北部");
		var b = CreateSokuhou(place: "岩手県沿岸南部");
		Assert.False(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "IsCancelledが異なるFragmentは等価判定でfalse")]
	public void IntensityFragment_DifferentCancelFlag_IsNotEquivalent()
	{
		var a = CreateSokuhou();
		var b = CreateSokuhou(isCancelled: true);
		Assert.False(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "IsTrainingが異なるFragmentは等価判定でfalse")]
	public void IntensityFragment_DifferentTraining_IsNotEquivalent()
	{
		var a = CreateSokuhou();
		var b = CreateSokuhou(isTraining: true);
		Assert.False(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "震源情報と震度速報は型が違うため等価判定でfalse")]
	public void DifferentTypes_IsNotEquivalent()
	{
		var a = CreateSokuhou();
		var b = CreateHypo();
		Assert.False(a.IsEquivalentContent(b));
		Assert.False(b.IsEquivalentContent(a));
	}

	[Fact(DisplayName = "同一の震源情報Fragmentは等価判定でtrue")]
	public void HypoFragment_SameContent_IsEquivalent()
	{
		var a = CreateHypo();
		var b = CreateHypo();
		Assert.True(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "マグニチュードが異なる震源情報Fragmentは等価判定でfalse")]
	public void HypoFragment_DifferentMagnitude_IsNotEquivalent()
	{
		var a = CreateHypo(magnitude: 6.8f);
		var b = CreateHypo(magnitude: 7.0f);
		Assert.False(a.IsEquivalentContent(b));
	}

	[Fact(DisplayName = "深さが異なる震源情報Fragmentは等価判定でfalse")]
	public void HypoFragment_DifferentDepth_IsNotEquivalent()
	{
		var a = CreateHypo(depth: 50);
		var b = CreateHypo(depth: 60);
		Assert.False(a.IsEquivalentContent(b));
	}
}
