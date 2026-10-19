using System;
using LConnectCore.Products;

namespace LConnect3cs.Products;

internal struct RPMPhaseKey
{
	private const string Delimiter = "|";

	public FanSize FanSize { get; set; }

	public RPMReferenceSource ReferenceSource { get; set; }

	public RPMControlType ControlType { get; set; }

	public RPMPhaseKey(FanSize fanSize, RPMReferenceSource referenceSource, RPMControlType controlType)
	{
		FanSize = fanSize;
		ReferenceSource = referenceSource;
		ControlType = controlType;
	}

	public static bool TryParse(string key, out RPMPhaseKey phaseKey)
	{
		phaseKey = default;
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		string[] array = key.Split(new string[1] { "|" }, StringSplitOptions.None);
		if (array.Length != 3)
		{
			return false;
		}
		if (!Enum.IsDefined(typeof(FanSize), array[0]) || !Enum.TryParse<FanSize>(array[0], out var result))
		{
			return false;
		}
		if (!Enum.IsDefined(typeof(RPMReferenceSource), array[1]) || !Enum.TryParse<RPMReferenceSource>(array[1], out var result2))
		{
			return false;
		}
		if (!Enum.IsDefined(typeof(RPMControlType), array[2]) || !Enum.TryParse<RPMControlType>(array[2], out var result3))
		{
			return false;
		}
		phaseKey = new RPMPhaseKey(result, result2, result3);
		return true;
	}

	public override string ToString()
	{
		return string.Join("|", FanSize, ReferenceSource, ControlType);
	}
}
