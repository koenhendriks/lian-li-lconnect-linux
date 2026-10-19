using System;
using System.Windows.Media;
using LConnectCore.ExtensionMethods;

namespace LConnect3cs.Utils;

public struct HSLColor
{
	public const int MinHue = 0;

	public const int MaxHue = 360;

	public const int MinSaturation = 0;

	public const int MaxSaturation = 100;

	public const int MinLightness = 0;

	public const int MaxLightness = 100;

	private int hue;

	private int saturation;

	private int lightness;

	public int Hue
	{
		get
		{
			return hue;
		}
		set
		{
			hue = value.Constrain(0, 360);
		}
	}

	public int Saturation
	{
		get
		{
			return saturation;
		}
		set
		{
			saturation = value.Constrain(0, 100);
		}
	}

	public int Lightness
	{
		get
		{
			return lightness;
		}
		set
		{
			lightness = value.Constrain(0, 100);
		}
	}

	public HSLColor(int hue, int saturation, int lightness)
	{
		this.hue = 0;
		this.saturation = 0;
		this.lightness = 0;
		this.hue = hue.Constrain(0, 360);
		this.saturation = saturation.Constrain(0, 100);
		this.lightness = lightness.Constrain(0, 100);
	}

	public static HSLColor FromColor(Color color)
	{
		return FromRgb(color.R, color.G, color.B);
	}

	public static HSLColor FromRgb(byte red, byte green, byte blue)
	{
		HSLColor result = new HSLColor(0, 0, 0);
		double num = result.byteToScale(red);
		double num2 = result.byteToScale(green);
		double num3 = result.byteToScale(blue);
		double num4 = Math.Min(Math.Min(num, num2), num3);
		double num5 = Math.Max(Math.Max(num, num2), num3);
		double num6 = num5 - num4;
		double num7 = (num5 + num4) / 2.0;
		if (num6 == 0.0)
		{
			result.Hue = 0;
		}
		else
		{
			double num8 = 0.0;
			if (num5 == num)
			{
				num8 = (num2 - num3) / num6 % 6.0;
			}
			else if (num5 == num2)
			{
				num8 = (num3 - num) / num6 + 2.0;
			}
			else if (num5 == num3)
			{
				num8 = (num - num2) / num6 + 4.0;
			}
			result.Hue = (int)Math.Floor(60.0 * num8) % 360;
		}
		double num9 = ((!(num7 <= 0.0) && !(num7 >= 1.0)) ? (num6 / (1.0 - Math.Abs(2.0 * num5 - num6 - 1.0))) : 0.0);
		result.Saturation = (int)Math.Floor(num9 * 100.0);
		result.Lightness = (int)Math.Floor(num7 * 100.0);
		return result;
	}

	public Color ToColor()
	{
		double num = (double)Saturation / 100.0;
		double num2 = (double)Lightness / 100.0;
		double num3 = (1.0 - Math.Abs(2.0 * num2 - 1.0)) * num;
		int num4 = Hue / 60;
		double num5 = num3 * (1.0 - Math.Abs((double)Hue / 60.0 % 2.0 - 1.0));
		double num6;
		double num7;
		double num8;
		switch (num4)
		{
		case 0:
			num6 = num3;
			num7 = num5;
			num8 = 0.0;
			break;
		case 1:
			num6 = num5;
			num7 = num3;
			num8 = 0.0;
			break;
		case 2:
			num6 = 0.0;
			num7 = num3;
			num8 = num5;
			break;
		case 3:
			num6 = 0.0;
			num7 = num5;
			num8 = num3;
			break;
		case 4:
			num6 = num5;
			num7 = 0.0;
			num8 = num3;
			break;
		default:
			num6 = num3;
			num7 = 0.0;
			num8 = num5;
			break;
		}
		double num9 = num2 - num3 / 2.0;
		byte r = scaleToByte(num6 + num9);
		byte g = scaleToByte(num7 + num9);
		byte b = scaleToByte(num8 + num9);
		return Color.FromRgb(r, g, b);
	}

	private byte scaleToByte(double scaleRgb)
	{
		return (byte)(scaleRgb * 255.0);
	}

	private double byteToScale(byte rgb)
	{
		return (double)(int)rgb / 255.0;
	}
}
