using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products.Ene6K77Fan;
using LConnectCore.Products.TLFan;
using LConnectService.Enums;
using LConnectService.Helpers;
using LConnectService.Products;
using LConnectService.Products.Ene6K77Fan;
using LConnectService.Products.TLFan;
using LConnectService.Products.TLLCD;
using LConnectService.Properties;

namespace LConnectService;

// Port of the Windows service class. ServiceBase, the Windows event log, CPUID SDK, GPU-Z, CefSharp and the
// HttpListener are gone; Program.cs hosts this under systemd and routes Unix-socket HTTP requests to HandleRequest.
internal class MainService
{
	private class ControllerInfo
	{
		public IController Controller;

		public IHidDevice HidDevice;

		public ControllerInfo(IController controller, IHidDevice hidDevice)
		{
			Controller = controller;
			HidDevice = hidDevice;
		}
	}

	private readonly Dictionary<string, ControllerInfo> connectedControllers = new Dictionary<string, ControllerInfo>();

	private readonly TLLCDManager tllcdManager = new TLLCDManager();

	public const string EventLogName = "L-Connect Service";

	public const string EventSource = "L-Connect Service";

	private NonOverlappingTimer systemResourceUpdaterTimer;

	private USBDeviceWatcher usbDeviceWatcher;

	// Serializes device add/remove/reload between the watcher thread, resume handling and requests.
	private readonly object deviceLocker = new object();

	public bool Start()
	{
		EventLogHelper.Info("Service starting", EventIDs.ServiceStart);
		if (!initCore())
		{
			return false;
		}
		startSystemResourceUpdater();
		lock (deviceLocker)
		{
			searchConnectedDevice();
			tllcdManager.searchConnectedTLLCDDevice();
			startDeviceWatcher();
			tllcdManager.matchTLLCDFan(connectedControllers.Select((KeyValuePair<string, ControllerInfo> p) => p.Value.Controller).ToList());
			tllcdManager.initTLLCDSetting();
		}
		EventLogHelper.Info("Service started", EventIDs.ServiceStart);
		return true;
	}

	public void Stop()
	{
		stopDeviceWatcher();
		stopSystemResourceUpdater();
		lock (deviceLocker)
		{
			tllcdManager.stopAll();
			foreach (string item in connectedControllers.Keys.ToList())
			{
				removeConnectedController(item);
			}
		}
		SystemInformation.Shutdown();
		disposeCore();
		EventLogHelper.Info("Service stopped", EventIDs.ServiceStop);
	}

	// Equivalent of the original PowerModes.Resume handler; called by the systemd sleep hook (POST /reapply).
	public void Resume()
	{
		usbDeviceWatcher?.Poll();
		lock (deviceLocker)
		{
			tllcdManager.reloadDevice(connectedControllers.Select((KeyValuePair<string, ControllerInfo> p) => p.Value.Controller).ToList());
		}
	}

	// Re-reads every saved setting and applies it, e.g. after 'lconnectctl import'.
	public void ReapplyAll()
	{
		lock (deviceLocker)
		{
			foreach (KeyValuePair<string, ControllerInfo> connectedController in connectedControllers)
			{
				try
				{
					connectedController.Value.Controller.ApplyAll(DeviceSettingManager.LoadAll(connectedController.Key));
				}
				catch (Exception ex)
				{
					EventLogHelper.Error("Re-apply '" + connectedController.Key + "' failed. " + ex.Message);
				}
			}
			tllcdManager.reloadDevice(connectedControllers.Select((KeyValuePair<string, ControllerInfo> p) => p.Value.Controller).ToList());
		}
	}

	public object GetStatus()
	{
		lock (deviceLocker)
		{
			return new
			{
				Controllers = connectedControllers.Select((KeyValuePair<string, ControllerInfo> p) => new
				{
					DevicePath = p.Key,
					Type = p.Value.Controller.GetType().Name,
					VendorID = p.Value.HidDevice.VendorID,
					ProductID = p.Value.HidDevice.ProductID,
					Product = p.Value.HidDevice.Product,
					SerialNumber = p.Value.HidDevice.SerialNumber,
					SettingsKey = DeviceSettingManager.GetDeviceKey(p.Key)
				}).ToList(),
				LCDs = tllcdManager.GetProductInfos(),
				Resource = SystemInformation.Resource,
				DataDirectory = LConnectApp.Path.DataRoot
			};
		}
	}

