namespace LConnect3cs.Products.TLFan;

internal struct TLFanLightingViewSetting
{
	public int ColorCount { get; set; }

	public bool Speed { get; set; }

	public bool Brightness { get; set; }

	public bool Direction { get; set; }

	public TLFanLightingViewSetting(int colorCount = 0, bool speed = false, bool brightness = false, bool direction = false)
	{
		ColorCount = colorCount;
		Speed = speed;
		Brightness = brightness;
		Direction = direction;
	}

	public override string ToString()
	{
		return string.Format("{0}({1}={2}, {3}={4}, {5}={6}, {7}={8})", "TLFanLightingViewSetting", "ColorCount", ColorCount, "Speed", Speed, "Brightness", Brightness, "Direction", Direction);
	}
}
