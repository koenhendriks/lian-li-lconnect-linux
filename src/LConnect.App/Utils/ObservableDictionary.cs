using System;
using System.Collections.Generic;

namespace LConnect3cs.Utils;

public class ObservableDictionary<TKey, TValue> : Dictionary<TKey, TValue>
{
	public class DictionaryCountChangedEventArgs : EventArgs
	{
		public TKey Key { get; private set; }

		public int Count { get; private set; }

		public bool IsEmpty => Count == 0;

		public DictionaryCountChangedEventArgs(TKey key, int count)
		{
			Key = key;
			Count = count;
		}
	}

	public delegate void OnDictionaryCountChangedEventHandler(object sender, DictionaryCountChangedEventArgs e);

	public delegate void EventHandler();

	public delegate void OnChangedEventHandler(TKey key);

	public new TValue this[TKey key]
	{
		get
		{
			return base[key];
		}
		set
		{
			base[key] = value;
			OnSet?.Invoke(key);
		}
	}

	public event OnDictionaryCountChangedEventHandler OnDictionaryCountChanged;

	public event EventHandler OnClear;

	public event OnChangedEventHandler OnSet;

	public event OnChangedEventHandler OnAdded;

	public event OnChangedEventHandler OnRemoved;

	public new void Remove(TKey key)
	{
		base.Remove(key);
		OnRemoved?.Invoke(key);
		OnDictionaryCountChanged?.Invoke(this, new DictionaryCountChangedEventArgs(key, Count));
	}

	public new void Add(TKey key, TValue value)
	{
		base.Add(key, value);
		OnAdded?.Invoke(key);
		OnDictionaryCountChanged?.Invoke(this, new DictionaryCountChangedEventArgs(key, Count));
	}

	public new void Clear()
	{
		base.Clear();
		OnClear?.Invoke();
		OnDictionaryCountChanged?.Invoke(this, new DictionaryCountChangedEventArgs(default, Count));
	}
}
