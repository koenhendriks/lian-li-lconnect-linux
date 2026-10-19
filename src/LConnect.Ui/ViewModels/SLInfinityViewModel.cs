using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LConnect3cs.Helpers;
using LConnect3cs.Products;
using LConnect3cs.Products.Ene6K77Fan;
using LConnectCore.Products;
using LConnectCore.Products.Ene6K77Fan;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

public partial class SLGroupItem : ObservableObject
{
	[ObservableProperty]
	private int rpm;

	[ObservableProperty]
	private int fanQuantity;

	[ObservableProperty]
	private string modeText = string.Empty;

	[ObservableProperty]
	private string profileText = string.Empty;

	public int Index { get; init; }

	public string Name { get; init; }

	public string RpmText => $"{Rpm} rpm";

	public string FansText => $"{FanQuantity} fan(s)";

	partial void OnRpmChanged(int value)
	{
		OnPropertyChanged("RpmText");
	}

	partial void OnFanQuantityChanged(int value)
	{
		OnPropertyChanged("FansText");
	}
}

public sealed class SLModeItem
{
	public LightingMode Mode { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public sealed class SLScopeItem
{
	public LightingScope Scope { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

// SL Infinity hub: a port of Ene6K77FanMainPage (lighting per group with inner/outer rings or merged across
// groups, fan profile per group) plus the fan quantity of the device settings page.
public partial class SLInfinityViewModel : PageViewModel
{
	private bool loading;

	private string signature = string.Empty;

	[ObservableProperty]
	private ControllerItem selectedController;

	[ObservableProperty]
	private SLGroupItem selectedGroup;

	[ObservableProperty]
	private SLScopeItem selectedScope;

	[ObservableProperty]
	private SLModeItem selectedMode;

	[ObservableProperty]
	private bool isMerged;

	[ObservableProperty]
	private bool isMergeAvailable;

	[ObservableProperty]
	private int fanQuantity;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private bool hasController;

	public override string Title => Loc.Get("I18n.MainMenu.SLInfinityUtility", "SL Infinity Utility");

	public override string IconKey => "Ring";

	protected override TimeSpan PollInterval => TimeSpan.FromSeconds(1.0);

	public ObservableCollection<ControllerItem> Controllers { get; } = new ObservableCollection<ControllerItem>();

	public ObservableCollection<SLGroupItem> Groups { get; } = new ObservableCollection<SLGroupItem>();

	public ObservableCollection<SLScopeItem> Scopes { get; } = new ObservableCollection<SLScopeItem>();

	public ObservableCollection<SLModeItem> Modes { get; } = new ObservableCollection<SLModeItem>();

	public LightingEditorViewModel Editor { get; } = new LightingEditorViewModel();

	public RpmCurveViewModel Curve { get; } = new RpmCurveViewModel();

	public bool MultipleControllers => Controllers.Count > 1;

	public string NoDeviceHint
	{
		get
		{
			if (!ControllerHub.Instance.DaemonReachable)
			{
				return "lconnectd is not reachable (" + DaemonClient.SocketPath + "). Is the service running and are you in the 'lconnect' group?";
			}
			return "No SL Infinity controller connected.";
		}
	}

	internal SLInfinityController Controller => (SelectedController == null) ? null : ControllerHub.Instance.FindSL(SelectedController.DevicePath);

	private Ene6K77FanSubProfile sub
	{
		get
		{
			SLInfinityController controller = Controller;
			if (controller == null || SelectedGroup == null)
			{
				return null;
			}
			return controller.Profile.SubProfiles.FirstOrDefault((Ene6K77FanSubProfile s) => s.GroupIndex == SelectedGroup.Index);
		}
	}

	private LightingScope scope => SelectedScope?.Scope ?? LightingScope.All;

	public SLInfinityViewModel()
	{
		Scopes.Add(new SLScopeItem
		{
			Scope = LightingScope.All,
			Name = Loc.Get("I18n.Text.All", "All")
		});
		Scopes.Add(new SLScopeItem
		{
			Scope = LightingScope.Inner,
			Name = "Inner ring"
		});
		Scopes.Add(new SLScopeItem
		{
			Scope = LightingScope.Outer,
			Name = "Outer ring"
		});
		selectedScope = Scopes[0];
		ControllerHub.Instance.Changed += delegate
		{
			if (IsActive)
			{
				Refresh();
			}
		};
	}

	protected override void OnActivated()
	{
		signature = string.Empty;
		Refresh();
	}

	protected override void OnPoll()
	{
		Refresh();
		updateLive();
	}

	public void Refresh()
	{
		List<SLInfinityController> sLInfinityControllers = ControllerHub.Instance.SLInfinityControllers;
		string text = (ControllerHub.Instance.DaemonReachable ? "R" : "-") + string.Join("|", sLInfinityControllers.Select((SLInfinityController c) => c.DevicePath + "@" + c.Profile.GetHashCode()));
		if (text == signature)
		{
			return;
		}
		signature = text;
		string keep = SelectedController?.DevicePath;
		int? num = SelectedGroup?.Index;
		loading = true;
		try
		{
			Controllers.Clear();
			int num2 = 1;
			foreach (SLInfinityController item in sLInfinityControllers)
			{
				Controllers.Add(new ControllerItem
				{
					DevicePath = item.DevicePath,
					Name = ((sLInfinityControllers.Count > 1) ? $"SL Infinity {num2++}" : "SL Infinity")
				});
			}
			HasController = Controllers.Count > 0;
			OnPropertyChanged("MultipleControllers");
			OnPropertyChanged("NoDeviceHint");
			SelectedController = Controllers.FirstOrDefault((ControllerItem c) => c.DevicePath == keep) ?? Controllers.FirstOrDefault();
			rebuildGroups(num);
		}
		finally
		{
			loading = false;
		}
		loadGroup();
	}

	private void rebuildGroups(int? keep)
	{
		Groups.Clear();
		SLInfinityController controller = Controller;
		if (controller != null)
		{
			Ene6K77FanSubProfile[] subProfiles = controller.Profile.SubProfiles;
			foreach (Ene6K77FanSubProfile ene6K77FanSubProfile in subProfiles)
			{
				Groups.Add(new SLGroupItem
				{
					Index = ene6K77FanSubProfile.GroupIndex,
					Name = (string.IsNullOrEmpty(ene6K77FanSubProfile.GroupName) ? $"Port{ene6K77FanSubProfile.GroupIndex + 1}" : ene6K77FanSubProfile.GroupName),
					FanQuantity = ene6K77FanSubProfile.FanQuantity,
					Rpm = ene6K77FanSubProfile.FanSpeed
				});
			}
			updateGroupTexts();
		}
		SelectedGroup = Groups.FirstOrDefault((SLGroupItem g) => g.Index == keep) ?? Groups.FirstOrDefault();
	}

	private void updateGroupTexts()
	{
		SLInfinityController controller = Controller;
		if (controller == null)
		{
			return;
		}
		Ene6K77FanProfile profile = controller.Profile;
		foreach (SLGroupItem group in Groups)
		{
			Ene6K77FanSubProfile ene6K77FanSubProfile = profile.SubProfiles.FirstOrDefault((Ene6K77FanSubProfile s) => s.GroupIndex == group.Index);
			if (ene6K77FanSubProfile != null)
			{
				group.ModeText = (profile.IsMerged ? (Names.Of(profile.LightingModeBeforeMerge.Value) + " (merged)") : (ene6K77FanSubProfile.IsIndividualMode ? (Names.Of(ene6K77FanSubProfile.LightingModeInner) + " / " + Names.Of(ene6K77FanSubProfile.LightingModeOuter)) : Names.Of(ene6K77FanSubProfile.LightingMode)));
				group.ProfileText = Names.Of(ene6K77FanSubProfile.RPMSetting.Mode);
				group.FanQuantity = ene6K77FanSubProfile.FanQuantity;
			}
		}
	}

	private void updateLive()
	{
		SLInfinityController controller = Controller;
		if (controller == null)
		{
			return;
		}
		foreach (SLGroupItem group in Groups)
		{
			Ene6K77FanSubProfile ene6K77FanSubProfile = controller.Profile.SubProfiles.FirstOrDefault((Ene6K77FanSubProfile s) => s.GroupIndex == group.Index);
			if (ene6K77FanSubProfile != null)
			{
				group.Rpm = ene6K77FanSubProfile.FanSpeed;
			}
		}
	}

	partial void OnSelectedControllerChanged(ControllerItem value)
	{
		if (!loading)
		{
			loading = true;
			try
			{
				rebuildGroups(null);
			}
			finally
			{
				loading = false;
			}
			loadGroup();
		}
	}

	partial void OnSelectedGroupChanged(SLGroupItem value)
	{
		if (!loading)
		{
			loadGroup();
		}
	}

	// FanGroupRadioButton_Checked.
	private void loadGroup()
	{
		SLInfinityController controller = Controller;
		Ene6K77FanSubProfile ene6K77FanSubProfile = sub;
		if (controller == null || ene6K77FanSubProfile == null)
		{
			Modes.Clear();
			Curve.Load(null, Array.Empty<RPMMode>(), null);
			return;
		}
		Ene6K77FanProfile profile = controller.Profile;
		loading = true;
		try
		{
			FanQuantity = ene6K77FanSubProfile.FanQuantity;
			IsMerged = profile.IsMerged;
			LightingMode mode;
			if (profile.IsMerged)
			{
				SelectedScope = Scopes[0];
				mode = profile.LightingModeBeforeMerge.Value;
			}
			else if (ene6K77FanSubProfile.IsIndividualMode)
			{
				SelectedScope = Scopes[1];
				mode = ene6K77FanSubProfile.LightingModeInner;
			}
			else
			{
				SelectedScope = Scopes[0];
				mode = ene6K77FanSubProfile.LightingMode;
			}
			fillModes(mode);
		}
		finally
		{
			loading = false;
		}
		loadEditor();
		Curve.Load(ene6K77FanSubProfile.RPMSetting, profile.GetFanModeOrder(), profile.GetDefaultFanRPMPhaseInfo);
		Curve.IsLocked = UiSettings.Current.MotherboardRPMSync && ene6K77FanSubProfile.RPMSetting.SupportedMotherboardSync;
	}

	private void fillModes(LightingMode select)
	{
		SLInfinityController controller = Controller;
		Modes.Clear();
		if (controller == null)
		{
			return;
		}
		Ene6K77FanProfile profile = controller.Profile;
		LightingMode[] array = scope switch
		{
			LightingScope.Inner => profile.GetLightingModeInnerOrder(),
			LightingScope.Outer => profile.GetLightingModeOuterOrder(),
			_ => profile.GetLightingModeOrder(),
		};
		LightingMode[] array2 = array;
		foreach (LightingMode lightingMode in array2)
		{
			Modes.Add(new SLModeItem
			{
				Mode = lightingMode,
				Name = Names.Of(lightingMode)
			});
		}
		SelectedMode = Modes.FirstOrDefault((SLModeItem m) => m.Mode == select) ?? Modes.FirstOrDefault();
	}

	partial void OnSelectedScopeChanged(SLScopeItem value)
	{
		if (!loading && value != null)
		{
			Ene6K77FanSubProfile ene6K77FanSubProfile = sub;
			if (ene6K77FanSubProfile != null)
			{
				LightingMode select = value.Scope switch
				{
					LightingScope.Inner => ene6K77FanSubProfile.LightingModeInner,
					LightingScope.Outer => ene6K77FanSubProfile.LightingModeOuter,
					_ => ene6K77FanSubProfile.LightingMode,
				};
				loading = true;
				try
				{
					fillModes(select);
				}
				finally
				{
					loading = false;
				}
				loadEditor();
			}
		}
	}

	partial void OnSelectedModeChanged(SLModeItem value)
	{
		if (!loading)
		{
			loadEditor();
		}
	}

	partial void OnIsMergedChanged(bool value)
	{
		if (!loading)
		{
			loadEditor();
		}
	}

	private Ene6K77FanLightingSetting editedSetting(bool create)
	{
		SLInfinityController controller = Controller;
		Ene6K77FanSubProfile ene6K77FanSubProfile = sub;
		if (controller == null || ene6K77FanSubProfile == null || SelectedMode == null)
		{
			return null;
		}
		Ene6K77FanProfile profile = controller.Profile;
		LightingMode mode = SelectedMode.Mode;
		Dictionary<LightingMode, Ene6K77FanLightingSetting> dictionary;
		if (IsMerged && IsMergeAvailable)
		{
			mode = profile.GetMergeLightingMode(mode).Value;
			dictionary = profile.LightingSettings;
		}
		else
		{
			dictionary = ene6K77FanSubProfile.LightingSettings;
		}
		if (!dictionary.TryGetValue(mode, out var value))
		{
			value = profile.GetDefaultLightingSetting(mode);
			if (create)
			{
				dictionary[mode] = value;
			}
		}
		return value;
	}

	// updateLightingSettingUI / applyLightingSetting.
	private void loadEditor()
	{
		SLInfinityController controller = Controller;
		IsMergeAvailable = controller != null && SelectedMode != null && scope == LightingScope.All && controller.Profile.HasMergeLightingMode(SelectedMode.Mode);
		Ene6K77FanLightingSetting ene6K77FanLightingSetting = editedSetting(create: false);
		if (ene6K77FanLightingSetting == null)
		{
			return;
		}
		Ene6K77FanLightingViewSetting ene6K77FanLightingViewSetting;
		try
		{
			ene6K77FanLightingViewSetting = controller.Profile.GetLightingViewSetting(ene6K77FanLightingSetting.Mode);
		}
		catch
		{
			ene6K77FanLightingViewSetting = default(Ene6K77FanLightingViewSetting);
		}
		Editor.Load(ene6K77FanLightingViewSetting.ColorCount, ene6K77FanLightingViewSetting.Speed, ene6K77FanLightingViewSetting.Brightness, ene6K77FanLightingViewSetting.Direction, ene6K77FanLightingSetting.Colors, ene6K77FanLightingSetting.Speed, ene6K77FanLightingSetting.Brightness, ene6K77FanLightingSetting.Direction == LightingDirection.Left);
	}

	private void applyEditorTo(Ene6K77FanLightingSetting setting)
	{
		Ene6K77FanLightingViewSetting ene6K77FanLightingViewSetting;
		try
		{
			ene6K77FanLightingViewSetting = Controller.Profile.GetLightingViewSetting(setting.Mode);
		}
		catch
		{
			ene6K77FanLightingViewSetting = default(Ene6K77FanLightingViewSetting);
		}
		if (Editor.ColorCount > 0)
		{
			setting.Colors = Editor.GetColors();
		}
		if (ene6K77FanLightingViewSetting.Speed)
		{
			setting.Speed = Editor.SpeedValue;
		}
		if (ene6K77FanLightingViewSetting.Brightness)
		{
			setting.Brightness = Editor.BrightnessValue;
		}
		if (ene6K77FanLightingViewSetting.Direction)
		{
			setting.Direction = (Editor.DirectionLeft ? LightingDirection.Left : LightingDirection.Right);
		}
	}

	[RelayCommand]
	public async Task ApplyLightingAsync()
	{
		SLInfinityController controller = Controller;
		Ene6K77FanSubProfile s = sub;
		if (controller == null || s == null || SelectedMode == null || IsBusy)
		{
			return;
		}
		SLInfinityProfile profile = controller.Profile;
		bool wasMerged = profile.IsMerged;
		LightingMode mode = SelectedMode.Mode;
		bool merge = IsMerged && IsMergeAvailable;
		if (merge)
		{
			// MergeCheckBox_Checked: the merged effect drives all groups through port 0.
			profile.LightingModeBeforeMerge = mode;
		}
		else
		{
			profile.LightingModeBeforeMerge = null;
			switch (scope)
			{
			case LightingScope.All:
				s.IsIndividualMode = false;
				s.LightingMode = mode;
				break;
			case LightingScope.Inner:
				s.IsIndividualMode = true;
				s.LightingModeInner = mode;
				break;
			case LightingScope.Outer:
				s.IsIndividualMode = true;
				s.LightingModeOuter = mode;
				break;
			}
		}
		applyEditorTo(editedSetting(create: true));
		await run(delegate
		{
			controller.SaveProfile();
			if (merge || wasMerged)
			{
				controller.SendLightingConfig();
			}
			else
			{
				controller.SendLightingConfig(s.GroupIndex);
			}
		}, merge ? (SelectedMode.Name + " merged across all groups.") : $"{SelectedGroup.Name}: {SelectedMode.Name} applied.");
		updateGroupTexts();
	}

	// LightingApplyToAllButton_Click.
	[RelayCommand]
	public async Task ApplyLightingToAllAsync()
	{
		SLInfinityController controller = Controller;
		Ene6K77FanSubProfile s = sub;
		if (controller == null || s == null || IsBusy)
		{
			return;
		}
		if (IsMerged && IsMergeAvailable)
		{
			await ApplyLightingAsync();
			return;
		}
		await ApplyLightingAsync();
		SLInfinityProfile profile = controller.Profile;
		Ene6K77FanSubProfile[] subProfiles = profile.SubProfiles;
		foreach (Ene6K77FanSubProfile ene6K77FanSubProfile in subProfiles)
		{
			if (ene6K77FanSubProfile.GroupIndex != s.GroupIndex)
			{
				ene6K77FanSubProfile.IsIndividualMode = s.IsIndividualMode;
				if (ene6K77FanSubProfile.IsIndividualMode)
				{
					ene6K77FanSubProfile.LightingModeInner = s.LightingModeInner;
					ene6K77FanSubProfile.LightingModeOuter = s.LightingModeOuter;
					ene6K77FanSubProfile.LightingSettings[s.LightingModeInner] = s.LightingSettings[s.LightingModeInner].Clone();
					ene6K77FanSubProfile.LightingSettings[s.LightingModeOuter] = s.LightingSettings[s.LightingModeOuter].Clone();
				}
				else
				{
					ene6K77FanSubProfile.LightingMode = s.LightingMode;
					ene6K77FanSubProfile.LightingSettings[s.LightingMode] = s.LightingSettings[s.LightingMode].Clone();
				}
			}
		}
		await run(delegate
		{
			controller.SaveProfile();
			controller.SendLightingConfig();
		}, "Lighting applied to all groups.");
		updateGroupTexts();
	}

	[RelayCommand]
	public async Task ResetLightingAsync()
	{
		SLInfinityController controller = Controller;
		Ene6K77FanLightingSetting ene6K77FanLightingSetting = editedSetting(create: true);
		if (controller != null && ene6K77FanLightingSetting != null)
		{
			controller.Profile.GetDefaultLightingSetting(ene6K77FanLightingSetting.Mode).CopyTo(ene6K77FanLightingSetting);
			loadEditor();
			await ApplyLightingAsync();
		}
	}

	[RelayCommand]
	public async Task ApplyFanQuantityAsync()
	{
		SLInfinityController controller = Controller;
		Ene6K77FanSubProfile ene6K77FanSubProfile = sub;
		if (controller != null && ene6K77FanSubProfile != null && !IsBusy)
		{
			ene6K77FanSubProfile.FanQuantity = Math.Clamp(FanQuantity, 0, 4);
			await run(delegate
			{
				controller.SaveProfile();
				controller.SendFanQuantity();
			}, $"{SelectedGroup.Name}: {ene6K77FanSubProfile.FanQuantity} fan(s).");
			updateGroupTexts();
		}
	}

	[RelayCommand]
	public async Task ApplyFanAsync()
	{
		SLInfinityController controller = Controller;
		Ene6K77FanSubProfile s = sub;
		if (controller != null && s != null && !IsBusy && Curve.CommitTo(s.RPMSetting))
		{
			await run(delegate
			{
				controller.SaveProfile();
				controller.SendFanConfig(s.GroupIndex);
			}, $"{SelectedGroup.Name}: fan profile {Curve.ModeName} applied.");
			updateGroupTexts();
		}
	}

	[RelayCommand]
	public async Task ApplyFanToAllAsync()
	{
		SLInfinityController controller = Controller;
		if (controller != null && !IsBusy)
		{
			Ene6K77FanSubProfile[] subProfiles = controller.Profile.SubProfiles;
			foreach (Ene6K77FanSubProfile ene6K77FanSubProfile in subProfiles)
			{
				Curve.CommitTo(ene6K77FanSubProfile.RPMSetting);
			}
			await run(delegate
			{
				controller.SaveProfile();
				controller.SendFanConfig();
			}, "Fan profile " + Curve.ModeName + " applied to all groups.");
			updateGroupTexts();
		}
	}

	[RelayCommand]
	public async Task ResetFanAsync()
	{
		Curve.ResetToDefault();
		await ApplyFanAsync();
	}

	private async Task run(Action action, string message)
	{
		IsBusy = true;
		try
		{
			await Task.Run(action);
			ShowStatus(message);
		}
		catch (Exception ex)
		{
			ShowStatus("Failed: " + ex.Message, isError: true);
		}
		finally
		{
			IsBusy = false;
		}
	}
}
