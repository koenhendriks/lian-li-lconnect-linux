using System.Threading;

namespace LConnectCore;

public class AtomicBoolean
{
	private const long BooleanTrue = 1L;

	private const long BooleanFalse = 0L;

	private long innerValue;

	public bool Value
	{
		get
		{
			return Interlocked.Read(ref innerValue) == 1;
		}
		set
		{
			Interlocked.Exchange(ref innerValue, boolToLong(value));
		}
	}

	public AtomicBoolean(bool initialValue = false)
	{
		innerValue = boolToLong(initialValue);
	}

	private long boolToLong(bool value)
	{
		return value ? 1 : 0;
	}
}
