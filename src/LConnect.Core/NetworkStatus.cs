namespace LConnectCore;

public class NetworkStatus
{
	public ulong LastReceived { get; set; }

	public ulong NowReceived { get; set; }

	public ulong LastSent { get; set; }

	public ulong NowSent { get; set; }

	public NetworkStatus()
		: this(0uL, 0uL)
	{
	}

	public NetworkStatus(ulong received, ulong sent)
	{
		LastReceived = received;
		NowReceived = received;
		LastSent = sent;
		NowSent = sent;
	}

	public void Update(ulong received, ulong sent)
	{
		LastReceived = NowReceived;
		NowReceived = received;
		LastSent = NowSent;
		NowSent = sent;
	}

	public ulong GetDeltaReceived()
	{
		return NowReceived - LastReceived;
	}

	public ulong GetDeltaSent()
	{
		return NowSent - LastSent;
	}
}
