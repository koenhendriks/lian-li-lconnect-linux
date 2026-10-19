using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LConnectCore.Devices;

namespace LConnectCore.Simulation;

// Software stand-ins for the TL controller, TL LCD fan and SL Infinity hub, enabled with LCONNECT_SIMULATE=1.
// Lets the daemon, CLI and UI be exercised end to end without hardware: commands are logged to
// <data>/sim/*.log and every image the LCD receives is written to <data>/sim/lcd-p<port>-i<index>.jpg.
//
// LCONNECT_SIMULATE_TL="0:LED,LED;2:LCD" sets which fans sit on which TL port (default: one LCD fan on port 2,
// like the machine this port was made on). LCONNECT_SIMULATE_SLINF=1 adds an SL Infinity hub.
public static class HidSimulator
{
	public sealed class Spec
	{
		public string Path;

		public ushort Vid;

		public ushort Pid;

		public int Interface;

		public ushort UsagePage;

		public ushort Usage;

		public string Product;
	}

	public static bool Enabled => Environment.GetEnvironmentVariable("LCONNECT_SIMULATE") == "1";

	private static readonly object locker = new object();

	private static readonly Dictionary<string, SimulatedDevice> devices = new Dictionary<string, SimulatedDevice>();

	public static string OutputDirectory => Path.Combine(LConnectApp.Path.DataRoot, "sim");

	// Port -> fan model codes (ModelB as the firmware reports it).
	public static string[][] TLPorts()
	{
		string[][] array = new string[4][]
		{
			Array.Empty<string>(),
			Array.Empty<string>(),
			Array.Empty<string>(),
			Array.Empty<string>()
		};
		string text = Environment.GetEnvironmentVariable("LCONNECT_SIMULATE_TL");
		if (string.IsNullOrEmpty(text))
		{
			text = "2:LCD";
		}
		string[] array2 = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
		foreach (string text2 in array2)
		{
			string[] array3 = text2.Split(':');
			int num = int.Parse(array3[0]);
			array[num] = array3[1].Split(',').Select((string t) => (t.Trim().ToUpperInvariant() == "LCD") ? "TLFC12" : "TL12").ToArray();
		}
		return array;
	}

	public static IEnumerable<Spec> Specs()
	{
		yield return new Spec
		{
			Path = "sim:tl-controller:1.2#0416:7372",
			Vid = 1046,
			Pid = 29554,
			Interface = 2,
			UsagePage = 65307,
			Usage = 145,
			Product = "TL_Series_Controller (simulated)"
		};
		string[][] array = TLPorts();
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < array[i].Length; j++)
			{
				if (array[i][j] == "TLFC12" && j < 3)
				{
					yield return new Spec
					{
						Path = $"sim:tl-lcd-p{i}-i{j}:1.0#04fc:7393",
						Vid = 1276,
						Pid = 29587,
						Interface = 0,
						UsagePage = 65286,
						Usage = 1,
						Product = "TL_LCD (simulated)"
					};
				}
			}
		}
		if (Environment.GetEnvironmentVariable("LCONNECT_SIMULATE_SLINF") == "1")
		{
			yield return new Spec
			{
				Path = "sim:sl-infinity:1.1#0cf2:a102",
				Vid = 3314,
				Pid = 41218,
				Interface = 1,
				UsagePage = 65280,
				Usage = 1,
				Product = "LianLi-SL-infinity (simulated)"
			};
		}
	}

	public static IList<DeviceFinderResult> Find(ushort vendorId, ushort productId)
	{
		List<DeviceFinderResult> list = new List<DeviceFinderResult>();
		foreach (Spec item in Specs())
		{
			if ((vendorId == 0 || vendorId == item.Vid) && (productId == 0 || productId == item.Pid))
			{
				USBDeviceID uSBDeviceID = new USBDeviceID(item.Vid, item.Pid);
				list.Add(new DeviceFinderResult
				{
					ControllerType = KnownDevices.GetControllerType(uSBDeviceID),
					USBDeviceID = uSBDeviceID,
					DevicePath = item.Path,
					UsagePage = item.UsagePage,
					Usage = item.Usage,
					InterfaceNumber = item.Interface,
					LocationInfo = "usb.000",
					LocationPaths = string.Empty
				});
			}
		}
		return list;
	}

	public static SimulatedDevice Open(string path)
	{
		lock (locker)
		{
			if (!devices.TryGetValue(path, out var value))
			{
				Spec spec = Specs().FirstOrDefault((Spec s) => s.Path == path);
				if (spec == null)
				{
					return null;
				}
				value = new SimulatedDevice(spec);
				devices[path] = value;
			}
			return value;
		}
	}
}

