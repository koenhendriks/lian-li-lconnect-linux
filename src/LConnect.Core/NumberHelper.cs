using System;

namespace LConnectCore;

public sealed class NumberHelper
{
	private const string OrderOfMagnitudeSymbols = "kMGTPEZYRQ";

	private static (double, char) convertNumberToMagnitude(double value, double based)
	{
		int num = -1;
		while (value > based && num < "kMGTPEZYRQ".Length)
		{
			value /= based;
			num++;
		}
		num = Math.Min(num, "kMGTPEZYRQ".Length - 1);
		char item = ((num != -1) ? "kMGTPEZYRQ"[num] : '\0');
		return (value, item);
	}

	private static (double, char) convertNumberToMagnitude(ulong number, double based)
	{
		return convertNumberToMagnitude((double)number, based);
	}

	public static (double, char) ConvertNumberToMagnitude1000(ulong number)
	{
		return convertNumberToMagnitude(number, 1000.0);
	}

	public static (double, char) ConvertNumberToMagnitude1000(double value)
	{
		return convertNumberToMagnitude(value, 1000.0);
	}

	public static (double, char) ConvertNumberToMagnitude1024(ulong number)
	{
		return convertNumberToMagnitude(number, 1024.0);
	}

	public static (double, char) ConvertNumberToMagnitude1024(double value)
	{
		return convertNumberToMagnitude(value, 1024.0);
	}
}
