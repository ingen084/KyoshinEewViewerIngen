using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.DCReportParser.CAMF;

/// <summary>
/// Common Alert Message Format
/// </summary>
public record CommonAlertMessage(
	MessageType MessageType,
	ushort RegionName,
	byte ProviderIdentifier,
	byte HazardCategory,
	Severity Severity,
	HazardOnset HazardOnset,
	HazardDuration HazardDuration,
	bool IsUseCountryOrRegionGuidanceLibrary,// int SelectionOfLibrary,
	byte VersionOfLibrary,
	ushort GuidanceToReactLibrary,
	float EllipseCentreLatitude,
	float EllipseCentreLongitude,
	byte EllipseSemiMajorAxis,
	byte EllipseSemiMinorAxis,
	float EllipseAzimuth,
	int MainSubjectForSpecificSettings,
	int SpecificSettings
)
{
}
