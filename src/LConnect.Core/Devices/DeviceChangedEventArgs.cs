using System;

namespace LConnectCore.Devices;

public class DeviceChangedEventArgs : EventArgs
{
	public USBDeviceID DeviceID { get; }

	public string DevicePath { get; }

	public DeviceChangedEventArgs(USBDeviceID deviceID, string devicePath)
	{
		DeviceID = deviceID;
		DevicePath = devicePath;
	}
}
