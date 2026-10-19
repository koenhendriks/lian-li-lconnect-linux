using System.Collections.Generic;

namespace LConnectCore.Products.TLFan;

public class FanGroupRPMConfig
{
	public Dictionary<PortType, Dictionary<FanSize, RPMConfig>[]>[] Configs { get; set; }
}
