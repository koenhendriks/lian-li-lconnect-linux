using System;
using System.Linq;
using System.Text;
using System.Windows.Media;
using LConnectCore.Devices;

namespace LConnectCore.Products.TLFan;

public class TLFanDevice : ITLFanDevice, IHidDeviceWrapper
{
	public IHidDevice HidDevice { get; private set; }

	public TLFanDevice(IHidDevice hidDevice)
	{
		HidDevice = hidDevice;
		hidDevice.ReadTimeout = 100;
		hidDevice.WriteTimeout = 100;
	}

	public TLFanHandshakeInfo GetHandshakeInfo()
	{
		byte b = 161;
		writeCommand(new LEDPacket
		{
			Command = b
		});
		LEDPacket lEDPacket = readPacket();
		if (lEDPacket?.Command != b || !TLFanHandshakeInfo.TryParse(lEDPacket.Data, out var handshakeInfo))
		{
			return new TLFanHandshakeInfo();
		}
		return handshakeInfo;
	}

	public void SetFanLight(int port, int fanIndex, LightingConfig config, bool isSync, bool disable)
	{
		byte command = 163;
		byte[] array = new byte[20]
		{
			(byte)((uint)(port << 4) | (isSync ? 1u : 0u)),
			(byte)((port << 4) | fanIndex),
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			disable ? ((byte)1) : ((byte)0),
			0
		};
		if (!isSync && !disable)
		{
			Color[] array2 = config.Colors ?? Array.Empty<Color>();
			array[2] = getLightingModeValue(config.Mode);
			array[3] = config.Brightness;
			array[4] = config.Speed;
			for (int i = 0; i < array2.Length; i++)
			{
				array[5 + i * 3] = array2[i].R;
				array[5 + i * 3 + 1] = array2[i].G;
				array[5 + i * 3 + 2] = array2[i].B;
			}
			array[17] = (byte)config.Direction;
			array[19] = (byte)array2.Length;
		}
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = array
		});
		readPacket();
	}

	public FirmwareVersion GetControllerFirmwareVersion()
	{
		return getFirmwareVersion(isSlave: false, 0, 0);
	}

	public FirmwareVersion GetFanFirmwareVersion(int port, int fanIndex)
	{
		return getFirmwareVersion(isSlave: true, port, fanIndex);
	}

	public void SetFanSpeed(int port, int fanIndex, int pwm)
	{
		byte command = 170;
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = new byte[2]
			{
				(byte)((port << 4) | fanIndex),
				(byte)pwm
			}
		});
		readPacket();
	}

	public void SetFanGroup(int groupNumber, FanGroupParams[] groupParams)
	{
		if (groupParams != null && groupParams.Length != 0)
		{
			byte command = 173;
			byte[] array = new byte[2 + groupParams.Length];
			array[0] = (byte)groupNumber;
			array[1] = (byte)groupParams.Length;
			for (int i = 0; i < groupParams.Length; i++)
			{
				array[2 + i] = (byte)(((groupParams[i].TopSide ? 1u : 0u) << 7) | ((groupParams[i].BottomSide ? 1u : 0u) << 6) | (uint)(groupParams[i].Port << 4) | (uint)groupParams[i].FanIndex);
			}
			writeCommand(new LEDPacket
			{
				Command = command,
				Data = array
			});
			readPacket();
		}
	}

	public void SetFanDirection(int port, int fanIndex, bool isSwapTopBottom, bool isSwapLeftRight)
	{
		byte command = 174;
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = new byte[2]
			{
				(byte)((port << 4) | fanIndex),
				(byte)(((isSwapTopBottom ? 1u : 0u) << 1) | (isSwapLeftRight ? 1u : 0u))
			}
		});
		readPacket();
	}

	public void SetPortDirection(int port, bool isSwap)
	{
		byte command = 175;
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = new byte[2]
			{
				(byte)(port << 4),
				isSwap ? ((byte)1) : ((byte)0)
			}
		});
		readPacket();
	}

	public void SetGroupLight(int groupNumber, LightingConfig config, bool disable)
	{
		byte command = 176;
		byte[] array = new byte[20]
		{
			0,
			(byte)groupNumber,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			disable ? ((byte)1) : ((byte)0),
			0
		};
		if (!disable)
		{
			Color[] array2 = config.Colors ?? Array.Empty<Color>();
			array[2] = getLightingModeValue(config.Mode);
			array[3] = config.Brightness;
			array[4] = config.Speed;
			for (int i = 0; i < array2.Length; i++)
			{
				array[5 + i * 3] = array2[i].R;
				array[5 + i * 3 + 1] = array2[i].G;
				array[5 + i * 3 + 2] = array2[i].B;
			}
			array[17] = (byte)config.Direction;
			array[19] = (byte)array2.Length;
		}
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = array
		});
		readPacket();
	}

	public void SetMBRPMSync(int port, int fanIndex, bool isSync)
	{
		byte command = 177;
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = new byte[1] { (byte)(((isSync ? 1u : 0u) << 7) | (uint)(port << 4) | (uint)fanIndex) }
		});
		readPacket();
	}

	public void TestFanLighting(int port, int fanIndex, bool isBottom)
	{
		testLighting(1, (byte)(((port & 3) << 4) | (fanIndex & 0xF)), isBottom);
	}

	public void TestGroupLighting(int groupNumber, bool isBottom)
	{
		testLighting(2, (byte)(groupNumber & 0x1F), isBottom);
	}

	public void TestPortLighting(int port, bool isBottom)
	{
		testLighting(3, (byte)(port & 3), isBottom);
	}

	private void testLighting(byte type, byte number, bool isBottom)
	{
		byte command = 179;
		writeCommand(new LEDPacket
		{
			Command = command,
			Data = new byte[3]
			{
				type,
				number,
				isBottom ? ((byte)1) : ((byte)0)
			}
		});
		readPacket();
	}

	public FirmwareVersion getFirmwareVersion(bool isSlave, int port, int fanIndex)
	{
		byte b = 166;
		byte b2 = (isSlave ? ((byte)1) : ((byte)0));
		byte b3 = (byte)(isSlave ? ((uint)((port << 4) | fanIndex)) : 0u);
		writeCommand(new LEDPacket
		{
			Command = b,
			Data = new byte[2] { b2, b3 }
		});
		LEDPacket lEDPacket = readPacket();
		LEDPacket lEDPacket2 = readPacket();
		if (lEDPacket?.Command != b || lEDPacket2?.Command != b)
		{
			return default;
		}
		byte[] data = lEDPacket.Data;
		byte[] data2 = lEDPacket2.Data;
		if (data.Length < 3 || data2.Length < 3 || data[0] != b2 || data2[0] != b2 || data[1] != b3 || data2[1] != b3 || data[2] != 1 || data2[2] != 2)
		{
			return default;
		}
		try
		{
			string versionStr = getString(data);
			string dateTimeStr = getString(data2);
			if (FirmwareVersion.TryParse(versionStr, dateTimeStr, out var firmwareVersion))
			{
				return firmwareVersion;
			}
		}
		catch
		{
		}
		return default;
		static string getString(byte[] binary)
		{
			return Encoding.ASCII.GetString(binary.Skip(3).TakeWhile((byte b4) => b4 != 0).ToArray());
		}
	}

	private int writeCommand(LEDPacket packet)
	{
		byte[] bytes = packet.GetBytes();
		return HidDevice.Write(bytes, bytes.Length);
	}

	private int readCommand(out byte[] packetBytes)
	{
		return HidDevice.Read(out packetBytes, 64, HidDevice.ReadTimeout);
	}

	private LEDPacket readPacket()
	{
		if (readCommand(out var packetBytes) == -1 || !LEDPacket.TryParse(packetBytes, out var packet))
		{
			return null;
		}
		return packet;
	}

	private byte getLightingModeValue(LightingMode mode)
	{
		return (byte)((int)mode % 1000);
	}
}
