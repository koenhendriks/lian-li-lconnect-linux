using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Windows.Media;
using LConnect3cs.Helpers;
using LConnect3cs.Products;
using LConnect3cs.Products.TLFan;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products;
using LConnectCore.Products.TLFan;

namespace LConnectCli;

// Edits the same UI profile the GUI uses (profile/<md5>) and sends the same requests the original UI sent.
internal static class TLCommands
{
	public static int Run(List<string> args)
	{
		if (args.Count == 0)
		{
			throw new UsageException("usage: lconnectctl tl (lighting|fan|modes) ...");
		}
		string text = args[0];
		args.RemoveAt(0);
		return text switch
		{
			"lighting" => Lighting(args),
			"fan" => Fan(args),
			"modes" => Modes(),
			_ => throw new UsageException("unknown tl command '" + text + "'"),
		};
	}

	private static int Modes()
	{
		Console.WriteLine("Lighting modes (per group):");
		foreach (LightingMode item in TLFanProfile.LightingModeOrder)
		{
			TLFanLightingViewSetting value;
			TLFanLightingViewSetting tLFanLightingViewSetting = (TLFanProfile.LightingViewSettings.TryGetValue(item, out value) ? value : default(TLFanLightingViewSetting));
			Console.WriteLine($"  {item,-16} colours:{tLFanLightingViewSetting.ColorCount} speed:{yn(tLFanLightingViewSetting.Speed)} brightness:{yn(tLFanLightingViewSetting.Brightness)} direction:{yn(tLFanLightingViewSetting.Direction)}");
		}
		Console.WriteLine("Directions: " + string.Join(", ", Enum.GetNames(typeof(LightingDirection))));
		Console.WriteLine("Fan modes: Quiet, StandardSpeed, HighSpeed, FullSpeed, UserDefine1-4, FixRPM");
		return 0;
		static string yn(bool b)
		{
			return b ? "y" : "-";
		}
	}

	internal static TLFanController OpenController()
	{
		List<ControllerWatcher.DaemonController> list = ControllerWatcher.QueryDaemon() ?? throw new Exception("lconnectd not reachable");
		ControllerWatcher.DaemonController daemonController = list.FirstOrDefault((ControllerWatcher.DaemonController c) => c.VendorID == 1046 && c.ProductID == 29554) ?? throw new Exception("no TL controller connected");
		return new TLFanController(ControllerType.TLFan, daemonController.DevicePath, new USBDeviceID(daemonController.VendorID, daemonController.ProductID));
	}

