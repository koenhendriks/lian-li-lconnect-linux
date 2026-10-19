namespace LConnectCore.Products;

public struct RPMPhase
{
	public int Temperature { get; set; }

	public int Speed { get; set; }

	public RPMPhase(int temperature, int value)
	{
		Temperature = temperature;
		Speed = value;
	}

	public override string ToString()
	{
		return string.Format("{0}({1}={2}, {3}={4})", "RPMPhase", "Temperature", Temperature, "Speed", Speed);
	}
}
