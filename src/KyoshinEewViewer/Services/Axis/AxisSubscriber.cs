using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KyoshinEewViewer.Core.Models;
using Splat;

namespace KyoshinEewViewer.Services.Axis;

public class AxisSubscriber
{
	public AxisSubscriber(KyoshinEewViewerConfiguration config)
	{
        SplatRegistrations.RegisterLazySingleton<AxisSubscriber>();


	}
}
