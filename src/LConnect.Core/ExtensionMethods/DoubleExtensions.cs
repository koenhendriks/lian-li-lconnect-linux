namespace LConnectCore.ExtensionMethods;

public static class DoubleExtensions
{
	public static double Constrain(this double value, double min, double max)
	{
		if (double.IsNaN(min) || double.IsNaN(max) || double.IsNaN(value))
		{
			return double.NaN;
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

	public static double ConstrainTo(this ref double value, double min, double max)
	{
		value = value.Constrain(min, max);
		return value;
	}
}
