namespace LConnectCore.Products.TLLCD;

public class ProductInfo
{
	public string ControllerDevicePath { get; set; }

	public int ControllerIndex { get; set; }

	public string DevicePath { get; set; }

	public FirmwareVersion Version { get; set; }

	public int PortNo { get; set; }

	public int LCDIndex { get; set; }
}
