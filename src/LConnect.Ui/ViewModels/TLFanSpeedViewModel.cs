using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LConnect3cs.Helpers;
using LConnect3cs.Products;
using LConnect3cs.Products.TLFan;
using LConnectCore.Products;
using LConnectCore.Products.TLFan;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

// TL fan profiles: TLFanUtilityFanProfilePage (mode per group) + TLFanProfileSubPage (curve, reference
// source, start/stop, fixed speed). Apply = SaveProfile + SendFanSpeedConfigRequest.
public partial class TLFanSpeedViewModel : TLPageBase
{
	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private bool motherboardRpmSync;

	[ObservableProperty]
	private string groupRpmText = string.Empty;

	public override string Title => "TL · " + Loc.Get("I18n.Text.FanProfile", "Fan Profile");

	public override string IconKey => "Fan";

	public RpmCurveViewModel Curve { get; } = new RpmCurveViewModel();

	public bool CanEdit => SelectedGroupProfile != null;

	public TLFanSpeedViewModel()
	{
		motherboardRpmSync = UiSettings.Current.MotherboardRPMSync;
	}

	protected override void OnActivated()
	{
		MotherboardRpmSync = UiSettings.Current.MotherboardRPMSync;
		base.OnActivated();
	}

	protected override void OnSelectionChanged()
	{
		TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
		TLFanPortProfile port = SelectedPortProfile;
		Curve.Load(selectedGroupProfile?.RPMSetting, TLFanProfile.FanModeOrder, (RPMMode mode, RPMPhaseKey key) => (port?.PortType == PortType.LCD) ? TLFanProfile.GetDefaultFanRPMPhaseInfoLCD(mode, key) : TLFanProfile.GetDefaultFanRPMPhaseInfo(mode, key));
		Curve.IsLocked = MotherboardRpmSync && (selectedGroupProfile?.RPMSetting?.SupportedMotherboardSync ?? false);
		OnPropertyChanged("CanEdit");
		OnLiveUpdated();
	}

	protected override void OnLiveUpdated()
	{
		GroupRpmText = (SelectedGroup == null) ? string.Empty : $"{SelectedGroup.Rpm} rpm";
	}

	partial void OnMotherboardRpmSyncChanged(bool value)
	{
		Curve.IsLocked = value && (SelectedGroupProfile?.RPMSetting?.SupportedMotherboardSync ?? false);
	}

	[RelayCommand]
	public async Task ApplyAsync()
	{
		TLFanController controller = Controller;
		TLFanGroupProfile selectedGroupProfile = SelectedGroupProfile;
		if (controller == null || selectedGroupProfile == null || IsBusy)
		{
			return;
		}
		if (!Curve.CommitTo(selectedGroupProfile.RPMSetting))
		{
			ShowStatus("This fan profile has no curve for the detected fan size.", isError: true);
			return;
		}
		await send(controller, $"{SelectedGroup.Name}: {Curve.ModeName} applied.");
	}

	// Same profile on every group of the selected port (FanProfilePage_ApplyToAllButtonClicked, extended to the curve).
	[RelayCommand]
	public async Task ApplyToPortAsync()
	{
		TLFanController controller = Controller;
		TLFanPortProfile selectedPortProfile = SelectedPortProfile;
		if (controller == null || selectedPortProfile == null || IsBusy)
		{
			return;
		}
		int num = 0;
		int[] order;
		TLFanGroupProfile[] array = GroupsOf(selectedPortProfile, out order);
		foreach (TLFanGroupProfile tLFanGroupProfile in array)
		{
			if (Curve.CommitTo(tLFanGroupProfile.RPMSetting))
			{
				num++;
			}
		}
		await send(controller, $"{Curve.ModeName} applied to {num} group(s) on {SelectedPort.Name}.");
	}

	[RelayCommand]
	public async Task ResetToDefaultAsync()
	{
		Curve.ResetToDefault();
		await ApplyAsync();
	}

	private async Task send(TLFanController controller, string message)
	{
		IsBusy = true;
		try
		{
			await Task.Run(delegate
			{
				controller.SaveProfile();
				controller.SendFanSpeedConfigRequest();
			});
			ShowStatus(message);
			OnSelectionChanged();
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

	[RelayCommand]
	public async Task ToggleMotherboardSyncAsync()
	{
		bool value = MotherboardRpmSync;
		UiSettings.Current.MotherboardRPMSync = value;
		UiSettings.Current.Save();
		await LConnectServiceHttpUtility.TrySendMotherboardRPMSyncRequestAsync(value);
		ShowStatus(value ? "Motherboard RPM sync enabled." : "Motherboard RPM sync disabled.");
	}
}
