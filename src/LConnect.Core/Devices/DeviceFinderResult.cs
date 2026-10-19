namespace LConnectCore.Devices;

public struct DeviceFinderResult
{
	public ControllerType ControllerType;

	public USBDeviceID USBDeviceID;

	public ushort UsagePage;

	public ushort Usage;

	public int InterfaceNumber;

	public string DevicePath;

	public string LocationInfo;

	public string LocationPaths;
}
