using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LConnect3cs.Products.Ene6K77Fan;
using LConnect3cs.Products.TLFan;
using LConnectCore;
using LConnectCore.Products.TLFan;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

public sealed class OverviewRow
{
	public string Title { get; init; }

	public string Detail { get; init; }

	public string Extra { get; init; }
}

public sealed class SensorTile
{
	public string Label { get; init; }

	public string Value { get; init; }

	public string Sub { get; init; }

	public double Percent { get; init; }
}

public partial class OverviewViewModel : PageViewModel
{
	private bool refreshing;

	[ObservableProperty]
	private bool daemonReachable;

	[ObservableProperty]
	private bool hasLoaded;

	[ObservableProperty]
	private string dataDirectory = string.Empty;

	public override string Title => "Overview";

	public override string IconKey => "Home";

	protected override TimeSpan PollInterval => TimeSpan.FromSeconds(2.0);

	public ObservableCollection<OverviewRow> Controllers { get; } = new ObservableCollection<OverviewRow>();

	public ObservableCollection<OverviewRow> Lcds { get; } = new ObservableCollection<OverviewRow>();

	public ObservableCollection<OverviewRow> FanPorts { get; } = new ObservableCollection<OverviewRow>();

	public ObservableCollection<SensorTile> Sensors { get; } = new ObservableCollection<SensorTile>();

	public string SocketPath => DaemonClient.SocketPath;

	public string DaemonHint => "Cannot reach lconnectd at " + DaemonClient.SocketPath + ". Check that the service is running (systemctl status lconnectd) and that your user is in the 'lconnect' group (log out and back in after adding it).";

	public bool ShowDaemonHint
	{
		get
		{
			if (HasLoaded)
			{
				return !DaemonReachable;
			}
			return false;
		}
	}

	public bool HasControllers => Controllers.Count > 0;

	public bool HasLcds => Lcds.Count > 0;

	public bool HasFanPorts => FanPorts.Count > 0;

	partial void OnDaemonReachableChanged(bool value)
	{
		OnPropertyChanged("ShowDaemonHint");
	}

	partial void OnHasLoadedChanged(bool value)
	{
		OnPropertyChanged("ShowDaemonHint");
	}

	protected override void OnActivated()
	{
		RefreshAsync();
	}

	protected override void OnPoll()
	{
		RefreshAsync();
	}

	private sealed class Snapshot
	{
		public DaemonStatus Status;

		public List<(string Path, FanGroupStatus Status)> TL = new List<(string, FanGroupStatus)>();

		public List<(string Path, int[] Speeds)> SL = new List<(string, int[])>();
	}

	public async Task RefreshAsync()
	{
		if (refreshing)
		{
			return;
		}
		refreshing = true;
		try
		{
			Snapshot snapshot = await Task.Run(delegate
			{
				Snapshot snapshot2 = new Snapshot
				{
					Status = DaemonClient.GetStatus()
				};
				if (snapshot2.Status != null)
				{
					foreach (DaemonControllerInfo controller in snapshot2.Status.Controllers)
					{
						if (controller.VendorID == 1046 && controller.ProductID == 29554)
						{
							snapshot2.TL.Add((controller.DevicePath, DaemonClient.GetFanGroupStatus(controller.DevicePath)));
						}
						else if (controller.Type == "SLInfinityController")
						{
							snapshot2.SL.Add((controller.DevicePath, DaemonClient.GetSLFanSpeeds(controller.DevicePath)));
						}
					}
				}
				return snapshot2;
			});
			apply(snapshot);
		}
		finally
		{
			refreshing = false;
			HasLoaded = true;
		}
	}

