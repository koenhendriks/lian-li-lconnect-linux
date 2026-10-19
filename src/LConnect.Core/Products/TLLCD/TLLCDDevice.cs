using System;
using System.Collections.Generic;
using System.Text;
using LConnectCore.Devices;

namespace LConnectCore.Products.TLLCD;

public class TLLCDDevice : ITLLCDDevice, IHidDeviceWrapper
{
	private FirmwareVersion firmwareVersion;

	private readonly string portString;

	public IHidDevice HidDevice { get; private set; }

	public int PortNo { get; set; }

	public int LCDIndex { get; set; }

	public bool Matched { get; set; }

	// How long the first packet of the last command took to be accepted. A firmware that is still busy with the
	// previous frame holds the endpoint here, while slow intake spreads the time over all packets.
	public TimeSpan LastFirstPacketTime { get; private set; }

	public event GetHandshakeInfoEventHandler OnGetHandshakeInfo;

	public void SetMatched(bool value)
	{
		Matched = value;
	}

	public bool IsMatched()
	{
		return Matched;
	}

	public string GetPortString()
	{
		return portString;
	}

	public int GetLCDIndex()
	{
		return LCDIndex;
	}

	public int GetPort()
	{
		return PortNo;
	}

	public FirmwareVersion FirmwareVersionV()
	{
		return firmwareVersion;
	}

	public TLLCDDevice(IHidDevice hidDevice, string portString)
		: this(hidDevice)
	{
		this.portString = portString;
	}

	public TLLCDDevice(IHidDevice hidDevice)
	{
		HidDevice = hidDevice;
		hidDevice.ReadTimeout = 200;
		hidDevice.WriteTimeout = 200;
	}

	public bool Open()
	{
		HidDevice.Open();
		bool result = GetPortAndIndex() != 0;
		firmwareVersion = GetFirmwareVersion();
		return result;
	}

	public void Dispose()
	{
		throw new NotImplementedException();
	}

	public void SendGetHandshakeInfo()
	{
		writeCommand(60, read: false);
	}

	public HandshakeInfo GetHandshakeInfo()
	{
		byte b = 60;
		SendGetHandshakeInfo();
		if (readCommand(out var packetBytes) == -1 || !LCDPacket.TryParse(packetBytes, out var packet) || packet.Command != b)
		{
			return default;
		}
		HandshakeInfo.TryParse(packet.Data, out var info);
		return info;
	}

	public FirmwareVersion GetFirmwareVersion()
	{
		byte b = 61;
		writeCommand(b, read: false);
		int num = readCommand(out var packetBytes);
		int num2 = readCommand(out var packetBytes2);
		if (num == -1 || num2 == -1 || !LCDPacket.TryParse(packetBytes, out var packet) || !LCDPacket.TryParse(packetBytes2, out var packet2) || packet.Command != b || packet2.Command != b)
		{
			return default;
		}
		string versionStr = Encoding.ASCII.GetString(packet.Data);
		string dateTimeStr = Encoding.ASCII.GetString(packet2.Data);
		FirmwareVersion.TryParse(versionStr, dateTimeStr, out var result);
		return result;
	}

	public int GetPortAndIndex()
	{
		int num = writeCommand(62, read: false);
		if (num != 0)
		{
			return num;
		}
		int num2 = readCommand(out var packetBytes, 64, HidDevice.ReadTimeout);
		if (num2 > 0 && LCDPacket.TryParse(packetBytes, out var packet) && packet.Command == 62 && SerialNumberInfo.TryParse(packet.Data, out var info) && info.IsValid)
		{
			if (0 <= info.Port && info.Port < 4)
			{
				PortNo = info.Port;
			}
			if (0 <= info.Index && info.Index < 3)
			{
				LCDIndex = info.Index;
			}
		}
		return num2;
	}

	public int LCDControl(LCDControlSetting setting)
	{
		int num = writeCommand(64, setting.ToBytes(), read: true);
		if (num == 0)
		{
			return 0;
		}
		return num;
	}

	public int SendJPG(byte[] bytes)
	{
		return writeCommand(65, bytes, read: true);
	}

	public int SendAVI(byte[] bytes)
	{
		return writeCommand(69, bytes, read: true);
	}

	public int SendSyncJPG(byte[] bytes)
	{
		return writeCommand(70, bytes, read: false);
	}

	public int SendBootVideo(byte[] bytes)
	{
		return writeCommand(71, bytes, read: true);
	}

	public int SendBootJPG(byte[] bytes)
	{
		return writeCommand(72, bytes, read: true);
	}

	public int ReadPacket()
	{
		byte[] packetBytes;
		return readCommand(out packetBytes, 64, HidDevice.ReadTimeout);
	}

	private int readCommand(out byte[] packetBytes, int packetLength)
	{
		return readCommand(out packetBytes, packetLength, HidDevice.ReadTimeout);
	}

	private int readCommand(out byte[] packetBytes, int packetLength, int timeout)
	{
		return HidDevice.Read(out packetBytes, packetLength, timeout);
	}

	private int readCommand(out byte[] packetBytes)
	{
		return readCommand(out packetBytes, 64);
	}

	private int writeCommand(IEnumerable<Packet> packets, bool read)
	{
		if (!HidDevice.IsOpen)
		{
			Console.WriteLine("HidDevice not open " + GetPortString());
			return -1;
		}
		bool first = true;
		foreach (Packet packet2 in packets)
		{
			byte[] bytes = packet2.GetBytes();
			long start = System.Diagnostics.Stopwatch.GetTimestamp();
			int num = HidDevice.Write(bytes, bytes.Length);
			if (first)
			{
				LastFirstPacketTime = System.Diagnostics.Stopwatch.GetElapsedTime(start);
				first = false;
			}
			if (num < bytes.Length)
			{
				Console.WriteLine($"HidDevice Write {bytes.Length}, only write {num} at Port #{PortNo}, LCDIndex #{LCDIndex}");
				return num;
			}
			if (read)
			{
				int num2 = readCommand(out var packetBytes);
				if (num2 == -1 || !LCDPacket.TryParse(packetBytes, out var packet) || bytes[1] != packet.Command)
				{
					Console.WriteLine($"HidDevice Read {bytes.Length}, only read {num2}. Cmd:{bytes[1]} at Port #{PortNo}, LCDIndex #{LCDIndex}");
					return -1;
				}
			}
		}
		return 0;
	}

	private int writeCommand(byte command, bool read)
	{
		return writeCommand(command, Array.Empty<byte>(), read);
	}

	private int writeCommand(byte command, byte[] data, bool read)
	{
		return writeCommand(LCDPacket.BuildPacketsForWrite(command, data), read);
	}
}
