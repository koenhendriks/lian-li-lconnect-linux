using System.Collections.Generic;

namespace LConnectCore.Devices;

public static class KnownDevices
{
	private static readonly Dictionary<ControllerType, USBDeviceID> mapping;

	private static readonly Dictionary<USBDeviceID, ControllerType> reverseMapping;

	public static readonly USBDeviceID TLControllerUpgradeToolUSBDeviceID;

	static KnownDevices()
	{
		mapping = new Dictionary<ControllerType, USBDeviceID>
		{
			[ControllerType.SLFan] = new USBDeviceID(3314, 41216),
			[ControllerType.ALFan] = new USBDeviceID(3314, 41217),
			[ControllerType.SLInfinity] = new USBDeviceID(3314, 41218),
			[ControllerType.SLV2Fan] = new USBDeviceID(3314, 41219),
			[ControllerType.ALV2Fan] = new USBDeviceID(3314, 41220),
			[ControllerType.SLV2AFan] = new USBDeviceID(3314, 41221),
			[ControllerType.StrimerPlus] = new USBDeviceID(3314, 41472),
			[ControllerType.Galahad2TrinityP] = new USBDeviceID(1046, 29553),
			[ControllerType.Galahad2TrinityR] = new USBDeviceID(1046, 29555),
			[ControllerType.Galahad2LCD] = new USBDeviceID(1046, 29585),
			[ControllerType.Galahad2LCDV2] = new USBDeviceID(1046, 29589),
			[ControllerType.TLFan] = new USBDeviceID(1046, 29554),
			[ControllerType.TLLCD] = new USBDeviceID(1276, 29587)
		};
		reverseMapping = new Dictionary<USBDeviceID, ControllerType>();
		TLControllerUpgradeToolUSBDeviceID = new USBDeviceID(1046, 16129);
		foreach (KeyValuePair<ControllerType, USBDeviceID> item in mapping)
		{
			reverseMapping.Add(item.Value, item.Key);
		}
	}

	public static IEnumerable<USBDeviceID> GetDeviceIDs()
	{
		foreach (USBDeviceID value in mapping.Values)
		{
			yield return value;
		}
	}

	public static USBDeviceID? GetDeviceID(ControllerType type)
	{
		if (mapping.ContainsKey(type))
		{
			return mapping[type];
		}
		return null;
	}

	public static ControllerType GetControllerType(USBDeviceID deviceID)
	{
		if (reverseMapping.ContainsKey(deviceID))
		{
			return reverseMapping[deviceID];
		}
		return ControllerType.None;
	}
}
