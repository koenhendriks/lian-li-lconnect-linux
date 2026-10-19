using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LConnectCore.Native;
using LConnectCore.Platform;

namespace LConnectCore.Devices;

public sealed class HidApiDeviceFinder : IDeviceFinder
{
	public IList<DeviceFinderResult> Find()
	{
		return Find(0, 0);
	}

	public IList<DeviceFinderResult> Find(ushort vendorId, ushort productId)
	{
		return Find(vendorId, productId, null, null);
	}

	public unsafe IList<DeviceFinderResult> Find(ushort vendorId, ushort productId, ushort? usagePage, ushort? usage)
	{
		if (Simulation.HidSimulator.Enabled)
		{
			return Simulation.HidSimulator.Find(vendorId, productId);
		}
		List<DeviceFinderResult> list = new List<DeviceFinderResult>();
		try
		{
			HidApi.DeviceInfo* ptr = HidApi.Enumerate(vendorId, productId);
			for (HidApi.DeviceInfo* ptr2 = ptr; ptr2 != null; ptr2 = ptr2->Next)
			{
				if ((usagePage.HasValue && usagePage.Value == ptr2->UsagePage && usage.HasValue && usage.Value == ptr2->Usage) || (usagePage.HasValue && usagePage.Value == ptr2->UsagePage) || (usage.HasValue && usage.Value == ptr2->Usage) || (!usagePage.HasValue && !usage.HasValue))
				{
					USBDeviceID uSBDeviceID = new USBDeviceID(ptr2->VendorID, ptr2->ProductID);
					ControllerType controllerType = KnownDevices.GetControllerType(uSBDeviceID);
					if (controllerType != ControllerType.None)
					{
						string openPath = Marshal.PtrToStringUTF8(ptr2->Path);
						UsbTopology.Info info = UsbTopology.Describe(openPath, ptr2->VendorID, ptr2->ProductID, ptr2->InterfaceNumber);
						list.Add(new DeviceFinderResult
						{
							ControllerType = controllerType,
							USBDeviceID = uSBDeviceID,
							DevicePath = info?.StableId,
							UsagePage = ptr2->UsagePage,
							Usage = ptr2->Usage,
							InterfaceNumber = ptr2->InterfaceNumber,
							LocationInfo = info?.LocationInfo?.ToLowerInvariant(),
							LocationPaths = info?.LocationPaths?.ToLowerInvariant()
						});
					}
				}
			}
			// The original freed the list starting from the terminating null pointer, leaking it. Free the head.
			HidApi.FreeEnumeration(ptr);
		}
		catch
		{
		}
		return list;
	}

	public IList<DeviceFinderResult> Find(ControllerType controllerType)
	{
		return (from result in Find()
			where result.ControllerType == controllerType
			select result).ToList();
	}
}
