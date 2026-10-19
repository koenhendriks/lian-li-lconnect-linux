using System;
using System.Threading;
using System.Threading.Tasks;

namespace LConnectCore;

public class NonOverlappingTimer : IDisposable
{
	public delegate void ErrorEventHandler(object sender, Exception exception);

	private Task task;

	private readonly object locker = new object();

	private CancellationTokenSource cancellationTokenSource;

	private int interval = 1000;

	public bool IsRunning => (!(task?.IsCompleted)) ?? false;

	public int Interval
	{
		get
		{
			return interval;
		}
		set
		{
			if (interval <= 0)
			{
				throw new ArgumentException("Interval");
			}
			interval = value;
		}
	}

	public bool ContinueOnCallbackError { get; set; }

	public event ErrorEventHandler TimerError;

	public event ErrorEventHandler CallbackError;

	public NonOverlappingTimer()
	{
		cancellationTokenSource = null;
		ContinueOnCallbackError = false;
	}

	public void Start(Action callback)
	{
		startTask(callback);
	}

	public void Start(Action callback, int interval)
	{
		Interval = interval;
		startTask(callback);
	}

	public void Start(Action callback, int interval, bool continueOnCallbackError)
	{
		Interval = interval;
		ContinueOnCallbackError = continueOnCallbackError;
		startTask(callback);
	}

	private void startTask(Action callback)
	{
		if (callback == null)
		{
			throw new ArgumentNullException("callback");
		}
		if (interval <= 0)
		{
			throw new ArgumentOutOfRangeException("Interval");
		}
		lock (locker)
		{
			if (task != null)
			{
				return;
			}
			cancellationTokenSource = new CancellationTokenSource();
			task = Task.Run(() =>
			{
				try
				{
					while (!cancellationTokenSource.IsCancellationRequested)
					{
						DateTime now = DateTime.Now;
						try
						{
							callback?.Invoke();
						}
						catch (Exception exception)
						{
							CallbackError?.Invoke(this, exception);
							if (!ContinueOnCallbackError)
							{
								break;
							}
						}
						TimeSpan timeSpan = DateTime.Now - now;
						double num = (double)interval - timeSpan.TotalMilliseconds;
						if (num > 0.0)
						{
							Thread.Sleep((int)Math.Min(num, 2147483647.0));
						}
					}
					Dispose();
				}
				catch (Exception exception2)
				{
					TimerError?.Invoke(this, exception2);
				}
				finally
				{
					try
					{
						Dispose();
					}
					catch
					{
					}
				}
			}, cancellationTokenSource.Token);
		}
	}

	public void Dispose()
	{
		try
		{
			cancellationTokenSource?.Cancel();
			cancellationTokenSource?.Dispose();
		}
		catch
		{
		}
	}

	public static NonOverlappingTimer StartNew(Action callback, int interval, bool continueOnCallbackError = false)
	{
		NonOverlappingTimer nonOverlappingTimer = new NonOverlappingTimer();
		nonOverlappingTimer.Interval = interval;
		nonOverlappingTimer.ContinueOnCallbackError = continueOnCallbackError;
		nonOverlappingTimer.startTask(callback);
		return nonOverlappingTimer;
	}
}
