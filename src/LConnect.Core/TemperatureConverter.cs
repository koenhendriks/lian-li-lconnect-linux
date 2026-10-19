namespace LConnectCore;

public sealed class TemperatureConverter
{
	private TemperatureConverter()
	{
	}

	public static double ConvertCtoF(double celsius)
	{
		return celsius * 1.8 + 32.0;
	}

	public static int ConvertCtoF(int celsius)
	{
		return (int)ConvertCtoF((double)celsius);
	}

	public static double ConvertFtoC(double fahrenheit)
	{
		return (fahrenheit - 32.0) / 1.8;
	}

	public static int ConvertFtoC(int fahrenheit)
	{
		return (int)ConvertFtoC((double)fahrenheit);
	}

	public static double ConvertCtoK(double celsius)
	{
		return celsius + 273.15;
	}

	public static int ConvertCtoK(int celsius)
	{
		return (int)ConvertCtoK((double)celsius);
	}

	public static double ConvertKtoC(double kelvin)
	{
		return kelvin - 273.15;
	}

	public static int ConvertKtoC(int kelvin)
	{
		return (int)ConvertKtoC((double)kelvin);
	}

	public static double ConvertFtoK(double fahrenheit)
	{
		return ConvertCtoK(ConvertFtoC(fahrenheit));
	}

	public static int ConvertFtoK(int fahrenheit)
	{
		return (int)ConvertFtoK((double)fahrenheit);
	}

	public static double ConvertKtoF(double kelvin)
	{
		return ConvertCtoF(ConvertKtoC(kelvin));
	}

	public static int ConvertKtoF(int kelvin)
	{
		return (int)ConvertKtoF((double)kelvin);
	}
}
