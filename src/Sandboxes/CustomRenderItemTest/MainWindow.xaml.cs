using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace CustomRenderItemTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		Typeface face;
		public MainWindow()
		{
			InitializeComponent();

			face = new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#MotoyaLMaru W3 mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			//var offset = 10;
			//drawingContext.DrawRectangle(Brushes.Gray, null, new Rect(new Point(offset, 10), new Size(100, 100)));
			//drawingContext.DrawText(new FormattedText("0", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 100, Brushes.White, 1), new Point(offset + 50 - 25, 10));

			//offset = 10 + 110;
			//drawingContext.DrawRectangle(Brushes.Gray, null, new Rect(new Point(offset, 10), new Size(100, 100)));
			//drawingContext.DrawText(new FormattedText("5", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 100, Brushes.White, 1), new Point(offset + 13, 10));
			//drawingContext.DrawText(new FormattedText("+", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 75, Brushes.White, 1), new Point(offset + 53, 10));

			//offset = 10 + 110 * 2;
			//drawingContext.DrawRectangle(Brushes.Gray, null, new Rect(new Point(offset, 10), new Size(100, 100)));
			//drawingContext.DrawText(new FormattedText("6", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 100, Brushes.White, 1), new Point(offset + 13, 10));
			//drawingContext.DrawText(new FormattedText("-", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 75, Brushes.White, 1), new Point(offset + 53, 10));

			//offset = 10 + 110 * 3;
			//drawingContext.DrawEllipse(Brushes.Gray, null, new Point(offset + 100 / 2, 10 + 100 / 2), 100 / 2, 100 / 2);
			//drawingContext.DrawText(new FormattedText("4", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 100, Brushes.White, 1), new Point(offset + 25, 10));

			//offset = 10 + 110 * 4;
			//drawingContext.DrawEllipse(Brushes.Gray, null, new Point(offset + 100 / 2, 10 + 100 / 2), 100 / 2, 100 / 2);
			//drawingContext.DrawText(new FormattedText("5", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 100, Brushes.White, 1), new Point(offset + 13, 10));
			//drawingContext.DrawText(new FormattedText("+", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 75, Brushes.White, 1), new Point(offset + 53, 10));
		}
	}
}
