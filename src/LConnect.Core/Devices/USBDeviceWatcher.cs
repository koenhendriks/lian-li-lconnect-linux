using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LConnectCore.Devices;

// Replaces the WMI (__InstanceCreationEvent on Win32_USBControllerDevice) watcher. The original raised one
// event per HID interface of a registered VID/PID with the interface's device path; we do the same by diffing
// hid_enumerate results. The WMI query polled every 2 s ("WITHIN 2"); we poll every second.
public class USBDeviceWatcher
{
	public delegate void InsertedEventHandler(object sender, DeviceChangedEventArgs e);

	public delegate void RemovedEventHandler(object sender, DeviceChangedEventArgs e);

	private readonly HashSet<USBDeviceID> usbDeviceIDs = new HashSet<USBDeviceID>();

	private readonly object locker = new object();

	private Dictionary<string, USBDeviceID> known = new Dictionary<string, USBDeviceID>();

	private Timer timer;

	private bool isWatching;

	private int polling;

	public int PollIntervalMs { get; set; } = 1000;

	public event InsertedEventHandler Inserted;

	public event RemovedEventHandler Removed;

	public void Register(int vid, int pid)
	{
		Register(new USBDeviceID(vid, pid));
	}

	public void Register(USBDeviceID deviceID)
	{
		lock (locker)
		{
			usbDeviceIDs.Add(deviceID);
		}
	}

	public void Unregister(int vid, int pid)
	{
		Unregister(new USBDeviceID(vid, pid));
	}

	public void Unregister(USBDeviceID deviceID)
	{
		lock (locker)
		{
			usbDeviceIDs.Remove(deviceID);
		}
	}

	public bool Start()
	{
		if (isWatching)
		{
			return true;
		}
		known = snapshot();
		timer = new Timer(delegate
		{
			Poll();
		}, null, PollIntervalMs, PollIntervalMs);
		isWatching = true;
		return true;
	}

	public void Stop()
	{
		if (isWatching)
		{
			timer?.Dispose();
			timer = null;
			isWatching = false;
		}
	}

	// Can also be called directly, e.g. from a udev monitor or after system resume.
	public void Poll()
	{
		if (Interlocked.Exchange(ref polling, 1) == 1)
		{
			return;
		}
		try
		{
			Dictionary<string, USBDeviceID> dictionary = snapshot();
			List<KeyValuePair<string, USBDeviceID>> list = known.Where((KeyValuePair<string, USBDeviceID> p) => !dictionary.ContainsKey(p.Key)).ToList();
			List<KeyValuePair<string, USBDeviceID>> list2 = dictionary.Where((KeyValuePair<string, USBDeviceID> p) => !known.ContainsKey(p.Key)).ToList();
			known = dictionary;
			foreach (KeyValuePair<string, USBDeviceID> item in list)
			{
				try
				{
					this.Removed?.Invoke(this, new DeviceChangedEventArgs(item.Value, item.Key));
				}
				catch
				{
				}
			}
			foreach (KeyValuePair<string, USBDeviceID> item2 in list2)
			{
				try
				{
					this.Inserted?.Invoke(this, new DeviceChangedEventArgs(item2.Value, item2.Key));
				}
				catch
				{
				}
			}
		}
		finally
		{
			Interlocked.Exchange(ref polling, 0);
		}
	}

	private Dictionary<string, USBDeviceID> snapshot()
	{
		HashSet<USBDeviceID> ids;
		lock (locker)
		{
			ids = new HashSet<USBDeviceID>(usbDeviceIDs);
		}
		Dictionary<string, USBDeviceID> dictionary = new Dictionary<string, USBDeviceID>();
		foreach (DeviceFinderResult item in new HidApiDeviceFinder().Find())
		{
			if (item.DevicePath != null && ids.Contains(item.USBDeviceID))
			{
				dictionary[item.DevicePath] = item.USBDeviceID;
			}
		}
		return dictionary;
	}
}
