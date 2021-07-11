using System;
using System.IO;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.InformationProviders
{
	public abstract class InformationProvider
	{
		public event Action<Information>? InformationArrived;
		protected void OnInformationArrived(Information information)
			=> InformationArrived?.Invoke(information);

		public event Action? StateUpdated;
		private string? _state = null;
		public string? State
		{
			get => _state;
			protected set
			{
				if (_state == value)
					return;
				_state = value;
				StateUpdated?.Invoke();
			}
		}

		public abstract Task<Information[]> StartAndPullInformationsAsync(string[] fetchTitles, string[] fetchKeys);
		public abstract Task StopAsync();
	}

	public class Information
	{
		public Information(string title, DateTime arrivalTime, Func<Task<(string, Stream)>> getBodyFunc)
		{
			Title = title ?? throw new ArgumentNullException(nameof(title));
			ArrivalTime = arrivalTime;
			GetBodyFunc = getBodyFunc ?? throw new ArgumentNullException(nameof(getBodyFunc));
		}

		public string Title { get; }
		public DateTime ArrivalTime { get; }
		private Func<Task<(string, Stream)>> GetBodyFunc { get; }
		public Task<(string, Stream)> GetBodyAsync() => GetBodyFunc();
	}
}
