namespace LConnectCore.Products.TLLCD;

public struct HandshakeInfo
{
	public bool IsValid { get; set; }

	public byte Mode { get; set; }

	public int FrameIndex { get; set; }

	public static bool TryParse(byte[] data, out HandshakeInfo info)
	{
		info = default;
		if (data == null || data.Length < 3)
		{
			return false;
		}
		info = new HandshakeInfo
		{
			IsValid = true,
			Mode = data[0],
			FrameIndex = ((data[1] << 8) | data[2])
		};
		return true;
	}
}
