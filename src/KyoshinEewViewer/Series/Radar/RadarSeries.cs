using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Radar
{
	public class RadarSeries : SeriesBase
	{
		public RadarSeries() : base("レーダー･ナウキャスト")
		{
		}

		private RadarView? control;
		public override Control DisplayControl => control ?? throw new Exception("初期化前にコントロールが呼ばれています");

		public override void Activating()
		{
			if (control != null)
				return;
			control = new RadarView
			{
				DataContext = this,
			};
			var time = DateTime.UtcNow.AddMinutes(-(DateTime.Now.Minute % 5) - 20);
			ImageTileProviders = new Map.Layers.ImageTile.ImageTileProvider[] { new RadarImageTileProvider(time, time) };
		}
		public override void Deactivated() { }
	}
}
