using System;

namespace LConnectCore.Products.Ene6K77Fan;

public class ALFanFirmwareVersion : FirmwareVersion
{
	public override Version GetVersion()
	{
		if (!IsValid)
		{
			return null;
		}
		int num = (FineTune >> 4) & 0xF;
		int num2 = FineTune & 0xF;
		if (num2 > 9)
		{
			return new Version(0, 0);
		}
		int num3 = ((FineTune < 8) ? 10 : (num * 10 + num2 + 2));
		int major = num3 / 10;
		int minor = num3 % 10;
		return new Version(major, minor);
	}

	public override bool Load(byte[] data)
	{
		if (data == null || data.Length < 5)
		{
			return false;
		}
		CustomerID = data[0];
		ProjectID = data[1];
		MajorID = data[2];
		MinorID = data[3];
		FineTune = data[4];
		IsValid = CustomerID == 224 && ProjectID == 80 && MajorID == 128 && MinorID == 195;
		return IsValid;
	}
}
