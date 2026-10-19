using System;
using System.IO;

namespace LConnectUi.Services;

// One UI per user session: an exclusively opened lock file in $XDG_RUNTIME_DIR (or the temp directory).
// A second launch drops a ".show" marker next to it, which the running instance watches to bring its window up.
public sealed class SingleInstance : IDisposable
{
	private FileStream lockStream;

	private FileSystemWatcher watcher;

	public string LockPath { get; }

	public string ShowRequestPath => LockPath + ".show";

	public event EventHandler ShowRequested;

	public SingleInstance(string lockPath = null)
	{
		LockPath = lockPath ?? DefaultLockPath();
	}

	public static string DefaultLockPath()
	{
		string text = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
		if (string.IsNullOrEmpty(text) || !Directory.Exists(text))
		{
			text = Path.GetTempPath();
		}
		return Path.Combine(text, "lconnect-ui-" + Environment.UserName + ".lock");
	}

	public bool TryAcquire()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(LockPath));
			lockStream = new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
		try
		{
			if (File.Exists(ShowRequestPath))
			{
				File.Delete(ShowRequestPath);
			}
			watcher = new FileSystemWatcher(Path.GetDirectoryName(LockPath), Path.GetFileName(ShowRequestPath))
			{
				NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
			};
			watcher.Created += onShowRequest;
			watcher.Changed += onShowRequest;
			watcher.EnableRaisingEvents = true;
		}
		catch
		{
		}
		return true;
	}

	// Called by a second instance: ask the running one to show its window.
	public void SignalRunningInstance()
	{
		try
		{
			File.WriteAllText(ShowRequestPath, DateTime.Now.ToString("O"));
		}
		catch
		{
		}
	}

	private void onShowRequest(object sender, FileSystemEventArgs e)
	{
		try
		{
			File.Delete(ShowRequestPath);
		}
		catch
		{
		}
		ShowRequested?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		watcher?.Dispose();
		lockStream?.Dispose();
		lockStream = null;
	}
}
