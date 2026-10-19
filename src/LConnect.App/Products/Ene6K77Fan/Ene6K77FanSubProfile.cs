using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LConnectCore.Products.Ene6K77Fan;

namespace LConnect3cs.Products.Ene6K77Fan;

internal class Ene6K77FanSubProfile : INotifyPropertyChanged
{
	private int fanSpeed;

	private int groupIndex;

	private string groupName;

	private bool isIndividualMode;

	private LightingMode lightingMode;

	private LightingMode lightingModeInner;

	private LightingMode lightingModeOuter;

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

	public int GroupIndex
	{
		get
		{
			return groupIndex;
		}
		set
		{
			groupIndex = value;
			OnPropertyChanged("GroupIndex");
		}
	}

	public string GroupName
	{
		get
		{
			return groupName;
		}
		set
		{
			groupName = value;
			OnPropertyChanged("GroupName");
		}
	}

	public int FanQuantity { get; set; }

	public RPMSetting RPMSetting { get; set; }

	public bool IsIndividualMode
	{
		get
		{
			return isIndividualMode;
		}
		set
		{
			isIndividualMode = value;
			OnPropertyChanged("IsIndividualMode");
		}
	}

	public LightingMode LightingMode
	{
		get
		{
			return lightingMode;
		}
		set
		{
			lightingMode = value;
			OnPropertyChanged("LightingMode");
		}
	}

	public LightingMode LightingModeInner
	{
		get
		{
			return lightingModeInner;
		}
		set
		{
			lightingModeInner = value;
			OnPropertyChanged("LightingModeInner");
		}
	}

	public LightingMode LightingModeOuter
	{
		get
		{
			return lightingModeOuter;
		}
		set
		{
			lightingModeOuter = value;
			OnPropertyChanged("LightingModeOuter");
		}
	}

	public Dictionary<LightingMode, Ene6K77FanLightingSetting> LightingSettings { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
