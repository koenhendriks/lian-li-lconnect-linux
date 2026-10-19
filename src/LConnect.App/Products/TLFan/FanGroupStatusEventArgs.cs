using System;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class FanGroupStatusEventArgs : EventArgs
{
	public class Status
	{
		public bool IsChanged
		{
			get
			{
				if (PortTypeBefore == PortTypeAfter)
				{
					return ConnectedFanQuantityBefore != ConnectedFanQuantityAfter;
				}
				return true;
			}
		}

		public PortType PortTypeBefore { get; set; }

		public int ConnectedFanQuantityBefore { get; set; }

		public PortType PortTypeAfter { get; set; }

		public int ConnectedFanQuantityAfter { get; set; }
	}

	public Status[] PortStatus { get; private set; }

	public FanGroupStatusEventArgs()
	{
		PortStatus = new Status[4];
		for (int i = 0; i < PortStatus.Length; i++)
		{
			PortStatus[i] = new Status();
		}
	}
}
