using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using LConnect3cs.Utils;

namespace LConnectUi.Services;

// String lookups through the original L-Connect locale dictionaries (LConnect3cs.Utils.I18n), with an English
// fallback for strings the original UI did not have. Bound strings update live when the language changes.
public static class Loc
{
	private static readonly List<WeakReference<LocEntry>> entries = new List<WeakReference<LocEntry>>();

	public static event EventHandler LocaleChanged;

	public static string Locale => I18n.Locale;

	public static string Get(string key, string fallback = null)
	{
		if (string.IsNullOrEmpty(key))
		{
			return fallback ?? string.Empty;
		}
		return I18n.TryGet(key) ?? fallback ?? key;
	}

	public static void SetLocale(string locale)
	{
		I18n.SetLocale(locale);
		List<LocEntry> list = new List<LocEntry>();
		lock (entries)
		{
			entries.RemoveAll((WeakReference<LocEntry> w) => !w.TryGetTarget(out _));
			foreach (WeakReference<LocEntry> entry in entries)
			{
				if (entry.TryGetTarget(out var target))
				{
					list.Add(target);
				}
			}
		}
		foreach (LocEntry item in list)
		{
			item.Raise();
		}
		LocaleChanged?.Invoke(null, EventArgs.Empty);
	}

	internal static LocEntry Register(LocEntry entry)
	{
		lock (entries)
		{
			entries.Add(new WeakReference<LocEntry>(entry));
		}
		return entry;
	}
}

public sealed class LocEntry : INotifyPropertyChanged
{
	private readonly string key;

	private readonly string fallback;

	public string Value => Loc.Get(key, fallback);

	public event PropertyChangedEventHandler PropertyChanged;

	public LocEntry(string key, string fallback)
	{
		this.key = key;
		this.fallback = fallback;
	}

	internal void Raise()
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
	}
}

// {svc:Tr I18n.Text.Apply} or {svc:Tr I18n.Text.Apply, 'Apply'} or {svc:Tr Default='Only English'}.
public sealed class TrExtension : MarkupExtension
{
	public string Key { get; set; }

	public string Default { get; set; }

	public TrExtension()
	{
	}

	public TrExtension(string key)
	{
		Key = key;
	}

	public TrExtension(string key, string @default)
	{
		Key = key;
		Default = @default;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return new Binding("Value")
		{
			Source = Loc.Register(new LocEntry(Key, Default)),
			Mode = BindingMode.OneWay
		};
	}
}
