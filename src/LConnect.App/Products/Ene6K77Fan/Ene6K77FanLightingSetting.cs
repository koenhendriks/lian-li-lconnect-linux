using System;
using System.Windows.Media;
using LConnectCore.Products.Ene6K77Fan;

namespace LConnect3cs.Products.Ene6K77Fan;

internal class Ene6K77FanLightingSetting
{
	public LightingMode Mode { get; set; }

	public Color[] Colors { get; set; }

	public int? Speed { get; set; }

	public int? Brightness { get; set; }

	public LightingDirection? Direction { get; set; }

	public Ene6K77FanLightingSetting()
	{
		Mode = LightingMode.None;
		Colors = Array.Empty<Color>();
		Speed = null;
		Brightness = null;
		Direction = null;
	}

	public Ene6K77FanLightingSetting Clone()
	{
		Ene6K77FanLightingSetting ene6K77FanLightingSetting = new Ene6K77FanLightingSetting();
		CopyTo(ene6K77FanLightingSetting);
		return ene6K77FanLightingSetting;
	}

	public void CopyTo(Ene6K77FanLightingSetting otherSetting)
	{
		if (otherSetting == null)
		{
			return;
		}
		otherSetting.Mode = Mode;
		otherSetting.Speed = Speed;
		otherSetting.Brightness = Brightness;
		otherSetting.Direction = Direction;
		if (Colors.Length != 0)
		{
			otherSetting.Colors = new Color[Colors.Length];
			for (int i = 0; i < Colors.Length; i++)
			{
				otherSetting.Colors[i] = Colors[i];
			}
		}
		else
		{
			otherSetting.Colors = Array.Empty<Color>();
		}
	}

	public override string ToString()
	{
		string text = ((Colors.Length == 0) ? string.Empty : ("{" + string.Join(", ", Colors) + "}"));
		return string.Format("{0}(Mode={1}), {2}={3}, {4}={5}, {6}={7}, {8}={9})", "Ene6K77FanLightingSetting", Mode, "Colors", text, "Speed", Speed, "Brightness", Brightness, "Direction", Direction);
	}
}
