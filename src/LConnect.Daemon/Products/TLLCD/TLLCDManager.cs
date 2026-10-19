using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LConnectCore.Devices;
using LConnectCore.Products.TLLCD;
using LConnectService.Enums;
using LConnectService.Helpers;
using LConnectService.Products.TLFan;
using PrecisionTiming;

namespace LConnectService.Products.TLLCD;

internal class TLLCDManager
{
	public class ControllerInfo
	{
		public IController Controller;

		public List<ITLLCDDevice>[] HidDevices;

		public ControllerInfo(IController controller, List<ITLLCDDevice>[] hidDevices)
		{
			Controller = controller;
			HidDevices = hidDevices;
		}
	}

	private bool enabled = true;

	private List<List<TLLCDDevice>> devices = new List<List<TLLCDDevice>>();

	public List<TLLCDController> controllers = new List<TLLCDController>();

	private List<TLLCDDevice> deviceList = new List<TLLCDDevice>();

	private List<string> portLists = new List<string>();

	private List<int> levelLists = new List<int>();

	public Dictionary<string, ControllerInfo> connectedTLLCDControllers = new Dictionary<string, ControllerInfo>();

	public PrecisionTimer VideoTimer { get; set; }

	public void initTLLCDSetting()
	{
		loadTLLCDSetting();
		VideoTimer = new PrecisionTimer();
		VideoTimer.SetInterval(() =>
		{
			foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
			{
				if (connectedTLLCDController.Value.Controller is TLLCDController)
				{
					((TLLCDController)connectedTLLCDController.Value.Controller).SyncVideoThread();
				}
			}
		}, 1000.0 / Constants.StreamFramesPerSecond);
	}

	public void loadTLLCDSetting()
	{
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			if (connectedTLLCDController.Value.Controller is TLLCDController)
			{
				TLLCDController tLLCDController = (TLLCDController)connectedTLLCDController.Value.Controller;
				tLLCDController.LoadSettings();
				tLLCDController.ExecuteSetting();
			}
		}
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	public void reloadDevice(List<IController> connectedControllers)
	{
		if (enabled)
		{
			searchConnectedTLLCDDevice();
			matchTLLCDFan(connectedControllers);
			loadTLLCDSetting();
		}
	}

	public void setEnabled(bool isEnabled)
	{
		enabled = isEnabled;
		if (enabled)
		{
			reloadDevice(new List<IController>());
		}
		else
		{
			stopAll();
		}
	}

