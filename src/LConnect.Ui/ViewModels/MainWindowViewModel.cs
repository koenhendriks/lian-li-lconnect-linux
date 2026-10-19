using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	[ObservableProperty]
	private PageViewModel currentPage;

	[ObservableProperty]
	private bool daemonReachable;

	public ObservableCollection<PageViewModel> Pages { get; } = new ObservableCollection<PageViewModel>();

	public OverviewViewModel Overview { get; } = new OverviewViewModel();

	public TLLightingViewModel TLLighting { get; } = new TLLightingViewModel();

	public TLFanSpeedViewModel TLFanSpeed { get; } = new TLFanSpeedViewModel();

	public LcdViewModel Lcd { get; } = new LcdViewModel();

	public SLInfinityViewModel SLInfinity { get; } = new SLInfinityViewModel();

	public SettingsViewModel Settings { get; } = new SettingsViewModel();

	public string DaemonStatusText => DaemonReachable ? "lconnectd connected" : "lconnectd not reachable";

	public MainWindowViewModel()
	{
		Pages.Add(Overview);
		Pages.Add(TLLighting);
		Pages.Add(TLFanSpeed);
		Pages.Add(Lcd);
		Pages.Add(SLInfinity);
		Pages.Add(Settings);
		DaemonReachable = ControllerHub.Instance.DaemonReachable;
		ControllerHub.Instance.Changed += delegate
		{
			DaemonReachable = ControllerHub.Instance.DaemonReachable;
		};
		CurrentPage = Overview;
	}

	partial void OnCurrentPageChanged(PageViewModel oldValue, PageViewModel newValue)
	{
		oldValue?.Deactivate();
		newValue?.Activate();
	}

	partial void OnDaemonReachableChanged(bool value)
	{
		OnPropertyChanged("DaemonStatusText");
	}

	public void Navigate(PageViewModel page)
	{
		CurrentPage = page;
	}
}
