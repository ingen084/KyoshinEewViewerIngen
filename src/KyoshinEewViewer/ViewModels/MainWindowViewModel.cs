using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace KyoshinEewViewer.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		[Reactive]
		public string Title { get; set; } = "KyoshinEewViewer for ingen";

		[Reactive]
		public double Scale { get; set; } = 1;

		public ObservableCollection<SeriesBase> Series { get; } = new ObservableCollection<SeriesBase>();

		[Reactive]
		public Rect MapPadding { get; set; } = BasePadding;
		private static Rect BasePadding { get; } = new(0, 36, 0, 0);
		private IDisposable? MapPaddingListener { get; set; }

		[Reactive]
		public IRenderObject[]? RenderObjects { get; protected set; }
		private IDisposable? RenderObjectsListener { get; set; }
		[Reactive]
		public RealtimeRenderObject[]? RealtimeRenderObjects { get; protected set; }
		private IDisposable? RealtimeRenderObjectsListener { get; set; }

		private IDisposable? FocusPointListener { get; set; }

		private SeriesBase? _selectedSeries;
		public SeriesBase? SelectedSeries
		{
			get => _selectedSeries;
			set
			{
				if (_selectedSeries == value)
					return;
				// デタッチ
				MapPaddingListener?.Dispose();
				MapPaddingListener = null;
				RenderObjectsListener?.Dispose();
				RenderObjectsListener = null;
				RealtimeRenderObjectsListener?.Dispose();
				RealtimeRenderObjectsListener = null;
				FocusPointListener?.Dispose();
				FocusPointListener = null;
				_selectedSeries?.Deactivated();

				value?.Activating();
				this.RaiseAndSetIfChanged(ref _selectedSeries, value);

				// アタッチ
				if (_selectedSeries != null)
				{
					MapPaddingListener = _selectedSeries.WhenAnyValue(x => x.MapPadding).Subscribe(x => MapPadding = new Rect(
						x.X + BasePadding.X,
						x.Y + BasePadding.Y,
						x.Width + BasePadding.Width,
						x.Height + BasePadding.Height
					));
					MapPadding = new Rect(
						_selectedSeries.MapPadding.X + BasePadding.X,
						_selectedSeries.MapPadding.Y + BasePadding.Y,
						_selectedSeries.MapPadding.Width + BasePadding.Width,
						_selectedSeries.MapPadding.Height + BasePadding.Height
					);

					RenderObjectsListener = _selectedSeries.WhenAnyValue(x => x.RenderObjects).Subscribe(x => RenderObjects = x);
					RenderObjects = _selectedSeries.RenderObjects;

					RealtimeRenderObjectsListener = _selectedSeries.WhenAnyValue(x => x.RealtimeRenderObjects).Subscribe(x => RealtimeRenderObjects = x);
					RealtimeRenderObjects = _selectedSeries.RealtimeRenderObjects;

					FocusPointListener = _selectedSeries.WhenAnyValue(x => x.FocusBound).WhereNotNull().Subscribe(x => MessageBus.Current.SendMessage(new MapNavigationRequested(x)));
					MessageBus.Current.SendMessage(new MapNavigationRequested(_selectedSeries.FocusBound));
				}
				DisplayControl = _selectedSeries?.DisplayControl;
			}
		}
		[Reactive]
		public Control? DisplayControl { get; set; }

		public MainWindowViewModel()
		{
			ConfigurationService.Default.WhenAnyValue(x => x.WindowScale)
				.Subscribe(x => Scale = x);

			Series.Add(new KyoshinMonitorSeries());
			Series.Add(new EarthquakeSeries());
		}
	}
}
