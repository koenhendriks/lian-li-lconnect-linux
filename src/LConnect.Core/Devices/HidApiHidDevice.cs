using System;
using System.IO;
using LConnectCore.Native;
using LConnectCore.Platform;

namespace LConnectCore.Devices;

public class HidApiHidDevice : IHidDevice
{
	private HidApi.HidDeviceSafeHandle deviceHandle;

	private HidReportDescriptor reportDescriptor;

	private Simulation.SimulatedDevice simulated;

	public string BinaryLogPath { get; set; }

	public bool IsOpen { get; private set; }

	// Stable identity (see UsbTopology). On Windows this is the HID interface path, as in the original.
	public string DevicePath { get; private set; }

	public string OpenPath { get; private set; }

	public ushort VendorID { get; private set; }

	public ushort ProductID { get; private set; }

	public string SerialNumber { get; private set; }

	public string Manufacturer { get; private set; }

	public string Product { get; private set; }

	public ushort UsagePage { get; private set; }

	public ushort Usage { get; private set; }

	public int InterfaceNumber { get; private set; }

	public string LocationInfo { get; private set; }

	public string LocationPaths { get; private set; }

	public int ReadTimeout { get; set; }

	public int WriteTimeout { get; set; }

	public HidApiHidDevice(string devicePath)
	{
		DevicePath = devicePath.ToLowerInvariant();
		ReadTimeout = 200;
		WriteTimeout = 200;
		BinaryLogPath = Environment.GetEnvironmentVariable("LCONNECT_HID_LOG");
	}

	public unsafe void Open()
	{
		if (IsOpen)
		{
			return;
		}
		if (DevicePath.StartsWith("sim:", StringComparison.Ordinal))
		{
			simulated = Simulation.HidSimulator.Open(DevicePath);
			if (simulated != null)
			{
				OpenPath = DevicePath;
				VendorID = simulated.Spec.Vid;
				ProductID = simulated.Spec.Pid;
				UsagePage = simulated.Spec.UsagePage;
				Usage = simulated.Spec.Usage;
				InterfaceNumber = simulated.Spec.Interface;
				Product = simulated.Spec.Product;
				Manufacturer = "Lian Li (simulated)";
				SerialNumber = "SIM";
				LocationInfo = "usb.000";
				LocationPaths = string.Empty;
				IsOpen = true;
			}
			return;
		}
		OpenPath = UsbTopology.ResolveOpenPath(DevicePath);
		if (OpenPath == DevicePath && OperatingSystem.IsLinux() && !DevicePath.StartsWith("/dev/", StringComparison.Ordinal))
		{
			new HidApiDeviceFinder().Find();
			OpenPath = UsbTopology.ResolveOpenPath(DevicePath);
		}
		deviceHandle = HidApi.OpenPath(OpenPath);
		if (deviceHandle.IsInvalid)
		{
			return;
		}
		HidApi.DeviceInfo* deviceInfo = HidApi.GetDeviceInfo(deviceHandle);
		if (deviceInfo != null)
		{
			VendorID = deviceInfo->VendorID;
			ProductID = deviceInfo->ProductID;
			SerialNumber = HidApi.PtrToWideString(deviceInfo->SerialNumber);
			Manufacturer = HidApi.PtrToWideString(deviceInfo->ManufacturerString);
			Product = HidApi.PtrToWideString(deviceInfo->ProductString);
			UsagePage = deviceInfo->UsagePage;
			Usage = deviceInfo->Usage;
			InterfaceNumber = deviceInfo->InterfaceNumber;
		}
		UsbTopology.Info info = UsbTopology.Describe(OpenPath, VendorID, ProductID, InterfaceNumber);
		LocationInfo = info?.LocationInfo?.ToLowerInvariant();
		LocationPaths = info?.LocationPaths?.ToLowerInvariant();
		try
		{
			byte[] array = new byte[4096];
			int num = HidApi.GetReportDescriptor(deviceHandle, array, (nuint)array.Length);
			if (num > 0)
			{
				reportDescriptor = HidReportDescriptor.Parse(array, num);
			}
		}
		catch
		{
			reportDescriptor = null;
		}
		IsOpen = true;
	}

