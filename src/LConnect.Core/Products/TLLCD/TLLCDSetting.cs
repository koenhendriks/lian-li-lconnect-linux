using System.Collections.Generic;

namespace LConnectCore.Products.TLLCD;

public class TLLCDSetting
{
	public int Port { get; set; }

	public TLLCDConfig Config { get; set; }

	public List<TLLCDScreenSetting> Screens { get; set; } = new List<TLLCDScreenSetting>();
}
