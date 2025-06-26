using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Series.Earthquake.Models;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake;

public partial class EarthquakeTimetableWindow : Window
{
	private int _currentIndex = 0;

	private EarthquakeTimetable[]? _timetables;
	public EarthquakeTimetable[]? Timetables
	{
		get => _timetables;
		set
		{
			_timetables = value;
			if (_timetables != null && _timetables.Any())
				DataContext = _timetables[_currentIndex = _timetables.Length - 1];
		}
	}

	public EarthquakeTimetableWindow()
    {
        InitializeComponent();

		previousButton.Click += (_, _) => {
			if (_timetables == null || _timetables.Length == 0)
				return;
			if (_currentIndex > 0)
			{
				DataContext = _timetables[--_currentIndex];
			}
		};
		nextButton.Click += (_, _) => {
			if (_timetables == null || _timetables.Length == 0)
				return;
			if (_currentIndex < _timetables.Length - 1)
			{
				DataContext = _timetables[++_currentIndex];
			}
		};
	}
}

public record EarthquakeTimetable(DateTime Date, EarthquakeTimetableEntry[] Entries);
public record EarthquakeTimetableEntry(DateTime Time, EarthquakeEvent[] SortedEvents);
