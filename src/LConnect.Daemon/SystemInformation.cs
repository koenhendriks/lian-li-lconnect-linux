using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LConnectCore;
using LConnectService.Helpers;

namespace LConnectService;

// Replaces the CPUID SDK / GPU-Z based sensor reader with Linux sources:
//   CPU temperature : hwmon k10temp "Tctl" (AMD), zenpower "Tdie", coretemp "Package id 0" (Intel)
//   CPU load        : /proc/stat deltas
//   GPU             : NVML (libnvidia-ml.so.1) for NVIDIA, amdgpu hwmon/sysfs otherwise; like the original, a
//                     discrete GPU is preferred over the integrated one
//   RAM/network/disk: /proc/meminfo, /proc/net/dev, statvfs via DriveInfo
// For testing, LCONNECT_FAKE_CPU_TEMP / LCONNECT_FAKE_GPU_TEMP / LCONNECT_FAKE_CPU_LOAD / LCONNECT_FAKE_GPU_LOAD
// override readings, and a JSON file at $LCONNECT_DATA_DIR/sensor-override.json (same keys) does too.
internal static class SystemInformation
{
	private sealed class GPUInfo
	{
		public string Name;

		public bool IsIGP;

		public double? Temperature;

		public double? Clock;

		public double? Load;

		public double? FanSpeed;
	}

	private static readonly object locker = new object();

	private static bool isUpdating;

	private static (ulong Idle, ulong Total)? lastCpuTimes;

	private static string cpuTempPath;

	private static bool cpuTempSearched;

	private static Nvml nvml;

	private static bool nvmlTried;

	public static SystemResource Resource = new SystemResource
	{
		LastTime = DateTime.MinValue,
		Networks = new Dictionary<string, NetworkStatus>(),
		Storages = new Dictionary<string, StorageStatus>()
	};

	public static void Update()
	{
		lock (locker)
		{
			if (isUpdating)
			{
				return;
			}
			isUpdating = true;
		}
		try
		{
			updateCPU();
			updateGPU();
			updateRam();
			updateNetwork();
			updateStorage();
			applyOverrides();
			Resource.LastTime = DateTime.Now;
		}
		catch (Exception ex)
		{
			EventLogHelper.Debug("Sensor update failed: " + ex.Message);
		}
		finally
		{
			lock (locker)
			{
				isUpdating = false;
			}
		}
	}

	public static bool IsUpdating()
	{
		lock (locker)
		{
			return isUpdating;
		}
	}

	public static void Shutdown()
	{
		nvml?.Dispose();
		nvml = null;
	}

	private static string readText(string path)
	{
		try
		{
			return File.ReadAllText(path).Trim();
		}
		catch
		{
			return null;
		}
	}

