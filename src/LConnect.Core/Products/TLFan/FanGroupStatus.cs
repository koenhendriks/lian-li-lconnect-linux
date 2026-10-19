namespace LConnectCore.Products.TLFan;

public class FanGroupStatus
{
	public int[] PortOrder { get; set; } = new int[4];

	public PortType[] PortTypes { get; set; } = new PortType[4];

	public FanType[][] FanTypes { get; set; } = new FanType[4][];

	public FanSize[][] FanSizes { get; set; } = new FanSize[4][];

	public int[][] FanSpeeds { get; set; } = new int[4][];

	public int[][] GroupOrders { get; set; } = new int[4][];

	public int[][] FanQuantities { get; set; } = new int[4][];

	public int[] TotalQuantities { get; set; } = new int[4];

	public int[] ActualTotalQuantities { get; set; } = new int[4];

	public override bool Equals(object obj)
	{
		if (!(obj is FanGroupStatus fanGroupStatus))
		{
			return false;
		}
		for (int i = 0; i < 4; i++)
		{
			if (PortTypes[i] != fanGroupStatus.PortTypes[i] || TotalQuantities[i] != fanGroupStatus.TotalQuantities[i] || ActualTotalQuantities[i] != fanGroupStatus.ActualTotalQuantities[i])
			{
				return false;
			}
			if (FanSpeeds[i] != null && fanGroupStatus.FanSpeeds[i] != null && FanSpeeds[i].Length == fanGroupStatus.FanSpeeds[i].Length)
			{
				for (int j = 0; j < FanSpeeds[i].Length; j++)
				{
					if (FanSpeeds[i][j] != fanGroupStatus.FanSpeeds[i][j])
					{
						return false;
					}
				}
				if (FanSizes[i] != null && fanGroupStatus.FanSizes[i] != null && FanSizes[i].Length == fanGroupStatus.FanSizes[i].Length)
				{
					for (int k = 0; k < FanSizes[i].Length; k++)
					{
						if (FanSizes[i][k] != fanGroupStatus.FanSizes[i][k])
						{
							return false;
						}
					}
					if (FanTypes[i] != null && fanGroupStatus.FanTypes[i] != null && FanTypes[i].Length == fanGroupStatus.FanTypes[i].Length)
					{
						for (int l = 0; l < FanTypes[i].Length; l++)
						{
							if (FanTypes[i][l] != fanGroupStatus.FanTypes[i][l])
							{
								return false;
							}
						}
						if (FanQuantities[i] != null && fanGroupStatus.FanQuantities[i] != null && FanQuantities[i].Length == fanGroupStatus.FanQuantities[i].Length)
						{
							for (int m = 0; m < FanQuantities[i].Length; m++)
							{
								if (FanQuantities[i][m] != fanGroupStatus.FanQuantities[i][m])
								{
									return false;
								}
							}
							continue;
						}
						return false;
					}
					return false;
				}
				return false;
			}
			return false;
		}
		return true;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
