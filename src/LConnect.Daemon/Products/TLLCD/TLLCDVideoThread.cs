using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using LConnectCore;
using LConnectCore.Products.TLLCD;
using LConnectService.Helpers;
using LConnectService.Products.TLFan;

namespace LConnectService.Products.TLLCD;

public class TLLCDVideoThread
{
	public bool Enabled { get; set; }

	public int Port { get; set; }

	public int Index { get; set; }

	public ushort Type { get; set; }

	public ITLLCDDevice Device { get; set; }

	public int Frame { get; set; }

	public string FilePath { get; set; }

	public int Block { get; set; }

	public bool Horizontal { get; set; }

	public Guid Guid { get; set; }

	public List<byte[]> Images { get; set; }

	internal SensorScreenRenderer ScreenWebBrowser { get; set; }

	public TLLCDSensorSetting SensorSetting { get; set; }

	private TLFanController FanController { get; set; }

	public ushort Locked { get; set; }

	public volatile bool TickMissed;

	public TLLCDVideoThread Sync { get; set; }

	private readonly System.Threading.SemaphoreSlim browserLock = new System.Threading.SemaphoreSlim(1, 1);

	private DateTime nextBrowserRetry = DateTime.MinValue;

	private TimeSpan browserRetryDelay = TimeSpan.FromSeconds(10);

	private static readonly bool logStats = Environment.GetEnvironmentVariable("LCONNECT_LCD_STATS") == "1";

	private readonly System.Diagnostics.Stopwatch statsWindow = System.Diagnostics.Stopwatch.StartNew();

	private int statsFrames;

	private long statsBytes;

	private TimeSpan statsWriteTotal;

	private TimeSpan statsWriteMax;

	private TimeSpan statsFirstPacketTotal;

	public TLLCDVideoThread(int port, int index)
	{
		Index = index;
		Port = port;
		Images = new List<byte[]>();
	}

	public void Reset()
	{
		Enabled = false;
		Guid = Guid.NewGuid();
		FilePath = string.Empty;
		Frame = 0;
		Sync = null;
		Device = null;
		Locked = 0;
		TickMissed = false;
		Type = 0;
		SensorSetting = null;
		Images.Clear();
		disposeWebBrowser();
		GC.Collect();
	}

	public void SetVideo(string filePath, int block, bool horizontal)
	{
		Type = 0;
		FilePath = filePath;
		Frame = 0;
		Block = block;
		Horizontal = horizontal;
		Guid threadGuid = Guid;
		Task.Run(() =>
		{
			MediaConverter.ConvertVideoToJpegs(FilePath, Block, Horizontal, (byte[] image) =>
			{
				if (Guid == threadGuid)
				{
					Images.Add(image);
					return true;
				}
				return false;
			});
		});
		Enabled = true;
	}

	public void SetSyncVideo(TLLCDVideoThread syncThread, string filePath, int block, bool horizontal)
	{
		FilePath = filePath;
		Sync = syncThread;
		Block = block;
		if (syncThread.FilePath != FilePath || syncThread.Block != block)
		{
			SetVideo(filePath, block, horizontal);
		}
		Enabled = true;
	}

	// hid_write on hidraw returns once the interrupt transfer completes, so a firmware that is still busy with the
	// previous frame (NAKing the endpoint) shows up as longer writes and a lower achieved rate.
	public void RecordFrame(int bytes, TimeSpan writeTime, TimeSpan firstPacketTime)
	{
		if (!logStats)
		{
			return;
		}
		statsFrames++;
		statsBytes += bytes;
		statsWriteTotal += writeTime;
		statsFirstPacketTotal += firstPacketTime;
		if (writeTime > statsWriteMax)
		{
			statsWriteMax = writeTime;
		}
		double seconds = statsWindow.Elapsed.TotalSeconds;
		if (seconds < 5.0)
		{
			return;
		}
		EventLogHelper.Info($"LCD port {Port} index {Index}: {statsFrames / seconds:0.0} fps (target {Constants.StreamFramesPerSecond}), {statsBytes / statsFrames / 1024.0:0.0} KB/frame, {statsBytes / seconds / 1048576.0:0.00} MB/s, write avg {statsWriteTotal.TotalMilliseconds / statsFrames:0.0} ms (first packet {statsFirstPacketTotal.TotalMilliseconds / statsFrames:0.0} ms) max {statsWriteMax.TotalMilliseconds:0.0} ms");
		statsFrames = 0;
		statsBytes = 0;
		statsWriteTotal = TimeSpan.Zero;
		statsFirstPacketTotal = TimeSpan.Zero;
		statsWriteMax = TimeSpan.Zero;
		statsWindow.Restart();
	}

	public int GetFanRPM()
	{
		if (FanController == null)
		{
			return 0;
		}
		return FanController.GetFanSpeed(Port, Index);
	}

	// Returns false when Reset() ran while the browser was starting: a newer setting owns the thread by then.
	internal async Task<bool> SetSensor(TLLCDSensorSetting setting, TLFanController fanController)
	{
		Guid guid = Guid;
		Type = 1;
		SensorSetting = setting;
		FanController = fanController;
		await initWebBrowser();
		await setupScreenConfigJson();
		await SensorValueUpdater();
		if (guid != Guid)
		{
			return false;
		}
		Images.Clear();
		Images.Add(null);
		Enabled = true;
		return true;
	}