	internal static FanGroupStatus GetStatus(TLFanController controller)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("devicePath", Convert.ToBase64String(Encoding.UTF8.GetBytes(controller.DevicePath)));
		nameValueCollection.Add("type", RequestType.FanGroupStatus.ToString());
		if (!LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.Device, out var json, nameValueCollection, null, 5000) || string.IsNullOrEmpty(json))
		{
			throw new Exception("could not read fan status from lconnectd");
		}
		return JsonSerializer.Deserialize<FanGroupStatus>(json);
	}

	private static TLFanGroupProfile selectGroup(TLFanController controller, int port, int group)
	{
		if (port < 0 || port > 3)
		{
			throw new UsageException("--port must be 0-3");
		}
		PortType portType = GetStatus(controller).PortTypes[port];
		TLFanPortProfile tLFanPortProfile = controller.Profile.PortProfiles[port];
		TLFanGroupProfile[] array = portType switch
		{
			PortType.LCD => tLFanPortProfile.LCDGroupProfiles,
			PortType.LED => tLFanPortProfile.LEDGroupProfiles,
			_ => throw new Exception($"no fans detected on port {port}"),
		};
		if (group < 0 || group >= array.Length)
		{
			throw new UsageException($"--group must be 0-{array.Length - 1} for a {portType} port");
		}
		Console.WriteLine($"port {port} ({portType}) group {group}");
		return array[group];
	}

	private static int Lighting(List<string> args)
	{
		int port = Program.takeInt(args, "--port") ?? throw new UsageException("--port is required");
		int group = Program.takeInt(args, "--group").GetValueOrDefault();
		string text = Program.takeOption(args, "--mode") ?? throw new UsageException("--mode is required (see 'lconnectctl tl modes')");
		if (!Enum.TryParse<LightingMode>(text, ignoreCase: true, out var result) || !TLFanProfile.LightingModeOrder.Contains(result))
		{
			throw new UsageException("unknown mode '" + text + "' (see 'lconnectctl tl modes')");
		}
		TLFanController tLFanController = OpenController();
		TLFanGroupProfile tLFanGroupProfile = selectGroup(tLFanController, port, group);
		TLFanLightingSetting tLFanLightingSetting = (tLFanGroupProfile.LightingSettings.TryGetValue(result, out var value) ? value.Clone() : TLFanProfile.GetDefaultLightingSetting(result));
		int? num = Program.takeInt(args, "--speed");
		if (num.HasValue)
		{
			tLFanLightingSetting.Speed = Math.Clamp(num.Value, 0, 100);
		}
		int? num2 = Program.takeInt(args, "--brightness");
		if (num2.HasValue)
		{
			tLFanLightingSetting.Brightness = Math.Clamp(num2.Value, 0, 100);
		}
		string text2 = Program.takeOption(args, "--direction");
		if (text2 != null)
		{
			if (!Enum.TryParse<LightingDirection>(text2, ignoreCase: true, out var result2))
			{
				throw new UsageException("unknown direction '" + text2 + "'");
			}
			tLFanLightingSetting.Direction = result2;
		}
		string text3 = Program.takeOption(args, "--colors");
		if (text3 != null)
		{
			Color[] array = text3.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Program.ParseColor).ToArray();
			int colorCount = TLFanProfile.LightingViewSettings[result].ColorCount;
			Color[] array2 = (Color[])(tLFanLightingSetting.Colors ?? Array.Empty<Color>()).Clone();
			if (array2.Length < colorCount)
			{
				Array.Resize(ref array2, colorCount);
			}
			for (int i = 0; i < Math.Min(array.Length, array2.Length); i++)
			{
				array2[i] = array[i];
			}
			tLFanLightingSetting.Colors = array2;
		}
		tLFanController.Profile.IsMerged = false;
		tLFanGroupProfile.IsGrouping = true;
		tLFanGroupProfile.IsSingleSideMode = false;
		tLFanGroupProfile.LightingMode = result;
		tLFanGroupProfile.LightingSettings[result] = tLFanLightingSetting;
		tLFanController.SaveProfile();
		tLFanController.SendLightingConfigRequest();
		Console.WriteLine("applied " + tLFanLightingSetting);
		return 0;
	}

	private static int Fan(List<string> args)
	{
		int port = Program.takeInt(args, "--port") ?? throw new UsageException("--port is required");
		int group = Program.takeInt(args, "--group").GetValueOrDefault();
		string text = Program.takeOption(args, "--mode") ?? throw new UsageException("--mode is required");
		if (!Enum.TryParse<RPMMode>(text, ignoreCase: true, out var result))
		{
			throw new UsageException("unknown fan mode '" + text + "'");
		}
		TLFanController tLFanController = OpenController();
		TLFanGroupProfile tLFanGroupProfile = selectGroup(tLFanController, port, group);
		RPMSetting rPMSetting = tLFanGroupProfile.RPMSetting;
		if (!rPMSetting.Profiles.TryGetValue(result, out var value))
		{
			throw new UsageException($"mode {result} not available for this group (available: {string.Join(", ", rPMSetting.Profiles.Keys)})");
		}
		string text2 = Program.takeOption(args, "--source");
		if (text2 != null)
		{
			if (!Enum.TryParse<RPMReferenceSource>(text2, ignoreCase: true, out var result2))
			{
				throw new UsageException("--source must be CPU or GPU");
			}
			value.RPMReferenceSource = result2;
		}
		int? num = Program.takeInt(args, "--rpm");
		if (num.HasValue)
		{
			if (result != RPMMode.FixRPM)
			{
				throw new UsageException("--rpm only applies to FixRPM");
			}
			RPMPhaseInfo currentRPMPhaseInfo = value.GetCurrentRPMPhaseInfo();
			int speed = Math.Clamp(num.Value, currentRPMPhaseInfo.MinSpeed, currentRPMPhaseInfo.MaxSpeed);
			currentRPMPhaseInfo.Phases = new RPMPhase[1]
			{
				new RPMPhase(0, speed)
			};
		}
		rPMSetting.Mode = result;
		tLFanController.SaveProfile();
		tLFanController.SendFanSpeedConfigRequest();
		Console.WriteLine($"fan mode {result} ({value.RPMReferenceSource}), curve: {string.Join(" ", value.GetCurrentRPMPhaseInfo().Phases.Select((RPMPhase p) => $"{p.Temperature}°C={p.Speed}rpm"))}");
		return 0;
	}
}
