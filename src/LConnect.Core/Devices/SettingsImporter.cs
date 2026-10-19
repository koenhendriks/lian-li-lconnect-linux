using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LConnectCore.Devices;

// Imports settings written by Windows L-Connect 3 (its "appdata" folder) into this port's data directory.
//
// Windows keys everything by md5 of the HID interface path ("\\?\hid#vid_0416&pid_7372&mi_02#..."). Each setting
// is re-keyed to the Linux stable id of the matching device (same VID, PID and interface number). Settings for
// devices that are not connected right now are parked under pending/<vid>_<pid>_<mi>/ and adopted by the daemon
// the first time such a device appears (see AdoptPending).
public static class SettingsImporter
{
	public sealed class ConnectedDevice
	{
		public string DevicePath { get; set; }

		public int VendorID { get; set; }

		public int ProductID { get; set; }

		public int InterfaceNumber { get; set; }
	}

	public sealed class Result
	{
		public List<string> Messages { get; } = new List<string>();

		public int Imported { get; set; }

		public int Pending { get; set; }

		public int MediaCopied { get; set; }
	}

	public const string LcdFolderName = "TL-LCD-Controller-240110";

	private static readonly Regex WindowsHidPath = new Regex("vid_([0-9a-f]{4})&pid_([0-9a-f]{4})(?:&mi_([0-9a-f]{2}))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex LinuxStableId = new Regex(":\\d+\\.(\\d+)#([0-9a-f]{4}):([0-9a-f]{4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static string PendingDirectory => Path.Combine(LConnectApp.Path.DataRoot, "pending");

	// Extracts (vid, pid, interface) from a Windows HID path or a Linux stable id.
	public static (int Vid, int Pid, int Interface)? ParseIdentity(string devicePath)
	{
		if (string.IsNullOrEmpty(devicePath))
		{
			return null;
		}
		Match match = WindowsHidPath.Match(devicePath);
		if (match.Success)
		{
			int item = (match.Groups[3].Success ? Convert.ToInt32(match.Groups[3].Value, 16) : 0);
			return (Convert.ToInt32(match.Groups[1].Value, 16), Convert.ToInt32(match.Groups[2].Value, 16), item);
		}
		match = LinuxStableId.Match(devicePath);
		if (match.Success)
		{
			return (Convert.ToInt32(match.Groups[2].Value, 16), Convert.ToInt32(match.Groups[3].Value, 16), int.Parse(match.Groups[1].Value));
		}
		return null;
	}

	private static string pendingKey((int Vid, int Pid, int Interface) id)
	{
		return $"{id.Vid:x4}_{id.Pid:x4}_{id.Interface:x2}";
	}

	public static byte[] Gunzip(string path)
	{
		using FileStream stream = File.OpenRead(path);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		gZipStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	public static void Gzip(string path, byte[] data)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + ".tmp";
		using (FileStream stream = new FileStream(text, FileMode.Create, FileAccess.Write))
		{
			using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Compress);
			gZipStream.Write(data, 0, data.Length);
		}
		File.Move(text, path, overwrite: true);
	}

