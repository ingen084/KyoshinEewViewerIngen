using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace KyoshinEewViewer
{
	//https://stackoverflow.com/questions/20564862
	public class ResourceBinding : MarkupExtension
	{
		#region Helper properties

		public static object GetResourceBindingKeyHelper(DependencyObject obj)
			=> obj.GetValue(ResourceBindingKeyHelperProperty);

		public static void SetResourceBindingKeyHelper(DependencyObject obj, object value)
			=> obj.SetValue(ResourceBindingKeyHelperProperty, value);

		// Using a DependencyProperty as the backing store for ResourceBindingKeyHelper.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ResourceBindingKeyHelperProperty =
			DependencyProperty.RegisterAttached("ResourceBindingKeyHelper", typeof(object), typeof(ResourceBinding), new PropertyMetadata(null, ResourceKeyChanged));

		private static void ResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (!(d is FrameworkElement target)
			 || !(e.NewValue is Tuple<object, DependencyProperty> newVal))
				return;

			var dp = newVal.Item2;

			if (newVal.Item1 == null)
			{
				target.SetValue(dp, dp.GetMetadata(target).DefaultValue);
				return;
			}

			target.SetResourceReference(dp, newVal.Item1);
		}

		#endregion Helper properties

		public ResourceBinding()
		{
		}

		public ResourceBinding(string path)
		{
			Path = new PropertyPath(path);
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (!(serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTargetService))
				return null;

			if (provideValueTargetService.TargetObject != null &&
				provideValueTargetService.TargetObject.GetType().FullName == "System.Windows.SharedDp")
				return this;

			if (!(provideValueTargetService.TargetObject is FrameworkElement targetObject)
			 || !(provideValueTargetService.TargetProperty is DependencyProperty targetProperty))
				return null;

			var binding = new Binding();

			#region binding

			binding.Path = Path;
			binding.XPath = XPath;
			binding.Mode = Mode;
			binding.UpdateSourceTrigger = UpdateSourceTrigger;
			binding.StringFormat = StringFormat;
			binding.Converter = Converter;
			binding.ConverterParameter = ConverterParameter;
			binding.ConverterCulture = ConverterCulture;

			if (RelativeSource != null)
				binding.RelativeSource = RelativeSource;

			if (ElementName != null)
				binding.ElementName = ElementName;

			if (Source != null)
				binding.Source = Source;

			binding.FallbackValue = FallbackValue;

			#endregion binding

			var multiBinding = new MultiBinding
			{
				Converter = HelperConverter.Current,
				ConverterParameter = (this, targetProperty)
			};

			multiBinding.Bindings.Add(binding);
			multiBinding.StringFormat = StringFormat;

			multiBinding.NotifyOnSourceUpdated = true;

			targetObject.SetBinding(ResourceBindingKeyHelperProperty, multiBinding);

			return null;
		}

		#region Binding Members

		/// <summary> The source path (for CLR bindings).</summary>
		public object Source { get; set; }
		/// <summary> The source path (for CLR bindings).</summary>
		public PropertyPath Path { get; set; }
		/// <summary> The XPath path (for XML bindings).</summary>
		[DefaultValue(null)]
		public string XPath { get; set; }
		/// <summary> Binding mode </summary>
		[DefaultValue(BindingMode.Default)]
		public BindingMode Mode { get; set; }
		/// <summary> Update type </summary>
		[DefaultValue(UpdateSourceTrigger.Default)]
		public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
		[DefaultValue(null)]
		public string StringFormat { get; set; }
		/// <summary> The Converter to apply </summary>
		[DefaultValue(null)]
		public IValueConverter Converter { get; set; }
		/// <summary>
		/// The parameter to pass to converter.
		/// </summary>
		/// <value></value>
		[DefaultValue(null)]
		public object ConverterParameter { get; set; }
		/// <summary> Culture in which to evaluate the converter </summary>
		[DefaultValue(null)]
		[TypeConverter(typeof(CultureInfoIetfLanguageTagConverter))]
		public CultureInfo ConverterCulture { get; set; }
		/// <summary>
		/// Description of the object to use as the source, relative to the target element.
		/// </summary>
		[DefaultValue(null)]
		public RelativeSource RelativeSource { get; set; }
		/// <summary> Name of the element to use as the source </summary>
		[DefaultValue(null)]
		public string ElementName { get; set; }

		#endregion Binding Members

		#region BindingBase Members

		/// <summary> Value to use when source cannot provide a value </summary>
		/// <remarks>
		///     Initialized to DependencyProperty.UnsetValue; if FallbackValue is not set, BindingExpression
		///     will return target property's default when Binding cannot get a real value.
		/// </remarks>
		public object FallbackValue
		{
			get;
			set;
		}

		#endregion BindingBase Members

		#region Nested types

		private class HelperConverter : IMultiValueConverter
		{
			public static readonly HelperConverter Current = new HelperConverter();

			public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			{
				var value = values[0];

				(ResourceBinding binding, DependencyProperty dp) = ((ResourceBinding, DependencyProperty))parameter;
				if (!string.IsNullOrWhiteSpace(binding.StringFormat))
					value = string.Format(binding.StringFormat, value);

				return Tuple.Create(value, dp);
			}

			public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
				=> throw new NotImplementedException();
		}

		#endregion Nested types
	}
}
