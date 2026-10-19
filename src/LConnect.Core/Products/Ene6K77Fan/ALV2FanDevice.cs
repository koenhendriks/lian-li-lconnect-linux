using System;
using System.Threading;
using System.Windows.Media;
using LConnectCore.Devices;

namespace LConnectCore.Products.Ene6K77Fan;

public class ALV2FanDevice : IEne6K77FanDevice, IHidDeviceWrapper
{
	protected const byte ReportID = 224;

	protected const int InputReportDataSize = 64;

	private int writeDelayTime;

	public int WriteDelayTime
	{
		get
		{
			return writeDelayTime;
		}
		set
		{
			writeDelayTime = Math.Max(0, value);
		}
	}

	public IHidDevice HidDevice { get; private set; }

	public ALV2FanDevice(IHidDevice hidDevice)
	{
		HidDevice = hidDevice;
		writeDelayTime = 20;
		hidDevice.ReadTimeout = 10;
		hidDevice.WriteTimeout = 10;
	}

	public FirmwareVersion GetFirmwareVersion()
	{
		sendFeatureReport(new byte[3] { 224, 80, 1 });
		byte[] inputReportData = getInputReportData(5);
		ALV2FanFirmwareVersion aLV2FanFirmwareVersion = new ALV2FanFirmwareVersion();
		aLV2FanFirmwareVersion.Load(inputReportData);
		return aLV2FanFirmwareVersion;
	}

	public int[] GetFanSpeed()
	{
		sendFeatureReport(new byte[3] { 224, 80, 0 });
		byte[] inputReportData = getInputReportData(9);
		if (inputReportData == null)
		{
			return null;
		}
		int[] array = new int[4];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (inputReportData[i * 2 + 1] << 8) | inputReportData[i * 2 + 2];
		}
		return array;
	}

	public void SetFanSpeed(int groupIndex, int duty)
	{
		sendFeatureReport(new byte[4]
		{
			224,
			(byte)(0x20 | (groupIndex & 0xF)),
			0,
			(byte)duty
		});
	}

	public void Ping(int groupIndex, byte speed, byte brightness, bool isMerged)
	{
		if (isMerged)
		{
			byte[] array = new byte[6] { 224, 16, 80, 0, 0, 0 };
			array[3] = (byte)groupIndex;
			sendFeatureReport(array);
		}
		else
		{
			sendFeatureReport(new byte[6]
			{
				224,
				(byte)(0x10 | ((groupIndex * 2) & 0xF)),
				54,
				speed,
				0,
				brightness
			});
		}
	}

	public void SetColorSetting(byte port, Color[] colors)
	{
		if (colors.Length != 0)
		{
			byte[] array = new byte[2 + colors.Length * 3];
			array[0] = 224;
			array[1] = (byte)(0x30 | (port & 0xF));
			int num = 2;
			for (int i = 0; i < colors.Length; i++)
			{
				Color color = colors[i];
				array[num] = color.R;
				array[num + 1] = color.B;
				array[num + 2] = color.G;
				num += 3;
			}
			sendOutputReport(array);
		}
	}

	public void SetEffectSetting(byte port, byte mode, byte speed, byte direction, byte brightness)
	{
		sendFeatureReport(new byte[6]
		{
			224,
			(byte)(0x10 | (port & 0xF)),
			mode,
			speed,
			direction,
			brightness
		});
	}

	public int GetFrame()
	{
		sendFeatureReport(new byte[3] { 224, 85, 5 });
		byte[] inputReportData = getInputReportData(5);
		if (inputReportData != null && inputReportData[0] == 245)
		{
			return (inputReportData[1] << 8) | inputReportData[2];
		}
		return int.MinValue;
	}

	public void SetFrame(int frame)
	{
		sendFeatureReport(new byte[4]
		{
			224,
			96,
			(byte)((frame >> 8) & 0xFF),
			(byte)(frame & 0xFF)
		});
	}

	public void SetFanMotherboardSync(int groupIndex, bool isSync)
	{
		byte[] array = new byte[6] { 224, 16, 98, 0, 0, 0 };
		array[3] = (byte)((uint)(1 << groupIndex + 4) | ((isSync ? 1u : 0u) << groupIndex));
		sendFeatureReport(array);
	}

	public void SetLightingMotherboardSync(bool isSync)
	{
		byte[] array = new byte[6] { 224, 16, 97, 0, 0, 0 };
		array[3] = (isSync ? ((byte)1) : ((byte)0));
		sendFeatureReport(array);
	}

	public void SetMergeOrder(int[] groupIndexOrder)
	{
		byte[] array = new byte[8] { 224, 16, 99, 0, 0, 0, 0, 8 };
		array[3] = (byte)groupIndexOrder[0];
		array[4] = (byte)groupIndexOrder[1];
		array[5] = (byte)groupIndexOrder[2];
		array[6] = (byte)groupIndexOrder[3];
		sendFeatureReport(array);
	}

	public void SetQuantity(int groupIndex, int quantity)
	{
		byte[] array = new byte[6] { 224, 16, 96, 0, 0, 0 };
		array[3] = (byte)(groupIndex + 1);
		array[4] = (byte)quantity;
		sendFeatureReport(array);
	}

	protected int sendFeatureReport(byte[] data)
	{
		int result = HidDevice.SendFeatureReport(data, data.Length);
		Thread.Sleep(writeDelayTime);
		return result;
	}

	protected int sendOutputReport(byte[] data)
	{
		int result = HidDevice.Write(data, data.Length);
		Thread.Sleep(writeDelayTime);
		return result;
	}

	protected byte[] getInputReportData(int length)
	{
		byte[] array = new byte[length];
		byte[] data = new byte[65];
		data[0] = 224;
		if (HidDevice.GetInputReport(ref data, data.Length) < array.Length)
		{
			return null;
		}
		Array.Copy(data, 1, array, 0, array.Length);
		return array;
	}
}
