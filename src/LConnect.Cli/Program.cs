using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using LConnect3cs.Helpers;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;

namespace LConnectCli;

internal static class Program
{
	private const string Usage = @"lconnectctl - control the L-Connect 3 Linux port

Daemon:
  status                         Show devices and sensors known to lconnectd
  reapply                        Make lconnectd re-read and apply all saved settings
  resume                         Re-initialise devices (what the sleep hook does after resume)

Local (no daemon needed):
  devices                        List supported Lian Li HID interfaces and their settings keys
  probe                          Talk to the hardware directly (stop lconnectd first): firmware, fans, LCDs
  import --from <appdata> [--install <dir>] [--dry-run]
                                 Import settings from Windows L-Connect 3 (its 'appdata' folder)
  assets import --from <install dir>
                                 Copy LCD sensor screens, built-in animations and media from a Windows install

Lighting / fans / LCD (through lconnectd):
  tl lighting --port N [--group G] --mode MODE [--speed 0-100] [--brightness 0-100]
              [--direction DIR] [--colors RRGGBB,...]
  tl fan --port N [--group G] --mode Quiet|StandardSpeed|HighSpeed|FullSpeed|FixRPM [--rpm N] [--source CPU|GPU]
  tl modes                       List lighting modes and directions
  lcd show --port N [--index I] (--image F | --video F | --gif F | --animation NAME | --sensor TYPE [--style 0-4]
           [--text RRGGBB] [--bg RRGGBB] | --none) [--rotate 0|90|180|270]
  lcd animations                 List built-in LCD animations
  lcd status --port N            Show what each LCD on a port is configured to show

Options:
  --data-dir DIR                 Data directory (default /var/lib/lconnect, or $LCONNECT_DATA_DIR)
  --socket PATH                  Daemon socket (default /run/lconnect/lconnect.sock, or $LCONNECT_SOCKET)
";

	[DllImport("libc", EntryPoint = "umask")]
	private static extern uint umask(uint mask);

	private static int Main(string[] args)
	{
		List<string> list = args.ToList();
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
		if (!OperatingSystem.IsWindows())
		{
			// Files we create in the shared data directory must stay writable for the daemon's group.
			try
			{
				umask(7u);
			}
			catch
			{
			}
		}
		if (list.Count == 0 || list[0] == "-h" || list[0] == "--help" || list[0] == "help")
		{
			Console.Write(Usage);
			return 0;
		}
		try
		{
			string text3 = list[0];
			list.RemoveAt(0);
			switch (text3)
			{
			case "status":
				return Commands.Status();
			case "reapply":
				return Commands.Post("reapply");
			case "resume":
				return Commands.Post("resume");
			case "devices":
				return Commands.Devices();
			case "probe":
				return Commands.Probe();
			case "import":
				return Commands.Import(list);
			case "assets":
				return Commands.Assets(list);
			case "tl":
				return TLCommands.Run(list);
			case "lcd":
				return LcdCommands.Run(list);
			default:
				Console.Error.WriteLine("Unknown command '" + text3 + "'. Run 'lconnectctl --help'.");
				return 2;
			}
		}
		catch (UsageException ex)
		{
			Console.Error.WriteLine("error: " + ex.Message);
			return 2;
		}
		catch (HttpRequestException ex2)
		{
			Console.Error.WriteLine("error: cannot reach lconnectd at " + LConnectApp.Path.ServiceSocketPath + " (" + ex2.Message + "). Is the service running and are you in the 'lconnect' group?");
			return 1;
		}
		catch (Exception ex3)
		{
			Console.Error.WriteLine("error: " + ex3.Message);
			return 1;
		}
	}

	internal static string takeOption(List<string> args, string name)
	{
		int num = args.IndexOf(name);
		if (num < 0)
		{
			return null;
		}
		if (num + 1 >= args.Count)
		{
			throw new UsageException(name + " needs a value");
		}
		string result = args[num + 1];
		args.RemoveRange(num, 2);
		return result;
	}

	internal static bool takeFlag(List<string> args, string name)
	{
		return args.Remove(name);
	}

	internal static int? takeInt(List<string> args, string name)
	{
		string text = takeOption(args, name);
		if (text == null)
		{
			return null;
		}
		if (!int.TryParse(text, out var result))
		{
			throw new UsageException(name + " expects a number");
		}
		return result;
	}

	internal static System.Windows.Media.Color ParseColor(string hex)
	{
		hex = hex.Trim().TrimStart('#');
		if (hex.Length != 6)
		{
			throw new UsageException("colour '" + hex + "' must be RRGGBB");
		}
		return System.Windows.Media.Color.FromRgb(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
	}

	internal static int[] ParseRgb(string hex)
	{
		System.Windows.Media.Color color = ParseColor(hex);
		return new int[3] { color.R, color.G, color.B };
	}
}

internal sealed class UsageException : Exception
{
	public UsageException(string message)
		: base(message)
	{
	}
}