	private bool initCore()
	{
		try
		{
			if (Core.Init())
			{
				return true;
			}
			EventLogHelper.Error("Init core library failed", EventIDs.CoreInitFailed);
		}
		catch (Exception ex)
		{
			EventLogHelper.Error("Init core library failed. " + ex.Message + "\n" + ex.StackTrace, EventIDs.CoreInitError);
		}
		return false;
	}

	private void disposeCore()
	{
		try
		{
			Core.Dispose();
		}
		catch
		{
		}
	}

	private void startSystemResourceUpdater()
	{
		try
		{
			systemResourceUpdaterTimer = NonOverlappingTimer.StartNew(SystemInformation.Update, 1000, continueOnCallbackError: true);
		}
		catch (Exception ex)
		{
			EventLogHelper.Error("Start system resource updater failed. " + ex.Message + "\n" + ex.StackTrace, EventIDs.SystemResourceUpdaterErrorOnStart);
		}
	}

	private void stopSystemResourceUpdater()
	{
		try
		{
			systemResourceUpdaterTimer?.Dispose();
			for (int i = 0; i < 10; i++)
			{
				if (!SystemInformation.IsUpdating())
				{
					break;
				}
				Thread.Sleep(1000);
			}
		}
		catch
		{
		}
	}

	private void searchConnectedDevice()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DeviceFinderResult item in new HidApiDeviceFinder().Find())
		{
			if ((item.ControllerType != ControllerType.TLFan || item.UsagePage == 65307) && (item.ControllerType != ControllerType.Galahad2TrinityP || item.UsagePage == 65307 || item.Usage == 145) && (item.ControllerType != ControllerType.Galahad2TrinityR || item.UsagePage == 65307 || item.Usage == 145))
			{
				stringBuilder.Append($"Find {item.ControllerType} device: {item.DevicePath} ... ");
				if (addConnectedController(item.ControllerType, item.DevicePath))
				{
					stringBuilder.AppendLine("opened");
				}
				else
				{
					stringBuilder.AppendLine("can't open");
				}
			}
		}
		if (stringBuilder.Length > 0)
		{
			EventLogHelper.Info(stringBuilder.ToString(), EventIDs.SearchDeviceResult);
		}
	}

	private void startDeviceWatcher()
	{
		try
		{
			usbDeviceWatcher = new USBDeviceWatcher();
			foreach (USBDeviceID deviceID in KnownDevices.GetDeviceIDs())
			{
				usbDeviceWatcher.Register(deviceID);
			}
			usbDeviceWatcher.Inserted += usbDeviceWatcher_Inserted;
			usbDeviceWatcher.Removed += usbDeviceWatcher_Removed;
			usbDeviceWatcher.Start();
		}
		catch (Exception ex)
		{
			EventLogHelper.Error("Start device watcher failed. " + ex.Message + "\n" + ex.StackTrace, EventIDs.DeviceWatcherErrorOnStart);
		}
	}

	private void stopDeviceWatcher()
	{
		if (usbDeviceWatcher == null)
		{
			return;
		}
		try
		{
			usbDeviceWatcher.Inserted -= usbDeviceWatcher_Inserted;
			usbDeviceWatcher.Removed -= usbDeviceWatcher_Removed;
			usbDeviceWatcher.Stop();
		}
		catch (Exception ex)
		{
			EventLogHelper.Error("Stop device watcher failed. " + ex.Message + "\n" + ex.StackTrace, EventIDs.DeviceWatcherErrorOnStop);
		}
	}

	private void usbDeviceWatcher_Inserted(object sender, DeviceChangedEventArgs e)
	{
		lock (deviceLocker)
		{
			usbDeviceWatcher_InsertedLocked(e);
		}
	}

	private void usbDeviceWatcher_InsertedLocked(DeviceChangedEventArgs e)
	{
		EventLogHelper.Info($"Device inserted: {e.DeviceID} {e.DevicePath}");
		ControllerType controllerType = KnownDevices.GetControllerType(e.DeviceID);
		switch (controllerType)
		{
		case ControllerType.None:
			break;
		case ControllerType.TLLCD:
			tllcdManager.reloadDevice(connectedControllers.Select((KeyValuePair<string, ControllerInfo> p) => p.Value.Controller).ToList());
			break;
		default:
			addConnectedController(controllerType, e.DevicePath);
			break;
		}
	}

	private void usbDeviceWatcher_Removed(object sender, DeviceChangedEventArgs e)
	{
		lock (deviceLocker)
		{
			usbDeviceWatcher_RemovedLocked(e);
		}
	}

	private void usbDeviceWatcher_RemovedLocked(DeviceChangedEventArgs e)
	{
		EventLogHelper.Info($"Device removed: {e.DeviceID} {e.DevicePath}");
		switch (KnownDevices.GetControllerType(e.DeviceID))
		{
		case ControllerType.None:
			break;
		case ControllerType.TLLCD:
			tllcdManager.removeConnectedTLLCDDevice(e.DevicePath);
			break;
		default:
			removeConnectedController(e.DevicePath);
			break;
		}
	}

	private bool addConnectedController(ControllerType type, string devicePath)
	{
		IHidDevice hidDevice = new HidApiHidDevice(devicePath);
		if (type == ControllerType.TLFan)
		{
			try
			{
				hidDevice.Open();
			}
			catch (Exception ex)
			{
				EventLogHelper.Error($"Open '{devicePath}' ({type}) failed. {ex.Message}\n{ex.StackTrace}", EventIDs.OpenDeviceError);
				return false;
			}
			if (hidDevice.UsagePage != 65307)
			{
				return false;
			}
		}
		IController controller;
		switch (type)
		{
		case ControllerType.SLInfinity:
			controller = new SLInfinityController(new SLInfinityDevice(hidDevice));
			break;
		case ControllerType.SLFan:
			controller = new SLFanController(new SLFanDevice(hidDevice));
			break;
		case ControllerType.SLV2Fan:
		case ControllerType.SLV2AFan:
			controller = new SLV2FanController(new SLV2FanDevice(hidDevice));
			break;
		case ControllerType.ALFan:
			controller = new ALFanController(new ALFanDevice(hidDevice));
			break;
		case ControllerType.ALV2Fan:
			controller = new ALV2FanController(new ALV2FanDevice(hidDevice));
			break;
		case ControllerType.TLFan:
			controller = new TLFanController(new TLFanDevice(hidDevice));
			break;
		default:
			controller = null;
			break;
		}
		IController controller2 = controller;
		if (controller2 == null)
		{
			return false;
		}
		ControllerInfo value = new ControllerInfo(controller2, hidDevice);
		try
		{
			if (!hidDevice.IsOpen)
			{
				hidDevice.Open();
			}
		}
		catch (Exception ex2)
		{
			EventLogHelper.Error($"Open '{devicePath}' ({type}) failed. {ex2.Message}\n{ex2.StackTrace}", EventIDs.OpenDeviceError);
		}
		if (!hidDevice.IsOpen)
		{
			return false;
		}
		try
		{
			controller2.Init();
		}
		catch (Exception ex3)
		{
			EventLogHelper.Error($"Init '{devicePath}' ({type}) failed. {ex3.Message}\n{ex3.StackTrace}", EventIDs.InitControllerError);
		}
		try
		{
			if (SettingsImporter.AdoptLegacy(devicePath, hidDevice.VendorID, hidDevice.ProductID))
			{
				EventLogHelper.Info($"Adopted settings saved under an earlier hidraw-based id for '{devicePath}' ({type})");
			}
			if (SettingsImporter.AdoptPending(devicePath, hidDevice.VendorID, hidDevice.ProductID, hidDevice.InterfaceNumber))
			{
				EventLogHelper.Info($"Adopted imported settings for '{devicePath}' ({type})");
			}
		}
		catch (Exception exAdopt)
		{
			EventLogHelper.Error($"Adopting imported settings for '{devicePath}' failed. {exAdopt.Message}");
		}
		try
		{
			controller2.ApplyAll(DeviceSettingManager.LoadAll(devicePath));
		}
		catch (Exception ex4)
		{
			EventLogHelper.Error($"Apply '{devicePath}' ({type}) settings failed. {ex4.Message}\n{ex4.StackTrace}", EventIDs.ApplyControllerSettingOnStartError);
		}
		connectedControllers.Add(devicePath, value);
		return true;
	}

	private void removeConnectedController(string devicePath)
	{
		if (!connectedControllers.ContainsKey(devicePath))
		{
			return;
		}
		ControllerInfo controllerInfo = connectedControllers[devicePath];
		connectedControllers.Remove(devicePath);
		try
		{
			controllerInfo.Controller.Dispose();
		}
		catch
		{
		}
		try
		{
			if (controllerInfo.HidDevice.IsOpen)
			{
				controllerInfo.HidDevice.Close();
			}
		}
		catch
		{
		}
	}

	// Same dispatch as the original handleHttpRequest; returns the object to serialize as JSON (or null).
	public object HandleRequest(RequestData requestData)
	{
		try
		{
			if (requestData != null && requestData.Query.TryGetValue("action", out var value) && Enum.IsDefined(typeof(HttpRequestActionType), value) && Enum.TryParse<HttpRequestActionType>(value, out var result))
			{
				return result switch
				{
					HttpRequestActionType.Ping => "OK",
					HttpRequestActionType.Device => handleDeviceRequest(requestData),
					HttpRequestActionType.SystemResource => SystemInformation.Resource,
					HttpRequestActionType.SystemSpec => SystemInformation.GetSystemSpecification(),
					HttpRequestActionType.MotherboardARGBSync => handleMotherboardARGBSyncRequest(requestData),
					HttpRequestActionType.MotherboardRPMSync => handleMotherboardRPMSyncRequest(requestData),
					HttpRequestActionType.TLLCD => handleTLLCDRequest(requestData),
					_ => null,
				};
			}
		}
		catch (Exception ex)
		{
			EventLogHelper.Error("Request failed. " + ex.Message + "\n" + ex.StackTrace);
		}
		return null;
	}

	private object handleDeviceRequest(RequestData requestData)
	{
		if (!requestData.Query.TryGetValue("devicePath", out var value) || string.IsNullOrEmpty(value))
		{
			return null;
		}
		byte[] bytes = Convert.FromBase64String(value);
		string key = Encoding.UTF8.GetString(bytes);
		if (!connectedControllers.ContainsKey(key))
		{
			return null;
		}
		return connectedControllers[key].Controller.HandleRequestData(requestData);
	}

	private object handleMotherboardARGBSyncRequest(RequestData requestData)
	{
		if (!requestData.Query.TryGetValue("value", out var value) || string.IsNullOrEmpty(value) || !bool.TryParse(value, out var result))
		{
			return null;
		}
		try
		{
			Settings.Default.IsMotherboardARGBSync = result;
			Settings.Default.Save();
		}
		catch
		{
		}
		return null;
	}

	private object handleMotherboardRPMSyncRequest(RequestData requestData)
	{
		if (!requestData.Query.TryGetValue("value", out var value) || string.IsNullOrEmpty(value) || !bool.TryParse(value, out var result))
		{
			return null;
		}
		try
		{
			Settings.Default.IsMotherboardRPMSync = result;
			Settings.Default.Save();
		}
		catch
		{
		}
		return null;
	}

	private object handleTLLCDRequest(RequestData requestData)
	{
		if (requestData.Query.TryGetValue("cmd", out var value))
		{
			switch (value)
			{
			case "start":
				tllcdManager.setEnabled(isEnabled: true);
				return null;
			case "stop":
				tllcdManager.setEnabled(isEnabled: false);
				return null;
			case "getAllDevices":
				return tllcdManager.GetProductInfos();
			default:
				return null;
			}
		}
		if (!requestData.Query.TryGetValue("devicePath", out var value2) || string.IsNullOrEmpty(value2))
		{
			return null;
		}
		byte[] bytes = Convert.FromBase64String(value2);
		string key = Encoding.UTF8.GetString(bytes);
		if (!tllcdManager.connectedTLLCDControllers.ContainsKey(key))
		{
			return null;
		}
		return tllcdManager.connectedTLLCDControllers[key].Controller.HandleRequestData(requestData);
	}
}
