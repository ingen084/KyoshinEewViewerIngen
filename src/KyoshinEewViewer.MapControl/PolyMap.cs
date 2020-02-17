using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KyoshinEewViewer.MapControl
{
	public class PolyMap : FrameworkElement
	{
		static PolyMap()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PolyMap), new FrameworkPropertyMetadata(typeof(PolyMap)));
		}
	}
}
