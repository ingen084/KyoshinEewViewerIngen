using System;
using System.Collections;
using System.Collections.Generic;

namespace KyoshinEewViewer.JmaXmlParser;

/// <summary>
/// 同名の要素の一覧。既定値は空の一覧として振る舞う
/// </summary>
public readonly struct JmaXmlList<T> : IReadOnlyList<T>
{
	private readonly List<T>? _items;

	public JmaXmlList(List<T>? items)
	{
		_items = items;
	}

	public int Count => _items?.Count ?? 0;

	public T this[int index] => _items is { } items ? items[index] : throw new ArgumentOutOfRangeException(nameof(index));

	public Enumerator GetEnumerator() => new(_items);

	IEnumerator<T> IEnumerable<T>.GetEnumerator() => (_items ?? []).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => (_items ?? []).GetEnumerator();

	public struct Enumerator
	{
		private readonly List<T>? _items;
		private int _index;

		internal Enumerator(List<T>? items)
		{
			_items = items;
			_index = -1;
		}

		public readonly T Current => _items![_index];

		public bool MoveNext() => _items != null && ++_index < _items.Count;
	}
}
