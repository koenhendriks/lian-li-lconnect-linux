using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Web;
using LConnect3cs.Helpers;
using LConnectCore;
using LConnectCore.Products.TLLCD;

namespace LConnectCli;

// Same TLLCD requests the original LCD Screen page sends (control=update/setting/config, port=N).
internal static class LcdCommands
{
	public static int Run(List<string> args)
	{
		if (args.Count == 0)
		{
			throw new UsageException("usage: lconnectctl lcd (show|status|animations) ...");
		}
		string text = args[0];
		args.RemoveAt(0);
		return text switch
		{
			"show" => Show(args),
			"status" => Status(args),
			"animations" => Animations(),
			_ => throw new UsageException("unknown lcd command '" + text + "'"),
		};
	}

	internal static string Send(string control, int port, object data)
	{
		string devicePath = TLCommands.OpenController().DevicePath;
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("control", control);
		nameValueCollection.Add("devicePath", Convert.ToBase64String(Encoding.UTF8.GetBytes(devicePath)));
		nameValueCollection.Add("port", port.ToString());
		if (!LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.TLLCD, out var json, nameValueCollection, data, 30000))
		{
			throw new Exception("lconnectd rejected the LCD request");
		}
		return json;
	}

	private static int Animations()
	{
		string[] array = new string[2] { "v", "h" };
		foreach (string text in array)
		{
			string text2 = Path.Combine(LConnectApp.Path.RepoDirectory, "animation", "1", text);
			string[] array2 = (Directory.Exists(text2) ? Directory.GetFiles(text2, "*.avi") : Array.Empty<string>());
			Console.WriteLine($"{((text == "v") ? "vertical" : "horizontal")} ({array2.Length}):");
			foreach (string item in array2.OrderBy(File.GetCreationTime))
			{
				Console.WriteLine("  " + Path.GetFileNameWithoutExtension(item));
			}
		}
		if (!Directory.Exists(Path.Combine(LConnectApp.Path.RepoDirectory, "animation")))
		{
			Console.WriteLine("No animations imported yet: run 'lconnectctl assets import --from <L-Connect 3 install dir>'.");
		}
		return 0;
	}

	private static int Status(List<string> args)
	{
		int port = Program.takeInt(args, "--port") ?? throw new UsageException("--port is required");
		Console.WriteLine("devices: " + Send("devices", port, null));
		Console.WriteLine("config:  " + Send("config", port, null));
		List<TLLCDScreenSetting> list = JsonSerializer.Deserialize<List<TLLCDScreenSetting>>(Send("setting", port, null)) ?? new List<TLLCDScreenSetting>();
		foreach (TLLCDScreenSetting item in list.OrderBy((TLLCDScreenSetting s) => s.Index))
		{
			string text = ((item.MediaType == TLLCDMediaType.Sensor && item.Sensor != null) ? $"{item.Sensor.Type} style {item.Sensor.Style}" : item.FilePath);
			Console.WriteLine($"  lcd {item.Index}: {item.MediaType} {text} rotate {item.Rotate} block {item.Block}/{item.TotalBlock}");
		}
		return 0;
	}

	private static ScreenRotation parseRotation(string text)
	{
		return text switch
		{
			null => ScreenRotation.Rotate0,
			"0" => ScreenRotation.Rotate0,
			"90" => ScreenRotation.Rotate90,
			"180" => ScreenRotation.Rotate180,
			"270" => ScreenRotation.Rotate270,
			_ => throw new UsageException("--rotate must be 0, 90, 180 or 270"),
		};
	}

	private static int Show(List<string> args)
	{
		int num = Program.takeInt(args, "--port") ?? throw new UsageException("--port is required");
		int index = Program.takeInt(args, "--index").GetValueOrDefault();
		ScreenRotation rotate = parseRotation(Program.takeOption(args, "--rotate"));
		TLLCDScreenSetting tLLCDScreenSetting = new TLLCDScreenSetting
		{
			Index = index,
			TotalBlock = 1,
			Block = 1,
			Horizontal = false,
			Rotate = rotate
		};
		string text = Program.takeOption(args, "--image");
		string text2 = Program.takeOption(args, "--video");
		string text3 = Program.takeOption(args, "--gif");
		string text4 = Program.takeOption(args, "--animation");
		string text5 = Program.takeOption(args, "--sensor");
		bool flag = Program.takeFlag(args, "--none");
		Action<int> progress = delegate(int p)
		{
			Console.Write($"\rconverting {Math.Clamp(p, 0, 100)}%");
		};
		if (text != null)
		{
			tLLCDScreenSetting.MediaType = TLLCDMediaType.Image;
			tLLCDScreenSetting.FilePath = requireConverted(MediaConverter.SaveImage(Path.GetFullPath(text), 1, horizontal: false));
		}
		else if (text2 != null)
		{
			tLLCDScreenSetting.MediaType = TLLCDMediaType.Video;
			tLLCDScreenSetting.FilePath = requireConverted(MediaConverter.SaveMP4(Path.GetFullPath(text2), 1, horizontal: false, progress));
			Console.WriteLine();
		}
		else if (text3 != null)
		{
			tLLCDScreenSetting.MediaType = TLLCDMediaType.Dynamic;
			tLLCDScreenSetting.FilePath = requireConverted(MediaConverter.SaveGif(Path.GetFullPath(text3), 1, horizontal: false, progress));
			Console.WriteLine();
		}
		else if (text4 != null)
		{
			string path = Path.Combine(LConnectApp.Path.RepoDirectory, "animation", "1", "v");
			string text6 = (Directory.Exists(path) ? Directory.GetFiles(path, "*.avi").FirstOrDefault((string f) => Path.GetFileNameWithoutExtension(f).StartsWith(text4, StringComparison.OrdinalIgnoreCase)) : null);
			tLLCDScreenSetting.MediaType = TLLCDMediaType.Animation;
			tLLCDScreenSetting.FilePath = text6 ?? throw new UsageException("animation '" + text4 + "' not found (see 'lconnectctl lcd animations')");
		}
		else if (text5 != null)
		{
			if (!Enum.TryParse<TLLCDSensorType>(text5, ignoreCase: true, out var result))
			{
				throw new UsageException("--sensor must be one of " + string.Join(", ", Enum.GetNames(typeof(TLLCDSensorType))));
			}
			int value = Program.takeInt(args, "--style").GetValueOrDefault();
			int[] array = Program.ParseRgb(Program.takeOption(args, "--text") ?? "ffffff");
			int[] array2 = Program.ParseRgb(Program.takeOption(args, "--bg") ?? "0000ff");
			int[] backgroundColor = Program.ParseRgb(Program.takeOption(args, "--bg2") ?? "00d7ff");
			tLLCDScreenSetting.MediaType = TLLCDMediaType.Sensor;
			tLLCDScreenSetting.Sensor = new TLLCDSensorSetting
			{
				Type = result,
				Style = Math.Clamp(value, 0, 4),
				TextColor1 = array,
				TextColor2 = array,
				TextColor3 = array,
				BackgroundColor1 = array2,
				BackgroundColor2 = ((value >= 3) ? backgroundColor : null)
			};
		}
		else
		{
			if (!flag)
			{
				throw new UsageException("choose one of --image, --video, --gif, --animation, --sensor or --none");
			}
			tLLCDScreenSetting.MediaType = TLLCDMediaType.None;
		}
		if (tLLCDScreenSetting.FilePath != null)
		{
			tLLCDScreenSetting.FileMD5 = MediaConverter.calculateSHA256(tLLCDScreenSetting.FilePath);
		}
		Send("update", num, tLLCDScreenSetting);
		Console.WriteLine($"port {num} lcd {index}: {tLLCDScreenSetting.MediaType} {tLLCDScreenSetting.FilePath}");
		return 0;
	}

	private static string requireConverted(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			throw new Exception("media conversion failed (is ffmpeg installed, and can you write to " + LConnectApp.Path.RepoDirectory + "?)");
		}
		return path;
	}
}
