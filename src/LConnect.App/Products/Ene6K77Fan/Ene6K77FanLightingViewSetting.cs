namespace LConnect3cs.Products.Ene6K77Fan;

internal struct Ene6K77FanLightingViewSetting
{
	public int ColorCount { get; set; }

	public bool Speed { get; set; }

	public bool Brightness { get; set; }

	public bool Direction { get; set; }

	public Ene6K77FanLightingViewSetting(int colorCount = 0, bool speed = false, bool brightness = false, bool direction = false)
	{
		ColorCount = colorCount;
		Speed = speed;
		Brightness = brightness;
		Direction = direction;
	}

	public override string ToString()
	{
		return string.Format("{0}({1}={2}, {3}={4}, {5}={6}, {7}={8})", "Ene6K77FanLightingViewSetting", "ColorCount", ColorCount, "Speed", Speed, "Brightness", Brightness, "Direction", Direction);
	}
}
