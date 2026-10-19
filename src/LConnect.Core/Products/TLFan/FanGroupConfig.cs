using System.Collections.Generic;

namespace LConnectCore.Products.TLFan;

public class FanGroupConfig
{
	public class GroupConfig
	{
		public int[] GroupOrder { get; set; }

		public int[] GroupQuantities { get; set; }
	}

	public int[] PortOrder { get; set; } = new int[4];

	public Dictionary<PortType, GroupConfig[]> GroupConfigs { get; set; } = new Dictionary<PortType, GroupConfig[]>
	{
		[PortType.LED] = new GroupConfig[4],
		[PortType.LCD] = new GroupConfig[4]
	};

	public FanGroupConfig()
	{
		for (int i = 0; i < PortOrder.Length; i++)
		{
			PortOrder[i] = i;
		}
		foreach (GroupConfig[] value in GroupConfigs.Values)
		{
			for (int j = 0; j < value.Length; j++)
			{
				value[j] = new GroupConfig();
			}
		}
	}
}
