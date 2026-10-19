using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using LConnect3cs.Products.Ene6K77Fan;
using LConnect3cs.Products.TLFan;
using LConnect3cs.Utils;

namespace LConnectUi.Services;

// Bridges ControllerWatcher (which polls lconnectd from a timer thread and fills the stores) to the UI thread.
internal sealed class ControllerHub
{
	private static readonly Lazy<ControllerHub> lazy = new Lazy<ControllerHub>(() => new ControllerHub());

	private readonly HashSet<TLFanController> hooked = new HashSet<TLFanController>();

	private bool started;

	public static ControllerHub Instance => lazy.Value;

	public bool DaemonReachable => ControllerWatcher.Instance.DaemonReachable;

	// Raised on the UI thread whenever the daemon state or the set of controllers changes, or a TL controller
	// reports a different fan topology.
	public event EventHandler Changed;

	private ControllerHub()
	{
		ControllerWatcher.Instance.DaemonStateChanged += delegate
		{
			raise();
		};
		ControllerWatcher.Instance.ControllersChanged += delegate
		{
			hookControllers();
			raise();
		};
	}

	public void Start()
	{
		if (started)
		{
			return;
		}
		started = true;
		Task.Run(delegate
		{
			ControllerWatcher.Instance.Start();
			hookControllers();
			raise();
		});
	}

	// Synchronous poll (used by tests and after imports).
	public void PollNow()
	{
		ControllerWatcher.Instance.Poll();
		hookControllers();
	}

	public List<TLFanController> TLControllers => snapshot(() => TLFanStore.Instance.Controllers.Values.ToList());

	public List<SLInfinityController> SLInfinityControllers => snapshot(() => SLInfinityStore.Instance.Controllers.Values.ToList());

	public TLFanController FindTL(string devicePath)
	{
		return TLControllers.FirstOrDefault((TLFanController c) => c.DevicePath == devicePath);
	}

	public SLInfinityController FindSL(string devicePath)
	{
		return SLInfinityControllers.FirstOrDefault((SLInfinityController c) => c.DevicePath == devicePath);
	}

	private static List<T> snapshot<T>(Func<List<T>> get)
	{
		// The stores are filled from the watcher's timer thread; retry if we raced a modification.
		for (int i = 0; i < 5; i++)
		{
			try
			{
				return get();
			}
			catch (InvalidOperationException)
			{
			}
		}
		return new List<T>();
	}

	private void hookControllers()
	{
		foreach (TLFanController tLController in TLControllers)
		{
			lock (hooked)
			{
				if (!hooked.Add(tLController))
				{
					continue;
				}
			}
			tLController.FanGroupStatusChanged += delegate
			{
				raise();
			};
			tLController.ProfileReloaded += delegate
			{
				raise();
			};
		}
		foreach (SLInfinityController sLInfinityController in SLInfinityControllers)
		{
			sLInfinityController.ProfileReloaded -= onProfileReloaded;
			sLInfinityController.ProfileReloaded += onProfileReloaded;
		}
	}

	private void onProfileReloaded(object sender, EventArgs e)
	{
		raise();
	}

	private void raise()
	{
		Dispatcher.UIThread.Post(delegate
		{
			Changed?.Invoke(this, EventArgs.Empty);
		});
	}
}
