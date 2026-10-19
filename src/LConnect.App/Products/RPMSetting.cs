using System.Collections.Generic;
using LConnectCore.Products;

namespace LConnect3cs.Products;

internal class RPMSetting
{
	public bool SupportedMotherboardSync { get; set; }

	public bool SupportedStartStop { get; set; }

	public bool SupportedRenameProfileName { get; set; }

	public FanSize[] SupportedFanSize { get; set; }

	public RPMReferenceSource[] SupportedRPMReferenceSource { get; set; }

	public RPMSpeedUnit SpeedUnit { get; set; }

	public RPMMode Mode { get; set; }

	public Dictionary<RPMMode, RPMProfile> Profiles { get; set; } = new Dictionary<RPMMode, RPMProfile>();

	public RPMProfile GetCurrentProfile()
	{
		if (Profiles.TryGetValue(Mode, out var value))
		{
			return value;
		}
		return null;
	}

	public RPMSetting Clone()
	{
		RPMSetting rPMSetting = new RPMSetting
		{
			SupportedMotherboardSync = SupportedMotherboardSync,
			SupportedStartStop = SupportedStartStop,
			SupportedRenameProfileName = SupportedRenameProfileName,
			SupportedFanSize = (FanSize[])SupportedFanSize?.Clone(),
			SupportedRPMReferenceSource = (RPMReferenceSource[])SupportedRPMReferenceSource?.Clone(),
			SpeedUnit = SpeedUnit,
			Mode = Mode,
			Profiles = new Dictionary<RPMMode, RPMProfile>()
		};
		foreach (KeyValuePair<RPMMode, RPMProfile> profile in Profiles)
		{
			rPMSetting.Profiles.Add(profile.Key, profile.Value.Clone());
		}
		return rPMSetting;
	}

	public void CopyTo(RPMSetting otherSetting)
	{
		if (otherSetting == null)
		{
			return;
		}
		otherSetting.SupportedMotherboardSync = SupportedMotherboardSync;
		otherSetting.SupportedStartStop = SupportedStartStop;
		otherSetting.SupportedRenameProfileName = SupportedRenameProfileName;
		otherSetting.SupportedFanSize = (FanSize[])SupportedFanSize?.Clone();
		otherSetting.SupportedRPMReferenceSource = (RPMReferenceSource[])SupportedRPMReferenceSource?.Clone();
		otherSetting.SpeedUnit = SpeedUnit;
		otherSetting.Mode = Mode;
		otherSetting.Profiles = new Dictionary<RPMMode, RPMProfile>();
		foreach (KeyValuePair<RPMMode, RPMProfile> profile in Profiles)
		{
			otherSetting.Profiles.Add(profile.Key, profile.Value.Clone());
		}
	}
}
