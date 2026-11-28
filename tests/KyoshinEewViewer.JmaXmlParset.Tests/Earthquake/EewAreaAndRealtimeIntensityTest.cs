using System;
using System.Linq;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests.Earthquake;

public class EewAreaAndRealtimeIntensityTest
{
	[Fact(DisplayName = "緊急地震速報の細分区域から予報カテゴリー・予測値・主要動の到達予測時刻と状況を読み取れる")]
	public void ReadsForecastAreas()
	{
		var forecast = JmaXml.Read<TestView>("""
			<Report>
				<Body>
					<Intensity>
						<Forecast>
							<Pref>
								<Name>宮城</Name>
								<Code>9040</Code>
								<Area>
									<Name>宮城県中部</Name>
									<Code>222</Code>
									<Category><Kind><Name>緊急地震速報（警報）</Name><Code>11</Code></Kind></Category>
									<ForecastInt><From>5-</From><To>over</To></ForecastInt>
									<ForecastLgInt><From>3</From><To>4</To></ForecastLgInt>
									<ArrivalTime>2008-06-14T08:44:00+09:00</ArrivalTime>
								</Area>
							</Pref>
							<Pref>
								<Name>岩手</Name>
								<Code>9030</Code>
								<Area>
									<Name>岩手県内陸南部</Name>
									<Code>213</Code>
									<Category><Kind><Name>緊急地震速報（警報）</Name><Code>11</Code></Kind></Category>
									<ForecastInt><From>6-</From><To>6-</To></ForecastInt>
									<Condition>既に主要動到達と推測</Condition>
								</Area>
							</Pref>
							<Pref>
								<Name>熊本</Name>
								<Code>9430</Code>
								<Area>
									<Name>熊本県熊本</Name>
									<Code>741</Code>
									<Category><Kind><Name>緊急地震速報（警報）</Name><Code>19</Code></Kind></Category>
									<ForecastInt><From>4</From><To>5+</To></ForecastInt>
									<ArrivalTime>2016-04-14T21:26:39+09:00</ArrivalTime>
								</Area>
							</Pref>
						</Forecast>
					</Intensity>
				</Body>
			</Report>
			"""u8).EarthquakeBody.Intensity!.Forecast!;

		(int PrefCode, int AreaCode, string Name, string KindCode, string From, string To, string? LgFrom, string? LgTo, DateTimeOffset? ArrivalTime, string? Condition)[] expected =
		[
			(9040, 222, "宮城県中部", "11", "5-", "over", "3", "4", new DateTimeOffset(2008, 6, 14, 8, 44, 0, TimeSpan.FromHours(9)), null),
			(9030, 213, "岩手県内陸南部", "11", "6-", "6-", null, null, null, "既に主要動到達と推測"),
			(9430, 741, "熊本県熊本", "19", "4", "5+", null, null, new DateTimeOffset(2016, 4, 14, 21, 26, 39, TimeSpan.FromHours(9)), null),
		];
		Assert.Equal(expected.Length, forecast.Prefs.Count);
		foreach (var (pref, e) in forecast.Prefs.Zip(expected))
		{
			Assert.Equal(e.PrefCode, pref.Code);
			var area = Assert.Single(pref.Areas);
			Assert.Equal(e.AreaCode, area.Code);
			Assert.Equal(e.Name, area.Name);
			Assert.Equal("緊急地震速報（警報）", area.Category!.Kind.Name);
			Assert.Equal(e.KindCode, area.Category.Kind.Code);
			Assert.Null(area.Category.LastKind);
			Assert.Equal(e.From, area.ForecastIntFrom);
			Assert.Equal(e.To, area.ForecastIntTo);
			Assert.Equal(e.LgFrom, area.ForecastLgIntFrom);
			Assert.Equal(e.LgTo, area.ForecastLgIntTo);
			Assert.Equal(e.ArrivalTime, area.ArrivalTime);
			Assert.Equal(e.Condition, area.Condition);
		}
	}

	[Fact(DisplayName = "リアルタイム震度電文から観測点ごとのリアルタイム震度の震度階級と計測震度を読み取れ、計測震度のない観測点では null になる")]
	public void ReadsRealtimeIntensity()
	{
		var intensity = JmaXml.Read<TestView>("""
			<Report>
				<Body>
					<Intensity>
						<Observation>
							<CodeDefine>
								<Type xpath="Pref/Code">地震情報／都道府県等</Type>
								<Type xpath="Pref/Area/Code">地震情報／細分区域</Type>
								<Type xpath="Pref/Area/City/Code">気象・地震・火山情報／市町村等</Type>
								<Type xpath="Pref/Area/City/IntensityStation/Code">リアルタイム震度観測点</Type>
							</CodeDefine>
							<Pref>
								<Name>宮城県</Name>
								<Code>04</Code>
								<Area>
									<Name>宮城県中部</Name>
									<Code>222</Code>
									<City>
										<Name>石巻市</Name>
										<Code>0420200</Code>
										<IntensityStation><Name>石巻市○○</Name><Code>0420202</Code><Int>3</Int><K>2.6</K></IntensityStation>
										<IntensityStation><Name>石巻市××</Name><Code>0420203</Code><Int>0</Int><K>-1.2</K></IntensityStation>
									</City>
								</Area>
							</Pref>
						</Observation>
					</Intensity>
				</Body>
			</Report>
			"""u8).EarthquakeBody.Intensity!;

		Assert.Null(intensity.Forecast);
		var observation = intensity.Observation!;
		Assert.Null(observation.MaxInt);
		Assert.Equal(
			["Pref/Code", "Pref/Area/Code", "Pref/Area/City/Code", "Pref/Area/City/IntensityStation/Code"],
			observation.CodeDefineTypes.Select(t => t.XPath));
		Assert.Equal("リアルタイム震度観測点", observation.CodeDefineTypes[3].Value);

		var pref = Assert.Single(observation.Prefs);
		Assert.Equal(4, pref.Code);
		var city = Assert.Single(Assert.Single(pref.Areas).Cities);
		Assert.Equal(420200, city.Code);
		Assert.Equal(
			[("0420202", "3", (float?)2.6f), ("0420203", "0", -1.2f)],
			city.IntensityStations.Select(s => (s.Code, s.Int, s.K)));

		var station = JmaXml.Read<TestView>("""
			<Report>
				<Body>
					<Intensity>
						<Observation>
							<Pref><Area><City>
								<IntensityStation><Name>石巻市桃生町</Name><Code>0420231</Code><Int>5-</Int></IntensityStation>
							</City></Area></Pref>
						</Observation>
					</Intensity>
				</Body>
			</Report>
			"""u8).EarthquakeBody.Intensity!.Observation!.Prefs[0].Areas[0].Cities[0].IntensityStations[0];
		Assert.Equal("5-", station.Int);
		Assert.Null(station.K);
	}
}
