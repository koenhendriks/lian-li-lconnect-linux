using System;
using System.IO;
using System.Text.Json;

namespace LConnectUi.Services;

// Per-user UI preferences (~/.config/lconnect/ui.json, %APPDATA%\lconnect\ui.json on Windows).
// Device settings live in the daemon's data directory; only presentation choices are kept here.
public sealed class UiSettings
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static UiSettings current;

	public string Locale { get; set; }

	public bool MotherboardARGBSync { get; set; }

	public bool MotherboardRPMSync { get; set; }

	public bool CloseToTray { get; set; } = true;

	public static UiSettings Current => current ??= Load();

	public static string FilePath
	{
		get
		{
			string text = Environment.GetEnvironmentVariable("LCONNECT_UI_CONFIG");
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return Path.Combine(ConfigDirectory, "ui.json");
		}
	}

	public static string ConfigDirectory
	{
		get
		{
			if (OperatingSystem.IsWindows())
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lconnect");
			}
			string text = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
			if (string.IsNullOrEmpty(text))
			{
				text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
			}
			return Path.Combine(text, "lconnect");
		}
	}

	public static UiSettings Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(FilePath)) ?? new UiSettings();
			}
		}
		catch
		{
		}
		return new UiSettings();
	}

	public static void Reload()
	{
		current = Load();
	}

	public bool Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			string text = FilePath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(this, Options));
			File.Move(text, FilePath, overwrite: true);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
