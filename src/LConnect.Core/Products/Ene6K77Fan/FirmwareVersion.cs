using System;

namespace LConnectCore.Products.Ene6K77Fan;

public abstract class FirmwareVersion
{
	public bool IsValid { get; set; }

	public byte CustomerID { get; set; }

	public byte ProjectID { get; set; }

	public byte MajorID { get; set; }

	public byte MinorID { get; set; }

	public byte FineTune { get; set; }

	public abstract Version GetVersion();

	public abstract bool Load(byte[] data);
}
