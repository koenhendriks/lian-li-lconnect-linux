using System;
using System.Windows.Media;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class TLFanLightingSetting
{
	public LightingMode Mode { get; set; }

	public Color[] Colors { get; set; }

	public int? Speed { get; set; }

	public int? Brightness { get; set; }

	public LightingDirection? Direction { get; set; }

	public TLFanLightingSetting()
	{
		Mode = LightingMode.Unknown;
		Colors = Array.Empty<Color>();
		Speed = null;
		Brightness = null;
		Direction = null;
	}

	public TLFanLightingSetting Clone()
	{
		TLFanLightingSetting tLFanLightingSetting = new TLFanLightingSetting();
		CopyTo(tLFanLightingSetting);
		return tLFanLightingSetting;
	}

	public void CopyTo(TLFanLightingSetting otherSetting)
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
		return string.Format("{0}(Mode={1}), {2}={3}, {4}={5}, {6}={7}, {8}={9})", "TLFanLightingSetting", Mode, "Colors", text, "Speed", Speed, "Brightness", Brightness, "Direction", Direction);
	}
}