	// sourceAppData: the Windows "appdata" folder. sourceInstallDir (optional): the Windows install folder, used to
	// copy LCD media referenced by the settings (repo/...).
	public static Result Import(string sourceAppData, string sourceInstallDir, IList<ConnectedDevice> connected, bool dryRun)
	{
		Result result = new Result();
		string path = Path.Combine(sourceAppData, "device");
		if (!Directory.Exists(path))
		{
			throw new DirectoryNotFoundException("No 'device' folder in " + sourceAppData + ". Point --from at the L-Connect 3 'appdata' folder.");
		}
		string text = DeviceSettingManager.GetMD5String(LcdFolderName);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string item in Directory.GetDirectories(path))
		{
			if (Path.GetFileName(item) == text)
			{
				continue;
			}
			foreach (string item2 in Directory.GetFiles(item, "*.0"))
			{
				JsonNode jsonNode;
				try
				{
					jsonNode = JsonNode.Parse(Gunzip(item2));
				}
				catch (Exception ex)
				{
					result.Messages.Add("skip unreadable " + item2 + ": " + ex.Message);
					continue;
				}
				string text2 = (string)jsonNode["DevicePath"];
				string text3 = (string)jsonNode["Type"];
				(int Vid, int Pid, int Interface)? tuple = ParseIdentity(text2);
				if (!tuple.HasValue || text3 == null)
				{
					result.Messages.Add("skip " + item2 + ": unknown device path '" + text2 + "'");
					continue;
				}
				dictionary[Path.GetFileName(item)] = text2;
				ConnectedDevice connectedDevice = match(connected, tuple.Value, result);
				string fileName = DeviceSettingManager.GetMD5String(text3) + ".0";
				if (connectedDevice != null)
				{
					jsonNode["DevicePath"] = connectedDevice.DevicePath;
					string text4 = Path.Combine(LConnectApp.Path.DeviceSettingDirectory, DeviceSettingManager.GetDeviceKey(connectedDevice.DevicePath), fileName);
					result.Messages.Add($"{text3,-16} {text2} -> {connectedDevice.DevicePath}");
					if (!dryRun)
					{
						Gzip(text4, Encoding.UTF8.GetBytes(jsonNode.ToJsonString()));
					}
					result.Imported++;
				}
				else
				{
					string text5 = Path.Combine(PendingDirectory, pendingKey(tuple.Value), "device", fileName);
					result.Messages.Add($"{text3,-16} {text2} -> pending (device not connected)");
					if (!dryRun)
					{
						Gzip(text5, Encoding.UTF8.GetBytes(jsonNode.ToJsonString()));
					}
					result.Pending++;
				}
			}
		}
		string path2 = Path.Combine(path, text);
		if (Directory.Exists(path2))
		{
			foreach (string item3 in Directory.GetFiles(path2, "*.LCD"))
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item3);
				if (!dictionary.TryGetValue(fileNameWithoutExtension, out var value))
				{
					// The LCD file is named md5(controller path); find the TL controller among the imported devices.
					value = dictionary.Values.FirstOrDefault((string p) => ParseIdentity(p)?.Pid == 29554);
				}
				(int Vid, int Pid, int Interface)? tuple2 = ParseIdentity(value);
				if (!tuple2.HasValue)
				{
					result.Messages.Add("skip " + item3 + ": cannot tell which TL controller it belongs to");
					continue;
				}
				JsonNode jsonNode2 = JsonNode.Parse(Gunzip(item3));
				result.MediaCopied += rewriteLcdMedia(jsonNode2, sourceInstallDir, dryRun, result);
				ConnectedDevice connectedDevice2 = match(connected, tuple2.Value, result);
				string text6 = ((connectedDevice2 != null) ? Path.Combine(LConnectApp.Path.DeviceSettingDirectory, text, DeviceSettingManager.GetMD5String(connectedDevice2.DevicePath) + ".LCD") : Path.Combine(PendingDirectory, pendingKey(tuple2.Value), "lcd.LCD"));
				result.Messages.Add("LCD screens      " + value + " -> " + ((connectedDevice2 != null) ? connectedDevice2.DevicePath : "pending"));
				if (!dryRun)
				{
					Gzip(text6, Encoding.UTF8.GetBytes(jsonNode2.ToJsonString()));
				}
				if (connectedDevice2 != null)
				{
					result.Imported++;
				}
				else
				{
					result.Pending++;
				}
			}
		}
		string path3 = Path.Combine(sourceAppData, "profile");
		if (Directory.Exists(path3))
		{
			foreach (string item4 in Directory.GetFiles(path3))
			{
				string fileName2 = Path.GetFileName(item4);
				if (!dictionary.TryGetValue(fileName2, out var value2))
				{
					result.Messages.Add("skip profile " + fileName2 + ": no matching device settings");
					continue;
				}
				(int Vid, int Pid, int Interface) value3 = ParseIdentity(value2).Value;
				ConnectedDevice connectedDevice3 = match(connected, value3, result);
				string text7 = ((connectedDevice3 != null) ? Path.Combine(LConnectApp.Path.ControllerProfileDirectory, DeviceSettingManager.GetDeviceKey(connectedDevice3.DevicePath)) : Path.Combine(PendingDirectory, pendingKey(value3), "profile"));
				result.Messages.Add("UI profile       " + value2 + " -> " + ((connectedDevice3 != null) ? connectedDevice3.DevicePath : "pending"));
				if (!dryRun)
				{
					Directory.CreateDirectory(Path.GetDirectoryName(text7));
					FileSystemHelper.CopyShared(item4, text7);
				}
			}
		}
		return result;
	}

	private static ConnectedDevice match(IList<ConnectedDevice> connected, (int Vid, int Pid, int Interface) id, Result result)
	{
		List<ConnectedDevice> list = connected.Where((ConnectedDevice d) => d.VendorID == id.Vid && d.ProductID == id.Pid && d.InterfaceNumber == id.Interface).ToList();
		if (list.Count > 1)
		{
			result.Messages.Add($"warning: {list.Count} identical devices {id.Vid:x4}:{id.Pid:x4}; using {list[0].DevicePath}");
		}
		return list.FirstOrDefault();
	}

	// Windows media paths look like "C:\Program Files\Lian-Li\L-Connect 3\repo\jpg\1\h\<guid>.jpg".
	private static int rewriteLcdMedia(JsonNode lcd, string sourceInstallDir, bool dryRun, Result result)
	{
		int num = 0;
		if (!(lcd is JsonArray jsonArray))
		{
			return 0;
		}
		foreach (JsonNode item in jsonArray)
		{
			if (!(item?["Screens"] is JsonArray jsonArray2))
			{
				continue;
			}
			foreach (JsonNode item2 in jsonArray2)
			{
				string text = (string)item2?["FilePath"];
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				string text2 = text.Replace('\\', '/');
				int num2 = text2.IndexOf("/repo/", StringComparison.OrdinalIgnoreCase);
				if (num2 < 0)
				{
					result.Messages.Add("warning: LCD media outside repo, left as is: " + text);
					continue;
				}
				string text3 = text2.Substring(num2 + "/repo/".Length);
				string text4 = Path.Combine(LConnectApp.Path.RepoDirectory, text3.Replace('/', Path.DirectorySeparatorChar));
				item2["FilePath"] = text4;
				if (sourceInstallDir == null)
				{
					continue;
				}
				string text5 = Path.Combine(sourceInstallDir, "repo", text3.Replace('/', Path.DirectorySeparatorChar));
				if (File.Exists(text5))
				{
					if (!dryRun)
					{
						Directory.CreateDirectory(Path.GetDirectoryName(text4));
						FileSystemHelper.CopyShared(text5, text4);
					}
					num++;
				}
				else
				{
					result.Messages.Add("warning: LCD media not found: " + text5);
				}
			}
		}
		return num;
	}

	// Called by the daemon when a device is connected. If settings were imported while it was unplugged, move them
	// into place (only if the device has no settings of its own yet). Returns true if anything was adopted.
	public static bool AdoptPending(string devicePath, int vendorId, int productId, int interfaceNumber)
	{
		string path = Path.Combine(PendingDirectory, pendingKey((vendorId, productId, interfaceNumber)));
		if (!Directory.Exists(path))
		{
			return false;
		}
		string text = Path.Combine(LConnectApp.Path.DeviceSettingDirectory, DeviceSettingManager.GetDeviceKey(devicePath));
		bool result = false;
		string path2 = Path.Combine(path, "device");
		if (Directory.Exists(path2) && (!Directory.Exists(text) || !Directory.EnumerateFiles(text, "*.0").Any()))
		{
			foreach (string item in Directory.GetFiles(path2, "*.0"))
			{
				JsonNode jsonNode = JsonNode.Parse(Gunzip(item));
				jsonNode["DevicePath"] = devicePath;
				Gzip(Path.Combine(text, Path.GetFileName(item)), Encoding.UTF8.GetBytes(jsonNode.ToJsonString()));
			}
			result = true;
		}
		string text2 = Path.Combine(path, "lcd.LCD");
		if (File.Exists(text2))
		{
			string text3 = Path.Combine(LConnectApp.Path.DeviceSettingDirectory, DeviceSettingManager.GetMD5String(LcdFolderName), DeviceSettingManager.GetMD5String(devicePath) + ".LCD");
			if (!File.Exists(text3))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text3));
				FileSystemHelper.CopyShared(text2, text3);
				result = true;
			}
		}
		string text4 = Path.Combine(path, "profile");
		string text5 = Path.Combine(LConnectApp.Path.ControllerProfileDirectory, DeviceSettingManager.GetDeviceKey(devicePath));
		if (File.Exists(text4) && !File.Exists(text5))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(text5));
			FileSystemHelper.CopyShared(text4, text5);
			result = true;
		}
		try
		{
			Directory.Delete(path, recursive: true);
		}
		catch
		{
		}
		return result;
	}

	// Earlier builds failed to resolve the sysfs location and keyed devices as "hid-hidrawN#vid:pid", so every boot
	// that renumbered hidraw nodes started from empty settings. When a device with a real stable id has no settings
	// yet, copy the most recently written of those legacy sets with the same VID:PID. The old files are left alone.
	public static bool AdoptLegacy(string devicePath, int vendorId, int productId)
	{
		if (string.IsNullOrEmpty(devicePath) || devicePath.StartsWith("hid-", StringComparison.Ordinal))
		{
			return false;
		}
		string lcdDirectory = Path.Combine(LConnectApp.Path.DeviceSettingDirectory, DeviceSettingManager.GetMD5String(LcdFolderName));
		(string Device, string Lcd, string Profile) paths(string key) => (Path.Combine(LConnectApp.Path.DeviceSettingDirectory, key), Path.Combine(lcdDirectory, key + ".LCD"), Path.Combine(LConnectApp.Path.ControllerProfileDirectory, key));
		var target = paths(DeviceSettingManager.GetDeviceKey(devicePath));
		if ((Directory.Exists(target.Device) && Directory.EnumerateFiles(target.Device, "*.0").Any()) || File.Exists(target.Lcd) || File.Exists(target.Profile))
		{
			return false;
		}
		(string Device, string Lcd, string Profile) source = default;
		DateTime dateTime = DateTime.MinValue;
		for (int i = 0; i < 256; i++)
		{
			var candidate = paths(DeviceSettingManager.GetDeviceKey($"hid-hidraw{i}#{vendorId:x4}:{productId:x4}"));
			IEnumerable<string> files = (Directory.Exists(candidate.Device) ? Directory.EnumerateFiles(candidate.Device, "*.0") : Enumerable.Empty<string>()).Append(candidate.Lcd).Append(candidate.Profile).Where(File.Exists);
			foreach (string file in files)
			{
				DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(file);
				if (lastWriteTimeUtc > dateTime)
				{
					dateTime = lastWriteTimeUtc;
					source = candidate;
				}
			}
		}
		if (source.Device == null)
		{
			return false;
		}
		if (Directory.Exists(source.Device))
		{
			foreach (string item in Directory.GetFiles(source.Device, "*.0"))
			{
				JsonNode jsonNode = JsonNode.Parse(Gunzip(item));
				jsonNode["DevicePath"] = devicePath;
				Gzip(Path.Combine(target.Device, Path.GetFileName(item)), Encoding.UTF8.GetBytes(jsonNode.ToJsonString()));
			}
		}
		if (File.Exists(source.Lcd))
		{
			FileSystemHelper.CopyShared(source.Lcd, target.Lcd);
		}
		if (File.Exists(source.Profile))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(target.Profile));
			FileSystemHelper.CopyShared(source.Profile, target.Profile);
		}
		return true;
	}
}
