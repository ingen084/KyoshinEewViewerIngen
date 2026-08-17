using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

[JsonDerivedType(typeof(AshFallReportGroup))]
[JsonDerivedType(typeof(DCXReportGroup))]
[JsonDerivedType(typeof(EewReportGroup))]
[JsonDerivedType(typeof(FloodReportGroup))]
[JsonDerivedType(typeof(HypocenterReportGroup))]
[JsonDerivedType(typeof(MarineReportGroup))]
[JsonDerivedType(typeof(NankaiTroughEarthquakeReportGroup))]
[JsonDerivedType(typeof(NorthwestPacificTsunamiReportGroup))]
[JsonDerivedType(typeof(SeismicIntensityReportGroup))]
[JsonDerivedType(typeof(TsunamiReportGroup))]
[JsonDerivedType(typeof(TyphoonReportGroup))]
[JsonDerivedType(typeof(UnknownReportGroup))]
[JsonDerivedType(typeof(VolcanoReportGroup))]
[JsonDerivedType(typeof(WeatherReportGroup))]
public abstract class DisasterCrisisInformation : ObservableObject
{
	public abstract string Type { get; }
}
