using System;
using System.Text;
using LConnectCore.Products;
using LConnectCore.Products.TLLCD;

namespace LConnectUi.Services;

// Display names, looked up in the original locale dictionaries where the original UI had them.
public static class Names
{
	private static readonly string[] ModeSuffixes = new string[6] { "_Side_Merge", "_Merge", "_Side", "_Inner", "_Outer", "_Fan" };

	public static string LightingEffect(string enumName)
	{
		string text = enumName;
		string[] modeSuffixes = ModeSuffixes;
		foreach (string text2 in modeSuffixes)
		{
			if (text.EndsWith(text2, StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - text2.Length);
				break;
			}
		}
		return Loc.Get("I18n.LightingEffect." + text, SplitCamel(text));
	}

	public static string Of(LConnectCore.Products.TLFan.LightingMode mode)
	{
		return LightingEffect(mode.ToString());
	}

	public static string Of(LConnectCore.Products.Ene6K77Fan.LightingMode mode)
	{
		return LightingEffect(mode.ToString());
	}

	public static string Of(RPMMode mode)
	{
		string text = mode switch
		{
			RPMMode.UserDefine1 => "Custom 1",
			RPMMode.UserDefine2 => "Custom 2",
			RPMMode.UserDefine3 => "Custom 3",
			RPMMode.UserDefine4 => "Custom 4",
			RPMMode.StandardSpeed => "Standard",
			RPMMode.HighSpeed => "High speed",
			RPMMode.FullSpeed => "Full speed",
			_ => SplitCamel(mode.ToString()),
		};
		return Loc.Get("I18n.Text.RPMMode." + mode, text);
	}

	public static string Of(TLLCDGroupType type)
	{
		return type switch
		{
			TLLCDGroupType.Group1 => "1",
			TLLCDGroupType.Group11 => "1-1",
			TLLCDGroupType.Group111 => "1-1-1",
			TLLCDGroupType.Group12 => "1-2",
			TLLCDGroupType.Group2 => "2",
			TLLCDGroupType.Group21 => "2-1",
			TLLCDGroupType.Group3 => "3",
			_ => type.ToString(),
		};
	}

	public static string Of(TLLCDSensorType type)
	{
		return type switch
		{
			TLLCDSensorType.CPUTemp => Loc.Get("I18n.Text.CPUTemp", "CPU Temp"),
			TLLCDSensorType.GPUTemp => Loc.Get("I18n.Text.GPUTemp", "GPU Temp"),
			TLLCDSensorType.CPULoad => Loc.Get("I18n.Text.CPULoad", "CPU Load"),
			TLLCDSensorType.GPULoad => Loc.Get("I18n.Text.GPULoad", "GPU Load"),
			TLLCDSensorType.FanRPM => "Fan RPM",
			TLLCDSensorType.CPUTempLoad => "CPU Temp + Load",
			_ => type.ToString(),
		};
	}

	public static string Rotation(ScreenRotation rotation)
	{
		return rotation switch
		{
			ScreenRotation.Rotate90 => "90°",
			ScreenRotation.Rotate180 => "180°",
			ScreenRotation.Rotate270 => "270°",
			_ => "0°",
		};
	}

	public static string LocaleName(string locale)
	{
		return locale switch
		{
			"en-us" => "English",
			"de-de" => "Deutsch",
			"es-es" => "Español",
			"fr-fr" => "Français",
			"zh-cn" => "简体中文",
			"zh-tw" => "繁體中文",
			_ => locale,
		};
	}

	public static string SplitCamel(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length + 8);
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '_')
			{
				stringBuilder.Append(' ');
				continue;
			}
			if (i > 0 && char.IsUpper(c) && !char.IsUpper(text[i - 1]) && text[i - 1] != '_')
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append(c);
		}
		return stringBuilder.ToString();
	}
}
