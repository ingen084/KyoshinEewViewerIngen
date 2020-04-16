using KyoshinEewViewer.CustomControls;
using KyoshinMonitorLib;
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
			return point.GetRegionName();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}