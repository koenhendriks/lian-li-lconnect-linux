using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using LConnectCore;

namespace LConnectUi.Services;

// Polls the daemon's sensor readings (action=SystemResource) every two seconds for the live temperature
// markers in the fan curve editors and the LCD sensor previews.
public sealed class SensorService
{
	private static readonly Lazy<SensorService> lazy = new Lazy<SensorService>(() => new SensorService());

	private CancellationTokenSource cts;

	public static SensorService Instance => lazy.Value;

	public SystemResource? Latest { get; private set; }

	public event EventHandler Updated;

	public void Start()
	{
		if (cts != null)
		{
			return;
		}
		cts = new CancellationTokenSource();
		CancellationToken token = cts.Token;
		Task.Run(async delegate
		{
			while (!token.IsCancellationRequested)
			{
				await RefreshAsync();
				try
				{
					await Task.Delay(2000, token);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		});
	}

	public void Stop()
	{
		cts?.Cancel();
		cts = null;
	}

	public async Task RefreshAsync()
	{
		SystemResource? resource = await Task.Run(() => DaemonClient.GetSystemResource());
		Latest = resource;
		Dispatcher.UIThread.Post(delegate
		{
			Updated?.Invoke(this, EventArgs.Empty);
		});
	}

	public static double? TemperatureFor(LConnectCore.Products.RPMReferenceSource source)
	{
		SystemResource? latest = Instance.Latest;
		if (!latest.HasValue)
		{
			return null;
		}
		return (source == LConnectCore.Products.RPMReferenceSource.GPU) ? latest.Value.GPUTemperature : latest.Value.CPUTemperature;
	}
}