	public void Close()
	{
		if (IsOpen && simulated != null)
		{
			simulated = null;
			IsOpen = false;
		}
		if (IsOpen)
		{
			deviceHandle.Dispose();
			deviceHandle = null;
			IsOpen = false;
		}
	}

	public int Read(out byte[] data, int length)
	{
		return Read(out data, length, ReadTimeout);
	}

	public int Read(out byte[] data, int length, int timeout)
	{
		data = new byte[length];
		int num = -1;
		try
		{
			num = ((simulated != null) ? simulated.Read(data, length, timeout) : HidApi.ReadTimeout(deviceHandle, data, (nuint)length, timeout));
		}
		catch
		{
		}
		if (num == -1)
		{
			data = null;
		}
		writeBinaryLog(isInput: true, data);
		return num;
	}

	public int Write(byte[] data, int length)
	{
		int result = -1;
		try
		{
			if (simulated != null)
			{
				result = simulated.Write(data, length);
			}
			else
			{
				byte[] array = pad(HidReportDescriptor.ReportKind.Output, data, length);
				result = Math.Min(HidApi.Write(deviceHandle, array, (nuint)array.Length), length);
			}
		}
		catch
		{
		}
		writeBinaryLog(isInput: false, data);
		return result;
	}

	public int SendFeatureReport(byte[] data, int length)
	{
		int result = -1;
		try
		{
			if (simulated != null)
			{
				result = simulated.SendFeatureReport(data, length);
			}
			else
			{
				byte[] array = pad(HidReportDescriptor.ReportKind.Feature, data, length);
				result = Math.Min(HidApi.SendFeatureReport(deviceHandle, array, (nuint)array.Length), length);
			}
		}
		catch
		{
		}
		writeBinaryLog(isInput: false, data, "FEAT");
		return result;
	}

	public int GetFeatureReport(ref byte[] data, int length)
	{
		int result = -1;
		try
		{
			result = HidApi.GetFeatureReport(deviceHandle, data, (nuint)length);
		}
		catch
		{
		}
		return result;
	}

	public int GetInputReport(ref byte[] data, int length)
	{
		int result = -1;
		try
		{
			result = ((simulated != null) ? simulated.GetInputReport(data, length) : HidApi.GetInputReport(deviceHandle, data, (nuint)length));
		}
		catch
		{
		}
		writeBinaryLog(isInput: true, data, "GETIN");
		return result;
	}

	// Mirror Windows: writes shorter than the declared report are zero-padded to the full report length.
	private byte[] pad(HidReportDescriptor.ReportKind kind, byte[] data, int length)
	{
		int num = ((reportDescriptor != null && data.Length != 0) ? reportDescriptor.GetReportLength(kind, data[0]) : 0);
		if (num <= length)
		{
			if (length == data.Length)
			{
				return data;
			}
			byte[] array = new byte[length];
			Array.Copy(data, array, Math.Min(length, data.Length));
			return array;
		}
		byte[] array2 = new byte[num];
		Array.Copy(data, array2, Math.Min(length, data.Length));
		return array2;
	}

	private void writeBinaryLog(bool isInput, byte[] data, string tag = null)
	{
		string binaryLogPath = BinaryLogPath;
		if (string.IsNullOrEmpty(binaryLogPath))
		{
			return;
		}
		try
		{
			string directoryName = Path.GetDirectoryName(binaryLogPath);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string arg = (tag ?? (isInput ? "IN" : "OUT")).PadRight(5);
			string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {arg}";
			string text2 = ((data == null || data.Length == 0) ? string.Empty : BitConverter.ToString(data));
			File.AppendAllText(binaryLogPath, text + " | " + DevicePath + " | " + text2 + "\n");
		}
		catch
		{
		}
	}
}
