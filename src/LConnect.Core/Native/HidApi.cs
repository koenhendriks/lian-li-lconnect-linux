using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LConnectCore.Native;

// Bindings for the standard hidapi 0.14 ABI.
// L-Connect ships a patched hidapi.dll whose hid_device_info has extra location fields appended and exports
// hid_get_location_path/hid_get_location_info. Those are not available upstream; location data now comes
// from LConnectCore.Platform.UsbTopology instead. The struct below is a prefix of the vendor struct, so the
// vendor DLL can still be used for testing on Windows.
public sealed class HidApi
{
	public class HidDeviceSafeHandle : SafeHandle
	{
		public override bool IsInvalid => handle == IntPtr.Zero;

		public HidDeviceSafeHandle()
			: base(IntPtr.Zero, ownsHandle: true)
		{
		}

		// hid_close runs here, not from HidApiHidDevice.Close: the SafeHandle ref count defers it until a hid_write or
		// hid_read on another thread (the LCD frame loop during shutdown) has returned. Closing directly freed the
		// device under that call and crashed the daemon on stop.
		protected override bool ReleaseHandle()
		{
			CloseHandle(handle);
			return true;
		}
	}

	public struct ApiVersion
	{
		public int Major;

		public int Minor;

		public int Patch;
	}

	public struct DeviceInfo
	{
		public IntPtr Path;

		public ushort VendorID;

		public ushort ProductID;

		public IntPtr SerialNumber;

		public ushort ReleaseNumber;

		public IntPtr ManufacturerString;

		public IntPtr ProductString;

		public ushort UsagePage;

		public ushort Usage;

		public int InterfaceNumber;

		public unsafe DeviceInfo* Next;

		public BusType BusType;
	}

	public enum BusType
	{
		HID_API_BUS_UNKNOWN,
		HID_API_BUS_USB,
		HID_API_BUS_BLUETOOTH,
		HID_API_BUS_I2C,
		HID_API_BUS_SPI
	}

	public const string HidApiDllName = "hidapi";

	// hidapi uses wchar_t strings: UTF-16 on Windows, UTF-32 on Linux.
	public static string PtrToWideString(IntPtr ptr)
	{
		if (ptr == IntPtr.Zero)
		{
			return null;
		}
		if (OperatingSystem.IsWindows())
		{
			return Marshal.PtrToStringUni(ptr);
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; ; i += 4)
		{
			int num = Marshal.ReadInt32(ptr, i);
			if (num == 0)
			{
				break;
			}
			stringBuilder.Append(char.ConvertFromUtf32(num));
		}
		return stringBuilder.ToString();
	}

	[ModuleInitializer]
	internal static void RegisterResolver()
	{
		NativeLibrary.SetDllImportResolver(typeof(HidApi).Assembly, (string name, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath) =>
		{
			if (name != HidApiDllName)
			{
				return IntPtr.Zero;
			}
			string text = Environment.GetEnvironmentVariable("LCONNECT_HIDAPI");
			if (!string.IsNullOrEmpty(text) && NativeLibrary.TryLoad(text, out var handle))
			{
				return handle;
			}
			string[] array = (OperatingSystem.IsWindows() ? new string[2]
			{
				Path.Combine(AppContext.BaseDirectory, "hidapi.dll"),
				"hidapi.dll"
			} : new string[3] { "libhidapi-hidraw.so.0", "libhidapi-hidraw.so", "libhidapi-libusb.so.0" });
			foreach (string libraryPath in array)
			{
				if (NativeLibrary.TryLoad(libraryPath, assembly, searchPath, out handle))
				{
					return handle;
				}
			}
			return IntPtr.Zero;
		});
	}

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_version")]
	public static extern IntPtr Version();

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_version_str")]
	public static extern IntPtr VersionStr();

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_init")]
	public static extern int Init();

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_exit")]
	public static extern int Exit();

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_enumerate")]
	public unsafe static extern DeviceInfo* Enumerate(ushort vendorId, ushort productId);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_free_enumeration")]
	public unsafe static extern void FreeEnumeration(DeviceInfo* devs);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_open_path")]
	public static extern HidDeviceSafeHandle OpenPath([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_write")]
	public static extern int Write(HidDeviceSafeHandle dev, [In] byte[] data, nuint length);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_read_timeout")]
	public static extern int ReadTimeout(HidDeviceSafeHandle dev, [Out] byte[] data, nuint length, int milliseconds);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_read")]
	public static extern int Read(HidDeviceSafeHandle dev, [Out] byte[] data, nuint length);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_set_nonblocking")]
	public static extern int SetNonblocking(HidDeviceSafeHandle dev, int nonblock);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_send_feature_report")]
	public static extern int SendFeatureReport(HidDeviceSafeHandle dev, [In] byte[] data, nuint length);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_get_feature_report")]
	public static extern int GetFeatureReport(HidDeviceSafeHandle dev, [In][Out] byte[] data, nuint length);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_get_input_report")]
	public static extern int GetInputReport(HidDeviceSafeHandle dev, [In][Out] byte[] data, nuint length);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_close")]
	private static extern void CloseHandle(IntPtr dev);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_get_device_info")]
	public unsafe static extern DeviceInfo* GetDeviceInfo(HidDeviceSafeHandle dev);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_get_report_descriptor")]
	public static extern int GetReportDescriptor(HidDeviceSafeHandle dev, [Out] byte[] buf, nuint bufSize);

	[DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, EntryPoint = "hid_error")]
	public static extern IntPtr Error(HidDeviceSafeHandle dev);

	public static string GetError(HidDeviceSafeHandle dev)
	{
		try
		{
			return PtrToWideString(Error(dev));
		}
		catch
		{
			return null;
		}
	}
}
