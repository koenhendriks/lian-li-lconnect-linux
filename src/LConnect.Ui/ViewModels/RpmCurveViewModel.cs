using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LConnect3cs.Products;
using LConnectCore.Products;
using LConnectUi.Controls;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

public sealed class RpmModeItem
{
	public RPMMode Mode { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

// Fan profile editor: works on a clone of the selected mode's RPMProfile (like TLFanProfileSubPage's
// cloneSelectRPMProfile) and copies it back into the RPMSetting on apply.
public partial class RpmCurveViewModel : ObservableObject
{
	private RPMSetting setting;

	private RPMProfile clone;

	private Func<RPMMode, RPMPhaseKey, RPMPhaseInfo> defaults;

	private bool loading;

	[ObservableProperty]
	private RpmModeItem selectedMode;

	[ObservableProperty]
	private bool useGpu;

	[ObservableProperty]
	private bool isFixed;

	[ObservableProperty]
	private bool isStartStop;

	[ObservableProperty]
	private RPMPhase[] phases;

	[ObservableProperty]
	private int selectedPointIndex;

	[ObservableProperty]
	private int minSpeed;

	[ObservableProperty]
	private int maxSpeed = 2000;

	[ObservableProperty]
	private int chartMaxSpeed = 2000;

	[ObservableProperty]
	private double? temperature;

	[ObservableProperty]
	private bool hasCurve;

	[ObservableProperty]
	private bool isLocked;

	[ObservableProperty]
	private string fanSizeText = string.Empty;

	[ObservableProperty]
	private int pointTemperature;

	[ObservableProperty]
	private int pointSpeed;

	public ObservableCollection<RpmModeItem> Modes { get; } = new ObservableCollection<RpmModeItem>();

	public bool SupportsStartStop => setting?.SupportedStartStop ?? false;

	public bool CanEditCurve
	{
		get
		{
			if (HasCurve)
			{
				return !IsLocked;
			}
			return false;
		}
	}

	public bool ShowPointTemperature => !IsFixed;

	public string LockedHint => "Motherboard RPM sync is on: the fans follow the motherboard's PWM signal, so fan profiles are not used.";

	public RpmCurveViewModel()
	{
		SensorService.Instance.Updated += delegate
		{
			updateTemperature();
		};
	}

	internal void Load(RPMSetting rpmSetting, RPMMode[] order, Func<RPMMode, RPMPhaseKey, RPMPhaseInfo> defaultProvider)
	{
		setting = rpmSetting;
		defaults = defaultProvider;
		loading = true;
		try
		{
			Modes.Clear();
			if (rpmSetting == null)
			{
				SelectedMode = null;
				clone = null;
				HasCurve = false;
				Phases = null;
				return;
			}
			foreach (RPMMode rPMMode in order)
			{
				if (rpmSetting.Profiles.TryGetValue(rPMMode, out var value))
				{
					Modes.Add(new RpmModeItem
					{
						Mode = rPMMode,
						Name = (string.IsNullOrEmpty(value.Name) ? Names.Of(rPMMode) : value.Name)
					});
				}
			}
			SelectedMode = Modes.FirstOrDefault((RpmModeItem m) => m.Mode == rpmSetting.Mode) ?? Modes.FirstOrDefault();
		}
		finally
		{
			loading = false;
		}
		loadClone();
		OnPropertyChanged("SupportsStartStop");
	}

	partial void OnSelectedModeChanged(RpmModeItem value)
	{
		if (!loading)
		{
			loadClone();
		}
	}

	private void loadClone()
	{
		if (setting == null || SelectedMode == null || !setting.Profiles.TryGetValue(SelectedMode.Mode, out var value))
		{
			clone = null;
			HasCurve = false;
			Phases = null;
			return;
		}
		clone = value.Clone();
		loading = true;
		try
		{
			UseGpu = clone.RPMReferenceSource == RPMReferenceSource.GPU;
			IsFixed = clone.ControlType == RPMControlType.Fixed;
			IsStartStop = setting.SupportedStartStop && clone.IsStartStop;
			SelectedPointIndex = 0;
		}
		finally
		{
			loading = false;
		}
		loadPhaseInfo();
	}

	private void loadPhaseInfo()
	{
		RPMPhaseInfo rPMPhaseInfo = clone?.GetCurrentRPMPhaseInfo();
		FanSizeText = ((clone == null) ? string.Empty : ((clone.FanSize == FanSize.None) ? "unknown fan size" : $"{(int)clone.FanSize} mm fans"));
		if (rPMPhaseInfo == null || rPMPhaseInfo.Phases == null)
		{
			HasCurve = false;
			Phases = null;
			OnPropertyChanged("CanEditCurve");
			return;
		}
		loading = true;
		try
		{
			MinSpeed = rPMPhaseInfo.MinSpeed;
			MaxSpeed = rPMPhaseInfo.MaxSpeed;
			ChartMaxSpeed = Math.Max(rPMPhaseInfo.ChartMaxSpeed, rPMPhaseInfo.MaxSpeed);
			Phases = (RPMPhase[])rPMPhaseInfo.Phases.Clone();
			HasCurve = true;
			if (SelectedPointIndex >= Phases.Length)
			{
				SelectedPointIndex = 0;
			}
		}
		finally
		{
			loading = false;
		}
		syncPointFields();
		updateTemperature();
		OnPropertyChanged("CanEditCurve");
		OnPropertyChanged("ShowPointTemperature");
	}

	partial void OnUseGpuChanged(bool value)
	{
		if (!loading && clone != null)
		{
			clone.RPMReferenceSource = (value ? RPMReferenceSource.GPU : RPMReferenceSource.CPU);
			loadPhaseInfo();
		}
	}

	partial void OnIsFixedChanged(bool value)
	{
		if (!loading && clone != null)
		{
			clone.ControlType = (value ? RPMControlType.Fixed : RPMControlType.Curve);
			SelectedPointIndex = 0;
			loadPhaseInfo();
		}
		OnPropertyChanged("ShowPointTemperature");
	}

	partial void OnIsStartStopChanged(bool value)
	{
		if (!loading && clone != null)
		{
			clone.IsStartStop = value;
		}
	}

	partial void OnPhasesChanged(RPMPhase[] value)
	{
		if (!loading && clone != null && value != null)
		{
			RPMPhaseInfo currentRPMPhaseInfo = clone.GetCurrentRPMPhaseInfo();
			if (currentRPMPhaseInfo != null)
			{
				currentRPMPhaseInfo.Phases = (RPMPhase[])value.Clone();
			}
			syncPointFields();
		}
	}

	partial void OnSelectedPointIndexChanged(int value)
	{
		syncPointFields();
	}

	partial void OnIsLockedChanged(bool value)
	{
		OnPropertyChanged("CanEditCurve");
	}

	partial void OnHasCurveChanged(bool value)
	{
		OnPropertyChanged("CanEditCurve");
	}

	partial void OnPointTemperatureChanged(int value)
	{
		if (!loading)
		{
			movePoint(value, PointSpeed);
		}
	}

	partial void OnPointSpeedChanged(int value)
	{
		if (!loading)
		{
			movePoint(PointTemperature, value);
		}
	}

	private void movePoint(int temperature, int speed)
	{
		if (Phases != null && SelectedPointIndex >= 0 && SelectedPointIndex < Phases.Length)
		{
			Phases = CurveMath.Move(Phases, SelectedPointIndex, temperature, speed, MinSpeed, MaxSpeed);
		}
	}

	private void syncPointFields()
	{
		if (Phases == null || SelectedPointIndex < 0 || SelectedPointIndex >= Phases.Length)
		{
			return;
		}
		bool flag = loading;
		loading = true;
		try
		{
			PointTemperature = Phases[SelectedPointIndex].Temperature;
			PointSpeed = Phases[SelectedPointIndex].Speed;
		}
		finally
		{
			loading = flag;
		}
	}

	private void updateTemperature()
	{
		Temperature = SensorService.TemperatureFor(UseGpu ? RPMReferenceSource.GPU : RPMReferenceSource.CPU);
	}

	// Copies the edited clone into the setting (TLFanProfileSubPage.Apply).
	internal bool CommitTo(RPMSetting target)
	{
		if (target == null || clone == null)
		{
			return false;
		}
		target.Mode = clone.Mode;
		RPMProfile currentProfile = target.GetCurrentProfile();
		if (currentProfile == null)
		{
			return false;
		}
		currentProfile.IsStartStop = IsStartStop;
		currentProfile.RPMReferenceSource = clone.RPMReferenceSource;
		currentProfile.FanSize = clone.FanSize;
		currentProfile.ControlType = clone.ControlType;
		RPMPhaseInfo currentRPMPhaseInfo = clone.GetCurrentRPMPhaseInfo();
		RPMPhaseInfo currentRPMPhaseInfo2 = currentProfile.GetCurrentRPMPhaseInfo();
		if (currentRPMPhaseInfo != null && currentRPMPhaseInfo2 != null)
		{
			currentRPMPhaseInfo2.Phases = (RPMPhase[])currentRPMPhaseInfo.Phases.Clone();
		}
		return true;
	}

	internal bool Commit()
	{
		return CommitTo(setting);
	}

	// Default button: restore the factory curve of this mode for the current fan size/reference/control type.
	internal void ResetToDefault()
	{
		if (clone != null && defaults != null)
		{
			RPMPhaseKey currentRPMPhaseKey = clone.GetCurrentRPMPhaseKey();
			RPMPhaseInfo rPMPhaseInfo = defaults(clone.Mode, currentRPMPhaseKey);
			if (rPMPhaseInfo != null)
			{
				clone.PhaseInfos[currentRPMPhaseKey] = rPMPhaseInfo.Clone();
				loadPhaseInfo();
			}
		}
	}

	public string ModeName => SelectedMode?.Name ?? string.Empty;
}
