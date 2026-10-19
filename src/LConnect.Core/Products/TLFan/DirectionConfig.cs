using System.Collections.Generic;

namespace LConnectCore.Products.TLFan;

public class DirectionConfig
{
	public Dictionary<PortType, bool[]> Port { get; set; } = new Dictionary<PortType, bool[]>();

	public Dictionary<PortType, bool[][]> GroupSwapLeftRight { get; set; } = new Dictionary<PortType, bool[][]>();

	public Dictionary<PortType, bool[][]> GroupSwapTopBottom { get; set; } = new Dictionary<PortType, bool[][]>();

	public Dictionary<PortType, bool[][][]> FanSwapLeftRight { get; set; } = new Dictionary<PortType, bool[][][]>();

	public Dictionary<PortType, bool[][][]> FanSwapTopBottom { get; set; } = new Dictionary<PortType, bool[][][]>();

	public DirectionConfig()
	{
		Dictionary<PortType, (int, int)> dictionary = new Dictionary<PortType, (int, int)>
		{
			[PortType.LED] = (4, 4),
			[PortType.LCD] = (1, 3)
		};
		foreach (KeyValuePair<PortType, (int, int)> item in dictionary)
		{
			var (num, num2) = dictionary[item.Key];
			Port.Add(item.Key, new bool[4]);
			GroupSwapLeftRight.Add(item.Key, new bool[4][]);
			GroupSwapTopBottom.Add(item.Key, new bool[4][]);
			FanSwapLeftRight.Add(item.Key, new bool[4][][]);
			FanSwapTopBottom.Add(item.Key, new bool[4][][]);
			for (int i = 0; i < 4; i++)
			{
				GroupSwapLeftRight[item.Key][i] = new bool[num];
				GroupSwapTopBottom[item.Key][i] = new bool[num];
				FanSwapLeftRight[item.Key][i] = new bool[num][];
				FanSwapTopBottom[item.Key][i] = new bool[num][];
				for (int j = 0; j < num; j++)
				{
					FanSwapLeftRight[item.Key][i][j] = new bool[num2];
					FanSwapTopBottom[item.Key][i][j] = new bool[num2];
				}
			}
		}
	}
}
