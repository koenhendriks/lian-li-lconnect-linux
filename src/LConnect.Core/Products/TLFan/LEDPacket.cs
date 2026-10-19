using System;
using System.Collections.Generic;
using LConnectCore.Devices;

namespace LConnectCore.Products.TLFan;

public class LEDPacket : Packet
{
	public const byte ReportID = 1;

	public const int PacketLength = 64;

	public const int HeaderLength = 6;

	public const int PayloadLength = 58;

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

	public int PacketNumber
	{
		get
		{
			return (bytes[3] << 8) | bytes[4];
		}
		set
		{
			bytes[3] = (byte)((value >> 8) & 0xFF);
			bytes[4] = (byte)(value & 0xFF);
		}
	}

	public int Length
	{
		get
		{
			return bytes[5];
		}
		set
		{
			bytes[5] = (byte)(Math.Min(value, 58) & 0xFF);
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
			int num = Math.Min(Length, 58);
			byte[] array = new byte[num];
			Array.Copy(bytes, 6, array, 0, num);
			return array;
		}
		set
		{
			Array.Clear(bytes, 6, 0);
			int length = 0;
			if (value != null)
			{
				length = Math.Min(value.Length, 58);
				Array.Copy(value, 0, bytes, 6, length);
			}
			Length = length;
		}
	}

	public LEDPacket()
		: base(64)
	{
		bytes[0] = 1;
		bytes[2] = 0;
	}

	public static bool TryParse(byte[] packetBytes, out LEDPacket packet)
	{
		packet = null;
		if (packetBytes == null || packetBytes.Length < 6 || packetBytes[0] != 1)
		{
			return false;
		}
		packet = new LEDPacket();
		int length = Math.Min(packetBytes.Length, 64);
		Array.Copy(packetBytes, packet.bytes, length);
		return true;
	}

	public static IEnumerable<LEDPacket> BuildPacketsForWrite(byte command, byte[] data)
	{
		LEDPacket packet = new LEDPacket
		{
			Command = command
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
			int num = Math.Min(data.Length - offset, 58);
			if (num < 58)
			{
				int num2 = 6 + num;
				Array.Clear(packet.bytes, num2, 64 - num2);
			}
			packet.PacketNumber = packNumber;
			packet.Length = num;
			Array.Copy(data, offset, packet.bytes, 6, num);
			int num3 = packNumber + 1;
			packNumber = num3;
			offset += num;
			yield return packet;
		}
	}
}
