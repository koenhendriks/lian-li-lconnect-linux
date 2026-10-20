using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectUi.Services;

namespace LConnect.Ui.Tests;

// Test-wide environment: a private data directory + socket (set before anything reads LConnectApp.Path) and a
// simulated lconnectd (LCONNECT_SIMULATE=1) started from the repository's build output.
//
// LCONNECT_UI_TEST_EXTERNAL=1 uses an already running daemon instead: LCONNECT_DATA_DIR / LCONNECT_SOCKET must
// then point at it.
public static class TestEnvironment
{
	private static readonly object locker = new object();

	private static Task<bool> daemonTask;

	private static Process daemon;

	public static string DataDir { get; private set; }

	public static string SocketPath { get; private set; }

	public static bool External { get; private set; }

	public static string RepoRoot { get; private set; }

	public static string ScreenshotDir { get; private set; }

	public static string SimDir => Path.Combine(DataDir, "sim");

	public static string WindowsInstallDir
	{
		get
		{
			string text = Environment.GetEnvironmentVariable("LCONNECT_UI_TEST_INSTALL_DIR");
			if (string.IsNullOrEmpty(text))
			{
				text = "C:\\Program Files\\Lian-Li\\L-Connect 3";
			}
			return text;
		}
	}

	[ModuleInitializer]
	internal static void Init()
	{
		External = Environment.GetEnvironmentVariable("LCONNECT_UI_TEST_EXTERNAL") == "1";
		if (External)
		{
			DataDir = Environment.GetEnvironmentVariable("LCONNECT_DATA_DIR");
			SocketPath = Environment.GetEnvironmentVariable("LCONNECT_SOCKET");
		}
		else
		{
			DataDir = Path.Combine(Path.GetTempPath(), $"lcui-{Environment.ProcessId}");
			SocketPath = Path.Combine(DataDir, "lconnect.sock");
			Directory.CreateDirectory(DataDir);
			Environment.SetEnvironmentVariable("LCONNECT_DATA_DIR", DataDir);
			Environment.SetEnvironmentVariable("LCONNECT_SOCKET", SocketPath);
		}
		Environment.SetEnvironmentVariable("LCONNECT_UI_CONFIG", Path.Combine(DataDir, "ui-test.json"));
		LConnectApp.ReloadPaths();
		UiSettings.Reload();
		RepoRoot = findRepoRoot();
		ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
		Directory.CreateDirectory(ScreenshotDir);
		AppDomain.CurrentDomain.ProcessExit += delegate
		{
			StopDaemon();
		};
	}

	private static string findRepoRoot()
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory);
		while (directoryInfo != null && !File.Exists(Path.Combine(directoryInfo.FullName, "LConnect.sln")))
		{
			directoryInfo = directoryInfo.Parent;
		}
		return directoryInfo?.FullName;
	}

	public static string DaemonExecutable
	{
		get
		{
			if (RepoRoot == null)
			{
				return null;
			}
			string path = Path.Combine(RepoRoot, "src", "LConnect.Daemon", "bin", "Debug", "net10.0", OperatingSystem.IsWindows() ? "lconnectd.exe" : "lconnectd");
			if (!File.Exists(path))
			{
				return null;
			}
			return path;
		}
	}

	public static Task<bool> EnsureDaemonAsync()
	{
		lock (locker)
		{
			return daemonTask ??= Task.Run(startDaemon);
		}
	}

	private static bool startDaemon()
	{
		if (External)
		{
			return waitReachable(TimeSpan.FromSeconds(20.0));
		}
		string daemonExecutable = DaemonExecutable;
		if (daemonExecutable == null)
		{
			return false;
		}
		if (AssetImporter.LooksLikeInstallDirectory(WindowsInstallDir) && !AssetImporter.HasSensorAssets)
		{
			try
			{
				AssetImporter.Import(WindowsInstallDir);
			}
			catch
			{
			}
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo(daemonExecutable)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			WorkingDirectory = DataDir
		};
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			["LCONNECT_SIMULATE"] = "1",
			["LCONNECT_SIMULATE_SLINF"] = "1",
			["LCONNECT_SIMULATE_TL"] = Environment.GetEnvironmentVariable("LCONNECT_UI_TEST_TL") ?? "0:LED,LED,LED;2:LCD,LCD,LCD",
			["LCONNECT_FAKE_CPU_TEMP"] = "55",
			["LCONNECT_FAKE_GPU_TEMP"] = "48",
			["LCONNECT_DATA_DIR"] = DataDir,
			["LCONNECT_SOCKET"] = SocketPath
		};
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			processStartInfo.Environment[item.Key] = item.Value;
		}
		if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dotnet");
			if (Directory.Exists(text))
			{
				processStartInfo.Environment["DOTNET_ROOT"] = text;
			}
		}
		daemon = Process.Start(processStartInfo);
		StreamWriter log = new StreamWriter(Path.Combine(DataDir, "daemon.log"))
		{
			AutoFlush = true
		};
		daemon.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			lock (log)
			{
				log.WriteLine(e.Data);
			}
		};
		daemon.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			lock (log)
			{
				log.WriteLine(e.Data);
			}
		};
		daemon.BeginOutputReadLine();
		daemon.BeginErrorReadLine();
		return waitReachable(TimeSpan.FromSeconds(30.0));
	}

	private static bool waitReachable(TimeSpan timeout)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < timeout)
		{
			if (DaemonClient.GetStatus(1000) != null)
			{
				return true;
			}
			Thread.Sleep(250);
		}
		return false;
	}

	// Stops the daemon so a test can see the "not reachable" state; the next EnsureDaemonAsync starts it again.
	public static void StopDaemonForRestart()
	{
		lock (locker)
		{
			StopDaemon();
			daemon = null;
			daemonTask = null;
		}
	}

	// Kills the daemon this test run started, including the headless browser it may have spawned.
	public static void StopDaemon()
	{
		try
		{
			if (daemon != null && !daemon.HasExited)
			{
				daemon.Kill(entireProcessTree: true);
				daemon.WaitForExit(5000);
			}
		}
		catch
		{
		}
	}

	// Pumps the Avalonia dispatcher until the condition holds.
	public static async Task<bool> WaitFor(Func<bool> condition, int timeoutMs = 10000)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeoutMs)
		{
			Dispatcher.UIThread.RunJobs();
			if (condition())
			{
				return true;
			}
			await Task.Delay(50);
		}
		Dispatcher.UIThread.RunJobs();
		return condition();
	}

	// Progress trace (xunit only shows test output at the end; this helps when something hangs).
	public static void Trace(string message)
	{
		try
		{
			File.AppendAllText(Path.Combine(DataDir, "progress.log"), $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
		}
		catch
		{
		}
	}

	public static async Task Pump(int ms)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < ms)
		{
			Dispatcher.UIThread.RunJobs();
			await Task.Delay(25);
		}
		Dispatcher.UIThread.RunJobs();
	}
}
