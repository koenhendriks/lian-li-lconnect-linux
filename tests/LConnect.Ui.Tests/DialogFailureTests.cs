using System;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using LConnectUi.Services;
using LConnectUi.ViewModels;
using Xunit;

namespace LConnect.Ui.Tests;

public class DialogFailureTests
{
	// xdg-desktop-portal answers AccessDenied when the UI runs under 'newgrp lconnect'; that used to abort the app.
	[AvaloniaFact]
	public async Task Portal_refusal_becomes_a_status_error()
	{
		Func<string, Task<string>> original = UiDialogs.PickFolder;
		UiDialogs.PickFolder = _ => throw new InvalidOperationException("Portal operation not allowed");
		try
		{
			SettingsViewModel settings = new SettingsViewModel();
			await settings.BrowseWindowsSettingsCommand.ExecuteAsync(null);
			Assert.True(settings.StatusIsError);
			Assert.Contains("Portal operation not allowed", settings.StatusMessage);
		}
		finally
		{
			UiDialogs.PickFolder = original;
		}
	}
}
