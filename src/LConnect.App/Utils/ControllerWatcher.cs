using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using LConnect3cs.Helpers;
using LConnectCore.Devices;

namespace LConnect3cs.Utils;

// The original UI enumerated HID devices itself and watched WMI events. Here the daemon owns the hardware, so the
// UI mirrors the daemon's list of connected controllers (GET /status) and adds/removes UI controllers accordingly.
internal class ControllerWatcher
{
	public sealed class DaemonController
	{
		public string DevicePath { get; set; }

		public string Type { get; set; }

		public int VendorID { get; set; }

		public int ProductID { get; set; }

		public string Product { get; set; }
	}

	private sealed class Status
	{
		public List<DaemonController> Controllers { get; set; }
	}

	private static readonly Lazy<ControllerWatcher> lazy = new Lazy<ControllerWatcher>(() => new ControllerWatcher());

	private readonly Dictionary<string, (ControllerType, USBDeviceID)> known = new Dictionary<string, (ControllerType, USBDeviceID)>();

	private Timer timer;

	private int polling;

	public static ControllerWatcher Instance => lazy.Value;

	public bool IsWatching { get; private set; }

	public bool DaemonReachable { get; private set; }

	public event EventHandler DaemonStateChanged;

	public event EventHandler ControllersChanged;

	public void Start()
	{
		if (!IsWatching)
		{
			Poll();
			timer = new Timer(delegate
			{
				Poll();
			}, null, 2000, 2000);
			IsWatching = true;
		}
	}

	public void Stop()
	{
		if (IsWatching)
		{
			timer?.Dispose();
			timer = null;
			IsWatching = false;
		}
	}

	public static List<DaemonController> QueryDaemon()
	{
		string text = LConnectServiceHttpUtility.Send(HttpMethod.Get, "status");
		if (text == null)
		{
			return null;
		}
		return JsonSerializer.Deserialize<Status>(text)?.Controllers ?? new List<DaemonController>();
	}

	public void Poll()
	{
		if (Interlocked.Exchange(ref polling, 1) == 1)
		{
			return;
		}
		try
		{
			List<DaemonController> list;
			try
			{
				list = QueryDaemon();
			}
			catch
			{
				list = null;
			}
			bool flag = list != null;
			if (flag != DaemonReachable)
			{
				DaemonReachable = flag;
				this.DaemonStateChanged?.Invoke(this, EventArgs.Empty);
			}
			if (list == null)
			{
				return;
			}
			Dictionary<string, (ControllerType, USBDeviceID)> dictionary = new Dictionary<string, (ControllerType, USBDeviceID)>();
			foreach (DaemonController item in list)
			{
				USBDeviceID uSBDeviceID = new USBDeviceID(item.VendorID, item.ProductID);
				ControllerType controllerType = KnownDevices.GetControllerType(uSBDeviceID);
				if (controllerType != ControllerType.None && controllerType != ControllerType.TLLCD)
				{
					dictionary[item.DevicePath] = (controllerType, uSBDeviceID);
				}
			}
			bool flag2 = false;
			foreach (KeyValuePair<string, (ControllerType, USBDeviceID)> item2 in known.Where((KeyValuePair<string, (ControllerType, USBDeviceID)> k) => !dictionary.ContainsKey(k.Key)).ToList())
			{
				Logger.Instance.Info($"'{item2.Value.Item1}' removed. ({item2.Key})");
				ControllerManager.Remove(item2.Value.Item1, item2.Key);
				known.Remove(item2.Key);
				flag2 = true;
			}
			foreach (KeyValuePair<string, (ControllerType, USBDeviceID)> item3 in dictionary.Where((KeyValuePair<string, (ControllerType, USBDeviceID)> k) => !known.ContainsKey(k.Key)).ToList())
			{
				Logger.Instance.Info($"'{item3.Value.Item1}' inserted. ({item3.Key})");
				ControllerManager.Add(item3.Value.Item1, item3.Key, item3.Value.Item2);
				known[item3.Key] = item3.Value;
				flag2 = true;
			}
			if (flag2)
			{
				this.ControllersChanged?.Invoke(this, EventArgs.Empty);
			}
		}
		finally
		{
			Interlocked.Exchange(ref polling, 0);
		}
	}
}
