using KyoshinMonitorLib;
using KyoshinMonitorLib.ApiResult.AppApi;
using System;
using System.Globalization;
using System.Windows.Data;

namespace KyoshinEewViewer.Converters
{
	public class ObservationPointToStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is LinkedObservationPoint point))
				throw new Exception("PrefectureToStringConverter");
			if (point.Point == null)
			{
				if (point.Site == null)
					return "不明";
				else
					return point.Site.Prefefecture.GetLongName();
			}
			if (point.Point.Region.Contains(" "))
				return point.Point.Region[..point.Point.Region.IndexOf(' ')];
			return $"{point.Point.Region}";//{point.Point.Name}";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}