using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LConnect3cs.Products.TLFan;
using LConnectCore.Products.TLFan;
using LConnectUi.Services;
using WColor = System.Windows.Media.Color;

namespace LConnectUi.ViewModels;

public sealed class TLModeItem
{
	public LightingMode Mode { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public sealed class ScopeItem
{
	public LightingScope Scope { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public sealed class TargetItem
{
	// -1 = the whole group, otherwise the fan index inside the group.
	public int FanIndex { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public partial class FanCalibrationItem : ObservableObject
{
	[ObservableProperty]
	private bool swapLeftRight;

	[ObservableProperty]
	private bool swapTopBottom;

	public int Index { get; init; }

	public string Name => $"Fan {Index + 1}";
}

// TL Fan Utility - lighting. Ports TLFanUtilityLightingEffectPage (mode/scope/merge editing) and
// TLFanUtilityMainPage.LightingEffectPage_ApplyButtonClicked (how the profile is updated before
// SaveProfile + SendLightingConfigRequest), plus the direction calibration and test light of the TL device
// settings page (SendDirectionConfigRequest / SendTestLightRequest).
public partial class TLLightingViewModel : TLPageBase
{
	private bool loading;

	[ObservableProperty]
	private bool isMerged;

	[ObservableProperty]
	private ScopeItem selectedScope;

	[ObservableProperty]
	private TargetItem selectedTarget;

	[ObservableProperty]
	private TLModeItem selectedMode;

	[ObservableProperty]
	private bool isSyncSide;

	[ObservableProperty]
	private bool syncBottomSide;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private bool portReverse;

	[ObservableProperty]
	private bool groupSwapLeftRight;

	[ObservableProperty]
	private bool groupSwapTopBottom;

	public override string Title => "TL · " + Loc.Get("I18n.Text.LightingEffects", "Lighting Effects");

	public override string IconKey => "Lighting";

	public LightingEditorViewModel Editor { get; } = new LightingEditorViewModel();

	public ObservableCollection<ScopeItem> Scopes { get; } = new ObservableCollection<ScopeItem>();

	public ObservableCollection<TargetItem> Targets { get; } = new ObservableCollection<TargetItem>();

	public ObservableCollection<TLModeItem> Modes { get; } = new ObservableCollection<TLModeItem>();

	public ObservableCollection<FanCalibrationItem> FanCalibrations { get; } = new ObservableCollection<FanCalibrationItem>();

	public bool IsSideScope
	{
		get
		{
			ScopeItem scopeItem = SelectedScope;
			if (scopeItem == null)
			{
				return false;
			}
			return scopeItem.Scope != LightingScope.All;
		}
	}

	public bool ShowTargets
	{
		get
		{
			if (!IsMerged && !IsSideScope)
			{
				return Targets.Count > 1;
			}
			return false;
		}
	}

	public bool IsFanTarget
	{
		get
		{
			if (!IsMerged && !IsSideScope)
			{
				TargetItem targetItem = SelectedTarget;
				if (targetItem == null)
				{
					return false;
				}
				return targetItem.FanIndex >= 0;
			}
			return false;
		}
	}

	public bool CanEdit
	{
		get
		{
			if (Controller != null)
			{
				if (!IsMerged)
				{
					return SelectedGroup != null;
				}
				return true;
			}
			return false;
		}
	}

	public string TargetDescription
	{
		get
		{
			if (IsMerged)
			{
				return "All ports of this controller run one synchronised effect.";
			}
			if (SelectedPort == null || SelectedGroup == null)
			{
				return string.Empty;
			}
			string text = (IsFanTarget ? $" · fan {SelectedTarget.FanIndex + 1}" : string.Empty);
			return $"{SelectedPort.Name} · {SelectedGroup.Name}{text}";
		}
	}

	public TLLightingViewModel()
	{
		Scopes.Add(new ScopeItem
		{
			Scope = LightingScope.All,
			Name = Loc.Get("I18n.Text.All", "All")
		});
		Scopes.Add(new ScopeItem
		{
			Scope = LightingScope.Top,
			Name = "Top side"
		});
		Scopes.Add(new ScopeItem
		{
			Scope = LightingScope.Bottom,
			Name = "Bottom side"
		});
		selectedScope = Scopes[0];
	}

	protected override void OnSelectionChanged()
	{
		loadFromProfile(resetScope: true);
	}

	partial void OnIsMergedChanged(bool value)
	{
		if (!loading)
		{
			loadFromProfile(resetScope: false);
		}
	}

	partial void OnSelectedScopeChanged(ScopeItem value)
	{
		if (!loading)
		{
			loadFromProfile(resetScope: false);
		}
	}

	partial void OnSelectedTargetChanged(TargetItem value)
	{
		if (!loading)
		{
			loadFromProfile(resetScope: false);
		}
	}

	partial void OnSelectedModeChanged(TLModeItem value)
	{
		if (!loading && value != null)
		{
			TLFanLightingSetting tLFanLightingSetting = settingFor(value.Mode, create: true);
			loadEditor(value.Mode, tLFanLightingSetting);
		}
	}

	private void raiseDerived()
	{
		OnPropertyChanged("IsSideScope");
		OnPropertyChanged("ShowTargets");
		OnPropertyChanged("IsFanTarget");
		OnPropertyChanged("CanEdit");
		OnPropertyChanged("TargetDescription");
	}

	private TLFanFanProfile selectedFanProfile
	{
		get
		{
			TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
			if (selectedGroupProfile == null || !IsFanTarget)
			{
				return null;
			}
			int fanIndex = SelectedTarget.FanIndex;
			if (fanIndex >= selectedGroupProfile.FanProfiles.Length)
			{
				return null;
			}
			return selectedGroupProfile.FanProfiles[fanIndex];
		}
	}

	private LightingScope scope => SelectedScope?.Scope ?? LightingScope.All;

	private IEnumerable<LightingMode> modeOrder
	{
		get
		{
			if (IsFanTarget)
			{
				return TLFanProfile.LightingModeOrder;
			}
			if (IsMerged)
			{
				if (scope != LightingScope.All)
				{
					return TLFanProfile.LightingModeSideMergeOrder;
				}
				return TLFanProfile.LightingModeMergeOrder;
			}
			if (scope != LightingScope.All)
			{
				return TLFanProfile.LightingModeSideOrder;
			}
			return TLFanProfile.LightingModeOrder;
		}
	}

	private Dictionary<LightingMode, TLFanLightingSetting> currentDictionary
	{
		get
		{
			TLFanController controller = Controller;
			if (controller == null)
			{
				return null;
			}
			if (IsFanTarget)
			{
				return selectedFanProfile?.LightingSettings;
			}
			if (IsMerged)
			{
				TLFanProfile profile = controller.Profile;
				return scope switch
				{
					LightingScope.Top => profile.LightingSettingsTop,
					LightingScope.Bottom => profile.LightingSettingsBottom,
					_ => profile.LightingSettings,
				};
			}
			TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
			if (selectedGroupProfile == null)
			{
				return null;
			}
			return scope switch
			{
				LightingScope.Top => selectedGroupProfile.LightingSettingsTop,
				LightingScope.Bottom => selectedGroupProfile.LightingSettingsBottom,
				_ => selectedGroupProfile.LightingSettings,
			};
		}
	}

	private TLFanLightingSetting settingFor(LightingMode mode, bool create)
	{
		Dictionary<LightingMode, TLFanLightingSetting> dictionary = currentDictionary;
		if (dictionary == null)
		{
			return TLFanProfile.GetDefaultLightingSetting(mode);
		}
		if (!dictionary.TryGetValue(mode, out var value))
		{
			value = TLFanProfile.GetDefaultLightingSetting(mode);
			if (create)
			{
				dictionary[mode] = value;
			}
		}
		return value;
	}

	private LightingMode currentMode()
	{
		TLFanController controller = Controller;
		if (IsFanTarget)
		{
			return selectedFanProfile?.LightingMode ?? LightingMode.Rainbow;
		}
		if (IsMerged)
		{
			TLFanProfile profile = controller.Profile;
			return scope switch
			{
				LightingScope.Top => profile.LightingModeTop,
				LightingScope.Bottom => profile.LightingModeBottom,
				_ => profile.LightingMode,
			};
		}
		TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
		if (selectedGroupProfile == null)
		{
			return LightingMode.Rainbow;
		}
		return scope switch
		{
			LightingScope.Top => selectedGroupProfile.LightingModeTop,
			LightingScope.Bottom => selectedGroupProfile.LightingModeBottom,
			_ => selectedGroupProfile.LightingMode,
		};
	}

	// Mirrors Setup(...) / LightingScopeComboBox_SelectionChanged / LightingModeComboBox_SelectionChanged.
	private void loadFromProfile(bool resetScope)
	{
		loading = true;
		try
		{
			TLFanController controller = Controller;
			TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
			if (resetScope && controller != null)
			{
				IsMerged = controller.Profile.IsMerged;
				bool flag = (IsMerged ? controller.Profile.IsSingleSideMode : (selectedGroupProfile?.IsSingleSideMode ?? false));
				SelectedScope = Scopes[flag ? 1 : 0];
				int num = SelectedTarget?.FanIndex ?? (-1);
				Targets.Clear();
				Targets.Add(new TargetItem
				{
					FanIndex = -1,
					Name = "Whole group"
				});
				if (selectedGroupProfile != null)
				{
					int num2 = Math.Min(selectedGroupProfile.FanQuantity, selectedGroupProfile.FanProfiles?.Length ?? 0);
					for (int i = 0; i < num2; i++)
					{
						Targets.Add(new TargetItem
						{
							FanIndex = i,
							Name = $"Fan {i + 1}"
						});
					}
				}
				SelectedTarget = (selectedGroupProfile != null && !selectedGroupProfile.IsGrouping && Targets.Count > 1) ? Targets[1] : Targets[0];
				_ = num;
				loadCalibration();
			}
			if (controller != null)
			{
				if (IsMerged)
				{
					IsSyncSide = controller.Profile.IsSyncSideMode;
					SyncBottomSide = controller.Profile.IsSyncBottomSide;
				}
				else if (selectedGroupProfile != null)
				{
					IsSyncSide = selectedGroupProfile.IsSyncSideMode;
					SyncBottomSide = selectedGroupProfile.IsSyncBottomSide;
				}
			}
			Modes.Clear();
			foreach (LightingMode item in modeOrder)
			{
				Modes.Add(new TLModeItem
				{
					Mode = item,
					Name = Names.Of(item)
				});
			}
			raiseDerived();
			if (!CanEdit)
			{
				SelectedMode = null;
				return;
			}
			LightingMode mode = currentMode();
			SelectedMode = Modes.FirstOrDefault((TLModeItem m) => m.Mode == mode) ?? Modes.FirstOrDefault();
			if (SelectedMode != null)
			{
				loadEditor(SelectedMode.Mode, settingFor(SelectedMode.Mode, create: true));
			}
		}
		finally
		{
			loading = false;
		}
	}

	private void loadEditor(LightingMode mode, TLFanLightingSetting setting)
	{
		TLFanLightingViewSetting value = (TLFanProfile.LightingViewSettings.TryGetValue(mode, out value) ? value : default(TLFanLightingViewSetting));
		if (IsFanTarget && mode == LightingMode.StaticColor)
		{
			value.ColorCount = 1;
		}
		Editor.Load(value.ColorCount, value.Speed, value.Brightness, value.Direction, setting.Colors, setting.Speed, setting.Brightness, setting.Direction == LightingDirection.LeftOrCounterClockwise);
	}

	private void applyEditorTo(TLFanLightingSetting lightingSetting)
	{
		if (lightingSetting == null || SelectedMode == null)
		{
			return;
		}
		TLFanLightingViewSetting value = (TLFanProfile.LightingViewSettings.TryGetValue(SelectedMode.Mode, out value) ? value : default(TLFanLightingViewSetting));
		if (Editor.ColorCount > 0)
		{
			WColor[] colors = Editor.GetColors();
			lightingSetting.Colors = colors;
		}
		if (value.Speed)
		{
			lightingSetting.Speed = Editor.SpeedValue;
		}
		if (value.Brightness)
		{
			lightingSetting.Brightness = Editor.BrightnessValue;
		}
		if (value.Direction)
		{
			lightingSetting.Direction = (Editor.DirectionLeft ? LightingDirection.LeftOrCounterClockwise : LightingDirection.RightOrClockwise);
		}
	}

	private void setGroupSetting(TLFanGroupProfile groupProfile)
	{
		LightingMode mode = SelectedMode.Mode;
		groupProfile.IsGrouping = true;
		groupProfile.IsSingleSideMode = scope != LightingScope.All;
		groupProfile.IsSyncSideMode = IsSyncSide;
		groupProfile.IsSyncBottomSide = SyncBottomSide;
		Dictionary<LightingMode, TLFanLightingSetting> dictionary;
		switch (scope)
		{
		case LightingScope.Top:
			groupProfile.LightingModeTop = mode;
			dictionary = groupProfile.LightingSettingsTop;
			break;
		case LightingScope.Bottom:
			groupProfile.LightingModeBottom = mode;
			dictionary = groupProfile.LightingSettingsBottom;
			break;
		default:
			groupProfile.LightingMode = mode;
			dictionary = groupProfile.LightingSettings;
			break;
		}
		applyEditorTo(ensure(dictionary, mode));
	}

	private void setMergedSetting(TLFanProfile profile)
	{
		LightingMode mode = SelectedMode.Mode;
		profile.IsMerged = true;
		profile.IsSingleSideMode = scope != LightingScope.All;
		profile.IsSyncSideMode = IsSyncSide;
		profile.IsSyncBottomSide = SyncBottomSide;
		Dictionary<LightingMode, TLFanLightingSetting> dictionary;
		switch (scope)
		{
		case LightingScope.Top:
			profile.LightingModeTop = mode;
			dictionary = profile.LightingSettingsTop;
			break;
		case LightingScope.Bottom:
			profile.LightingModeBottom = mode;
			dictionary = profile.LightingSettingsBottom;
			break;
		default:
			profile.LightingMode = mode;
			dictionary = profile.LightingSettings;
			break;
		}
		applyEditorTo(ensure(dictionary, mode));
	}

	private void setFanSetting(TLFanFanProfile fanProfile)
	{
		LightingMode mode = SelectedMode.Mode;
		fanProfile.ParentGroupProfile.IsGrouping = false;
		fanProfile.LightingMode = mode;
		applyEditorTo(ensure(fanProfile.LightingSettings, mode));
	}

	private static TLFanLightingSetting ensure(Dictionary<LightingMode, TLFanLightingSetting> dict, LightingMode mode)
	{
		if (!dict.TryGetValue(mode, out var value))
		{
			value = (dict[mode] = TLFanProfile.GetDefaultLightingSetting(mode));
		}
		return value;
	}

	[RelayCommand]
	public async Task ApplyAsync()
	{
		TLFanController controller = Controller;
		if (controller == null || SelectedMode == null || IsBusy)
		{
			return;
		}
		if (!IsMerged && SelectedGroupProfile == null)
		{
			return;
		}
		TLFanFanProfile fanProfile = selectedFanProfile;
		controller.Profile.IsMerged = IsMerged;
		if (IsMerged)
		{
			setMergedSetting(controller.Profile);
		}
		else if (IsFanTarget && fanProfile != null)
		{
			fanProfile.ParentGroupProfile ??= SelectedGroupProfile;
			setFanSetting(fanProfile);
		}
		else
		{
			setGroupSetting(SelectedGroupProfile);
		}
		await sendLighting(controller, "Applied " + SelectedMode.Name + ".");
	}

	// "Apply To More" with every group selected: the current group effect on all groups of all ports.
	[RelayCommand]
	public async Task ApplyToAllGroupsAsync()
	{
		TLFanController controller = Controller;
		if (controller == null || SelectedMode == null || IsMerged || IsFanTarget || IsBusy)
		{
			return;
		}
		controller.Profile.IsMerged = false;
		int num = 0;
		TLFanPortProfile[] portProfiles = controller.Profile.PortProfiles;
		foreach (TLFanPortProfile port in portProfiles)
		{
			int[] order;
			TLFanGroupProfile[] array = GroupsOf(port, out order);
			foreach (TLFanGroupProfile tLFanGroupProfile in array)
			{
				if (tLFanGroupProfile.FanQuantity > 0)
				{
					setGroupSetting(tLFanGroupProfile);
					num++;
				}
			}
		}
		await sendLighting(controller, $"Applied {SelectedMode.Name} to {num} group(s).");
	}

	[RelayCommand]
	public async Task ResetToDefaultAsync()
	{
		if (SelectedMode != null)
		{
			loadEditor(SelectedMode.Mode, TLFanProfile.GetDefaultLightingSetting(SelectedMode.Mode));
			await ApplyAsync();
		}
	}

	private async Task sendLighting(TLFanController controller, string message)
	{
		IsBusy = true;
		try
		{
			await Task.Run(delegate
			{
				controller.SaveProfile();
				controller.SendLightingConfigRequest();
			});
			ShowStatus(message);
		}
		catch (Exception ex)
		{
			ShowStatus("Applying failed: " + ex.Message, isError: true);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void loadCalibration()
	{
		TLFanPortProfile selectedPortProfile = SelectedPortProfile;
		TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
		PortReverse = selectedPortProfile != null && ((selectedPortProfile.PortType == PortType.LCD) ? selectedPortProfile.LCDIsReverseDirection : selectedPortProfile.LEDIsReverseDirection);
		GroupSwapLeftRight = selectedGroupProfile?.IsSwapLeftRightDirection ?? false;
		GroupSwapTopBottom = selectedGroupProfile?.IsSwapTopBottomDirection ?? false;
		FanCalibrations.Clear();
		if (selectedGroupProfile?.FanProfiles == null)
		{
			return;
		}
		int num = Math.Min(selectedGroupProfile.FanQuantity, selectedGroupProfile.FanProfiles.Length);
		for (int i = 0; i < num; i++)
		{
			TLFanFanProfile tLFanFanProfile = selectedGroupProfile.FanProfiles[i];
			FanCalibrations.Add(new FanCalibrationItem
			{
				Index = i,
				SwapLeftRight = tLFanFanProfile.IsSwapLeftRightDirection,
				SwapTopBottom = tLFanFanProfile.IsSwapTopBottomDirection
			});
		}
	}

	[RelayCommand]
	public async Task ApplyCalibrationAsync()
	{
		TLFanController controller = Controller;
		TLFanPortProfile selectedPortProfile = SelectedPortProfile;
		TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
		if (controller == null || selectedPortProfile == null || IsBusy)
		{
			return;
		}
		if (selectedPortProfile.PortType == PortType.LCD)
		{
			selectedPortProfile.LCDIsReverseDirection = PortReverse;
		}
		else
		{
			selectedPortProfile.LEDIsReverseDirection = PortReverse;
		}
		if (selectedGroupProfile != null)
		{
			selectedGroupProfile.IsSwapLeftRightDirection = GroupSwapLeftRight;
			selectedGroupProfile.IsSwapTopBottomDirection = GroupSwapTopBottom;
			foreach (FanCalibrationItem fanCalibration in FanCalibrations)
			{
				if (fanCalibration.Index < selectedGroupProfile.FanProfiles.Length)
				{
					selectedGroupProfile.FanProfiles[fanCalibration.Index].IsSwapLeftRightDirection = fanCalibration.SwapLeftRight;
					selectedGroupProfile.FanProfiles[fanCalibration.Index].IsSwapTopBottomDirection = fanCalibration.SwapTopBottom;
				}
			}
		}
		IsBusy = true;
		try
		{
			await Task.Run(delegate
			{
				controller.SaveProfile();
				controller.SendDirectionConfigRequest();
			});
			ShowStatus("Direction settings applied.");
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public Task TestTopAsync()
	{
		return testLight(isBottomSide: false);
	}

	[RelayCommand]
	public Task TestBottomAsync()
	{
		return testLight(isBottomSide: true);
	}

	private async Task testLight(bool isBottomSide)
	{
		TLFanController controller = Controller;
		TLFanPortProfile port = SelectedPortProfile;
		TLFanGroupProfile group = SelectedGroupProfile;
		if (controller == null || port == null || group == null || IsBusy)
		{
			return;
		}
		IsBusy = true;
		try
		{
			await Task.Run(delegate
			{
				controller.SaveProfile();
				controller.SendTestLightRequest(port.Port, group.Index, isBottomSide);
			});
			ShowStatus($"Test light on {SelectedPort?.Name} {group.DisplayName} ({(isBottomSide ? "bottom" : "top")} side).");
			await Task.Delay(TimeSpan.FromSeconds(3.0));
		}
		finally
		{
			IsBusy = false;
		}
	}
}
