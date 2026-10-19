// Shared between tools/GoldenHarness (net48, compiled against the ORIGINAL L-Connect.Core.dll) and
// tests/LConnect.Core.Tests (net10, compiled against the port). Do not use APIs missing from either.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LConnectCore.Devices;

namespace Golden
{
	public enum SimKind
	{
		TLController,
		TLLCD,
		SLInfinity
	}

	// Records every transfer and answers reads like the real hardware would (simplified).
	public class FakeHidDevice : IHidDevice
	{
		private readonly Queue<byte[]> pendingReads = new Queue<byte[]>();

		public readonly List<string> Log = new List<string>();

		public SimKind Kind;

		// Fans per TL port: (count, modelB) e.g. port 2 has one "TLC12" (LCD 120mm reverse = "TLFC12").
		public string[][] TLPortFans = new string[4][]
		{
			new string[0],
			new string[0],
			new string[1] { "TLFC12" },
			new string[0]
		};

		public int LcdPort = 2;

		public int LcdIndex;

		public string BinaryLogPath { get; set; }

		public bool IsOpen { get; private set; }

		public string DevicePath { get; set; } = "fake";

		public ushort VendorID { get; set; }

		public ushort ProductID { get; set; }

		public string SerialNumber => "SN";

		public string Manufacturer => "Fake";

		public string Product => "Fake";

		public ushort UsagePage { get; set; } = 65307;

		public ushort Usage { get; set; } = 145;

		public int InterfaceNumber => 2;

		public string LocationInfo { get; set; } = "usb.000";

		public string LocationPaths { get; set; } = "";

		public int ReadTimeout { get; set; }

		public int WriteTimeout { get; set; }

		public FakeHidDevice(SimKind kind)
		{
			Kind = kind;
		}

		public void Open()
		{
			IsOpen = true;
		}

		public void Close()
		{
			IsOpen = false;
		}

		public int Read(out byte[] data, int length)
		{
			return Read(out data, length, ReadTimeout);
		}

		public int Read(out byte[] data, int length, int timeout)
		{
			if (pendingReads.Count == 0)
			{
				data = null;
				Log.Add("R-");
				return -1;
			}
			byte[] array = pendingReads.Dequeue();
			data = new byte[length];
			Array.Copy(array, data, Math.Min(length, array.Length));
			Log.Add("R");
			return Math.Min(length, array.Length);
		}

		public int Write(byte[] data, int length)
		{
			Log.Add("W:" + Hex(data, length));
			Respond(data);
			return length;
		}

		public int SendFeatureReport(byte[] data, int length)
		{
			Log.Add("F:" + Hex(data, length));
			return length;
		}

		public int GetFeatureReport(ref byte[] data, int length)
		{
			Log.Add("GF");
			return -1;
		}

		public int GetInputReport(ref byte[] data, int length)
		{
			Log.Add("GI:" + data[0]);
			for (int i = 1; i < length; i++)
			{
				data[i] = (byte)(16 + i);
			}
			return length;
		}

		public static string Hex(byte[] data, int length)
		{
			// Trim trailing zeros to keep golden files readable; the length is recorded separately.
			int num = length;
			while (num > 0 && data[num - 1] == 0)
			{
				num--;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(length).Append('/');
			for (int i = 0; i < num; i++)
			{
				stringBuilder.Append(data[i].ToString("x2"));
			}
			return stringBuilder.ToString();
		}

		private static byte[] tlPacket(byte command, byte[] payload)
		{
			byte[] array = new byte[64];
			array[0] = 1;
			array[1] = command;
			array[5] = (byte)payload.Length;
			Array.Copy(payload, 0, array, 6, payload.Length);
			return array;
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

		private void Respond(byte[] data)
		{
			if (Kind == SimKind.TLController && data[0] == 1)
			{
				byte b = data[1];
				switch (b)
				{
				case 161:
				{
					List<byte> list = new List<byte>();
					for (int i = 0; i < 4; i++)
					{
						for (int j = 0; j < TLPortFans[i].Length; j++)
						{
							int num = 900 + 10 * i + j;
							list.Add((byte)(0x80 | (i << 4) | j));
							list.Add((byte)(num >> 8));
							list.Add((byte)num);
						}
					}
					pendingReads.Enqueue(tlPacket(b, list.ToArray()));
					break;
				}
				case 166:
				{
					bool flag = data[6] == 1;
					int num2 = data[7] >> 4;
					int num3 = data[7] & 0xF;
					string text = "TLC";
					if (flag && num2 < 4 && num3 < TLPortFans[num2].Length)
					{
						text = TLPortFans[num2][num3];
					}
					else if (flag)
					{
						text = "NONE";
					}
					string s = (flag ? ("F_LED,01,HS,WM," + text + ",V1.00.10") : "M_CTRL,01,HS,WM,TLCTRL,V1.00.62");
					byte[] array = new byte[3] { data[6], data[7], 1 }.Concat(Encoding.ASCII.GetBytes(s)).ToArray();
					byte[] array2 = new byte[3] { data[6], data[7], 2 }.Concat(Encoding.ASCII.GetBytes("2024-01-02 03:04:05")).ToArray();
					pendingReads.Enqueue(tlPacket(b, array));
					pendingReads.Enqueue(tlPacket(b, array2));
					break;
				}
				default:
					pendingReads.Enqueue(tlPacket(b, new byte[1]));
					break;
				}
			}
			else if (Kind == SimKind.TLLCD && data[0] == 2)
			{
				byte b2 = data[1];
				switch (b2)
				{
				case 61:
					pendingReads.Enqueue(lcdPacket(b2, Encoding.ASCII.GetBytes("F_LCD,01,HS,WM,TL-LCD,V1.01.06,0.6")));
					pendingReads.Enqueue(lcdPacket(b2, Encoding.ASCII.GetBytes("2023-12-27 18:01:02")));
					break;
				case 62:
				{
					byte[] array3 = new byte[34];
					for (int k = 0; k < 32; k++)
					{
						array3[k] = (byte)(65 + k % 26);
					}
					array3[32] = (byte)LcdPort;
					array3[33] = (byte)LcdIndex;
					pendingReads.Enqueue(lcdPacket(b2, array3));
					break;
				}
				case 60:
					pendingReads.Enqueue(lcdPacket(b2, new byte[3] { 1, 0, 7 }));
					break;
				case 70:
					break;
				default:
					pendingReads.Enqueue(lcdPacket(b2, new byte[1]));
					break;
				}
			}
		}
	}
}
