namespace LConnectCore.Products.TLLCD;

public class TLLCDScreenSetting
{
	public int Index { get; set; }

	public TLLCDMediaType MediaType { get; set; }

	public string FileMD5 { get; set; }

	public string FilePath { get; set; }

	public int TotalBlock { get; set; }

	public int Block { get; set; }

	public bool Horizontal { get; set; }

	public TLLCDSensorSetting Sensor { get; set; }

	public ScreenRotation Rotate { get; set; }

	public int? Sync { get; set; }
}
