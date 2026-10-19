using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LConnect3cs.Products.TLFan;
using LConnectCore.Products.TLFan;
using LConnectUi;
using LConnectUi.Services;
using LConnectUi.ViewModels;
using Xunit.Abstractions;

namespace LConnect.Ui.Tests;

// Brings up the simulated daemon, the UI runtime (controller watcher, sensors) and - once - imports the
// Windows L-Connect 3 settings through the Settings page, like a user would on first start.
internal static class SimulatedSetup
{
	private static bool imported;

	public static async Task<bool> EnsureAsync(ITestOutputHelper output)
	{
		bool flag = await TestEnvironment.EnsureDaemonAsync();
		output?.WriteLine($"daemon: {(flag ? "running" : "NOT available")} (data {TestEnvironment.DataDir})");
		TestEnvironment.Trace("daemon " + flag);
		AppRuntime.Start(openLog: false);
		if (!flag)
		{
			return false;
		}
		ControllerHub.Instance.PollNow();
		TestEnvironment.Trace("polled");
		if (!imported && !TestEnvironment.External)
		{
			imported = true;
			string text = Path.Combine(TestEnvironment.WindowsInstallDir, "appdata");
			if (Directory.Exists(text))
			{
				SettingsViewModel settingsViewModel = new SettingsViewModel();
				var result = await settingsViewModel.ImportWindowsSettingsAsync(text);
				output?.WriteLine("settings import: " + settingsViewModel.StatusMessage);
			}
		}
		await TestEnvironment.WaitFor(() => ControllerHub.Instance.TLControllers.Any((TLFanController c) => c.Profile.PortProfiles.Any((TLFanPortProfile p) => p.PortType != PortType.None)), 15000);
		await TestEnvironment.WaitFor(() => ControllerHub.Instance.SLInfinityControllers.Count > 0, 5000);
		TestEnvironment.Trace("setup done");
		return true;
	}
}
