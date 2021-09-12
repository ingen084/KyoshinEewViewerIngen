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

		public event Action<Information[]>? InformationSwitched;
		protected void OnInformationSwitched(Information[] informations)
			=> InformationSwitched?.Invoke(informations);

		public event Action? Stopped;
		protected void OnStopped()
			=> Stopped?.Invoke();

		//public event Action? StateUpdated;
		//private string? _state = null;
		//public string? State
		//{
		//	get => _state;
		//	protected set
		//	{
		//		if (_state == value)
		//			return;
		//		_state = value;
		//		StateUpdated?.Invoke();
		//	}
		//}

		public abstract Task StartAsync(string[] fetchTitles, string[] fetchTypes);
		public abstract Task StopAsync();
	}

	public class Information
	{
		public Information(string key, string title, DateTime arrivalTime, Func<Task<Stream>> getBodyFunc, Action? cleanupFunc)
		{
			Key = key ?? throw new ArgumentNullException(nameof(key));
			Title = title ?? throw new ArgumentNullException(nameof(title));
			ArrivalTime = arrivalTime;
			GetBodyFunc = getBodyFunc ?? throw new ArgumentNullException(nameof(getBodyFunc));
			CleanupFunc = cleanupFunc;
		}

		public string Key { get; }
		public string Title { get; }
		public DateTime ArrivalTime { get; }
		private Func<Task<Stream>> GetBodyFunc { get; }
		private Action? CleanupFunc { get; }
		public Task<Stream> GetBodyAsync() => GetBodyFunc();
		public void Cleanup() => CleanupFunc?.Invoke();
	}
}
