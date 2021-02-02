using KyoshinEewViewer.Models;
using KyoshinEewViewer.Models.Events;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class PullEarthquakeInfoService
	{
		public List<Earthquake> Earthquakes => DmdataService.Status switch {
			DmdataStatus.Failed => JmaXmlPullReceiveService?.Earthquakes,
			DmdataStatus.Stopping => JmaXmlPullReceiveService?.Earthquakes,
			DmdataStatus.StoppingForInvalidKey => JmaXmlPullReceiveService?.Earthquakes,
			_ => DmdataService.Earthquakes,
		};
		private LoggerService Logger { get; }

		public DmdataService DmdataService { get; }
		private JmaXmlPullReceiveService JmaXmlPullReceiveService { get; }

		public PullEarthquakeInfoService(DmdataService dmdataService, JmaXmlPullReceiveService jmaXmlPullReceiveService, LoggerService logger, IEventAggregator eventAggregator)
		{
			DmdataService = dmdataService;
			JmaXmlPullReceiveService = jmaXmlPullReceiveService;
			Logger = logger ?? throw new ArgumentNullException(nameof(logger));

			eventAggregator.GetEvent<DmdataStatusUpdated>().Subscribe(async () => 
			{
				if (DmdataService.Available)
				{
					JmaXmlPullReceiveService.Disable();
					return;
				}
				await JmaXmlPullReceiveService.EnableAsync();
			});
		}

		public Task InitalizeAsync()
			=> DmdataService.InitalizeAsync();
	}
}