public sealed class SimulatedDevice
{
	private readonly Queue<byte[]> pendingReads = new Queue<byte[]>();

	private readonly object locker = new object();

	private readonly int[,] pwm = new int[4, 16];

	private MemoryStream lcdTransfer;

	private int lcdTransferSize;

	private long lcdBusyUntil;

	// LCONNECT_SIMULATE_LCD_DECODE_MS: how long the simulated LCD stays busy after each image, to exercise the
	// frame pacing against a slow firmware.
	private static readonly int LcdDecodeMs = int.TryParse(Environment.GetEnvironmentVariable("LCONNECT_SIMULATE_LCD_DECODE_MS"), out int ms) ? ms : 0;

	private byte lcdTransferCommand;

	private long framesReceived;

	private DateTime lastFrameWrite = DateTime.MinValue;

	public HidSimulator.Spec Spec { get; }

	public SimulatedDevice(HidSimulator.Spec spec)
	{
		Spec = spec;
		Directory.CreateDirectory(HidSimulator.OutputDirectory);
	}

	private string fileStem => Spec.Path.Substring(4).Split('#')[0].Split(':')[0];

	private string logPath => Path.Combine(HidSimulator.OutputDirectory, fileStem + ".log");

	private void log(string line)
	{
		try
		{
			File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {line}\n");
		}
		catch
		{
		}
	}

	public int Read(byte[] data, int length, int timeout)
	{
		lock (locker)
		{
			if (pendingReads.Count == 0)
			{
				return 0;
			}
			byte[] array = pendingReads.Dequeue();
			int num = Math.Min(length, array.Length);
			Array.Copy(array, data, num);
			return num;
		}
	}

	public int Write(byte[] data, int length)
	{
		lock (locker)
		{
			if (Spec.Pid == 29554)
			{
				tlController(data);
			}
			else if (Spec.Pid == 29587)
			{
				tlLcd(data);
			}
			else
			{
				log("OUT  " + BitConverter.ToString(data, 0, length));
			}
			return length;
		}
	}

	public int SendFeatureReport(byte[] data, int length)
	{
		log("FEAT " + BitConverter.ToString(data, 0, length));
		return length;
	}

	public int GetInputReport(byte[] data, int length)
	{
		// SL Infinity: firmware string / fan speeds.
		if (data.Length > 1)
		{
			byte[] bytes = Encoding.ASCII.GetBytes("V1.4");
			Array.Clear(data, 1, data.Length - 1);
			for (int i = 0; i < 4; i++)
			{
				int num = 800 + i * 50;
				data[1 + i * 2] = (byte)(num >> 8);
				data[2 + i * 2] = (byte)num;
			}
			_ = bytes;
		}
		return length;
	}

	private static byte[] tlPacket(byte command, byte[] payload)
	{
		byte[] array = new byte[64];
		array[0] = 1;
		array[1] = command;
		array[5] = (byte)payload.Length;
		Array.Copy(payload, 0, array, 6, Math.Min(58, payload.Length));
		return array;
	}

