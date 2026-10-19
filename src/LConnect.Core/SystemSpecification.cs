namespace LConnectCore;

public struct SystemSpecification
{
	public struct CPUSpec
	{
		public string Name { get; set; }

		public string Manufacturer { get; set; }

		public string CodeName { get; set; }

		public float StockClockFrequency { get; set; }

		public float TDP { get; set; }
	}

	public struct GPUSpec
	{
		public string Name { get; set; }

		public string CodeName { get; set; }

		public int MemorySize { get; set; }

		public int MemoryBusWidth { get; set; }
	}

	public struct MonitorSpec
	{
		public string Name { get; set; }

		public string ID { get; set; }

		public string Vendor { get; set; }

		public string Serial { get; set; }

		public float Size { get; set; }

		public int MaxPixelClock { get; set; }

		public int ManufacturingDateYear { get; set; }

		public int ManufacturingDateWeek { get; set; }
	}

	public struct MotherboardSpec
	{
		public string Model { get; set; }

		public string Revision { get; set; }

		public string Vendor { get; set; }

		public string SerialNumber { get; set; }
	}

	public struct MemorySpec
	{
		public string Manufacturer { get; set; }

		public string Specification { get; set; }

		public string Format { get; set; }

		public int Size { get; set; }

		public string SerialNumber { get; set; }
	}

	public struct StorageSpec
	{
		public string Name { get; set; }

		public string SerialNumber { get; set; }

		public string Revision { get; set; }

		public string BusType { get; set; }

		public int RotationSpeed { get; set; }

		public float TotalCapacity { get; set; }
	}

	public bool IsValid { get; set; }

	public CPUSpec[] CPUs { get; set; }

	public GPUSpec[] GPUs { get; set; }

	public MonitorSpec[] Monitors { get; set; }

	public MotherboardSpec Motherboard { get; set; }

	public MemorySpec[] Memories { get; set; }

	public StorageSpec[] Storages { get; set; }
}
