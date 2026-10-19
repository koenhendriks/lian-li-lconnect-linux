namespace LConnectCore.Devices;

public class Packet
{
	protected byte[] bytes;

	public Packet(int length)
	{
		bytes = new byte[length];
	}

	public byte[] GetBytes()
	{
		return bytes;
	}
}
