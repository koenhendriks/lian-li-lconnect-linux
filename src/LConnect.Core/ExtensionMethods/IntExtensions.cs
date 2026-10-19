namespace LConnectCore.ExtensionMethods;

public static class IntExtensions
{
	public static int Constrain(this int value, int min, int max)
	{
		if (max < min)
		{
			int num = max;
			max = min;
			min = num;
		}
		if (value < min)
		{
			return min;
		}
		if (value > max)
		{
			return max;
		}
		return value;
	}

	public static int ConstrainTo(this ref int value, int min, int max)
	{
		value = value.Constrain(min, max);
		return value;
	}
}
