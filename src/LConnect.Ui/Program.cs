using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using LConnectCore;
using LConnectUi.Services;

namespace LConnectUi;

internal static class Program
{
	private const string Usage = @"lconnect-ui - desktop GUI for the L-Connect 3 Linux port

Usage: lconnect-ui [--background] [--data-dir DIR] [--socket PATH]

  --background       start hidden in the system tray (for autostart)
  --data-dir DIR     data directory (default /var/lib/lconnect, or $LCONNECT_DATA_DIR)
  --socket PATH      lconnectd socket (default /run/lconnect/lconnect.sock, or $LCONNECT_SOCKET)
";

	[STAThread]
	public static int Main(string[] args)
	{
		List<string> list = args.ToList();
		if (list.Contains("--help") || list.Contains("-h"))
		{
			Console.Write(Usage);
			return 0;
		}
		string text = takeOption(list, "--data-dir");
		if (text != null)
		{
			Environment.SetEnvironmentVariable("LCONNECT_DATA_DIR", Path.GetFullPath(text));
		}
		string text2 = takeOption(list, "--socket");
		if (text2 != null)
		{
			Environment.SetEnvironmentVariable("LCONNECT_SOCKET", text2);
		}
		LConnectApp.ReloadPaths();
		Native.SetGroupWritableUmask();
		SingleInstance singleInstance = new SingleInstance();
		if (!singleInstance.TryAcquire())
		{
			singleInstance.SignalRunningInstance();
			Console.Error.WriteLine("lconnect-ui is already running; asked it to show its window.");
			return 0;
		}
		try
		{
			AppRuntime.SingleInstance = singleInstance;
			AppRuntime.StartHidden = list.Contains("--background");
			return BuildAvaloniaApp().StartWithClassicDesktopLifetime(list.ToArray(), ShutdownMode.OnExplicitShutdown);
		}
		finally
		{
			singleInstance.Dispose();
		}
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont()
			.LogToTrace();
	}

	private static string takeOption(List<string> args, string name)
	{
		int num = args.IndexOf(name);
		if (num < 0 || num + 1 >= args.Count)
		{
			return null;
		}
		string result = args[num + 1];
		args.RemoveRange(num, 2);
		return result;
	}
}
