using System;
using LConnectService.Enums;

namespace LConnectService.Helpers;

// Replaces the Windows Event Log writer. Output goes to stderr, which systemd forwards to the journal;
// the "<N>" prefixes are sd-daemon priority levels so journalctl shows the right severity.
internal class EventLogHelper
{
	private static readonly object locker = new object();

	public static bool Verbose { get; set; } = Environment.GetEnvironmentVariable("LCONNECT_VERBOSE") == "1";

	private EventLogHelper()
	{
	}

	public static bool Init(string source, string logName)
	{
		return true;
	}

	public static void Dispose()
	{
	}

	private static void write(int priority, string level, string message, EventIDs eventId)
	{
		lock (locker)
		{
			string text = ((eventId == EventIDs.None) ? string.Empty : $" [{eventId}]");
			foreach (string item in (message ?? string.Empty).TrimEnd().Split('\n'))
			{
				Console.Error.WriteLine($"<{priority}>{level}{text} {item.TrimEnd('\r')}");
			}
		}
	}

	public static void Info(string message, EventIDs eventId = EventIDs.None)
	{
		write(6, "info", message, eventId);
	}

	public static void Warn(string message, EventIDs eventId = EventIDs.None)
	{
		write(4, "warn", message, eventId);
	}

	public static void Error(string message, EventIDs eventId = EventIDs.None)
	{
		write(3, "error", message, eventId);
	}

	public static void Debug(string message)
	{
		if (Verbose)
		{
			write(7, "debug", message, EventIDs.None);
		}
	}
}
