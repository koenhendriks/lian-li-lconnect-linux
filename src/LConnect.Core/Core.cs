using System;
using FFMpegCore;
using LConnectCore.Native;

namespace LConnectCore;

public static class Core
{
	private static ILogger logger;

	public static bool Init()
	{
		return Init(null);
	}

	public static bool Init(ILogger logger)
	{
		// The original pointed FFmpeg.AutoGen and FFMpegCore at the bundled x64 DLLs. We use the system ffmpeg
		// binaries (or LCONNECT_FFMPEG_DIR) through FFMpegCore only.
		string environmentVariable = Environment.GetEnvironmentVariable("LCONNECT_FFMPEG_DIR");
		GlobalFFOptions.Configure(new FFOptions
		{
			BinaryFolder = (string.IsNullOrEmpty(environmentVariable) ? string.Empty : environmentVariable),
			Encoding = System.Text.Encoding.UTF8
		});
		Core.logger = logger;
		if (Simulation.HidSimulator.Enabled)
		{
			return true;
		}
		try
		{
			HidApi.Init();
		}
		catch (DllNotFoundException ex)
		{
			throw new DllNotFoundException("hidapi library not found (install the 'hidapi' package, or set LCONNECT_HIDAPI). " + ex.Message, ex);
		}
		return true;
	}

	public static void Dispose()
	{
		if (!Simulation.HidSimulator.Enabled)
		{
			HidApi.Exit();
		}
	}
}
