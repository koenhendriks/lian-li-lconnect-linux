using System;

namespace LConnectCore.Products.TLFan;

public struct TLFanHandshakeInfo
{
	public struct FanInfo
	{
		public bool IsDetected { get; private set; }

		public bool IsUpgrading { get; private set; }

		public int Port { get; private set; }

		public int FanIndex { get; private set; }

		public int FanSpeed { get; private set; }

		public FanInfo(ReadOnlySpan<byte> data)
		{
			IsDetected = Convert.ToBoolean(data[0] & 0x80);
			IsUpgrading = Convert.ToBoolean(data[0] & 0x40);
			Port = (data[0] >> 4) & 3;
			FanIndex = data[0] & 0xF;
			FanSpeed = (data[1] << 8) | data[2];
		}
	}

	public const int ParamLength = 3;

	public bool IsValid { get; private set; } = false;

	public FanInfo[,] FanInfos { get; private set; } = new FanInfo[4, 16];

	public int[] FanQuantity { get; private set; } = new int[4];

	public TLFanHandshakeInfo()
	{
	}

	public static bool TryParse(byte[] data, out TLFanHandshakeInfo handshakeInfo)
	{
		handshakeInfo = new TLFanHandshakeInfo();
		if (data == null)
		{
			return false;
		}
		int num = data.Length / 3;
		ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(data);
		for (int i = 0; i < num; i++)
		{
			FanInfo fanInfo = new FanInfo(readOnlySpan.Slice(i * 3, 3));
			if (fanInfo.IsDetected)
			{
				handshakeInfo.FanInfos[fanInfo.Port, fanInfo.FanIndex] = fanInfo;
				handshakeInfo.FanQuantity[fanInfo.Port] = Math.Max(fanInfo.FanIndex + 1, handshakeInfo.FanQuantity[fanInfo.Port]);
			}
		}
		handshakeInfo.IsValid = true;
		return true;
	}
}
