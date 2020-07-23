using System;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace KyoshinEewViewer.Models
{
#pragma warning disable CA2235 // Mark all non-serializable fields
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/", IsNullable = true)]
	public class Report
	{
		public ReportControl Control { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
		public Head Head { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
		public Body Body { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/")]
	public class ReportControl
	{
		public string Title { get; set; }
		public DateTime DateTime { get; set; }
		public string Status { get; set; }
		public string EditorialOffice { get; set; }
		public string PublishingOffice { get; set; }
	}

	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/", IsNullable = false)]
	public class Head
	{
		public string Title { get; set; }
		public DateTime ReportDateTime { get; set; }
		public DateTime TargetDateTime { get; set; }
		public ulong EventID { get; set; }
		public string InfoType { get; set; }
		public string InfoKind { get; set; }
		public string InfoKindVersion { get; set; }
		public Headline Headline { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class Headline
	{
		public string Text { get; set; }
		[XmlElement("Information")]
		public HeadlineInformation[] Informations { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformation
	{
		[XmlElement("Item")]
		public HeadlineInformationItem[] Items { get; set; }
		[XmlAttribute("type")]
		public string Type { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItem
	{
		public HeadlineInformationItemKind Kind { get; set; }
		public HeadlineInformationItemAreas Areas { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItemKind
	{
		public string Name { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItemAreas
	{
		[XmlElement("Area")]
		public HeadlineInformationItemAreasArea[] Area { get; set; }
		[XmlAttribute("codeType")]
		public string CodeType { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/informationBasis1/")]
	public class HeadlineInformationItemAreasArea
	{
		public string Name { get; set; }
		public uint Code { get; set; }
	}


	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/", IsNullable = true)]
	public class Body
	{
		public BodyEarthquake Earthquake { get; set; }
		public Intensity Intensity { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	public class BodyEarthquake
	{
		public DateTime OriginTime { get; set; }
		public DateTime ArrivalTime { get; set; }
		public EarthquakeHypocenter Hypocenter { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
		public Magnitude Magnitude { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	public class EarthquakeHypocenter
	{
		public BodyEarthquakeHypocenterArea Area { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/body/seismology1/")]
	public class BodyEarthquakeHypocenterArea
	{
		public string Name { get; set; }
		[XmlElement(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
		public Coordinate Coordinate { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/", IsNullable = false)]
	public class Coordinate
	{
		private readonly static Regex CoordinateRegex = new Regex(@"([+-]\d+(\.\d)?)([+-]\d+(\.\d)?)(-\d+(\.\d)?)?", RegexOptions.Compiled);

		[XmlAttribute("description")]
		public string Description { get; set; }
		[XmlAttribute("datum")]
		public string Datum { get; set; }
		[XmlText]
		public string Value { get; set; }

		public int? GetDepth()
		{
			var match = CoordinateRegex.Match(Value);

			if (int.TryParse(match?.Groups[5]?.Value, out var depth))
				return depth;
			return null;
		}
	}
	[Serializable]
	[XmlType(AnonymousType = true, Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/")]
	[XmlRoot(Namespace = "http://xml.kishou.go.jp/jmaxml1/elementBasis1/", IsNullable = false)]
	public class Magnitude
	{
		[XmlAttribute("type")]
		public string Type { get; set; }
		[XmlAttribute("description")]
		public string Description { get; set; }
		[XmlText]
		public float Value { get; set; }
	}

	[Serializable()]
	[XmlType(AnonymousType = true)]
	public class Intensity
	{
		public IntensityObservation Observation { get; set; }
	}
	[Serializable]
	[XmlType(AnonymousType = true)]
	public class IntensityObservation
	{
		public string MaxInt { get; set; }
	}
#pragma warning restore CA2235 // Mark all non-serializable fields
}
