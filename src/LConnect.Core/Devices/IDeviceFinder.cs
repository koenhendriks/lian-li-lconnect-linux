using System.Collections.Generic;

namespace LConnectCore.Devices;

public interface IDeviceFinder
{
	IList<DeviceFinderResult> Find();

	IList<DeviceFinderResult> Find(ushort vendorId, ushort productId);

	IList<DeviceFinderResult> Find(ushort vendorId, ushort productId, ushort? usagePage, ushort? usage);

	IList<DeviceFinderResult> Find(ControllerType controllerType);
}
