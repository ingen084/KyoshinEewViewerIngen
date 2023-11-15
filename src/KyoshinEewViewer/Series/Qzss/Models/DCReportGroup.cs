using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Qzss.Models;

public abstract class DCReportGroup<TReport> where TReport : DCReport
{
	public abstract string Title { get; }

	public List<TReport> Reports { get; } = new();
}
public class EewReportGroup : DCReportGroup<EewReport>
{
	public override string Title => "緊急地震速報(警報)";
}

public class SeismicIntensityReportGroup : DCReportGroup<SeismicIntensityReport>
{
	public override string Title => "震度情報";
}

public class HypocenterReportGroup : DCReportGroup<HypocenterReport>
{
	public override string Title => "震源情報";
}

public class NankaiTroughEarthquakeReportGroup : DCReportGroup<NankaiTroughEarthquakeReport>
{
	public override string Title => "南海トラフ地震情報";
}

public class TsunamiReportGroup : DCReportGroup<TsunamiReport>
{
	public override string Title => "津波情報";
}

public class NorthwestPacificTsunamiReportGroup : DCReportGroup<NorthwestPacificTsunamiReport>
{
	public override string Title => "北西太平洋津波情報";
}

public class VolcanoReportGroup : DCReportGroup<VolcanoReport>
{
	public override string Title => "火山情報";
}

public class WeatherReportGroup : DCReportGroup<WeatherReport>
{
	public override string Title => "気象情報";
}

public class FloodReportGroup : DCReportGroup<FloodReport>
{
	public override string Title => "洪水情報";
}

public class TyphoonReportGroup : DCReportGroup<TyphoonReport>
{
	public override string Title => "台風情報";
}

public class MarineReportGroup : DCReportGroup<MarineReport>
{
	public override string Title => "海上情報";
}

public class AshFallReportGroup : DCReportGroup<AshFallReport>
{
	public override string Title => "降灰情報";
}
