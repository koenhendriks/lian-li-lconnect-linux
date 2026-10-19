using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using LConnect3cs.Helpers;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products.Ene6K77Fan;
using LConnectCore.Products.TLFan;
using LConnectCore.Products.TLLCD;

namespace LConnectCli;

internal static class Commands
{
	public static string Get(string path)
	{
		string text = LConnectServiceHttpUtility.Send(HttpMethod.Get, path, null, 5000);
		if (text == null)
		{
			throw new Exception("lconnectd returned an error for " + path);
		}
		return text;
	}

	public static int Post(string path)
	{
		string text = LConnectServiceHttpUtility.Send(HttpMethod.Post, path, Array.Empty<byte>(), 60000);
		Console.WriteLine(text ?? "failed");
		return (text == null) ? 1 : 0;
	}

	public static int Status()
	{
		JsonNode jsonNode = JsonNode.Parse(Get("status"));
		Console.WriteLine("Data directory: " + (string)jsonNode["DataDirectory"]);
		Console.WriteLine();
		Console.WriteLine("Controllers:");
		JsonArray jsonArray = jsonNode["Controllers"]?.AsArray() ?? new JsonArray();
		if (jsonArray.Count == 0)
		{
			Console.WriteLine("  (none)");
		}
		foreach (JsonNode item in jsonArray)
		{
			Console.WriteLine($"  {(string)item["Type"],-22} {(int)item["VendorID"]:x4}:{(int)item["ProductID"]:x4}  {(string)item["DevicePath"]}");
			Console.WriteLine("  " + new string(' ', 22) + " settings: " + Path.Combine(LConnectApp.Path.DeviceSettingDirectory, (string)item["SettingsKey"]));
		}
		Console.WriteLine();
		Console.WriteLine("LCD fans:");
		JsonArray jsonArray2 = jsonNode["LCDs"]?.AsArray() ?? new JsonArray();
		if (jsonArray2.Count == 0)
		{
			Console.WriteLine("  (none)");
		}
		foreach (JsonNode item2 in jsonArray2)
		{
			Console.WriteLine($"  port {(int)item2["PortNo"]} lcd {(int)item2["LCDIndex"]}  fw {(string)item2["Version"]?["SoftwareVersion"]}  {(string)item2["DevicePath"]}");
		}
		JsonNode jsonNode2 = jsonNode["Resource"];
		Console.WriteLine();
		Console.WriteLine($"Sensors: CPU {(int)jsonNode2["CPUTemperature"]}°C {(int)jsonNode2["CPULoad"]}%   GPU {(int)jsonNode2["GPUTemperature"]}°C {(int)jsonNode2["GPULoad"]}%   RAM {(int)jsonNode2["RAMLoad"]}%");
		return 0;
	}

	public static int Devices()
	{
		Core.Init();
		IList<DeviceFinderResult> list = new HidApiDeviceFinder().Find();
		if (list.Count == 0)
		{
			Console.WriteLine("No supported Lian Li devices found (check permissions: you need access to /dev/hidraw*).");
			return 1;
		}
		foreach (DeviceFinderResult item in list.OrderBy((DeviceFinderResult r) => r.DevicePath))
		{
			string deviceKey = DeviceSettingManager.GetDeviceKey(item.DevicePath);
			bool flag = Directory.Exists(Path.Combine(LConnectApp.Path.DeviceSettingDirectory, deviceKey));
			string text = ((item.ControllerType == ControllerType.TLFan && item.UsagePage != 65307) ? " (non-control interface)" : string.Empty);
			Console.WriteLine($"{item.ControllerType,-12} {item.USBDeviceID.VendorID:x4}:{item.USBDeviceID.ProductID:x4} if{item.InterfaceNumber} page 0x{item.UsagePage:x4}{text}");
			Console.WriteLine("             id:       " + item.DevicePath);
			Console.WriteLine("             open:     " + LConnectCore.Platform.UsbTopology.ResolveOpenPath(item.DevicePath));
			Console.WriteLine("             settings: " + deviceKey + (flag ? " (present)" : ""));
		}
		return 0;
	}

