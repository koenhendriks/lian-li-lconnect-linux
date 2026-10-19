using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class TLFanGroupProfile : INotifyPropertyChanged
{
	private string name = string.Empty;

	private int fanSpeed;

	private bool isGrouping = true;

	[JsonIgnore]
	public TLFanPortProfile ParentPortProfile { get; set; }

	public int Index { get; set; }

	[JsonIgnore]
	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(Name))
			{
				return Name;
			}
			return $"Group{Index + 1}";
		}
	}

	public string Name
	{
		get
		{
			return name;
		}
		set
		{
			name = value;
			OnPropertyChanged("Name");
			OnPropertyChanged("DisplayName");
		}
	}

	[JsonIgnore]
	public int FanSpeed
	{
		get
		{
			return fanSpeed;
		}
		set
		{
			fanSpeed = value;
			OnPropertyChanged("FanSpeed");
		}
	}

	public bool IsSelectedAtLightApplyToAll { get; set; }

	public RPMSetting RPMSetting { get; set; } = new RPMSetting();

	public bool IsSwapLeftRightDirection { get; set; }

	public bool IsSwapTopBottomDirection { get; set; }

	public int FanQuantity { get; set; }

	public bool IsGrouping
	{
		get
		{
			return isGrouping;
		}
		set
		{
			isGrouping = value;
			OnPropertyChanged("IsGrouping");
		}
	}

	public bool IsSyncSideMode { get; set; }

	public bool IsSyncBottomSide { get; set; }

	public bool IsSingleSideMode { get; set; }

	public LightingMode LightingMode { get; set; } = LightingMode.Meteor;

	public LightingMode LightingModeTop { get; set; } = LightingMode.Meteor_Side;

	public LightingMode LightingModeBottom { get; set; } = LightingMode.Meteor_Side;

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettings { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettingsTop { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettingsBottom { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public TLFanFanProfile[] FanProfiles { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public TLFanGroupProfile()
	{
	}

	public TLFanGroupProfile(TLFanPortProfile parentPortProfile, int index, int maxFanCount)
	{
		ParentPortProfile = parentPortProfile;
		Index = index;
		FanProfiles = new TLFanFanProfile[maxFanCount];
		for (int i = 0; i < FanProfiles.Length; i++)
		{
			FanProfiles[i] = new TLFanFanProfile(this, i);
		}
	}
}