	private void tlController(byte[] data)
	{
		byte b = data[1];
		string[][] array = HidSimulator.TLPorts();
		switch (b)
		{
		case 161:
		{
			List<byte> list = new List<byte>();
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < array[i].Length; j++)
				{
					int num = ((array[i][j] == "TLFC12") ? 1900 : 2600) * Math.Max(pwm[i, j], 0) / 100;
					list.Add((byte)(0x80 | (i << 4) | j));
					list.Add((byte)(num >> 8));
					list.Add((byte)num);
				}
			}
			pendingReads.Enqueue(tlPacket(b, list.ToArray()));
			return;
		}
		case 166:
		{
			bool flag = data[6] == 1;
			int num2 = data[7] >> 4;
			int num3 = data[7] & 0xF;
			string text = ((flag && num3 < array[num2].Length) ? array[num2][num3] : "NONE");
			string s = (flag ? ("F_FAN,01,HS,WM," + text + "-FAN,V1.02.18") : "M_CTRL,01,HS,WM,TL12028-Controller,V1.00.62");
			pendingReads.Enqueue(tlPacket(b, new byte[3] { data[6], data[7], 1 }.Concat(Encoding.ASCII.GetBytes(s)).ToArray()));
			pendingReads.Enqueue(tlPacket(b, new byte[3] { data[6], data[7], 2 }.Concat(Encoding.ASCII.GetBytes("2024-08-12 10:00:00")).ToArray()));
			return;
		}
		case 170:
			pwm[(data[6] >> 4) & 3, data[6] & 0xF] = data[7];
			log($"SetFanSpeed port {(data[6] >> 4) & 3} fan {data[6] & 0xF} pwm {data[7]}%");
			break;
		default:
			log("cmd " + b + " " + BitConverter.ToString(data, 0, Math.Min(data.Length, 6 + data[5])));
			break;
		}
		pendingReads.Enqueue(tlPacket(b, new byte[1]));
	}

	private static byte[] lcdPacket(byte command, byte[] payload)
	{
		byte[] array = new byte[64];
		array[0] = 2;
		array[1] = command;
		int num = Math.Min(payload.Length, 53);
		array[9] = (byte)(num >> 8);
		array[10] = (byte)num;
		Array.Copy(payload, 0, array, 11, num);
		return array;
	}

	private void tlLcd(byte[] data)
	{
		byte b = data[1];
		int num = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];
		int num2 = (data[6] << 16) | (data[7] << 8) | data[8];
		int num3 = (data[9] << 8) | data[10];
		switch (b)
		{
		case 61:
			pendingReads.Enqueue(lcdPacket(b, Encoding.ASCII.GetBytes("F_LCD,01,HS,WM,TL-LCD,V1.01.06,0.6")));
			pendingReads.Enqueue(lcdPacket(b, Encoding.ASCII.GetBytes("2023-12-27 18:01:02")));
			return;
		case 62:
		{
			string text = Spec.Path.Split('#')[0];
			int num4 = text.IndexOf("-p", StringComparison.Ordinal);
			int num5 = int.Parse(text.Substring(num4 + 2, 1));
			int num6 = int.Parse(text.Substring(text.IndexOf("-i", StringComparison.Ordinal) + 2, 1));
			byte[] array = new byte[34];
			array[32] = (byte)num5;
			array[33] = (byte)num6;
			pendingReads.Enqueue(lcdPacket(b, array));
			return;
		}
		case 60:
			pendingReads.Enqueue(lcdPacket(b, new byte[3]));
			return;
		case 64:
			log($"LCDControl mode {data[11]} brightness {data[15]} fps {data[16]} rotation {data[17]}");
			pendingReads.Enqueue(lcdPacket(b, new byte[1]));
			return;
		}
		if (b == 65 || b == 69 || b == 70 || b == 71 || b == 72)
		{
			if (num2 == 0)
			{
				// Firmware still decoding the previous frame holds the first packet of the next one.
				int wait = (int)(lcdBusyUntil - Environment.TickCount64);
				if (wait > 0)
				{
					Thread.Sleep(wait);
				}
				lcdTransfer = new MemoryStream();
				lcdTransferSize = num;
				lcdTransferCommand = b;
			}
			lcdTransfer?.Write(data, 11, Math.Min(num3, data.Length - 11));
			if (lcdTransfer != null && lcdTransfer.Length >= lcdTransferSize)
			{
				completeTransfer();
				lcdBusyUntil = Environment.TickCount64 + LcdDecodeMs;
			}
			if (b != 70)
			{
				pendingReads.Enqueue(lcdPacket(b, new byte[1]));
			}
		}
		else
		{
			log("cmd " + b);
			pendingReads.Enqueue(lcdPacket(b, new byte[1]));
		}
	}

	private void completeTransfer()
	{
		byte[] array = lcdTransfer.ToArray();
		lcdTransfer = null;
		framesReceived++;
		string text = lcdTransferCommand switch
		{
			65 => "jpg",
			69 => "avi",
			70 => "sync",
			71 => "bootavi",
			72 => "bootjpg",
			_ => "data",
		};
		if (lcdTransferCommand == 70)
		{
			// Streaming: keep the newest frame on disk, at most a few times per second, plus a running count.
			if ((DateTime.Now - lastFrameWrite).TotalMilliseconds < 250.0)
			{
				return;
			}
			lastFrameWrite = DateTime.Now;
		}
		else
		{
			log($"received {text} ({array.Length} bytes)");
		}
		string text2 = fileStem;
		try
		{
			string path = Path.Combine(HidSimulator.OutputDirectory, text2 + "-" + text + ((text == "avi" || text == "bootavi") ? ".avi" : ".jpg"));
			File.WriteAllBytes(path + ".tmp", array);
			File.Move(path + ".tmp", path, overwrite: true);
			File.WriteAllText(Path.Combine(HidSimulator.OutputDirectory, text2 + "-frames.txt"), $"{framesReceived}\n");
		}
		catch
		{
		}
	}
}
