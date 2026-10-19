using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LConnect3cs.Helpers;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

public sealed class LanguageItem
{
	public string Code { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public partial class SettingsViewModel : PageViewModel
{
	private bool loading;

	[ObservableProperty]
	private LanguageItem selectedLanguage;

	[ObservableProperty]
	private bool motherboardArgbSync;

	[ObservableProperty]
	private bool motherboardRpmSync;

	[ObservableProperty]
	private bool closeToTray;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private string log = string.Empty;

	public override string Title => Loc.Get("I18n.MainMenu.Settings", "Settings");

	public override string IconKey => "Settings";

	public ObservableCollection<LanguageItem> Languages { get; } = new ObservableCollection<LanguageItem>();

	public string Version
	{
		get
		{
			Assembly entryAssembly = typeof(SettingsViewModel).Assembly;
			string text = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? entryAssembly.GetName().Version?.ToString() ?? "?";
			int num = text.IndexOf('+');
			if (num > 0)
			{
				text = text.Substring(0, num);
			}
			return "lconnect-ui " + text + " (port of L-Connect 3 v2.0.11)";
		}
	}

	public string DataDirectory => LConnectApp.Path.DataRoot;

	public string SocketPath => LConnectApp.Path.ServiceSocketPath;

	public string SettingsFile => UiSettings.FilePath;

	public string LogFile => Logger.Instance.LogPath ?? "(not open)";

	public SettingsViewModel()
	{
		foreach (string availableLocale in I18n.AvailableLocales)
		{
			Languages.Add(new LanguageItem
			{
				Code = availableLocale,
				Name = Names.LocaleName(availableLocale)
			});
		}
		load();
	}

	private void load()
	{
		loading = true;
		try
		{
			UiSettings current = UiSettings.Current;
			SelectedLanguage = Languages.FirstOrDefault((LanguageItem l) => l.Code == I18n.Locale) ?? Languages.First();
			MotherboardArgbSync = current.MotherboardARGBSync;
			MotherboardRpmSync = current.MotherboardRPMSync;
			CloseToTray = current.CloseToTray;
		}
		finally
		{
			loading = false;
		}
	}

	protected override void OnActivated()
	{
		load();
		OnPropertyChanged("LogFile");
	}

	partial void OnSelectedLanguageChanged(LanguageItem value)
	{
		if (!loading && value != null)
		{
			Loc.SetLocale(value.Code);
			UiSettings.Current.Locale = value.Code;
			UiSettings.Current.Save();
			ShowStatus("Language set to " + value.Name + ".");
		}
	}

	partial void OnMotherboardArgbSyncChanged(bool value)
	{
		if (!loading)
		{
			UiSettings.Current.MotherboardARGBSync = value;
			UiSettings.Current.Save();
			LConnectServiceHttpUtility.TrySendMotherboardARGBSyncRequestAsync(value);
			ShowStatus(value ? "Motherboard ARGB sync enabled." : "Motherboard ARGB sync disabled.");
		}
	}

	partial void OnMotherboardRpmSyncChanged(bool value)
	{
		if (!loading)
		{
			UiSettings.Current.MotherboardRPMSync = value;
			UiSettings.Current.Save();
			LConnectServiceHttpUtility.TrySendMotherboardRPMSyncRequestAsync(value);
			ShowStatus(value ? "Motherboard RPM sync enabled." : "Motherboard RPM sync disabled.");
		}
	}

	partial void OnCloseToTrayChanged(bool value)
	{
		if (!loading)
		{
			UiSettings.Current.CloseToTray = value;
			UiSettings.Current.Save();
		}
	}

	[RelayCommand]
	public async Task BrowseWindowsSettingsAsync()
	{
		string text = await PickAsync(() => UiDialogs.PickFolder("Select the 'appdata' folder of Windows L-Connect 3"));
		if (!string.IsNullOrEmpty(text))
		{
			await ImportWindowsSettingsAsync(text);
		}
	}

	// Same as 'lconnectctl import --from <appdata>': match the Windows settings files to the controllers the
	// daemon reports, then ask the daemon to apply them.
	public async Task<SettingsImporter.Result> ImportWindowsSettingsAsync(string appDataDir)
	{
		IsBusy = true;
		try
		{
			(SettingsImporter.Result, List<string>) tuple = await Task.Run(delegate
			{
				List<string> list = new List<string>();
				string text = null;
				string directoryName = Path.GetDirectoryName(Path.GetFullPath(appDataDir).TrimEnd('/', '\\'));
				if (directoryName != null && Directory.Exists(Path.Combine(directoryName, "repo")))
				{
					text = directoryName;
				}
				List<ControllerWatcher.DaemonController> list2 = null;
				try
				{
					list2 = ControllerWatcher.QueryDaemon();
				}
				catch
				{
				}
				if (list2 == null)
				{
					throw new InvalidOperationException("lconnectd is not reachable; start it first so the settings can be matched to your devices.");
				}
				List<SettingsImporter.ConnectedDevice> list3 = new List<SettingsImporter.ConnectedDevice>();
				foreach (ControllerWatcher.DaemonController item in list2)
				{
					(int, int, int)? tuple2 = SettingsImporter.ParseIdentity(item.DevicePath);
					list3.Add(new SettingsImporter.ConnectedDevice
					{
						DevicePath = item.DevicePath,
						VendorID = item.VendorID,
						ProductID = item.ProductID,
						InterfaceNumber = (tuple2?.Item3 ?? 0)
					});
				}
				SettingsImporter.Result item2 = SettingsImporter.Import(appDataDir, text, list3, dryRun: false);
				list.Add(DaemonClient.PostReapply() ? "Asked lconnectd to apply the imported settings." : "lconnectd did not accept the reapply request.");
				foreach (LConnect3cs.Products.TLFan.TLFanController tLController in ControllerHub.Instance.TLControllers)
				{
					tLController.ReloadProfile();
				}
				foreach (LConnect3cs.Products.Ene6K77Fan.SLInfinityController sLInfinityController in ControllerHub.Instance.SLInfinityControllers)
				{
					sLInfinityController.ReloadProfile();
				}
				return (item2, list);
			});
			SettingsImporter.Result item3 = tuple.Item1;
			Log = string.Join(Environment.NewLine, item3.Messages.Concat(tuple.Item2));
			ShowStatus($"Imported {item3.Imported} setting file(s), {item3.Pending} pending for unplugged devices, {item3.MediaCopied} LCD media file(s).");
			return item3;
		}
		catch (Exception ex)
		{
			ShowStatus("Import failed: " + ex.Message, isError: true);
			return null;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public async Task BrowseAssetsAsync()
	{
		string text = await PickAsync(() => UiDialogs.PickFolder("Select the Windows L-Connect 3 install folder"));
		if (!string.IsNullOrEmpty(text))
		{
			await ImportAssetsAsync(text);
		}
	}

	public async Task<bool> ImportAssetsAsync(string installDir)
	{
		if (!AssetImporter.LooksLikeInstallDirectory(installDir))
		{
			ShowStatus(installDir + " does not look like an L-Connect 3 install (assets/tl-sensor/index.html missing).", isError: true);
			return false;
		}
		IsBusy = true;
		try
		{
			AssetImporter.Result result = await Task.Run(() => AssetImporter.Import(installDir));
			Log = string.Join(Environment.NewLine, result.Messages);
			ShowStatus($"Imported {result.FilesCopied} asset file(s).");
			return true;
		}
		catch (Exception ex)
		{
			ShowStatus("Asset import failed: " + ex.Message, isError: true);
			return false;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public async Task BrowseExportProfilesAsync()
	{
		string text = await PickAsync(() => UiDialogs.SaveFile("Export profiles", "lconnect-profiles.zip", "zip"));
		if (!string.IsNullOrEmpty(text))
		{
			await ExportProfilesAsync(text);
		}
	}

	public async Task<bool> ExportProfilesAsync(string path)
	{
		bool flag = await Task.Run(() => ProfileManager.Instance.Export(path));
		ShowStatus(flag ? ("Profiles exported to " + path + ".") : "Export failed.", !flag);
		return flag;
	}

	[RelayCommand]
	public async Task BrowseImportProfilesAsync()
	{
		string text = await PickAsync(() => UiDialogs.OpenFile("Import profiles", new (string, string[])[1] { ("Zip (*.zip)", new string[1] { "*.zip" }) }));
		if (!string.IsNullOrEmpty(text))
		{
			await ImportProfilesAsync(text);
		}
	}

	public async Task<bool> ImportProfilesAsync(string path)
	{
		bool flag = await Task.Run(delegate
		{
			bool result = ProfileManager.Instance.Import(path);
			foreach (LConnect3cs.Products.TLFan.TLFanController tLController in ControllerHub.Instance.TLControllers)
			{
				tLController.SendLightingConfigRequest();
				tLController.SendFanSpeedConfigRequest();
				tLController.SendDirectionConfigRequest();
			}
			return result;
		});
		ShowStatus(flag ? ("Profiles imported from " + path + ".") : "Import failed (not a profile export?).", !flag);
		return flag;
	}
}
