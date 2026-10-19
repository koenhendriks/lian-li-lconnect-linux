using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LConnect3cs.Enums;
using LConnectCore;
using LConnectCore.Products.TLLCD;
using LConnectUi;
using LConnectUi.Services;
using LConnectUi.ViewModels;
using LConnectUi.Views;
using Xunit;
using Xunit.Abstractions;

namespace LConnect.Ui.Tests;

// Renders every page (headless Skia) against the simulated daemon and writes PNGs to bin/.../screenshots/.
public class PageRenderingTests
{
	private readonly ITestOutputHelper output;

	public PageRenderingTests(ITestOutputHelper output)
	{
		this.output = output;
	}

	internal static string Capture(Window window, string name)
	{
		Dispatcher.UIThread.RunJobs();
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		TestEnvironment.Trace("capture " + name);
		WriteableBitmap writeableBitmap = window.CaptureRenderedFrame();
		Assert.NotNull(writeableBitmap);
		string text = Path.Combine(TestEnvironment.ScreenshotDir, name + ".png");
		writeableBitmap.Save(text);
		Assert.True(new FileInfo(text).Length > 5000, name + " screenshot looks empty");
		return text;
	}

	[AvaloniaFact]
	public async Task Overview_without_daemon_shows_hint()
	{
		if (TestEnvironment.External)
		{
			return;
		}
		string value = Environment.GetEnvironmentVariable("LCONNECT_SOCKET");
		// The shared HttpClient keeps its connection to a running daemon alive, so stop ours as well.
		TestEnvironment.StopDaemonForRestart();
		try
		{
			Environment.SetEnvironmentVariable("LCONNECT_SOCKET", Path.Combine(TestEnvironment.DataDir, "nobody-listens.sock"));
			LConnectApp.ReloadPaths();
			OverviewViewModel vm = new OverviewViewModel();
			Window window = new Window
			{
				Width = 1056.0,
				Height = 640.0,
				Content = new OverviewView(),
				DataContext = vm
			};
			window.Show();
			await vm.RefreshAsync();
			Assert.True(await TestEnvironment.WaitFor(() => vm.HasLoaded));
			Assert.False(vm.DaemonReachable);
			Assert.True(vm.ShowDaemonHint);
			output.WriteLine(Capture(window, "00-overview-no-daemon"));
			window.Close();
		}
		finally
		{
			Environment.SetEnvironmentVariable("LCONNECT_SOCKET", value);
			LConnectApp.ReloadPaths();
		}
	}

	[AvaloniaFact]
	public async Task All_pages_render_against_simulated_daemon()
	{
		bool daemon = await SimulatedSetup.EnsureAsync(output);
		MainWindowViewModel vm = new MainWindowViewModel();
		MainWindow window = new MainWindow
		{
			DataContext = vm,
			Width = 1360.0,
			Height = 1180.0
		};
		window.Show();
		Dictionary<Type, Func<bool>> dictionary = new Dictionary<Type, Func<bool>>
		{
			[typeof(OverviewViewModel)] = () => vm.Overview.HasLoaded && (!daemon || vm.Overview.Sensors.Count > 0),
			[typeof(TLLightingViewModel)] = () => !daemon || (vm.TLLighting.HasPorts && vm.TLLighting.SelectedMode != null),
			[typeof(TLFanSpeedViewModel)] = () => !daemon || (vm.TLFanSpeed.HasPorts && vm.TLFanSpeed.Curve.HasCurve),
			[typeof(LcdViewModel)] = () => !daemon || (vm.Lcd.Screens.Count > 0 && !vm.Lcd.IsLoadingMedia),
			[typeof(SLInfinityViewModel)] = () => !daemon || (vm.SLInfinity.HasController && vm.SLInfinity.Modes.Count > 0),
			[typeof(SettingsViewModel)] = () => true
		};
		int num = 1;
		foreach (PageViewModel page in vm.Pages.ToList())
		{
			TestEnvironment.Trace("page " + page.GetType().Name);
			vm.CurrentPage = page;
			bool condition = await TestEnvironment.WaitFor(dictionary[page.GetType()], 20000);
			TestEnvironment.Trace("loaded " + condition);
			await TestEnvironment.Pump(1200);
			string text = page.GetType().Name.Replace("ViewModel", string.Empty).ToLowerInvariant();
			output.WriteLine(Capture(window, $"{num++:00}-{text}"));
			Assert.True(condition, page.GetType().Name + " did not finish loading");
		}
		if (daemon)
		{
			// A few interesting states: merged TL lighting, LCD sensor mode, a 3-LCD canvas layout.
			vm.CurrentPage = vm.TLLighting;
			await TestEnvironment.WaitFor(() => vm.TLLighting.SelectedMode != null);
			vm.TLLighting.SelectedMode = vm.TLLighting.Modes.First((TLModeItem m) => m.Mode == LConnectCore.Products.TLFan.LightingMode.Meteor);
			await TestEnvironment.Pump(500);
			output.WriteLine(Capture(window, "10-tl-lighting-meteor"));
			vm.CurrentPage = vm.Lcd;
			await TestEnvironment.WaitFor(() => vm.Lcd.Screens.Count > 0);
			if (vm.Lcd.DeviceCount >= 3)
			{
				await vm.Lcd.GroupChangedAsync(TLLCDGroupType.Group3);
				await TestEnvironment.WaitFor(() => vm.Lcd.Screens.Count == 1 && !vm.Lcd.IsLoadingMedia, 20000);
				await TestEnvironment.Pump(1500);
				output.WriteLine(Capture(window, "11-lcd-group3"));
				await vm.Lcd.GroupChangedAsync(TLLCDGroupType.Group111);
				await TestEnvironment.WaitFor(() => vm.Lcd.Screens.Count == 3 && !vm.Lcd.IsLoadingMedia, 20000);
			}
			vm.Lcd.SelectScreen(vm.Lcd.Screens[0]);
			vm.Lcd.SelectedMediaType = vm.Lcd.MediaTypes.First((LcdMediaTypeOption t) => t.Type == TLLCDTypeEnums.Sensor);
			await TestEnvironment.Pump(1500);
			output.WriteLine(Capture(window, "12-lcd-sensor"));
			vm.CurrentPage = vm.SLInfinity;
			await TestEnvironment.WaitFor(() => vm.SLInfinity.Modes.Count > 0);
			vm.SLInfinity.SelectedScope = vm.SLInfinity.Scopes[1];
			await TestEnvironment.Pump(500);
			output.WriteLine(Capture(window, "13-sl-infinity-inner"));
		}
		window.Close();
	}
}
