namespace LConnectCore.Devices;

public interface IHidDevice
{
	string BinaryLogPath { get; set; }

	bool IsOpen { get; }

	string DevicePath { get; }

	ushort VendorID { get; }

	ushort ProductID { get; }

	string SerialNumber { get; }

	string Manufacturer { get; }

	string Product { get; }

	ushort UsagePage { get; }

	ushort Usage { get; }

	int InterfaceNumber { get; }

	string LocationInfo { get; }

	string LocationPaths { get; }

	int ReadTimeout { get; set; }

	int WriteTimeout { get; set; }

	void Open();

	void Close();

	int Read(out byte[] data, int length);

	int Read(out byte[] data, int length, int timeout);

	int Write(byte[] data, int length);

	int SendFeatureReport(byte[] data, int length);

	int GetFeatureReport(ref byte[] data, int length);

	int GetInputReport(ref byte[] data, int length);
}
