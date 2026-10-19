namespace LConnectCore.Devices;

public class DeviceSetting<T>
{
	public string DevicePath { get; set; }

	public int VendorID { get; set; }

	public int ProductID { get; set; }

	public string Type { get; set; }

	public T Data { get; set; }
}
