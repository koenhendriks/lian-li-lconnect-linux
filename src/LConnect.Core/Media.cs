using LConnectCore.Products.TLLCD;

namespace LConnectCore;

public class Media
{
	public byte[] Bytes { get; set; }

	public string Path { get; set; }

	public TLLCDMediaType Type { get; set; }

	public string Name { get; set; }

	public string MD5 { get; set; }
}
