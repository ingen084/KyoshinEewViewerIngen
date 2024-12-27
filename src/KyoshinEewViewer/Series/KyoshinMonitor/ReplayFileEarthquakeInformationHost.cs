using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class ReplayFileEarthquakeInformationHost(KyoshinEewViewerConfiguration config) : EarthquakeInformationHost(true, config)
{
	public override DateTime CurrentTime => throw new NotImplementedException();
}
