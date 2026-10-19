using System.Collections.Generic;

namespace LConnectCore.Products.TLFan;

public class LightingConfigCollection
{
	public class GroupElement
	{
		public bool IsGrouping { get; set; } = true;

		public bool IsSyncSideMode { get; set; }

		public bool IsSyncBottomSide { get; set; }

		public bool IsSingleSideMode { get; set; }

		public LightingConfig[] Configs { get; set; }
	}

	public bool IsMerged { get; set; }

	public GroupElement MergedLightingConfigs { get; set; }

	public Dictionary<PortType, GroupElement[]>[] LightingConfigs { get; set; }
}
