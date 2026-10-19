using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LConnectCore.Platform;

// Maps between the path hidapi needs to open a device and the "device path" L-Connect uses as identity.
//
// The original code uses the Windows HID interface path as identity everywhere (settings folder = md5(path),
// controller dictionaries, HTTP requests). On Windows we keep that. On Linux the hidapi path is /dev/hidrawN,
// which changes between boots, so we use a stable id built from the PCI host controller + USB port chain +
// interface number, e.g. "usb-0000:0e:00.0-2.3.3:1.2#0416:7372".
public static class UsbTopology
{
	public sealed class Info
	{
		public string OpenPath { get; init; }

		public string StableId { get; init; }

		public string LocationInfo { get; init; }

		public string LocationPaths { get; init; }

		public int[] PortChain { get; init; } = Array.Empty<int>();
	}

	private static readonly ConcurrentDictionary<string, Info> byOpenPath = new ConcurrentDictionary<string, Info>(StringComparer.Ordinal);

	private static readonly ConcurrentDictionary<string, string> stableToOpen = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly Regex PciAddress = new Regex("^[0-9a-f]{4}:[0-9a-f]{2}:[0-9a-f]{2}\\.[0-9a-f]$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex UsbInterfaceDir = new Regex("^(\\d+)-([\\d.]+):(\\d+)\\.(\\d+)$", RegexOptions.Compiled);

	public static string SysfsRoot { get; set; } = "/sys";

	public static Info Describe(string openPath, ushort vendorId, ushort productId, int interfaceNumber)
	{
		if (string.IsNullOrEmpty(openPath))
		{
			return null;
		}
		Info info = byOpenPath.GetOrAdd(openPath, (string p) => OperatingSystem.IsLinux() ? DescribeLinux(p, vendorId, productId, interfaceNumber) : DescribeOther(p));
		stableToOpen[info.StableId] = info.OpenPath;
		return info;
	}

	public static string ResolveOpenPath(string devicePath)
	{
		if (string.IsNullOrEmpty(devicePath))
		{
			return devicePath;
		}
		if (stableToOpen.TryGetValue(devicePath, out var value))
		{
			return value;
		}
		return devicePath;
	}

	public static void Forget(string openPath)
	{
		if (openPath != null && byOpenPath.TryRemove(openPath, out var value))
		{
			stableToOpen.TryRemove(value.StableId, out var _);
		}
	}

	// The original TLLCDManager parses LocationInfo with int.Parse on every dot-separated token. On Windows the
	// first token is hex ("000a"), which throws, so every location decodes to all zeros and LCD fans are matched
	// to the controller purely by the port number the LCD firmware reports (ReadSerialNumber). We keep a
	// non-numeric first token so the ported code behaves identically.
	private static string BuildLocationInfo(int[] chain)
	{
		return "usb." + string.Join(".", chain.Select((int p) => p.ToString("000"))) + ".000";
	}

	// realpath(3) semantics. ResolveLinkTarget resolves a relative target against the link's path as written, but
	// /sys/class/hidraw/hidrawN is itself a symlink, so its "device -> ../../../0003:..." must be resolved against
	// the real directory. Getting this wrong yields "/sys/0003:..." and a per-boot "hid-hidrawN" identity.
	private static string RealPath(string path, int depth = 0)
	{
		if (depth > 40)
		{
			throw new IOException("Too many levels of symbolic links: " + path);
		}
		string fullPath = Path.GetFullPath(path);
		string text = Path.GetPathRoot(fullPath);
		foreach (string item in fullPath.Substring(text.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
		{
			string text2 = Path.Combine(text, item);
			string linkTarget = new FileInfo(text2).LinkTarget;
			text = ((linkTarget == null) ? text2 : RealPath(Path.GetFullPath(linkTarget, text), depth + 1));
		}
		return text;
	}

	private static Info DescribeOther(string openPath)
	{
		return new Info
		{
			OpenPath = openPath,
			StableId = openPath.ToLowerInvariant(),
			LocationInfo = BuildLocationInfo(Array.Empty<int>()),
			LocationPaths = string.Empty
		};
	}

	private static Info DescribeLinux(string openPath, ushort vendorId, ushort productId, int interfaceNumber)
	{
		string text = null;
		try
		{
			string fileName = Path.GetFileName(openPath);
			if (openPath.StartsWith("/dev/hidraw", StringComparison.Ordinal))
			{
				text = RealPath(Path.Combine(SysfsRoot, "class", "hidraw", fileName, "device"));
			}
			else if (UsbInterfaceDir.IsMatch(fileName))
			{
				text = RealPath(Path.Combine(SysfsRoot, "bus", "usb", "devices", fileName));
			}
		}
		catch
		{
		}
		string text2 = null;
		string text3 = null;
		string text4 = null;
		for (DirectoryInfo directoryInfo = (text != null) ? new DirectoryInfo(text) : null; directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (text2 == null && UsbInterfaceDir.IsMatch(directoryInfo.Name))
			{
				text2 = directoryInfo.Name;
			}
			if (text3 == null && File.Exists(Path.Combine(directoryInfo.FullName, "devpath")) && File.Exists(Path.Combine(directoryInfo.FullName, "busnum")))
			{
				text3 = directoryInfo.FullName;
			}
			if (PciAddress.IsMatch(directoryInfo.Name))
			{
				text4 = directoryInfo.Name.ToLowerInvariant();
				break;
			}
		}
		int[] array = Array.Empty<int>();
		string text5 = null;
		string text6 = null;
		if (text2 != null)
		{
			Match match = UsbInterfaceDir.Match(text2);
			text5 = match.Groups[2].Value;
			text6 = match.Groups[3].Value + "." + match.Groups[4].Value;
			array = text5.Split('.').Select(int.Parse).ToArray();
		}
		else if (text3 != null)
		{
			text5 = File.ReadAllText(Path.Combine(text3, "devpath")).Trim();
			array = text5.Split('.').Select(int.Parse).ToArray();
		}
		string text7 = ((text5 == null) ? ("hid-" + Path.GetFileName(openPath)) : ("usb-" + (text4 ?? "nopci") + "-" + text5 + ":" + (text6 ?? $"1.{interfaceNumber}")));
		string stableId = $"{text7}#{vendorId:x4}:{productId:x4}".ToLowerInvariant();
		return new Info
		{
			OpenPath = openPath,
			StableId = stableId,
			PortChain = array,
			LocationInfo = BuildLocationInfo(array),
			LocationPaths = ((text4 == null) ? string.Empty : ("pciroot(0)#pci(" + text4 + ")#usbroot(0)" + string.Concat(array.Select((int p) => $"#usb({p})")) + $"#usbmi({interfaceNumber})"))
		};
	}
}
