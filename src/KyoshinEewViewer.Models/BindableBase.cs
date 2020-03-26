using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace KyoshinEewViewer.Models
{
	//like Prism BindableBase
	public class BindableBase : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
			storage = value;
			RaisePropertyChanged(propertyName);
			return true;
		}

		protected void RaisePropertyChanged([CallerMemberName]string propertyName = null)
			=> OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		private void OnPropertyChanged(PropertyChangedEventArgs args)
			=> PropertyChanged?.Invoke(this, args);
	}
}