	public static int Probe()
	{
		Core.Init();
		foreach (DeviceFinderResult item in new HidApiDeviceFinder().Find())
		{
			try
			{
				switch (item.ControllerType)
				{
				case ControllerType.TLFan:
					if (item.UsagePage == 65307)
					{
						probeTL(item);
					}
					break;
				case ControllerType.TLLCD:
					probeLCD(item);
					break;
				case ControllerType.SLInfinity:
					probeSLInfinity(item);
					break;
				default:
					Console.WriteLine($"{item.ControllerType}: {item.DevicePath} (no probe)");
					break;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"{item.ControllerType}: {item.DevicePath} failed: {ex.Message}");
			}
		}
		return 0;
	}

	private static void probeTL(DeviceFinderResult r)
	{
		HidApiHidDevice hidApiHidDevice = new HidApiHidDevice(r.DevicePath);
		hidApiHidDevice.Open();
		if (!hidApiHidDevice.IsOpen)
		{
			Console.WriteLine("TL controller " + r.DevicePath + ": cannot open (permissions? is lconnectd running?)");
			return;
		}
		try
		{
			TLFanDevice tLFanDevice = new TLFanDevice(hidApiHidDevice);
			hidApiHidDevice.ReadTimeout = 1000;
			LConnectCore.Products.TLFan.FirmwareVersion controllerFirmwareVersion = tLFanDevice.GetControllerFirmwareVersion();
			TLFanHandshakeInfo handshakeInfo = tLFanDevice.GetHandshakeInfo();
			Console.WriteLine("TL controller " + r.DevicePath);
			Console.WriteLine($"  firmware {controllerFirmwareVersion.ModelB} {controllerFirmwareVersion.SoftwareVersion} ({controllerFirmwareVersion.DateTime:yyyy-MM-dd})  handshake valid: {handshakeInfo.IsValid}");
			for (int i = 0; i < 4; i++)
			{
				int num = handshakeInfo.FanQuantity[i];
				if (num == 0)
				{
					Console.WriteLine($"  port {i}: no fans");
					continue;
				}
				LConnectCore.Products.TLFan.FirmwareVersion fanFirmwareVersion = tLFanDevice.GetFanFirmwareVersion(i, 0);
				List<string> values = (from f in Enumerable.Range(0, num)
					select handshakeInfo.FanInfos[i, f].FanSpeed + " rpm").ToList();
				Console.WriteLine($"  port {i}: {num} fan(s) model {fanFirmwareVersion.ModelB} {fanFirmwareVersion.SoftwareVersion}: {string.Join(", ", values)}");
			}
		}
		finally
		{
			hidApiHidDevice.Close();
		}
	}

	private static void probeLCD(DeviceFinderResult r)
	{
		HidApiHidDevice hidDevice = new HidApiHidDevice(r.DevicePath);
		TLLCDDevice tLLCDDevice = new TLLCDDevice(hidDevice, r.LocationInfo);
		bool flag = tLLCDDevice.Open();
		try
		{
			LConnectCore.Products.TLLCD.FirmwareVersion firmwareVersion = tLLCDDevice.FirmwareVersionV();
			HandshakeInfo handshakeInfo = tLLCDDevice.GetHandshakeInfo();
			Console.WriteLine("TL LCD " + r.DevicePath);
			Console.WriteLine($"  serial query ok: {flag}  port {tLLCDDevice.GetPort()} index {tLLCDDevice.GetLCDIndex()}  firmware {firmwareVersion.ModelB} {firmwareVersion.SoftwareVersion}  mode {handshakeInfo.Mode} frame {handshakeInfo.FrameIndex}");
		}
		finally
		{
			tLLCDDevice.HidDevice.Close();
		}
	}

	private static void probeSLInfinity(DeviceFinderResult r)
	{
		HidApiHidDevice hidApiHidDevice = new HidApiHidDevice(r.DevicePath);
		hidApiHidDevice.Open();
		try
		{
			SLInfinityDevice sLInfinityDevice = new SLInfinityDevice(hidApiHidDevice);
			LConnectCore.Products.Ene6K77Fan.FirmwareVersion firmwareVersion = sLInfinityDevice.GetFirmwareVersion();
			int[] fanSpeed = sLInfinityDevice.GetFanSpeed();
			Console.WriteLine("SL Infinity " + r.DevicePath);
			Console.WriteLine("  firmware " + firmwareVersion?.ToString() + "  fan groups rpm: " + ((fanSpeed == null) ? "n/a" : string.Join(", ", fanSpeed)));
		}
		finally
		{
			hidApiHidDevice.Close();
		}
	}

