using System.Collections.Generic;

namespace LConnectCore.Devices;

// Computes report lengths from a HID report descriptor.
// Windows' HID stack pads every output/feature write to the declared report length; Linux hidraw sends exactly
// what it is given. L-Connect relies on the Windows behaviour (e.g. the ENE hubs send 3-8 byte feature
// reports), so HidApiHidDevice pads writes to the length computed here.
public sealed class HidReportDescriptor
{
	public enum ReportKind
	{
		Input,
		Output,
		Feature
	}

	private readonly Dictionary<(ReportKind, byte), int> bitLengths = new Dictionary<(ReportKind, byte), int>();

	public bool UsesReportIds { get; private set; }

	public static HidReportDescriptor Parse(byte[] descriptor, int length)
	{
		HidReportDescriptor hidReportDescriptor = new HidReportDescriptor();
		int num = 0;
		int num2 = 0;
		byte b = 0;
		Stack<(int, int, byte)> stack = new Stack<(int, int, byte)>();
		int num3 = 0;
		while (num3 < length)
		{
			byte b2 = descriptor[num3];
			if (b2 == 254)
			{
				if (num3 + 1 >= length)
				{
					break;
				}
				num3 += 3 + descriptor[num3 + 1];
				continue;
			}
			int num4 = b2 & 3;
			if (num4 == 3)
			{
				num4 = 4;
			}
			int num5 = (b2 >> 2) & 3;
			int num6 = b2 >> 4;
			uint num7 = 0u;
			for (int i = 0; i < num4 && num3 + 1 + i < length; i++)
			{
				num7 |= (uint)(descriptor[num3 + 1 + i] << 8 * i);
			}
			switch (num5)
			{
			case 0:
			{
				ReportKind? reportKind = num6 switch
				{
					8 => ReportKind.Input,
					9 => ReportKind.Output,
					11 => ReportKind.Feature,
					_ => null,
				};
				if (reportKind.HasValue)
				{
					(ReportKind, byte) key = (reportKind.Value, b);
					hidReportDescriptor.bitLengths.TryGetValue(key, out var value);
					hidReportDescriptor.bitLengths[key] = value + num * num2;
				}
				break;
			}
			case 1:
				switch (num6)
				{
				case 7:
					num = (int)num7;
					break;
				case 8:
					b = (byte)num7;
					hidReportDescriptor.UsesReportIds = true;
					break;
				case 9:
					num2 = (int)num7;
					break;
				case 10:
					stack.Push((num, num2, b));
					break;
				case 11:
					if (stack.Count > 0)
					{
						(num, num2, b) = stack.Pop();
					}
					break;
				}
				break;
			}
			num3 += 1 + num4;
		}
		return hidReportDescriptor;
	}

	// Length in bytes including the report ID byte, as Windows reports it in HIDP_CAPS.
	public int GetReportLength(ReportKind kind, byte reportId)
	{
		if (!bitLengths.TryGetValue((kind, UsesReportIds ? reportId : ((byte)0)), out var value))
		{
			return 0;
		}
		return (value + 7) / 8 + 1;
	}
}
