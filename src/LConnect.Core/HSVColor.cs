using System;
using System.Windows.Media;
using LConnectCore.ExtensionMethods;

namespace LConnectCore;

public struct HSVColor
{
	public const int MinHue = 0;

	public const int MaxHue = 360;

	public const int MinSaturation = 0;

	public const int MaxSaturation = 100;

	public const int MinValue = 0;

	public const int MaxValue = 100;

	private int hue;

	private int saturation;

	private int value;

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

	public int Value
	{
		get
		{
			return value;
		}
		set
		{
			this.value = value.Constrain(0, 100);
		}
	}

	public HSVColor(int hue, int saturation, int value)
	{
		this.hue = 0;
		this.saturation = 0;
		this.value = 0;
		this.hue = hue.Constrain(0, 360);
		this.saturation = saturation.Constrain(0, 100);
		this.value = value.Constrain(0, 100);
	}

	public static HSVColor FromColor(Color color)
	{
		return FromRgb(color.R, color.G, color.B);
	}

	public static HSVColor FromRgb(byte red, byte green, byte blue)
	{
		HSVColor result = new HSVColor(0, 0, 0);
		double num = result.byteToScale(red);
		double num2 = result.byteToScale(green);
		double num3 = result.byteToScale(blue);
		double num4 = Math.Min(Math.Min(num, num2), num3);
		double num5 = Math.Max(Math.Max(num, num2), num3);
		double num6 = num5 - num4;
		double num7 = num5;
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
		double num9 = ((num7 == 0.0) ? 0.0 : (num6 / num7));
		result.Saturation = (int)Math.Floor(num9 * 100.0);
		result.Value = (int)Math.Floor(num7 * 100.0);
		return result;
	}

	public Color ToColor()
	{
		double num = (double)Saturation / 100.0;
		double num2 = (double)Value / 100.0;
		double num3 = num2 * num;
		int num4 = Hue / 60;
		double num5 = num3 * (1.0 - Math.Abs((double)Hue / 60.0 % 2.0 - 1.0));
		(double, double, double) tuple = num4 switch
		{
			0 => (num3, num5, 0.0), 
			1 => (num5, num3, 0.0), 
			2 => (0.0, num3, num5), 
			3 => (0.0, num5, num3), 
			4 => (num5, 0.0, num3), 
			_ => (num3, 0.0, num5), 
		};
		double item = tuple.Item1;
		double item2 = tuple.Item2;
		double item3 = tuple.Item3;
		double num6 = num2 - num3;
		byte r = scaleToByte(item + num6);
		byte g = scaleToByte(item2 + num6);
		byte b = scaleToByte(item3 + num6);
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
