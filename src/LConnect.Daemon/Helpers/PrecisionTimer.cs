using System;
using System.Diagnostics;
using System.Threading;

namespace PrecisionTiming;

// Drop-in replacement for PrecisionTimer.NET (a Windows multimedia-timer wrapper). Runs the callback on a
// dedicated thread with Stopwatch deadlines: sleep until ~1 ms before the deadline, then spin.
public sealed class PrecisionTimer : IDisposable
{
	private Thread thread;

	private volatile bool running;

	public bool IsRunning => running;

	public void SetInterval(Action action, double periodMs)
	{
		Stop();
		running = true;
		thread = new Thread((ThreadStart)delegate
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			double num = periodMs;
			while (running)
			{
				double num2 = num - stopwatch.Elapsed.TotalMilliseconds;
				if (num2 > 1.0)
				{
					Thread.Sleep((int)(num2 - 1.0));
				}
				while (running && stopwatch.Elapsed.TotalMilliseconds < num)
				{
					Thread.SpinWait(50);
				}
				if (!running)
				{
					break;
				}
				try
				{
					action();
				}
				catch
				{
				}
				num += periodMs;
				// Don't try to catch up after a long stall (e.g. system suspend).
				if (stopwatch.Elapsed.TotalMilliseconds - num > periodMs * 5.0)
				{
					num = stopwatch.Elapsed.TotalMilliseconds + periodMs;
				}
			}
		})
		{
			IsBackground = true,
			Name = "lcd-frame-timer",
			Priority = ThreadPriority.AboveNormal
		};
		thread.Start();
	}

	public void Stop()
	{
		running = false;
		Thread obj = thread;
		thread = null;
		if (obj != null && obj != Thread.CurrentThread)
		{
			obj.Join(1000);
		}
	}

	public void Dispose()
	{
		Stop();
	}
}
