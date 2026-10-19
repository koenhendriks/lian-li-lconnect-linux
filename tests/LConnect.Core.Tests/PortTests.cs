using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using LConnectCore.Devices;
using LConnectCore.Platform;
using LConnectCore.Products.TLFan;
using Xunit;

namespace LConnect.Core.Tests;

public class PortTests
{
	private static string gunzip(string path)
	{
		using FileStream stream = File.OpenRead(path);
		using GZipStream stream2 = new GZipStream(stream, CompressionMode.Decompress);
		using StreamReader streamReader = new StreamReader(stream2);
		return streamReader.ReadToEnd();
	}

	private static void assertJsonEquivalent(JsonElement expected, JsonElement actual, string path)
	{
		if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
		{
			Assert.True(Math.Abs(expected.GetDouble() - actual.GetDouble()) < 1E-06, $"{path}: {expected} != {actual}");
			return;
		}
		Assert.Equal(expected.ValueKind, actual.ValueKind);
		switch (expected.ValueKind)
		{
		case JsonValueKind.Object:
		{
			string[] array = (from p in expected.EnumerateObject()
				select p.Name).ToArray();
			string[] array2 = (from p in actual.EnumerateObject()
				select p.Name).ToArray();
			Assert.Equal(array, array2);
			string[] array3 = array;
			foreach (string text in array3)
			{
				assertJsonEquivalent(expected.GetProperty(text), actual.GetProperty(text), path + "." + text);
			}
			break;
		}
		case JsonValueKind.Array:
		{
			JsonElement[] array4 = expected.EnumerateArray().ToArray();
			JsonElement[] array5 = actual.EnumerateArray().ToArray();
			Assert.Equal(array4.Length, array5.Length);
			for (int j = 0; j < array4.Length; j++)
			{
				assertJsonEquivalent(array4[j], array5[j], $"{path}[{j}]");
			}
			break;
		}
		default:
			Assert.Equal(expected.ToString(), actual.ToString());
			break;
		}
	}

	// Settings written by Windows L-Connect (WPF Color serialization) must load in the port and serialize back
	// to an equivalent document, so the two installs can share settings.
	[Fact]
	public void LightingSettingsRoundTrip()
	{
		string path = Path.Combine(GoldenTests.RepoRoot, "tests", "fixtures", "appdata", "device", "69ddedcae59aaa90668efb45766e139c");
		string text = Directory.GetFiles(path, "*.0").Single(delegate(string f)
		{
			DeviceSetting<object> deviceSetting = DeviceSettingManager.Load(f);
			return deviceSetting.Type == "Lighting";
		});
		DeviceSetting<LightingConfigCollection> deviceSetting2 = DeviceSettingManager.Load<LightingConfigCollection>(text);
		Assert.Equal((byte)0, deviceSetting2.Data.LightingConfigs[2][PortType.LCD][0].Configs[0].Colors[0].R);
		Assert.Equal((byte)215, deviceSetting2.Data.LightingConfigs[2][PortType.LCD][0].Configs[0].Colors[1].G);
		string json = JsonSerializer.Serialize(deviceSetting2);
		using JsonDocument jsonDocument = JsonDocument.Parse(gunzip(text));
		using JsonDocument jsonDocument2 = JsonDocument.Parse(json);
		assertJsonEquivalent(jsonDocument.RootElement, jsonDocument2.RootElement, "$");
	}

