using System;
using System.Timers;

namespace LConnectCore;

public class DelayOutput<T>
{
	private readonly Timer timer = new Timer
	{
		AutoReset = false
	};

	private T value;

	public double DelayTime
	{
		get
		{
			return timer.Interval;
		}
		set
		{
			timer.Interval = value;
		}
	}

	public T Value
	{
		get
		{
			return value;
		}
		set
		{
			this.value = value;
			timer.Stop();
			timer.Start();
		}
	}

	public Action<T> Callback { get; set; }

	public DelayOutput(double delayTime)
	{
		DelayTime = delayTime;
		timer.Elapsed += (object _, ElapsedEventArgs _) =>
		{
			Callback?.Invoke(value);
		};
	}
}
