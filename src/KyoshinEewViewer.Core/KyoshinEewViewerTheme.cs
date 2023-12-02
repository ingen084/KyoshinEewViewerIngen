using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Core;

public enum KyoshinEewViewerThemeMode
{
	Light,
	Dark,
	Blue,
}

// memo FluentThemeを参考に作った
public class KyoshinEewViewerTheme : IStyle, IResourceProvider
{
	private readonly Uri _baseUri;
	private IStyle[]? _loaded;
	private bool _isLoading;

	public KyoshinEewViewerTheme(Uri baseUri)
	{
		_baseUri = baseUri;
	}

	public KyoshinEewViewerTheme(IServiceProvider serviceProvider)
	{
		if (serviceProvider.GetService(typeof(IUriContext)) is not IUriContext context)
			throw new ArgumentException(null, nameof(serviceProvider));
		_baseUri = context.BaseUri;
	}
	public KyoshinEewViewerThemeMode Mode { get; set; }

	public IResourceHost? Owner => (Loaded as IResourceProvider)?.Owner;
	public IStyle Loaded
	{
		get {
			if (_loaded == null)
			{
				_isLoading = true;
				var loaded = (IStyle)AvaloniaXamlLoader.Load(GetUri(), _baseUri);
				_loaded = [loaded];
				_isLoading = false;
			}

			return _loaded?[0]!;
		}
	}

	bool IResourceNode.HasResources => (Loaded as IResourceProvider)?.HasResources ?? false;

	IReadOnlyList<IStyle> IStyle.Children => _loaded ?? Array.Empty<IStyle>();

	public event EventHandler? OwnerChanged
	{
		add {
			if (Loaded is IResourceProvider rp)
				rp.OwnerChanged += value;
		}
		remove {
			if (Loaded is IResourceProvider rp)
				rp.OwnerChanged -= value;
		}
	}

	public bool TryGetResource(object key, ThemeVariant? theme, out object? value)
	{
		if (!_isLoading && Loaded is IResourceProvider p)
			return p.TryGetResource(key, theme, out value);

		value = null;
		return false;
	}

	void IResourceProvider.AddOwner(IResourceHost owner) => (Loaded as IResourceProvider)?.AddOwner(owner);
	void IResourceProvider.RemoveOwner(IResourceHost owner) => (Loaded as IResourceProvider)?.RemoveOwner(owner);

	private Uri GetUri() => Mode switch
	{
		KyoshinEewViewerThemeMode.Dark => new Uri("avares://KyoshinEewViewer.Core/Themes/Dark.axaml", UriKind.Absolute),
		KyoshinEewViewerThemeMode.Blue => new Uri("avares://KyoshinEewViewer.Core/Themes/Blue.axaml", UriKind.Absolute),
		_ => new Uri("avares://KyoshinEewViewer.Core/Themes/Light.axaml", UriKind.Absolute),
	};
}
