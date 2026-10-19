namespace LConnectCore.Devices;

public struct USBDeviceID(int vid, int pid)
{
	public static readonly USBDeviceID Empty = new USBDeviceID(int.MinValue, int.MinValue);

	public int VendorID = vid;

	public int ProductID = pid;

	public static bool operator ==(USBDeviceID a, USBDeviceID b)
	{
		return a.Equals(b);
	}

	public static bool operator !=(USBDeviceID a, USBDeviceID b)
	{
		return !a.Equals(b);
	}

	public override bool Equals(object obj)
	{
		if (obj != null && obj is USBDeviceID uSBDeviceID && VendorID == uSBDeviceID.VendorID)
		{
			return ProductID == uSBDeviceID.ProductID;
		}
		return false;
	}

	public override int GetHashCode()
	{
		int num = VendorID & 0xFFFF;
		int num2 = ProductID & 0xFFFF;
		return ((num << 16) | num2).GetHashCode();
	}

	public override string ToString()
	{
		return $"VID=0x{VendorID:x04}&PID=0x{ProductID:x04}";
	}
}
