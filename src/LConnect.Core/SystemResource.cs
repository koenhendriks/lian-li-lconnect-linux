using System;
using System.Collections.Generic;

namespace LConnectCore;

public struct SystemResource
{
	public DateTime LastTime { get; set; }

	public int CPULoad { get; set; }

	public int CPUTemperature { get; set; }

	public bool CPUTemperatureHasValue { get; set; }

	public int CPUClockRate { get; set; }

	public int CPUFanSpeed { get; set; }

	public int GPULoad { get; set; }

	public int GPUTemperature { get; set; }

	public bool GPUTemperatureHasValue { get; set; }

	public int GPUClockRate { get; set; }

	public int? GPUFanSpeed { get; set; }

	public int RAMLoad { get; set; }

	public ulong RAMUsed { get; set; }

	public ulong RAMTotal { get; set; }

	public Dictionary<string, NetworkStatus> Networks { get; set; }

	public Dictionary<string, StorageStatus> Storages { get; set; }
}
