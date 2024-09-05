using KyoshinEewViewer.DCReportParser.CAMF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static KyoshinEewViewer.DCReportParser.DCXReport;

namespace KyoshinEewViewer.DCReportParser;

public class DCXReport(byte[] rawData, Preamble preamble, byte messageType, SatelliteDesignationMaskType satelliteDesignationMaskType, bool[] satelliteDesignationMask) : DCReport(rawData, preamble, messageType)
{
	public static DCXReport Parse(byte[] rawData, Preamble preamble, byte messageType)
	{
		var rc = (ReportClassification)GetValue(rawData, 14, 3);
		//if (!Enum.IsDefined(rc))
		//	throw new DCReportParseException("Rc が不正です: " + rc);

		var oc = (byte)GetValue(rawData, 17, 6);
		//if (oc is < 1 or > 60)
		//	throw new DCReportParseException("Oc が範囲外です: " + oc);

		return new DCXReport(rawData, preamble, messageType, rc, oc);
	}

	public SatelliteDesignationMaskType SatelliteDesignationMaskType { get; } = satelliteDesignationMaskType;
	public bool[] SatelliteDesignationMask { get; } = satelliteDesignationMask;

	public CommonAlertMessage CommonAlertMessage { get; }
}

public enum SatelliteDesignationMaskType
{
	// for Japan or for use outside Japan
	ServiceType = 0,
	// MT44 transmission status
	TransmissionStatus = 1,
}
