using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LConnectCore.Products;

using LConnect3cs.Utils;

namespace LConnect3cs.Products;

internal class RPMProfile : INotifyPropertyChanged
{
	private string name;

	private bool isStartStop;

	private FanSize fanSize;

	private RPMReferenceSource rpmReferenceSource;

	public RPMMode Mode { get; private set; }

	[JsonIgnore]
	public string DisplayName
	{
		get
		{
			if (string.IsNullOrEmpty(Name) && I18n.TryGet($"I18n.Text.RPMMode.{Mode}") is string result)
			{
				return result;
			}
			return Name;
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

	public bool IsStartStop
	{
		get
		{
			return isStartStop;
		}
		set
		{
			isStartStop = value;
			OnPropertyChanged("IsStartStop");
		}
	}

	public FanSize FanSize
	{
		get
		{
			return fanSize;
		}
		set
		{
			fanSize = value;
			OnPropertyChanged("FanSize");
		}
	}

	public RPMReferenceSource RPMReferenceSource
	{
		get
		{
			return rpmReferenceSource;
		}
		set
		{
			rpmReferenceSource = value;
			OnPropertyChanged("RPMReferenceSource");
		}
	}

	public RPMControlType ControlType { get; set; }

	[JsonConverter(typeof(RPMPhaseInfoDictionaryJsonConverter))]
	public Dictionary<RPMPhaseKey, RPMPhaseInfo> PhaseInfos { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public RPMProfile(RPMMode mode)
	{
		Mode = mode;
	}

	public RPMPhaseKey GetCurrentRPMPhaseKey()
	{
		return new RPMPhaseKey(FanSize, RPMReferenceSource, ControlType);
	}

	public RPMPhaseInfo GetCurrentRPMPhaseInfo()
	{
		RPMPhaseKey currentRPMPhaseKey = GetCurrentRPMPhaseKey();
		if (PhaseInfos.ContainsKey(currentRPMPhaseKey))
		{
			return PhaseInfos[currentRPMPhaseKey];
		}
		return null;
	}

	public RPMProfile Clone()
	{
		RPMProfile rPMProfile = new RPMProfile(Mode)
		{
			Name = Name,
			IsStartStop = IsStartStop,
			FanSize = FanSize,
			RPMReferenceSource = RPMReferenceSource,
			ControlType = ControlType,
			PhaseInfos = new Dictionary<RPMPhaseKey, RPMPhaseInfo>()
		};
		foreach (KeyValuePair<RPMPhaseKey, RPMPhaseInfo> phaseInfo in PhaseInfos)
		{
			rPMProfile.PhaseInfos.Add(phaseInfo.Key, phaseInfo.Value.Clone());
		}
		return rPMProfile;
	}

	public RPMConfig ToRPMConfig()
	{
		RPMPhaseInfo currentRPMPhaseInfo = GetCurrentRPMPhaseInfo();
		if (currentRPMPhaseInfo != null)
		{
			return new RPMConfig
			{
				MaxSpeed = currentRPMPhaseInfo.MaxSpeed,
				Reference = RPMReferenceSource,
				Phases = currentRPMPhaseInfo.ToRPMConfigPhases(IsStartStop)
			};
		}
		return null;
	}
}
