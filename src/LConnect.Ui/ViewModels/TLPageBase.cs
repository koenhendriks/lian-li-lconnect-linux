using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using LConnect3cs.Products.TLFan;
using LConnectCore.Products.TLFan;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

public sealed class ControllerItem
{
	public string DevicePath { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public partial class PortItem : ObservableObject
{
	[ObservableProperty]
	private string summary;

	public int Port { get; init; }

	public PortType Type { get; init; }

	public string Name { get; init; }

	public int FanCount { get; init; }

	public string TypeText => Type switch
	{
		PortType.LCD => "LCD",
		PortType.LED => "LED",
		_ => "—",
	};
}

public partial class GroupItem : ObservableObject
{
	[ObservableProperty]
	private int rpm;

	public int Index { get; init; }

	public int FanQuantity { get; init; }

	public string Name { get; init; }

	public string FansText => (FanQuantity == 1) ? "1 fan" : $"{FanQuantity} fans";

	public string RpmText => $"{Rpm} rpm";

	partial void OnRpmChanged(int value)
	{
		OnPropertyChanged("RpmText");
	}
}

// Controller -> port -> fan group selection of the TL pages, rebuilt from the fan topology the TL controller
// reports (FanGroupStatus: port types and fan quantities per group), like TLFanUtilityMainPage did.
public abstract partial class TLPageBase : PageViewModel
{
	private string topologySignature = string.Empty;

	protected bool suppressSelection;

	[ObservableProperty]
	private ControllerItem selectedController;

	[ObservableProperty]
	private PortItem selectedPort;

	[ObservableProperty]
	private GroupItem selectedGroup;

	[ObservableProperty]
	private bool hasController;

	[ObservableProperty]
	private bool hasPorts;

	public ObservableCollection<ControllerItem> Controllers { get; } = new ObservableCollection<ControllerItem>();

	public ObservableCollection<PortItem> Ports { get; } = new ObservableCollection<PortItem>();

	public ObservableCollection<GroupItem> Groups { get; } = new ObservableCollection<GroupItem>();

	public bool MultipleControllers => Controllers.Count > 1;

	public string NoDeviceHint
	{
		get
		{
			if (!ControllerHub.Instance.DaemonReachable)
			{
				return "lconnectd is not reachable (" + DaemonClient.SocketPath + "). Is the service running and are you in the 'lconnect' group?";
			}
			if (!HasController)
			{
				return "No TL fan controller connected.";
			}
			return "No fans detected on the controller's ports yet.";
		}
	}

	internal TLFanController Controller => (SelectedController == null) ? null : ControllerHub.Instance.FindTL(SelectedController.DevicePath);

	protected override TimeSpan PollInterval => TimeSpan.FromSeconds(1.0);

	protected TLPageBase()
	{
		ControllerHub.Instance.Changed += delegate
		{
			if (IsActive)
			{
				RefreshTopology();
			}
		};
	}

	protected override void OnActivated()
	{
		topologySignature = string.Empty;
		RefreshTopology();
	}

	protected override void OnPoll()
	{
		RefreshTopology();
		UpdateLive();
	}

	private protected virtual bool IncludePort(TLFanPortProfile port)
	{
		return port.PortType != PortType.None;
	}

	internal static TLFanGroupProfile[] GroupsOf(TLFanPortProfile port, out int[] order)
	{
		switch (port.PortType)
		{
		case PortType.LED:
			order = port.LEDGroupOrder;
			return port.LEDGroupProfiles;
		case PortType.LCD:
			order = port.LCDGroupOrder;
			return port.LCDGroupProfiles;
		default:
			order = Array.Empty<int>();
			return Array.Empty<TLFanGroupProfile>();
		}
	}

	internal TLFanPortProfile SelectedPortProfile
	{
		get
		{
			TLFanController controller = Controller;
			if (controller == null || SelectedPort == null)
			{
				return null;
			}
			return controller.Profile.PortProfiles[SelectedPort.Port];
		}
	}

	internal TLFanGroupProfile SelectedGroupProfile
	{
		get
		{
			TLFanPortProfile selectedPortProfile = SelectedPortProfile;
			if (selectedPortProfile == null || SelectedGroup == null)
			{
				return null;
			}
			int[] order;
			TLFanGroupProfile[] array = GroupsOf(selectedPortProfile, out order);
			return array.FirstOrDefault((TLFanGroupProfile g) => g.Index == SelectedGroup.Index);
		}
	}

	private string computeSignature(List<TLFanController> controllers)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(ControllerHub.Instance.DaemonReachable ? "R" : "-");
		foreach (TLFanController controller in controllers)
		{
			stringBuilder.Append('|').Append(controller.DevicePath);
			TLFanPortProfile[] portProfiles = controller.Profile.PortProfiles;
			foreach (TLFanPortProfile tLFanPortProfile in portProfiles)
			{
				stringBuilder.Append(';').Append((int)tLFanPortProfile.PortType).Append(':')
					.Append(tLFanPortProfile.ConnectedFanQuantity);
				int[] order;
				TLFanGroupProfile[] array = GroupsOf(tLFanPortProfile, out order);
				foreach (TLFanGroupProfile tLFanGroupProfile in array)
				{
					stringBuilder.Append(',').Append(tLFanGroupProfile.FanQuantity);
				}
			}
		}
		return stringBuilder.ToString();
	}

	public void RefreshTopology()
	{
		List<TLFanController> tLControllers = ControllerHub.Instance.TLControllers;
		string text = computeSignature(tLControllers);
		if (text == topologySignature)
		{
			return;
		}
		topologySignature = text;
		string b = SelectedController?.DevicePath;
		int? num = SelectedPort?.Port;
		int? num2 = SelectedGroup?.Index;
		suppressSelection = true;
		try
		{
			Controllers.Clear();
			int num3 = 1;
			foreach (TLFanController item in tLControllers)
			{
				Controllers.Add(new ControllerItem
				{
					DevicePath = item.DevicePath,
					Name = ((tLControllers.Count > 1) ? $"TL controller {num3++}" : "TL controller")
				});
			}
			HasController = Controllers.Count > 0;
			OnPropertyChanged("MultipleControllers");
			SelectedController = Controllers.FirstOrDefault((ControllerItem c) => c.DevicePath == b) ?? Controllers.FirstOrDefault();
			rebuildPorts(num);
			if (num2.HasValue && SelectedGroup == null)
			{
				SelectedGroup = Groups.FirstOrDefault();
			}
			else if (num2.HasValue)
			{
				SelectedGroup = Groups.FirstOrDefault((GroupItem g) => g.Index == num2.Value) ?? Groups.FirstOrDefault();
			}
		}
		finally
		{
			suppressSelection = false;
		}
		OnPropertyChanged("NoDeviceHint");
		OnTopologyChanged();
		OnSelectionChanged();
	}

	private void rebuildPorts(int? keepPort)
	{
		Ports.Clear();
		TLFanController controller = Controller;
		if (controller != null)
		{
			int[] portOrder = controller.Profile.PortOrder;
			foreach (int num in portOrder)
			{
				TLFanPortProfile tLFanPortProfile = controller.Profile.PortProfiles[num];
				if (IncludePort(tLFanPortProfile))
				{
					int[] order;
					int fanCount = GroupsOf(tLFanPortProfile, out order).Sum((TLFanGroupProfile g) => g.FanQuantity);
					PortItem portItem = new PortItem
					{
						Port = tLFanPortProfile.Port,
						Type = tLFanPortProfile.PortType,
						Name = $"Port {tLFanPortProfile.Port + 1}",
						FanCount = Math.Max(fanCount, tLFanPortProfile.ConnectedFanQuantity)
					};
					portItem.Summary = $"{portItem.TypeText} · {portItem.FanCount} fan(s)";
					Ports.Add(portItem);
				}
			}
		}
		HasPorts = Ports.Count > 0;
		SelectedPort = Ports.FirstOrDefault((PortItem p) => p.Port == keepPort) ?? Ports.FirstOrDefault();
		rebuildGroups();
	}

	private void rebuildGroups()
	{
		Groups.Clear();
		TLFanPortProfile selectedPortProfile = SelectedPortProfile;
		if (selectedPortProfile != null)
		{
			int[] order;
			TLFanGroupProfile[] array = GroupsOf(selectedPortProfile, out order);
			for (int i = 0; i < order.Length; i++)
			{
				TLFanGroupProfile tLFanGroupProfile = array[order[i]];
				if (tLFanGroupProfile.FanQuantity > 0)
				{
					Groups.Add(new GroupItem
					{
						Index = tLFanGroupProfile.Index,
						FanQuantity = tLFanGroupProfile.FanQuantity,
						Name = tLFanGroupProfile.DisplayName,
						Rpm = tLFanGroupProfile.FanSpeed
					});
				}
			}
		}
		SelectedGroup = Groups.FirstOrDefault();
	}

	partial void OnSelectedControllerChanged(ControllerItem value)
	{
		if (!suppressSelection)
		{
			suppressSelection = true;
			try
			{
				rebuildPorts(null);
			}
			finally
			{
				suppressSelection = false;
			}
			OnSelectionChanged();
		}
	}

	partial void OnSelectedPortChanged(PortItem value)
	{
		if (!suppressSelection)
		{
			suppressSelection = true;
			try
			{
				rebuildGroups();
			}
			finally
			{
				suppressSelection = false;
			}
			OnSelectionChanged();
		}
	}

	partial void OnSelectedGroupChanged(GroupItem value)
	{
		if (!suppressSelection)
		{
			OnSelectionChanged();
		}
	}

	protected void UpdateLive()
	{
		TLFanPortProfile selectedPortProfile = SelectedPortProfile;
		if (selectedPortProfile == null)
		{
			return;
		}
		int[] order;
		TLFanGroupProfile[] source = GroupsOf(selectedPortProfile, out order);
		foreach (GroupItem item in Groups)
		{
			TLFanGroupProfile tLFanGroupProfile = source.FirstOrDefault((TLFanGroupProfile g) => g.Index == item.Index);
			if (tLFanGroupProfile != null)
			{
				item.Rpm = tLFanGroupProfile.FanSpeed;
			}
		}
		OnLiveUpdated();
	}

	protected virtual void OnTopologyChanged()
	{
	}

	// Called after the selected controller/port/group changed (also after a topology rebuild).
	protected abstract void OnSelectionChanged();

	protected virtual void OnLiveUpdated()
	{
	}
}
