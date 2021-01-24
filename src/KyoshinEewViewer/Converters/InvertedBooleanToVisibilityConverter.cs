using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KyoshinEewViewer.Converters
{
	public class InvertedBooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not bool booleanValue)
				throw new NotSupportedException("boolにしか対応してません。");
			if (booleanValue)
				return Visibility.Collapsed;
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
