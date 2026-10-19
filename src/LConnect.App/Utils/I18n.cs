using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace LConnect3cs.Utils;

// Replaces WPF resource lookups (Application.Current.TryFindResource("I18n....")). The original locale
// ResourceDictionaries are embedded as-is and parsed into string tables.
internal static class I18n
{
	private static readonly object locker = new object();

	private static Dictionary<string, string> current;

	private static Dictionary<string, string> fallback;

	public static string Locale { get; private set; } = "en-us";

	public static IReadOnlyList<string> AvailableLocales { get; } = new string[6] { "en-us", "de-de", "es-es", "fr-fr", "zh-cn", "zh-tw" };

	public static void SetLocale(string locale)
	{
		lock (locker)
		{
			locale = (locale ?? "en-us").ToLowerInvariant().Replace('_', '-');
			if (!AvailableLocales.Contains(locale))
			{
				locale = AvailableLocales.FirstOrDefault((string l) => l.StartsWith(locale.Split('-')[0], StringComparison.Ordinal)) ?? "en-us";
			}
			Locale = locale;
			current = load(locale);
		}
	}

	public static string DefaultLocaleFromEnvironment()
	{
		string text = Environment.GetEnvironmentVariable("LC_ALL") ?? Environment.GetEnvironmentVariable("LC_MESSAGES") ?? Environment.GetEnvironmentVariable("LANG") ?? CultureInfo.CurrentUICulture.Name;
		return text.Split('.')[0];
	}

	private static Dictionary<string, string> load(string locale)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string text = executingAssembly.GetManifestResourceNames().FirstOrDefault((string n) => n.EndsWith("." + locale + ".xaml", StringComparison.OrdinalIgnoreCase));
		if (text == null)
		{
			return dictionary;
		}
		using Stream stream = executingAssembly.GetManifestResourceStream(text);
		XDocument xDocument = XDocument.Load(stream);
		XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
		foreach (XElement item in xDocument.Root.Elements())
		{
			string text2 = (string)item.Attribute(xNamespace + "Key");
			if (text2 != null && !item.HasElements)
			{
				dictionary[text2] = item.Value;
			}
		}
		return dictionary;
	}

	public static string TryGet(string key)
	{
		lock (locker)
		{
			if (current == null)
			{
				SetLocale(DefaultLocaleFromEnvironment());
			}
			if (fallback == null)
			{
				fallback = load("en-us");
			}
			if (current.TryGetValue(key, out var value) || fallback.TryGetValue(key, out value))
			{
				return value;
			}
			return null;
		}
	}

	public static string Get(string key, string defaultValue = null)
	{
		return TryGet(key) ?? defaultValue ?? key;
	}
}