	// A renderer that failed to start, or whose Chromium died later, is dropped instead of being kept forever (the
	// LCD would never get another frame); SensorValueUpdater retries with a growing delay.
	private async Task initWebBrowser()
	{
		await browserLock.WaitAsync();
		try
		{
			SensorScreenRenderer renderer = ScreenWebBrowser;
			if (renderer != null && !renderer.IsBroken)
			{
				return;
			}
			if (renderer != null)
			{
				EventLogHelper.Warn("LCD sensor screen browser stopped; restarting it.");
				ScreenWebBrowser = null;
				await renderer.DisposeAsync();
			}
			renderer = new SensorScreenRenderer();
			ScreenWebBrowser = renderer;
			try
			{
				await renderer.InitAsync();
				browserRetryDelay = TimeSpan.FromSeconds(10);
			}
			catch (Exception ex)
			{
				EventLogHelper.Error($"Init web browser failed. {ex.Message} Retrying in {browserRetryDelay.TotalSeconds:0} s.");
				if (ScreenWebBrowser == renderer)
				{
					ScreenWebBrowser = null;
				}
				await renderer.DisposeAsync();
				nextBrowserRetry = DateTime.UtcNow + browserRetryDelay;
				browserRetryDelay = TimeSpan.FromSeconds(Math.Min(browserRetryDelay.TotalSeconds * 2.0, 600.0));
			}
		}
		finally
		{
			browserLock.Release();
		}
	}

	private async Task setupScreenConfigJson()
	{
		try
		{
			if (SensorSetting != null)
			{
				string effect = SensorSetting.Style switch
				{
					0 => "RingOne", 
					1 => "RingTwo", 
					2 => "RingThree", 
					3 => "RingFour", 
					4 => "RingFive", 
					_ => "RingOne", 
				};
				_ = string.Empty;
				string text = ((SensorSetting.Style != 3 && SensorSetting.Style != 4) ? JsonSerializer.Serialize(new
				{
					Effect = effect,
					BackgroundColor = SensorSetting.BackgroundColor1,
					TextColors = new int[3][] { SensorSetting.TextColor1, SensorSetting.TextColor2, SensorSetting.TextColor3 },
					Sensor = SensorSetting.Type.ToString()
				}) : JsonSerializer.Serialize(new
				{
					Effect = effect,
					BackgroundColor1 = SensorSetting.BackgroundColor1,
					BackgroundColor2 = SensorSetting.BackgroundColor2,
					TextColors = new int[3][] { SensorSetting.TextColor1, SensorSetting.TextColor2, SensorSetting.TextColor3 },
					Sensor = SensorSetting.Type.ToString()
				}));
				await ScreenWebBrowser.CallAsync("setConfigs", text);
			}
		}
		catch
		{
		}
	}

	public async Task SensorValueUpdater()
	{
		if (SensorSetting == null)
		{
			return;
		}
		if (Enabled && Type == 1 && (ScreenWebBrowser == null || ScreenWebBrowser.IsBroken) && DateTime.UtcNow >= nextBrowserRetry)
		{
			await initWebBrowser();
			await setupScreenConfigJson();
		}
		object obj = SensorSetting.Type switch
		{
			TLLCDSensorType.CPUTemp => (object)new
			{
				Type = "CPU",
				Value = SystemInformation.Resource.CPUTemperature,
				Label = "TEMP",
				Unit = "°C",
				Progress = SystemInformation.Resource.CPUTemperature
			}, 
			TLLCDSensorType.GPUTemp => new
			{
				Type = "GPU",
				Value = SystemInformation.Resource.GPUTemperature,
				Label = "TEMP",
				Unit = "°C",
				Progress = SystemInformation.Resource.GPUTemperature
			}, 
			TLLCDSensorType.CPULoad => new
			{
				Type = "CPU",
				Value = SystemInformation.Resource.CPULoad,
				Label = "LOAD",
				Unit = "%",
				Progress = SystemInformation.Resource.CPULoad
			}, 
			TLLCDSensorType.GPULoad => new
			{
				Type = "GPU",
				Value = SystemInformation.Resource.GPULoad,
				Label = "LOAD",
				Unit = "%",
				Progress = SystemInformation.Resource.GPULoad
			}, 
			TLLCDSensorType.FanRPM => new
			{
				Type = "FAN",
				Value = GetFanRPM(),
				Label = "RPM",
				Unit = "RPM",
				Progress = GetFanRPM() * 100 / 1600
			}, 
			_ => null, 
		};
		if (obj == null)
		{
			return;
		}
		string text = JsonSerializer.Serialize(obj);
		if (ScreenWebBrowser == null)
		{
			return;
		}
		try
		{
			await ScreenWebBrowser.CallAsync("setSensorValue", text);
		}
		catch (Exception)
		{
		}
	}

	public byte[] WriteBrowserScreenshot()
	{
		try
		{
			if (ScreenWebBrowser == null || !ScreenWebBrowser.IsReady)
			{
				return null;
			}
			byte[] latestFrame = ScreenWebBrowser.LatestFrame;
			if (latestFrame == null)
			{
				return null;
			}
			Images[0] = latestFrame;
			return Images[0];
		}
		catch
		{
		}
		return null;
	}

	private void disposeWebBrowser()
	{
		try
		{
			SensorScreenRenderer screenWebBrowser = ScreenWebBrowser;
			ScreenWebBrowser = null;
			if (screenWebBrowser != null)
			{
				_ = screenWebBrowser.DisposeAsync().AsTask();
			}
		}
		catch
		{
		}
	}
}
