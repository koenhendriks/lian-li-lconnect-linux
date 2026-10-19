using System;
using System.Collections.Generic;
using LConnectCore.Devices;

namespace LConnectCore.Products.TLLCD;

public class LCDPacket : Packet
{
	public const byte ReportID = 2;

	public const int PacketLength_Output = 512;

	public const int PacketLength_Input = 64;

	public const int HeaderLength = 11;

	public const int PayloadLength_Output = 501;

	public byte Command
	{
		get
		{
			return bytes[1];
		}
		set
		{
			bytes[1] = value;
		}
	}

	public int DataSize
	{
		get
		{
			return (bytes[2] << 24) | (bytes[3] << 16) | (bytes[4] << 8) | bytes[5];
		}
		set
		{
			bytes[2] = (byte)((value >> 24) & 0xFF);
			bytes[3] = (byte)((value >> 16) & 0xFF);
			bytes[4] = (byte)((value >> 8) & 0xFF);
			bytes[5] = (byte)(value & 0xFF);
		}
	}

	public int PacketNumber
	{
		get
		{
			return (bytes[6] << 16) | (bytes[7] << 8) | bytes[8];
		}
		set
		{
			bytes[6] = (byte)((value >> 16) & 0xFF);
			bytes[7] = (byte)((value >> 8) & 0xFF);
			bytes[8] = (byte)(value & 0xFF);
		}
	}

	public int Length
	{
		get
		{
			return (bytes[9] << 8) | bytes[10];
		}
		set
		{
			value = Math.Min(value, 501);
			bytes[9] = (byte)((value >> 8) & 0xFF);
			bytes[10] = (byte)(value & 0xFF);
		}
	}

	public byte[] Data
	{
		get
		{
			if (Length == 0)
			{
				return Array.Empty<byte>();
			}
			int num = Math.Min(Length, 501);
			byte[] array = new byte[num];
			Array.Copy(bytes, 11, array, 0, num);
			return array;
		}
		set
		{
			Array.Clear(bytes, 11, 0);
			int length = 0;
			if (value != null)
			{
				length = Math.Min(value.Length, 501);
				Array.Copy(value, 0, bytes, 11, length);
			}
			Length = length;
		}
	}

	public LCDPacket()
		: base(512)
	{
		bytes = new byte[512];
		bytes[0] = 2;
	}

	public static bool TryParse(byte[] packetBytes, out LCDPacket packet)
	{
		packet = null;
		if (packetBytes == null || packetBytes.Length < 11 || packetBytes[0] != 2)
		{
			return false;
		}
		packet = new LCDPacket();
		int length = Math.Min(packetBytes.Length, 512);
		Array.Copy(packetBytes, packet.bytes, length);
		return true;
	}

	public static IEnumerable<Packet> BuildPacketsForWrite(byte command, byte[] data)
	{
		LCDPacket packet = new LCDPacket
		{
			Command = command,
			DataSize = data.Length
		};
		if (data.Length == 0)
		{
			yield return packet;
			yield break;
		}
		int packNumber = 0;
		int offset = 0;
		while (offset < data.Length)
		{
			int num = Math.Min(data.Length - offset, 501);
			if (num < 501)
			{
				int num2 = 11 + num;
				Array.Clear(packet.bytes, num2, 512 - num2);
			}
			packet.PacketNumber = packNumber;
			packet.Length = num;
			Array.Copy(data, offset, packet.bytes, 11, num);
			int num3 = packNumber + 1;
			packNumber = num3;
			offset += num;
			yield return packet;
		}
	}
}