	public void stopAll()
	{
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			if (connectedTLLCDController.Value.Controller is TLLCDController)
			{
				((TLLCDController)connectedTLLCDController.Value.Controller).CloseAllDevices();
			}
		}
	}

	public List<ProductInfo> GetProductInfos()
	{
		List<ProductInfo> list = new List<ProductInfo>();
		int index = 0;
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			if (connectedTLLCDController.Value.Controller is TLLCDController)
			{
				TLLCDController tLLCDController = (TLLCDController)connectedTLLCDController.Value.Controller;
				list.AddRange(tLLCDController.GetProductInfo(index));
			}
		}
		return list;
	}

	public void matchTLLCDFan(List<IController> connectedControllers)
	{
		List<TLFanController> source = (from p in connectedControllers
			where p is TLFanController
			select p as TLFanController).ToList();
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			if (connectedTLLCDController.Value.Controller is TLLCDController)
			{
				TLLCDController cont = (TLLCDController)connectedTLLCDController.Value.Controller;
				TLFanController tLFanController = source.FirstOrDefault((TLFanController p) => p.DevicePath == cont.DevicePath);
				cont.SetTLFanController(tLFanController);
			}
		}
	}

	public void searchConnectedTLLCDDevice()
	{
		IList<DeviceFinderResult> list = new HidApiDeviceFinder().Find();
		controllers.Clear();
		devices.Clear();
		foreach (DeviceFinderResult item in list)
		{
			if (item.ControllerType == ControllerType.TLFan && item.UsagePage == 65307)
			{
				controllers.Add(new TLLCDController(item.DevicePath, GetParentLocationPathToString(item.LocationInfo)));
			}
			else if (item.ControllerType == ControllerType.TLLCD)
			{
				AddTLLCDDevice(item);
			}
		}
		MatchTLLCDController();
		AddConnectedTLLCDController();
		printTLLCDDevice();
	}

	public void AddTLLCDDevice(DeviceFinderResult result)
	{
		string parentLocationPathToString = GetParentLocationPathToString(result.LocationInfo);
		portLists.Add(parentLocationPathToString);
		levelLists.Add(getLocationPathLength(parentLocationPathToString));
		deviceList.Add(new TLLCDDevice(new HidApiHidDevice(result.DevicePath), GetParentLocationPathToString(result.LocationInfo)));
		EventLogHelper.Info("found '" + result.LocationInfo + "'");
	}

	public void MatchTLLCDController()
	{
		if (matchTLLCDPortDevice() > 0)
		{
			matchTLLCDControllerDevice();
		}
	}

	private int matchTLLCDPortDevice()
	{
		int num = 0;
		int num2 = 0;
		while (portLists.Count != 0)
		{
			int index = findIndexOfMinLength(levelLists);
			string parentLocationPathToString = GetParentLocationPathToString(portLists[index]);
			devices.Add(new List<TLLCDDevice>());
			List<int> list = new List<int>();
			num2 = 0;
			foreach (string portList in portLists)
			{
				if (CheckIsParentLocation(parentLocationPathToString, portList, out var _, 1))
				{
					devices[num].Add(deviceList[num2]);
					list.Add(num2);
				}
				num2++;
			}
			list.Sort();
			for (num2 = list.Count - 1; num2 >= 0; num2--)
			{
				portLists.RemoveAt(list[num2]);
				deviceList.RemoveAt(list[num2]);
				levelLists.RemoveAt(list[num2]);
			}
			devices[num].Sort((TLLCDDevice x, TLLCDDevice y) => x.GetPortString().Length.CompareTo(y.GetPortString().Length));
			num++;
		}
		return num;
	}

	private void matchTLLCDControllerDevice()
	{
		List<string> list = new List<string>();
		for (int i = 0; i < devices.Count; i++)
		{
			list.Add(devices[i][0].GetPortString());
		}
		for (int j = 0; j < controllers.Count; j++)
		{
			string[] array = controllers[j].controllerLocationString.Split('.');
			if (array.Length == 0)
			{
				continue;
			}
			string[] value = array.Take(array.Count() - 1).ToArray();
			foreach (string item in list)
			{
				string[] array2 = item.Split('.');
				for (int k = 0; k < devices.Count; k++)
				{
					if (devices[k][0].IsMatched() || (array2.Length != array.Length && !devices[k][0].GetPortString().StartsWith(string.Join(".", value))))
					{
						continue;
					}
					foreach (TLLCDDevice item2 in devices[k])
					{
						((ITLLCDDevice)item2).Open();
						int port = ((ITLLCDDevice)item2).GetPort();
						((IHidDeviceWrapper)item2).HidDevice.Close();
						((ITLLCDDevice)item2).SetMatched(true);
						controllers[j].devices[port].Add(item2);
						controllers[j].portLocationString[port] = ((ITLLCDDevice)item2).GetPortString();
					}
					break;
				}
			}
		}
	}

	private bool AddConnectedTLLCDController()
	{
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			if (connectedTLLCDController.Value.Controller is TLLCDController)
			{
				((TLLCDController)connectedTLLCDController.Value.Controller).ClearThread();
			}
		}
		connectedTLLCDControllers.Clear();
		for (int i = 0; i < controllers.Count; i++)
		{
			if (controllers[i].DevicePath == null)
			{
				continue;
			}
			ControllerInfo value = new ControllerInfo(controllers[i], controllers[i].devices);
			for (int j = 0; j < 4; j++)
			{
				if (controllers[i].GetPortDevicesCount(j) <= 0)
				{
					continue;
				}
				for (int k = 0; k < controllers[i].GetDevices(j).Count; k++)
				{
					string devicePath = controllers[i].GetDevices(j)[k].HidDevice.DevicePath;
					try
					{
						controllers[i].GetDevices(j)[k].Open();
					}
					catch (Exception ex)
					{
						EventLogHelper.Error($"Open '{devicePath}' ({ControllerType.TLLCD}) failed. {ex.Message}\n{ex.StackTrace}", EventIDs.OpenDeviceError);
					}
					if (!controllers[i].GetDevices(j)[k].HidDevice.IsOpen)
					{
						return false;
					}
				}
			}
			if (controllers[i] != null)
			{
				try
				{
					controllers[i].Init();
				}
				catch (Exception ex2)
				{
					EventLogHelper.Error($"Init '{controllers[i].DevicePath}' ({ControllerType.TLLCD}) failed. {ex2.Message}\n{ex2.StackTrace}", EventIDs.InitControllerError);
				}
				try
				{
					controllers[i].ApplyAll(DeviceSettingManager.LoadAll(controllers[i].DevicePath));
				}
				catch (Exception ex3)
				{
					EventLogHelper.Error($"Apply '{controllers[i].DevicePath}' ({ControllerType.TLLCD}) settings failed. {ex3.Message}\n{ex3.StackTrace}", EventIDs.ApplyControllerSettingOnStartError);
				}
				connectedTLLCDControllers.Add(controllers[i].DevicePath, value);
			}
		}
		return true;
	}

	public bool addConnectedTLLCDDevice(ControllerType type, string devicePath)
	{
		if (type != ControllerType.TLLCD)
		{
			return false;
		}
		HidApiHidDevice hidApiHidDevice = new HidApiHidDevice(devicePath);
		((IHidDevice)hidApiHidDevice).Open();
		((IHidDevice)hidApiHidDevice).Close();
		TLLCDDevice tLLCDDevice = new TLLCDDevice(hidApiHidDevice, GetParentLocationPathToString(((IHidDevice)hidApiHidDevice).LocationInfo));
		tLLCDDevice.Open();
		tLLCDDevice.HidDevice.Close();
		Console.WriteLine($"Insert Device PortString: {tLLCDDevice.GetPortString()}, Port:{tLLCDDevice.PortNo}, Index:{tLLCDDevice.LCDIndex}");
		int chainNumber = -1;
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			list.Add(connectedTLLCDController.Key);
		}
		for (int i = 0; i < connectedTLLCDControllers.Count; i++)
		{
			ControllerInfo controllerInfo = connectedTLLCDControllers[list[i]];
			if (controllerInfo.Controller is TLLCDController)
			{
				TLLCDController tLLCDController = (TLLCDController)controllerInfo.Controller;
				if (tLLCDController.ChekcIsGroup(tLLCDDevice.GetPortString()))
				{
					for (int j = 0; j < 4; j++)
					{
						if (tLLCDController.GetPortDevicesCount(j) > 0)
						{
							if (CheckIsParentLocation(tLLCDController.devices[j][0].GetPortString(), tLLCDDevice.GetPortString(), out chainNumber, 1))
							{
								try
								{
									tLLCDDevice.HidDevice.Open();
								}
								catch (Exception ex)
								{
									EventLogHelper.Error($"Open '{devicePath}' ({type}) failed. {ex.Message}\n{ex.StackTrace}", EventIDs.OpenDeviceError);
								}
								tLLCDController.devices[j].Add(tLLCDDevice);
								tLLCDController.devices[j].Sort((ITLLCDDevice x, ITLLCDDevice y) => x.GetPortString().Length.CompareTo(y.GetPortString().Length));
								return true;
							}
						}
						else if (j == tLLCDDevice.PortNo)
						{
							try
							{
								tLLCDDevice.HidDevice.Open();
							}
							catch (Exception ex2)
							{
								EventLogHelper.Error($"Open '{devicePath}' ({type}) failed. {ex2.Message}\n{ex2.StackTrace}", EventIDs.OpenDeviceError);
							}
							tLLCDController.devices[j].Add(tLLCDDevice);
							return true;
						}
					}
				}
			}
			TLLCDController tLLCDController2 = new TLLCDController(tLLCDDevice.HidDevice.DevicePath, GetControllerPortString(tLLCDDevice.GetPortString(), tLLCDDevice.LCDIndex + 1));
			if (tLLCDController2 == null)
			{
				return false;
			}
			int portNo = tLLCDDevice.PortNo;
			tLLCDController2.devices[portNo].Add(tLLCDDevice);
			tLLCDController2.portLocationString[portNo] = tLLCDDevice.GetPortString();
			ControllerInfo value = new ControllerInfo(tLLCDController2, tLLCDController2.devices);
			try
			{
				tLLCDDevice.HidDevice.Open();
			}
			catch (Exception ex3)
			{
				EventLogHelper.Error($"Open '{devicePath}' ({type}) failed. {ex3.Message}\n{ex3.StackTrace}", EventIDs.OpenDeviceError);
			}
			if (!tLLCDDevice.HidDevice.IsOpen)
			{
				return false;
			}
			try
			{
				tLLCDController2.Init();
			}
			catch (Exception ex4)
			{
				EventLogHelper.Error($"Init '{devicePath}' ({type}) failed. {ex4.Message}\n{ex4.StackTrace}", EventIDs.InitControllerError);
			}
			try
			{
				tLLCDController2.ApplyAll(DeviceSettingManager.LoadAll(devicePath));
			}
			catch (Exception ex5)
			{
				EventLogHelper.Error($"Apply '{devicePath}' ({type}) settings failed. {ex5.Message}\n{ex5.StackTrace}", EventIDs.ApplyControllerSettingOnStartError);
			}
			connectedTLLCDControllers.Add(devicePath, value);
		}
		return true;
	}

	public void removeConnectedTLLCDDevice(string devicePath)
	{
		foreach (KeyValuePair<string, ControllerInfo> connectedTLLCDController in connectedTLLCDControllers)
		{
			if (!(connectedTLLCDController.Value.Controller is TLLCDController))
			{
				continue;
			}
			TLLCDController tLLCDController = (TLLCDController)connectedTLLCDController.Value.Controller;
			if (tLLCDController.ContainTLLCDDevice(devicePath))
			{
				tLLCDController.RemoveTLLCDDevice(devicePath);
				if (tLLCDController.GetTotalDeviceCount() == 0)
				{
					ControllerInfo controllerInfo = connectedTLLCDControllers[connectedTLLCDController.Key];
					connectedTLLCDControllers.Remove(connectedTLLCDController.Key);
					Console.WriteLine("Remove connectedController");
					try
					{
						controllerInfo.Controller.Dispose();
						break;
					}
					catch
					{
						break;
					}
				}
				break;
			}
		}
	}

	private static int findIndexOfMinLength(List<int> list)
	{
		int num = list[0];
		int result = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (num > list[i])
			{
				num = list[i];
				result = i;
			}
		}
		return result;
	}

	private static int getLocationPathLength(string locationPath)
	{
		return locationPath.Split('.').Length;
	}

	private static int getLocationPathLength(int[] portNumbers)
	{
		for (int i = 0; i < portNumbers.Length; i++)
		{
			if (portNumbers[i] == 0)
			{
				return i;
			}
		}
		return portNumbers.Length;
	}

	public static string GetControllerPortString(string locationString, int level)
	{
		int[] array = decodeLocationPath(locationString);
		string text = array[0].ToString();
		for (int i = 1; i < array.Length - level; i++)
		{
			if (array[i] != 0)
			{
				if (i < array.Length - 1)
				{
					text += ".";
				}
				text += array[i];
			}
		}
		return text;
	}

	private static int getControllerPortNumber(string locationString)
	{
		return getControllerPortNumber(locationString, 0);
	}

	private static int getControllerPortNumber(string locationString, int level)
	{
		int[] array = decodeLocationPath(locationString);
		int locationPathLength = getLocationPathLength(array);
		if (locationPathLength <= 1)
		{
			return 0;
		}
		return array[locationPathLength - 2 - level];
	}

	private static int getControllerPortNumber(DeviceFinderResult result)
	{
		int[] array = decodeLocationPath(result.LocationInfo);
		int locationPathLength = getLocationPathLength(array);
		if (locationPathLength <= 1)
		{
			return 0;
		}
		return array[locationPathLength - 1];
	}

	private bool checkParent_TLLCD(DeviceFinderResult parentDevice, DeviceFinderResult childDevice, out int chainNumber)
	{
		return CheckIsParentLocation(parentDevice.LocationInfo, childDevice.LocationInfo, out chainNumber, 1);
	}

	public static bool CheckIsParentLocation(string parentLocation, string childLocation, out int chainNumber, int discardLevel)
	{
		chainNumber = -1;
		int[] array = decodeLocationPath(parentLocation);
		int[] array2 = decodeLocationPath(childLocation);
		int locationPathLength = getLocationPathLength(array);
		int locationPathLength2 = getLocationPathLength(array2);
		bool flag = true;
		if (locationPathLength2 < locationPathLength)
		{
			return false;
		}
		for (int i = 0; i < locationPathLength - discardLevel; i++)
		{
			if (array[i] != array2[i])
			{
				return false;
			}
		}
		if (flag)
		{
			chainNumber = ((locationPathLength2 - locationPathLength > 0) ? (locationPathLength2 - locationPathLength) : 0);
		}
		return true;
	}

	private string getControllerLocationPathToString(string controllerLocation, string deviceLocation)
	{
		int[] array = decodeLocationPath(controllerLocation);
		int[] array2 = decodeLocationPath(deviceLocation);
		int locationPathLength = getLocationPathLength(array);
		int locationPathLength2 = getLocationPathLength(array2);
		bool flag = true;
		if (locationPathLength2 <= locationPathLength)
		{
			return string.Empty;
		}
		for (int i = 0; i < locationPathLength - 1; i++)
		{
			if (array[i] != array2[i])
			{
				return string.Empty;
			}
		}
		if (flag && locationPathLength2 - locationPathLength > 0)
		{
			int num = locationPathLength2 - locationPathLength;
			string text = string.Empty;
			for (int j = 0; j < locationPathLength2 - num; j++)
			{
				text += array2[j];
				if (j < locationPathLength2 - num - 1)
				{
					text += ".";
				}
			}
			return text;
		}
		return string.Empty;
	}

	public static string GetParentLocationPathToString(string paths)
	{
		int[] array = decodeLocationPath(paths);
		string text = array[0].ToString();
		for (int i = 1; i < array.Length; i++)
		{
			if (array[i] != 0)
			{
				if (i < array.Length - 1)
				{
					text += ".";
				}
				text += array[i];
			}
		}
		return text;
	}

	private static int[] decodeLocationPath(string paths)
	{
		int[] array = new int[12];
		try
		{
			string[] array2 = paths.Split('.');
			int num = 0;
			for (int i = 0; i < array2.Length && i < array.Length; i++)
			{
				int num2 = int.Parse(array2[i]);
				if (num2 != 0)
				{
					array[num++] = num2;
				}
			}
		}
		catch
		{
			return array;
		}
		return array;
	}

	public static int PortMap(int locationPort)
	{
		return locationPort switch
		{
			1 => 3, 
			2 => 2, 
			3 => 1, 
			4 => 0, 
			_ => 0, 
		};
	}

	private void printTLLCDDevice()
	{
	}
}
