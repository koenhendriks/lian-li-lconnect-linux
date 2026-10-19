using System;

namespace LConnectCore.Products.TLLCD;

public struct SerialNumberInfo
{
	public bool IsValid { get; set; }

	public byte[] SerialNumber { get; set; }

	public int Port { get; set; }

	public int Index { get; set; }

	public static bool TryParse(byte[] data, out SerialNumberInfo info)
	{
		info = default;
		if (data == null || data.Length < 34)
		{
			return false;
		}
		info = new SerialNumberInfo
		{
			IsValid = true,
			SerialNumber = new byte[32],
			Port = data[32],
			Index = data[33]
		};
		Array.Copy(data, 0, info.SerialNumber, 0, info.SerialNumber.Length);
		return true;
	}
}
