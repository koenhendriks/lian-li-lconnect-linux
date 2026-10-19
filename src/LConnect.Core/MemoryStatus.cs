using System;
using System.IO;

namespace LConnectCore;

public struct MemoryStatus
{
	public ulong TotalPhysical { get; private set; }

	public ulong AvailablePhysical { get; private set; }

	public ulong UsedPhysical { get; private set; }

	public static MemoryStatus Read()
	{
		MemoryStatus result = default;
		try
		{
			if (OperatingSystem.IsLinux())
			{
				ulong num = 0uL;
				ulong num2 = 0uL;
				foreach (string item in File.ReadLines("/proc/meminfo"))
				{
					string[] array = item.Split(new char[2] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
					if (array.Length >= 2)
					{
						if (array[0] == "MemTotal")
						{
							num = ulong.Parse(array[1]) * 1024;
						}
						else if (array[0] == "MemAvailable")
						{
							num2 = ulong.Parse(array[1]) * 1024;
						}
					}
				}
				result.TotalPhysical = num;
				result.AvailablePhysical = num2;
			}
			else
			{
				GCMemoryInfo gCMemoryInfo = GC.GetGCMemoryInfo();
				result.TotalPhysical = (ulong)gCMemoryInfo.TotalAvailableMemoryBytes;
				result.AvailablePhysical = (ulong)Math.Max(0L, gCMemoryInfo.TotalAvailableMemoryBytes - gCMemoryInfo.MemoryLoadBytes);
			}
			result.UsedPhysical = result.TotalPhysical - result.AvailablePhysical;
		}
		catch
		{
		}
		return result;
	}
}