	private void apply(Snapshot s)
	{
		DaemonReachable = s.Status != null;
		Controllers.Clear();
		Lcds.Clear();
		FanPorts.Clear();
		Sensors.Clear();
		if (s.Status == null)
		{
			raiseLists();
			return;
		}
		DataDirectory = s.Status.DataDirectory ?? LConnectApp.Path.DataRoot;
		foreach (DaemonControllerInfo controller in s.Status.Controllers)
		{
			Controllers.Add(new OverviewRow
			{
				Title = friendlyType(controller.Type),
				Detail = $"{controller.VendorID:x4}:{controller.ProductID:x4} · {controller.Product}",
				Extra = controller.DevicePath
			});
		}
		foreach (DaemonLcdInfo item in from l in s.Status.Lcds
			orderby l.PortNo, l.LCDIndex
			select l)
		{
			Lcds.Add(new OverviewRow
			{
				Title = $"Port {item.PortNo + 1} · LCD {item.LCDIndex + 1}",
				Detail = "Firmware " + (item.Firmware ?? "?"),
				Extra = item.DevicePath
			});
		}
		foreach (var (text, fanGroupStatus) in s.TL)
		{
			if (fanGroupStatus == null)
			{
				continue;
			}
			for (int i = 0; i < 4; i++)
			{
				PortType portType = fanGroupStatus.PortTypes[i];
				if (portType != PortType.None)
				{
					int[] source = fanGroupStatus.FanSpeeds?[i] ?? Array.Empty<int>();
					int num = fanGroupStatus.ActualTotalQuantities[i];
					List<int> list = source.Take(Math.Max(num, 0)).ToList();
					FanPorts.Add(new OverviewRow
					{
						Title = $"TL port {i + 1} · {portType}",
						Detail = $"{num} fan(s)" + ((list.Count > 0) ? (" · avg " + (int)list.Average() + " rpm") : string.Empty),
						Extra = string.Join("  ", list.Select((int r, int idx) => $"#{idx + 1} {r} rpm"))
					});
				}
			}
		}
		foreach (var (_, array) in s.SL)
		{
			if (array != null)
			{
				FanPorts.Add(new OverviewRow
				{
					Title = "SL Infinity",
					Detail = $"{array.Length} groups",
					Extra = string.Join("  ", array.Select((int r, int idx) => $"group {idx + 1}: {r} rpm"))
				});
			}
		}
		SystemResource? resource = s.Status.Resource;
		if (resource.HasValue)
		{
			SystemResource value = resource.Value;
			Sensors.Add(new SensorTile
			{
				Label = Loc.Get("I18n.Text.CPUTemp", "CPU Temp"),
				Value = (value.CPUTemperatureHasValue || value.CPUTemperature != 0) ? $"{value.CPUTemperature}°C" : "n/a",
				Sub = "CPU",
				Percent = Math.Clamp(value.CPUTemperature, 0, 100)
			});
			Sensors.Add(new SensorTile
			{
				Label = Loc.Get("I18n.Text.CPULoad", "CPU Load"),
				Value = $"{value.CPULoad}%",
				Sub = "CPU",
				Percent = Math.Clamp(value.CPULoad, 0, 100)
			});
			Sensors.Add(new SensorTile
			{
				Label = Loc.Get("I18n.Text.GPUTemp", "GPU Temp"),
				Value = (value.GPUTemperatureHasValue || value.GPUTemperature != 0) ? $"{value.GPUTemperature}°C" : "n/a",
				Sub = "GPU",
				Percent = Math.Clamp(value.GPUTemperature, 0, 100)
			});
			Sensors.Add(new SensorTile
			{
				Label = Loc.Get("I18n.Text.GPULoad", "GPU Load"),
				Value = $"{value.GPULoad}%",
				Sub = "GPU",
				Percent = Math.Clamp(value.GPULoad, 0, 100)
			});
			Sensors.Add(new SensorTile
			{
				Label = "RAM",
				Value = $"{value.RAMLoad}%",
				Sub = ((value.RAMTotal != 0) ? $"{value.RAMUsed / 1073741824.0:0.0} / {value.RAMTotal / 1073741824.0:0.0} GB" : "RAM"),
				Percent = Math.Clamp(value.RAMLoad, 0, 100)
			});
		}
		raiseLists();
	}

	private void raiseLists()
	{
		OnPropertyChanged("HasControllers");
		OnPropertyChanged("HasLcds");
		OnPropertyChanged("HasFanPorts");
	}

	private static string friendlyType(string type)
	{
		return type switch
		{
			"TLFanController" => "TL fan controller",
			"SLInfinityController" => "SL Infinity hub",
			"SLFanController" => "SL fan hub",
			"SLV2FanController" => "SL V2 fan hub",
			"ALFanController" => "AL fan hub",
			"ALV2FanController" => "AL V2 fan hub",
			_ => type,
		};
	}
}
