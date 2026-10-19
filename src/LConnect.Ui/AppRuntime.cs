using System;
using System.IO;
using Avalonia.Controls;
using LConnect3cs.Products.TLFan;
using LConnect3cs.Utils;
using LConnectUi.Services;

namespace LConnectUi;

// Non-visual startup, shared by the desktop app and the headless tests.
public static class AppRuntime
{
	private static bool started;

	public static SingleInstance SingleInstance { get; set; }

	public static bool StartHidden { get; set; }

	public static Window MainWindow { get; set; }

	public static string LogDirectory
	{
		get
		{
			if (OperatingSystem.IsWindows())
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lconnect");
			}
			string text = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
			if (string.IsNullOrEmpty(text))
			{
				text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
			}
			return Path.Combine(text, "lconnect");
		}
	}

	public static void Start(bool openLog = true)
	{
		if (started)
		{
			return;
		}
		started = true;
		string text = UiSettings.Current.Locale;
		Loc.SetLocale(string.IsNullOrEmpty(text) ? I18n.DefaultLocaleFromEnvironment() : text);
		if (openLog)
		{
			try
			{
				Directory.CreateDirectory(LogDirectory);
				Logger.Instance.Open(Path.Combine(LogDirectory, "lconnect-ui.log"));
			}
			catch
			{
			}
		}
		// Like the original App.xaml.cs: one TL LCD controller object for all TL LCD fans (they hang off the TL
		// controller; the daemon addresses them per port).
		TLLCDController tLLCDController = new TLLCDController();
		if (!TLLCDStore.Instance.Controllers.ContainsKey(tLLCDController.DevicePath))
		{
			TLLCDStore.Instance.Controllers.Add(tLLCDController.DevicePath, tLLCDController);
		}
		ControllerHub.Instance.Start();
		SensorService.Instance.Start();
	}
}
