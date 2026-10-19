using System;
using System.Runtime.InteropServices;

namespace LConnectUi.Services;

internal static class Native
{
	[DllImport("libc", EntryPoint = "umask")]
	private static extern uint umask(uint mask);

	// Files the UI writes into the shared data directory (profiles, converted LCD media) must stay writable for
	// the daemon's 'lconnect' group, so drop "other" permissions only.
	public static void SetGroupWritableUmask()
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}
		try
		{
			umask(7u);
		}
		catch
		{
		}
	}
}
