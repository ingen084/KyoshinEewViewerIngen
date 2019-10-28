using KyoshinMonitorLib;
using System;
using System.Globalization;
using System.Windows.Data;

namespace KyoshinEewViewer.Converters
{
	public class IntensityToStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is JmaIntensity intensity))
				throw new Exception("IntensityToStringConverter");
			return intensity switch
			{
				JmaIntensity.Int0 => "0",
				JmaIntensity.Int1 => "1",
				JmaIntensity.Int2 => "2",
				JmaIntensity.Int3 => "3",
				JmaIntensity.Int4 => "4",
				JmaIntensity.Int5Lower => "5-",
				JmaIntensity.Int5Upper => "5+",
				JmaIntensity.Int6Lower => "6-",
				JmaIntensity.Int6Upper => "6+",
				JmaIntensity.Int7 => "7",
				_ => "-",
			};
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}