	[Fact]
	public void SettingsFolderKeyMatchesWindows()
	{
		Assert.Equal("69ddedcae59aaa90668efb45766e139c", DeviceSettingManager.GetDeviceKey("\\\\?\\HID#VID_0416&PID_7372&MI_02#d&7418277&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
		Assert.Equal("802e72ff43e7ee26b13461d82d80e1ac", DeviceSettingManager.GetMD5String("TL-LCD-Controller-240110"));
		Assert.Equal("2e4b97fde8cf63085ec969fcc7e490c0", DeviceSettingManager.GetMD5String("Lighting"));
	}

	[Fact]
	public void ReportDescriptorLengths()
	{
		// Vendor page, report 0xE0: 64-byte input, 64-byte feature; report 1: 63-byte output.
		byte[] array = new byte[37]
		{
			6, 0, 255, 9, 1, 161, 1, 133, 224, 21,
			0, 38, 255, 0, 117, 8, 149, 64, 9, 1,
			129, 2, 9, 2, 177, 2, 133, 1, 149, 63,
			9, 3, 145, 2, 192, 0, 0
		};
		HidReportDescriptor hidReportDescriptor = HidReportDescriptor.Parse(array, 35);
		Assert.True(hidReportDescriptor.UsesReportIds);
		Assert.Equal(65, hidReportDescriptor.GetReportLength(HidReportDescriptor.ReportKind.Input, 224));
		Assert.Equal(65, hidReportDescriptor.GetReportLength(HidReportDescriptor.ReportKind.Feature, 224));
		Assert.Equal(64, hidReportDescriptor.GetReportLength(HidReportDescriptor.ReportKind.Output, 1));
		Assert.Equal(0, hidReportDescriptor.GetReportLength(HidReportDescriptor.ReportKind.Output, 224));
	}

	[Fact]
	public void AdoptLegacyTakesNewestHidrawSettings()
	{
		// Points LConnectApp.Path at the per-run temp directory instead of /var/lib/lconnect.
		System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(GoldenTests).TypeHandle);
		string lcdDirectory = Path.Combine(LConnectCore.LConnectApp.Path.DeviceSettingDirectory, DeviceSettingManager.GetMD5String(SettingsImporter.LcdFolderName));
		void writeLegacy(string legacyId, string marker, DateTime time)
		{
			string key = DeviceSettingManager.GetDeviceKey(legacyId);
			string setting = Path.Combine(LConnectCore.LConnectApp.Path.DeviceSettingDirectory, key, "lighting.0");
			SettingsImporter.Gzip(setting, System.Text.Encoding.UTF8.GetBytes($"{{\"DevicePath\":\"{legacyId}\",\"Type\":\"{marker}\"}}"));
			string lcd = Path.Combine(lcdDirectory, key + ".LCD");
			SettingsImporter.Gzip(lcd, System.Text.Encoding.UTF8.GetBytes(marker));
			string profile = Path.Combine(LConnectCore.LConnectApp.Path.ControllerProfileDirectory, key);
			Directory.CreateDirectory(Path.GetDirectoryName(profile));
			File.WriteAllText(profile, marker);
			foreach (string file in new[] { setting, lcd, profile })
			{
				File.SetLastWriteTimeUtc(file, time);
			}
		}
		writeLegacy("hid-hidraw3#beef:0001", "old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		writeLegacy("hid-hidraw9#beef:0001", "new", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
		writeLegacy("hid-hidraw4#beef:0002", "other", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
		string stableId = "usb-0000:0e:00.0-2.3.3:1.2#beef:0001";
		string newKey = DeviceSettingManager.GetDeviceKey(stableId);

		Assert.True(SettingsImporter.AdoptLegacy(stableId, 0xbeef, 0x0001));

		string adopted = gunzip(Path.Combine(LConnectCore.LConnectApp.Path.DeviceSettingDirectory, newKey, "lighting.0"));
		Assert.Contains("\"DevicePath\":\"" + stableId + "\"", adopted);
		Assert.Contains("\"Type\":\"new\"", adopted);
		Assert.Equal("new", gunzip(Path.Combine(lcdDirectory, newKey + ".LCD")));
		Assert.Equal("new", File.ReadAllText(Path.Combine(LConnectCore.LConnectApp.Path.ControllerProfileDirectory, newKey)));
		// Never overwrites settings the stable id already has, and never adopts for an unresolved id.
		Assert.False(SettingsImporter.AdoptLegacy(stableId, 0xbeef, 0x0001));
		Assert.False(SettingsImporter.AdoptLegacy("hid-hidraw5#beef:0001", 0xbeef, 0x0001));
	}

	[Fact]
	public void LinuxTopologyStableId()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}
		string text = Path.Combine(Path.GetTempPath(), "fake-sys-" + Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "devices", "pci0000:00", "0000:0e:00.0", "usb3", "3-2", "3-2.3", "3-2.3.3");
		string text3 = Path.Combine(text2, "3-2.3.3:1.2", "0003:0416:7372.0005");
		Directory.CreateDirectory(Path.Combine(text3, "hidraw", "hidraw7"));
		File.WriteAllText(Path.Combine(text2, "devpath"), "2.3.3\n");
		File.WriteAllText(Path.Combine(text2, "busnum"), "3\n");
		// Same relative links the kernel creates: the class entry is a symlink and "device" is relative to its target.
		Directory.CreateDirectory(Path.Combine(text, "class", "hidraw"));
		Directory.CreateSymbolicLink(Path.Combine(text, "class", "hidraw", "hidraw7"), "../../devices/pci0000:00/0000:0e:00.0/usb3/3-2/3-2.3/3-2.3.3/3-2.3.3:1.2/0003:0416:7372.0005/hidraw/hidraw7");
		Directory.CreateSymbolicLink(Path.Combine(text3, "hidraw", "hidraw7", "device"), "../../../0003:0416:7372.0005");
		string sysfsRoot = UsbTopology.SysfsRoot;
		try
		{
			UsbTopology.SysfsRoot = text;
			UsbTopology.Info info = UsbTopology.Describe("/dev/hidraw7", 1046, 29554, 2);
			Assert.Equal("usb-0000:0e:00.0-2.3.3:1.2#0416:7372", info.StableId);
			Assert.Equal("usb.002.003.003.000", info.LocationInfo);
			Assert.Equal("/dev/hidraw7", UsbTopology.ResolveOpenPath(info.StableId));
		}
		finally
		{
			UsbTopology.SysfsRoot = sysfsRoot;
			UsbTopology.Forget("/dev/hidraw7");
		}
	}
}
