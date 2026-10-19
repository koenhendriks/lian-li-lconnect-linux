using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class TLFanPortProfile : INotifyPropertyChanged
{
	private int connectedFanQuantity;

	private PortType portType;

	private string ledPortName = string.Empty;

	private string lcdPortName = string.Empty;

	private readonly int[] ledGroupOrder = new int[4];

	private readonly int[] lcdGroupOrder = new int[1];

	[JsonIgnore]
	public int ConnectedFanQuantity
	{
		get
		{
			return connectedFanQuantity;
		}
		set
		{
			connectedFanQuantity = value;
			OnPropertyChanged("ConnectedFanQuantity");
		}
	}

	[JsonIgnore]
	public PortType PortType
	{
		get
		{
			return portType;
		}
		set
		{
			portType = value;
			OnPropertyChanged("PortType");
			OnPropertyChanged("DisplayName");
		}
	}

	[JsonIgnore]
	public string DisplayName => portType switch
	{
		PortType.LED => string.IsNullOrEmpty(LEDPortName) ? $"TL_{Port + 1:d02}" : LEDPortName, 
		PortType.LCD => string.IsNullOrEmpty(LCDPortName) ? $"TL_LCD_{Port + 1:d02}" : LCDPortName, 
		_ => string.Empty, 
	};

	public int Port { get; set; }

	public string LEDPortName
	{
		get
		{
			return ledPortName;
		}
		set
		{
			ledPortName = value;
			OnPropertyChanged("LEDPortName");
			if (PortType == PortType.LED)
			{
				OnPropertyChanged("DisplayName");
			}
		}
	}

	public string LCDPortName
	{
		get
		{
			return lcdPortName;
		}
		set
		{
			lcdPortName = value;
			OnPropertyChanged("LCDPortName");
			if (PortType == PortType.LCD)
			{
				OnPropertyChanged("DisplayName");
			}
		}
	}

	public bool LEDIsReverseDirection { get; set; }

	public bool LCDIsReverseDirection { get; set; }

	public TLFanGroupProfile[] LEDGroupProfiles { get; set; } = new TLFanGroupProfile[4];

	public TLFanGroupProfile[] LCDGroupProfiles { get; set; } = new TLFanGroupProfile[1];

	public int[] LEDGroupOrder
	{
		get
		{
			return ledGroupOrder;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException();
			}
			if (value.Length != 4)
			{
				throw new ArgumentException();
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (int num in value)
			{
				if (num < 0 || num >= 4)
				{
					throw new ArgumentException();
				}
				hashSet.Add(num);
			}
			if (4 != hashSet.Count)
			{
				throw new ArgumentException();
			}
			Array.Copy(value, ledGroupOrder, ledGroupOrder.Length);
		}
	}

	public int[] LCDGroupOrder
	{
		get
		{
			return lcdGroupOrder;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException();
			}
			if (value.Length != 1)
			{
				throw new ArgumentException();
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (int num in value)
			{
				if (num < 0 || num >= 1)
				{
					throw new ArgumentException();
				}
				hashSet.Add(num);
			}
			if (1 != hashSet.Count)
			{
				throw new ArgumentException();
			}
			Array.Copy(value, lcdGroupOrder, lcdGroupOrder.Length);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public TLFanPortProfile()
	{
	}

	public TLFanPortProfile(int port)
	{
		Port = port;
		for (int i = 0; i < LEDGroupProfiles.Length; i++)
		{
			LEDGroupProfiles[i] = new TLFanGroupProfile(this, i, 4);
			ledGroupOrder[i] = i;
		}
		for (int j = 0; j < LCDGroupProfiles.Length; j++)
		{
			LCDGroupProfiles[j] = new TLFanGroupProfile(this, j, 3);
			lcdGroupOrder[j] = j;
		}
	}
}