	private static double? readMilli(string path)
	{
		string s = readText(path);
		if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result / 1000.0;
		}
		return null;
	}

	private static IEnumerable<string> hwmonDirs()
	{
		const string path = "/sys/class/hwmon";
		if (!Directory.Exists(path))
		{
			return Array.Empty<string>();
		}
		return Directory.GetDirectories(path).OrderBy((string d) => d, StringComparer.Ordinal);
	}

	private static string findCpuTempPath()
	{
		(string, string)[] array = new(string, string)[5]
		{
			("k10temp", "Tctl"),
			("k10temp", "Tdie"),
			("zenpower", "Tdie"),
			("coretemp", "Package id 0"),
			("cpu_thermal", null)
		};
		List<string> list = hwmonDirs().ToList();
		(string, string)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string, string) tuple = array2[i];
			foreach (string item in list)
			{
				if (readText(Path.Combine(item, "name")) != tuple.Item1)
				{
					continue;
				}
				foreach (string item2 in Directory.GetFiles(item, "temp*_input").OrderBy((string f) => f, StringComparer.Ordinal))
				{
					if (tuple.Item2 == null || readText(item2.Replace("_input", "_label")) == tuple.Item2)
					{
						return item2;
					}
				}
			}
		}
		return null;
	}

	private static void updateCPU()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}
		if (!cpuTempSearched)
		{
			cpuTempPath = findCpuTempPath();
			cpuTempSearched = true;
			EventLogHelper.Info("CPU temperature source: " + (cpuTempPath ?? "none"));
		}
		if (cpuTempPath != null)
		{
			double? num = readMilli(cpuTempPath);
			if (num.HasValue)
			{
				Resource.CPUTemperature = (int)Math.Round(num.Value);
				Resource.CPUTemperatureHasValue = true;
			}
		}
		try
		{
			string text = File.ReadLines("/proc/stat").First();
			ulong[] array = (from s in text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
				select ulong.Parse(s, CultureInfo.InvariantCulture)).ToArray();
			ulong num2 = array[3] + ((array.Length > 4) ? array[4] : 0);
			ulong num3 = 0uL;
			for (int i = 0; i < Math.Min(array.Length, 8); i++)
			{
				num3 += array[i];
			}
			if (lastCpuTimes.HasValue && num3 > lastCpuTimes.Value.Total)
			{
				double num4 = num3 - lastCpuTimes.Value.Total;
				double num5 = num2 - lastCpuTimes.Value.Idle;
				Resource.CPULoad = (int)Math.Ceiling(Math.Clamp((num4 - num5) / num4 * 100.0, 0.0, 100.0));
			}
			lastCpuTimes = (num2, num3);
		}
		catch
		{
		}
		try
		{
			double[] array2 = (from d in Directory.GetDirectories("/sys/devices/system/cpu", "cpu*")
				select readText(Path.Combine(d, "cpufreq", "scaling_cur_freq")) into s
				where s != null
				select double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
			if (array2.Length != 0)
			{
				Resource.CPUClockRate = (int)Math.Round(array2.Average() / 1000.0);
			}
		}
		catch
		{
		}
		try
		{
			foreach (string item in hwmonDirs())
			{
				foreach (string item2 in Directory.GetFiles(item, "fan*_label"))
				{
					string text2 = readText(item2);
					if (text2 != null && text2.Contains("cpu", StringComparison.OrdinalIgnoreCase))
					{
						string s2 = readText(item2.Replace("_label", "_input"));
						if (int.TryParse(s2, out var result))
						{
							Resource.CPUFanSpeed = result;
							return;
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void updateGPU()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}
		List<GPUInfo> list = new List<GPUInfo>();
		if (!nvmlTried)
		{
			nvmlTried = true;
			nvml = Nvml.TryCreate();
			EventLogHelper.Info("NVML: " + ((nvml != null) ? "available" : "not available"));
		}
		if (nvml != null)
		{
			list.AddRange(nvml.Read().Select(((string Name, double? Temp, double? Load, double? Clock, double? Fan) g) => new GPUInfo
			{
				Name = g.Name,
				IsIGP = false,
				Temperature = g.Temp,
				Load = g.Load,
				Clock = g.Clock,
				FanSpeed = g.Fan
			}));
		}
		list.AddRange(readAmdGpus());
		GPUInfo gPUInfo = null;
		foreach (GPUInfo item in list)
		{
			if (gPUInfo == null || (gPUInfo.IsIGP && !item.IsIGP))
			{
				gPUInfo = item;
			}
		}
		if (gPUInfo != null)
		{
			if (gPUInfo.Load.HasValue)
			{
				Resource.GPULoad = (int)Math.Ceiling(gPUInfo.Load.Value);
			}
			if (gPUInfo.Temperature.HasValue)
			{
				Resource.GPUTemperature = (int)Math.Round(gPUInfo.Temperature.Value);
				Resource.GPUTemperatureHasValue = true;
			}
			if (gPUInfo.Clock.HasValue)
			{
				Resource.GPUClockRate = (int)Math.Round(gPUInfo.Clock.Value);
			}
			if (gPUInfo.FanSpeed.HasValue)
			{
				Resource.GPUFanSpeed = (int)Math.Round(gPUInfo.FanSpeed.Value);
			}
		}
	}

	private static IEnumerable<GPUInfo> readAmdGpus()
	{
		List<GPUInfo> list = new List<GPUInfo>();
		foreach (string item in hwmonDirs())
		{
			if (readText(Path.Combine(item, "name")) != "amdgpu")
			{
				continue;
			}
			string path = Path.Combine(item, "device");
			GPUInfo gPUInfo = new GPUInfo
			{
				Name = "AMD GPU",
				Temperature = readMilli(Path.Combine(item, "temp1_input"))
			};
			if (double.TryParse(readText(Path.Combine(path, "gpu_busy_percent")), out var result))
			{
				gPUInfo.Load = result;
			}
			if (ulong.TryParse(readText(Path.Combine(path, "mem_info_vram_total")), out var result2))
			{
				// APUs report a small carve-out (typically <= 2 GiB) as VRAM.
				gPUInfo.IsIGP = result2 <= 2147483648u;
			}
			if (double.TryParse(readText(Path.Combine(item, "freq1_input")), out var result3))
			{
				gPUInfo.Clock = result3 / 1000000.0;
			}
			if (double.TryParse(readText(Path.Combine(item, "fan1_input")), out var result4))
			{
				gPUInfo.FanSpeed = result4;
			}
			list.Add(gPUInfo);
		}
		return list;
	}

	private static void updateRam()
	{
		MemoryStatus memoryStatus = MemoryStatus.Read();
		if (memoryStatus.TotalPhysical != 0)
		{
			Resource.RAMLoad = (int)Math.Round((double)memoryStatus.UsedPhysical / (double)memoryStatus.TotalPhysical * 100.0);
			Resource.RAMUsed = memoryStatus.UsedPhysical;
			Resource.RAMTotal = memoryStatus.TotalPhysical;
		}
	}

	private static void updateNetwork()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}
		try
		{
			foreach (string item in File.ReadLines("/proc/net/dev").Skip(2))
			{
				string[] array = item.Split(':', 2);
				if (array.Length != 2)
				{
					continue;
				}
				string text = array[0].Trim();
				if (!(text == "lo"))
				{
					string[] array2 = array[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
					ulong num = ulong.Parse(array2[0], CultureInfo.InvariantCulture);
					ulong num2 = ulong.Parse(array2[8], CultureInfo.InvariantCulture);
					if (Resource.Networks.TryGetValue(text, out var value))
					{
						value.Update(num, num2);
					}
					else
					{
						Resource.Networks[text] = new NetworkStatus(num, num2);
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void updateStorage()
	{
		try
		{
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo driveInfo in drives)
			{
				if (driveInfo.DriveType != DriveType.Fixed || !driveInfo.IsReady)
				{
					continue;
				}
				if (OperatingSystem.IsLinux() && !(driveInfo.Name == "/") && !driveInfo.Name.StartsWith("/home", StringComparison.Ordinal) && !driveInfo.Name.StartsWith("/mnt", StringComparison.Ordinal) && !driveInfo.Name.StartsWith("/run/media", StringComparison.Ordinal))
				{
					continue;
				}
				string key = (OperatingSystem.IsWindows() ? driveInfo.Name.Split(':', 2)[0] : driveInfo.Name);
				if (Resource.Storages.TryGetValue(key, out var value))
				{
					value.TotalFreeSpace = driveInfo.TotalFreeSpace;
					value.TotalSize = driveInfo.TotalSize;
					continue;
				}
				Resource.Storages[key] = new StorageStatus
				{
					TotalFreeSpace = driveInfo.TotalFreeSpace,
					TotalSize = driveInfo.TotalSize
				};
			}
		}
		catch
		{
		}
	}

	private static void applyOverrides()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		try
		{
			string path = Path.Combine(LConnectApp.Path.DataRoot, "sensor-override.json");
			if (File.Exists(path))
			{
				foreach (KeyValuePair<string, int> item in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? new Dictionary<string, int>())
				{
					dictionary[item.Key] = item.Value;
				}
			}
		}
		catch
		{
		}
		string[] array = new string[4] { "CPU_TEMP", "GPU_TEMP", "CPU_LOAD", "GPU_LOAD" };
		foreach (string text in array)
		{
			if (int.TryParse(Environment.GetEnvironmentVariable("LCONNECT_FAKE_" + text), out var result))
			{
				dictionary[text] = result;
			}
		}
		if (dictionary.TryGetValue("CPU_TEMP", out var value))
		{
			Resource.CPUTemperature = value;
			Resource.CPUTemperatureHasValue = true;
		}
		if (dictionary.TryGetValue("GPU_TEMP", out var value2))
		{
			Resource.GPUTemperature = value2;
			Resource.GPUTemperatureHasValue = true;
		}
		if (dictionary.TryGetValue("CPU_LOAD", out var value3))
		{
			Resource.CPULoad = value3;
		}
		if (dictionary.TryGetValue("GPU_LOAD", out var value4))
		{
			Resource.GPULoad = value4;
		}
	}

	public static SystemSpecification GetSystemSpecification()
	{
		SystemSpecification result = default;
		try
		{
			if (OperatingSystem.IsLinux())
			{
				string text = (from l in File.ReadLines("/proc/cpuinfo")
					where l.StartsWith("model name", StringComparison.Ordinal)
					select l.Split(':', 2)[1].Trim()).FirstOrDefault();
				string text2 = (from l in File.ReadLines("/proc/cpuinfo")
					where l.StartsWith("vendor_id", StringComparison.Ordinal)
					select l.Split(':', 2)[1].Trim()).FirstOrDefault();
				result.CPUs = new SystemSpecification.CPUSpec[1]
				{
					new SystemSpecification.CPUSpec
					{
						Name = text,
						Manufacturer = ((text2 == "AuthenticAMD") ? "AMD" : ((text2 == "GenuineIntel") ? "Intel" : text2)),
						CodeName = string.Empty,
						TDP = float.MinValue,
						StockClockFrequency = float.MinValue
					}
				};
				if (nvml != null)
				{
					result.GPUs = (from g in nvml.Read()
						select new SystemSpecification.GPUSpec
						{
							Name = g.Name,
							CodeName = string.Empty,
							MemorySize = int.MinValue,
							MemoryBusWidth = int.MinValue
						}).ToArray();
				}
				result.Motherboard = new SystemSpecification.MotherboardSpec
				{
					Model = readText("/sys/class/dmi/id/board_name"),
					Vendor = readText("/sys/class/dmi/id/board_vendor"),
					Revision = readText("/sys/class/dmi/id/board_version"),
					SerialNumber = string.Empty
				};
				MemoryStatus memoryStatus = MemoryStatus.Read();
				result.Memories = new SystemSpecification.MemorySpec[1]
				{
					new SystemSpecification.MemorySpec
					{
						Size = (int)(memoryStatus.TotalPhysical / 1048576),
						Manufacturer = string.Empty,
						Specification = string.Empty,
						Format = string.Empty,
						SerialNumber = string.Empty
					}
				};
				result.IsValid = true;
			}
		}
		catch
		{
		}
		return result;
	}

	// Minimal NVML binding (libnvidia-ml.so.1 ships with the NVIDIA driver / nvidia-utils).
	private sealed class Nvml : IDisposable
	{
		[StructLayout(LayoutKind.Sequential)]
		private struct Utilization
		{
			public uint Gpu;

			public uint Memory;
		}

		private delegate int InitFn();

		private delegate int ShutdownFn();

		private delegate int CountFn(out uint count);

		private delegate int HandleFn(uint index, out IntPtr device);

		private delegate int NameFn(IntPtr device, byte[] name, uint length);

		private delegate int TempFn(IntPtr device, int sensor, out uint temp);

		private delegate int UtilFn(IntPtr device, out Utilization util);

		private delegate int ClockFn(IntPtr device, int type, out uint clock);

		private delegate int FanFn(IntPtr device, out uint speed);

		private readonly IntPtr lib;

		private readonly ShutdownFn shutdown;

		private readonly CountFn count;

		private readonly HandleFn handle;

		private readonly NameFn name;

		private readonly TempFn temp;

		private readonly UtilFn util;

		private readonly ClockFn clock;

		private readonly FanFn fan;

		private Nvml(IntPtr lib)
		{
			this.lib = lib;
			shutdown = get<ShutdownFn>("nvmlShutdown");
			count = get<CountFn>("nvmlDeviceGetCount_v2");
			handle = get<HandleFn>("nvmlDeviceGetHandleByIndex_v2");
			name = get<NameFn>("nvmlDeviceGetName");
			temp = get<TempFn>("nvmlDeviceGetTemperature");
			util = get<UtilFn>("nvmlDeviceGetUtilizationRates");
			clock = get<ClockFn>("nvmlDeviceGetClockInfo");
			fan = get<FanFn>("nvmlDeviceGetFanSpeed");
		}

		private T get<T>(string symbol) where T : Delegate
		{
			return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, symbol));
		}

		public static Nvml TryCreate()
		{
			if (!NativeLibrary.TryLoad("libnvidia-ml.so.1", out var handle))
			{
				return null;
			}
			try
			{
				InitFn delegateForFunctionPointer = Marshal.GetDelegateForFunctionPointer<InitFn>(NativeLibrary.GetExport(handle, "nvmlInit_v2"));
				if (delegateForFunctionPointer() != 0)
				{
					NativeLibrary.Free(handle);
					return null;
				}
				return new Nvml(handle);
			}
			catch
			{
				NativeLibrary.Free(handle);
				return null;
			}
		}

		public List<(string Name, double? Temp, double? Load, double? Clock, double? Fan)> Read()
		{
			List<(string, double?, double?, double?, double?)> list = new List<(string, double?, double?, double?, double?)>();
			if (count(out var num) != 0)
			{
				return list;
			}
			for (uint num2 = 0u; num2 < num; num2++)
			{
				if (handle(num2, out var intPtr) == 0)
				{
					byte[] array = new byte[96];
					string item = ((name(intPtr, array, (uint)array.Length) == 0) ? System.Text.Encoding.ASCII.GetString(array).TrimEnd(default(char)) : "NVIDIA GPU");
					double? item2 = ((temp(intPtr, 0, out var num3) == 0) ? new double?(num3) : null);
					double? item3 = ((util(intPtr, out var utilization) == 0) ? new double?(utilization.Gpu) : null);
					double? item4 = ((clock(intPtr, 0, out var num4) == 0) ? new double?(num4) : null);
					double? item5 = ((fan(intPtr, out var num5) == 0) ? new double?(num5) : null);
					list.Add((item, item2, item3, item4, item5));
				}
			}
			return list;
		}

		public void Dispose()
		{
			try
			{
				shutdown();
			}
			catch
			{
			}
			NativeLibrary.Free(lib);
		}
	}
}