	private static List<SettingsImporter.ConnectedDevice> connectedDevices()
	{
		List<SettingsImporter.ConnectedDevice> list = new List<SettingsImporter.ConnectedDevice>();
		List<ControllerWatcher.DaemonController> list2 = null;
		try
		{
			list2 = ControllerWatcher.QueryDaemon();
		}
		catch
		{
		}
		if (list2 != null)
		{
			Console.WriteLine("Using the device list from lconnectd.");
			foreach (ControllerWatcher.DaemonController item in list2)
			{
				(int, int, int)? tuple = SettingsImporter.ParseIdentity(item.DevicePath);
				list.Add(new SettingsImporter.ConnectedDevice
				{
					DevicePath = item.DevicePath,
					VendorID = item.VendorID,
					ProductID = item.ProductID,
					InterfaceNumber = (tuple?.Item3 ?? 0)
				});
			}
			return list;
		}
		Console.WriteLine("lconnectd not reachable; enumerating devices locally.");
		Core.Init();
		foreach (DeviceFinderResult item2 in new HidApiDeviceFinder().Find())
		{
			if (item2.ControllerType != ControllerType.TLLCD && (item2.ControllerType != ControllerType.TLFan || item2.UsagePage == 65307))
			{
				list.Add(new SettingsImporter.ConnectedDevice
				{
					DevicePath = item2.DevicePath,
					VendorID = item2.USBDeviceID.VendorID,
					ProductID = item2.USBDeviceID.ProductID,
					InterfaceNumber = item2.InterfaceNumber
				});
			}
		}
		return list;
	}

	public static int Import(List<string> args)
	{
		string text = Program.takeOption(args, "--from") ?? throw new UsageException("import needs --from <path to Windows L-Connect 3 appdata folder>");
		string text2 = Program.takeOption(args, "--install");
		bool flag = Program.takeFlag(args, "--dry-run");
		if (text2 == null)
		{
			string directoryName = Path.GetDirectoryName(Path.GetFullPath(text).TrimEnd('/', '\\'));
			if (directoryName != null && Directory.Exists(Path.Combine(directoryName, "repo")))
			{
				text2 = directoryName;
			}
		}
		List<SettingsImporter.ConnectedDevice> list = connectedDevices();
		foreach (SettingsImporter.ConnectedDevice item in list)
		{
			Console.WriteLine($"  connected: {item.VendorID:x4}:{item.ProductID:x4} if{item.InterfaceNumber} {item.DevicePath}");
		}
		SettingsImporter.Result result = SettingsImporter.Import(text, text2, list, flag);
		foreach (string message in result.Messages)
		{
			Console.WriteLine("  " + message);
		}
		Console.WriteLine($"{(flag ? "Would import" : "Imported")} {result.Imported} setting file(s), {result.Pending} pending for unplugged devices, {result.MediaCopied} LCD media file(s).");
		if (!flag)
		{
			try
			{
				LConnectServiceHttpUtility.Send(HttpMethod.Post, "reapply", Array.Empty<byte>(), 60000);
				Console.WriteLine("Asked lconnectd to apply the imported settings.");
			}
			catch
			{
				Console.WriteLine("lconnectd not running; settings will be applied when it starts.");
			}
		}
		return 0;
	}

	public static int Assets(List<string> args)
	{
		if (args.Count == 0 || args[0] != "import")
		{
			throw new UsageException("usage: lconnectctl assets import --from <L-Connect 3 install dir>");
		}
		args.RemoveAt(0);
		string text = Program.takeOption(args, "--from") ?? throw new UsageException("assets import needs --from <L-Connect 3 install dir>");
		if (!AssetImporter.LooksLikeInstallDirectory(text))
		{
			throw new UsageException(text + " does not look like an L-Connect 3 install (assets/tl-sensor/index.html missing)");
		}
		foreach (string message in AssetImporter.Import(text).Messages)
		{
			Console.WriteLine(message);
		}
		return 0;
	}
}
