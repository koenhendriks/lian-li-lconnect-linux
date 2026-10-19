using System;
using LConnect3cs.Utils;

namespace LConnect3cs;

internal class SystemStatus : ViewModelBase
{
	private double cpuLoad;

	private double cpuTemperature;

	private double cpuClockRate;

	private double cpuFanSpeed;

	private double gpuLoad;

	private double gpuTemperature;

	private double gpuClockRate;

	private static readonly Lazy<SystemStatus> lazy = new Lazy<SystemStatus>(() => new SystemStatus());

	public double CPULoad
	{
		get
		{
			return cpuLoad;
		}
		set
		{
			cpuLoad = value;
			onPropertyChanged("CPULoad");
		}
	}

	public double CPUTemperature
	{
		get
		{
			return cpuTemperature;
		}
		set
		{
			cpuTemperature = value;
			onPropertyChanged("CPUTemperature");
		}
	}

	public double CPUClockRate
	{
		get
		{
			return cpuClockRate;
		}
		set
		{
			cpuClockRate = value;
			onPropertyChanged("CPUClockRate");
		}
	}

	public double CPUFanSpeed
	{
		get
		{
			return cpuFanSpeed;
		}
		set
		{
			cpuFanSpeed = value;
			onPropertyChanged("CPUFanSpeed");
		}
	}

	public double GPULoad
	{
		get
		{
			return gpuLoad;
		}
		set
		{
			gpuLoad = value;
			onPropertyChanged("GPULoad");
		}
	}

	public double GPUTemperature
	{
		get
		{
			return gpuTemperature;
		}
		set
		{
			gpuTemperature = value;
			onPropertyChanged("GPUTemperature");
		}
	}

	public double GPUClockRate
	{
		get
		{
			return gpuClockRate;
		}
		set
		{
			gpuClockRate = value;
			onPropertyChanged("GPUClockRate");
		}
	}

	public static SystemStatus Instance => lazy.Value;
}
