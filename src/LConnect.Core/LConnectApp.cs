using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace LConnectCore;

public static class LConnectApp
{
	public struct PathCollection
	{
		// Root for everything the original kept next to the executable (appdata, repo, assets).
		public string DataRoot { get; private set; }

		public string AppDataDirectory { get; private set; }

		public string DeviceSettingDirectory { get; private set; }

		public string ControllerProfileDirectory { get; private set; }

		public string UploadedDirectory { get; private set; }

		// Converted LCD media library: repo/{jpg,gif,mp4,animation}/{1,2,3}/{h,v}.
		public string RepoDirectory { get; private set; }

		// Vendor web assets imported from the user's L-Connect install (assets/tl-sensor).
		public string AssetsDirectory { get; private set; }

		public string ServiceSocketPath { get; private set; }

		public string ServiceHttpListenerPortFilePath { get; private set; }

		public PathCollection()
		{
			string environmentVariable = Environment.GetEnvironmentVariable("LCONNECT_DATA_DIR");
			if (string.IsNullOrEmpty(environmentVariable))
			{
				environmentVariable = (OperatingSystem.IsLinux() ? "/var/lib/lconnect" : System.IO.Path.Combine(AppContext.BaseDirectory, "data"));
			}
			DataRoot = environmentVariable;
			AppDataDirectory = DataRoot;
			DeviceSettingDirectory = System.IO.Path.Combine(AppDataDirectory, "device");
			ControllerProfileDirectory = System.IO.Path.Combine(AppDataDirectory, "profile");
			UploadedDirectory = System.IO.Path.Combine(AppDataDirectory, "upload");
			RepoDirectory = System.IO.Path.Combine(DataRoot, "repo");
			AssetsDirectory = System.IO.Path.Combine(DataRoot, "assets");
			string environmentVariable2 = Environment.GetEnvironmentVariable("LCONNECT_SOCKET");
			if (string.IsNullOrEmpty(environmentVariable2))
			{
				environmentVariable2 = (OperatingSystem.IsLinux() ? "/run/lconnect/lconnect.sock" : System.IO.Path.Combine(DataRoot, "lconnect.sock"));
			}
			ServiceSocketPath = environmentVariable2;
			ServiceHttpListenerPortFilePath = System.IO.Path.Combine(AppDataDirectory, "0");
		}
	}

	public const string CompanyName = "Lian-Li";

	public const string AppName = "L-Connect 3";

	public static PathCollection Path { get; private set; } = new PathCollection();

	// Re-read environment variables (used by tests and the CLI's --data-dir option).
	public static void ReloadPaths()
	{
		Path = new PathCollection();
	}

	public static int GetUnusedPort()
	{
		try
		{
			TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
			tcpListener.Start();
			int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
			tcpListener.Stop();
			return port;
		}
		catch
		{
		}
		return 0;
	}
}
