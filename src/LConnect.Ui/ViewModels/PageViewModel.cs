using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LConnectUi.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
	private DispatcherTimer timer;

	[ObservableProperty]
	private string statusMessage;

	[ObservableProperty]
	private bool statusIsError;

	public abstract string Title { get; }

	protected PageViewModel()
	{
		LConnectUi.Services.Loc.LocaleChanged += delegate
		{
			OnPropertyChanged("Title");
		};
	}

	public abstract string IconKey { get; }

	public bool IsActive { get; private set; }

	// Polling interval while the page is visible (0 = no polling).
	protected virtual TimeSpan PollInterval => TimeSpan.Zero;

	public void Activate()
	{
		if (IsActive)
		{
			return;
		}
		IsActive = true;
		OnActivated();
		if (PollInterval > TimeSpan.Zero)
		{
			timer = new DispatcherTimer
			{
				Interval = PollInterval
			};
			timer.Tick += delegate
			{
				OnPoll();
			};
			timer.Start();
		}
	}

	public void Deactivate()
	{
		if (IsActive)
		{
			IsActive = false;
			timer?.Stop();
			timer = null;
			OnDeactivated();
		}
	}

	protected virtual void OnActivated()
	{
	}

	protected virtual void OnDeactivated()
	{
	}

	protected virtual void OnPoll()
	{
	}

	protected void ShowStatus(string message, bool isError = false)
	{
		StatusMessage = message;
		StatusIsError = isError;
	}

	// Pickers go through xdg-desktop-portal, which can refuse the call (it does when the process runs under a
	// primary group the portal does not share, e.g. after 'newgrp'). An exception escaping a RelayCommand kills
	// the app, so turn it into a status message.
	protected async Task<string> PickAsync(Func<Task<string>> pick)
	{
		try
		{
			return await pick();
		}
		catch (Exception ex)
		{
			LConnect3cs.Utils.Logger.Instance.Warn("File dialog failed: " + ex.Message);
			ShowStatus("Could not open the file dialog: " + ex.Message, isError: true);
			return null;
		}
	}
}
