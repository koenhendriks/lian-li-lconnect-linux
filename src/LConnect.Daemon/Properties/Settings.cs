using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using LConnectCore;

namespace LConnectService.Properties;

// Replaces the ApplicationSettingsBase user.config with a small JSON file in the data directory.
// Controllers subscribe to PropertyChanged exactly as they did with the original settings class.
internal sealed class Settings : INotifyPropertyChanged
{
	private sealed class Data
	{
		public bool IsMotherboardARGBSync { get; set; }

		public bool IsMotherboardRPMSync { get; set; }
	}

	private static readonly Settings defaultInstance = new Settings();

	private readonly object locker = new object();

	private Data data = new Data();

	public static Settings Default => defaultInstance;

	private static string FilePath => Path.Combine(LConnectApp.Path.AppDataDirectory, "service-settings.json");

	public bool IsMotherboardARGBSync
	{
		get
		{
			return data.IsMotherboardARGBSync;
		}
		set
		{
			if (data.IsMotherboardARGBSync != value)
			{
				data.IsMotherboardARGBSync = value;
				OnPropertyChanged("IsMotherboardARGBSync");
			}
		}
	}

	public bool IsMotherboardRPMSync
	{
		get
		{
			return data.IsMotherboardRPMSync;
		}
		set
		{
			if (data.IsMotherboardRPMSync != value)
			{
				data.IsMotherboardRPMSync = value;
				OnPropertyChanged("IsMotherboardRPMSync");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private Settings()
	{
		Reload();
	}

	public void Reload()
	{
		lock (locker)
		{
			try
			{
				if (File.Exists(FilePath))
				{
					data = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data();
				}
			}
			catch
			{
				data = new Data();
			}
		}
	}

	public void Save()
	{
		lock (locker)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllText(FilePath, JsonSerializer.Serialize(data));
			}
			catch
			{
			}
		}
	}

	private void OnPropertyChanged(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
