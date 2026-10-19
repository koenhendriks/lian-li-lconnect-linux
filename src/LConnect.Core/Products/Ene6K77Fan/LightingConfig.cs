using System.Windows.Media;

namespace LConnectCore.Products.Ene6K77Fan;

public class LightingConfig
{
	public int Port { get; set; }

	public LightingMode Mode { get; set; }

	public LightingSpeed Speed { get; set; }

	public LightingDirection Direction { get; set; }

	public LightingBrightness Brightness { get; set; }

	public Color[] Colors { get; set; }

	public LightingConfig()
	{
		Mode = LightingMode.None;
		Speed = LightingSpeed.Normal;
		Direction = LightingDirection.Right;
		Brightness = LightingBrightness.Normal;
	}

	public LightingConfig Clone()
	{
		return new LightingConfig
		{
			Port = Port,
			Mode = Mode,
			Speed = Speed,
			Direction = Direction,
			Brightness = Brightness,
			Colors = (Color[])Colors.Clone()
		};
	}
}
