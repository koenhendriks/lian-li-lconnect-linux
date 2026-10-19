using AColor = Avalonia.Media.Color;
using WColor = System.Windows.Media.Color;

namespace LConnectUi.Services;

// The device library uses its own System.Windows.Media.Color stand-in; convert at the UI boundary.
public static class ColorUtil
{
	public static AColor ToAvalonia(this WColor c)
	{
		return AColor.FromRgb(c.R, c.G, c.B);
	}

	public static WColor ToDevice(this AColor c)
	{
		return WColor.FromRgb(c.R, c.G, c.B);
	}

	public static AColor FromRgbArray(int[] rgb, AColor fallback)
	{
		if (rgb == null || rgb.Length < 3)
		{
			return fallback;
		}
		return AColor.FromRgb((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
	}

	public static int[] ToRgbArray(AColor c)
	{
		return new int[3] { c.R, c.G, c.B };
	}
}
