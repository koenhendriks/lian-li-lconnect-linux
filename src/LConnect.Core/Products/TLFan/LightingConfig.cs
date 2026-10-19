using System.Windows.Media;

namespace LConnectCore.Products.TLFan;

public class LightingConfig
{
	public LightingMode Mode { get; set; } = LightingMode.Meteor;

	public byte Speed { get; set; } = 2;

	public LightingDirection Direction { get; set; }

	public byte Brightness { get; set; } = 2;

	public Color[] Colors { get; set; }

	public LightingConfig Clone()
	{
		return new LightingConfig
		{
			Mode = Mode,
			Speed = Speed,
			Direction = Direction,
			Brightness = Brightness,
			Colors = (Color[])Colors.Clone()
		};
	}
}
