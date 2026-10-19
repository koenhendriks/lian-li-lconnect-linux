using System.Collections.Generic;
using System.Text.Json.Serialization;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class TLFanFanProfile
{
	[JsonIgnore]
	public TLFanGroupProfile ParentGroupProfile { get; set; }

	public int Index { get; set; }

	public bool IsSwapLeftRightDirection { get; set; }

	public bool IsSwapTopBottomDirection { get; set; }

	public LightingMode LightingMode { get; set; } = LightingMode.Rainbow;

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettings { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public TLFanFanProfile()
	{
	}

	public TLFanFanProfile(TLFanGroupProfile parentGroupProfile, int index)
	{
		ParentGroupProfile = parentGroupProfile;
		Index = index;
	}
}
