namespace LConnectCore;

public class LockData<T>
{
	private readonly object locker = new object();

	private T value;

	public T Value
	{
		get
		{
			lock (locker)
			{
				return value;
			}
		}
		set
		{
			lock (locker)
			{
				this.value = value;
			}
		}
	}

	public LockData()
		: this(default(T))
	{
	}

	public LockData(T value)
	{
		this.value = value;
	}
}
