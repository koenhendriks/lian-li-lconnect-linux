using System;
using System.Collections.Generic;
using System.Threading;
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

	internal SensorScreenRenderer SensorRenderer { get; set; }

	public TLLCDSensorSetting SensorSetting { get; set; }

	private TLFanController FanController { get; set; }

	public ushort Locked { get; set; }

	public volatile bool TickMissed;

	public TLLCDVideoThread Sync { get; set; }

	// Fraction of the remaining distance covered per frame: at 25 fps a change is ~93% done after 1 s and settled
	// before the next reading 2 s later.
	private const double ProgressEasing = 0.1;

	private double targetProgress;

	private double shownProgress;

	private int animatingProgress;

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
		SensorRenderer = null;
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

	// Returns false when Reset() ran meanwhile from the stream loop: a newer setting owns the thread by then.
	internal bool SetSensor(TLLCDSensorSetting setting, TLFanController fanController)
	{
		Guid guid = Guid;
		Type = 1;
		SensorSetting = setting;
		FanController = fanController;
		SensorScreenRenderer renderer = SensorRenderer ?? new SensorScreenRenderer();
		SensorRenderer = renderer;
		renderer.Configure(setting);
		SensorValueUpdater();
		if (guid != Guid)
		{
			return false;
		}
		Images.Clear();
		Images.Add(null);
		Enabled = true;
		return true;
	}

	public void SensorValueUpdater()
	{
		TLLCDSensorSetting setting = SensorSetting;
		if (setting == null)
		{
			return;
		}
		(string Type, int Value, string Label, string Unit, int Progress)? sensor = setting.Type switch
		{
			TLLCDSensorType.CPUTemp => ("CPU", SystemInformation.Resource.CPUTemperature, "TEMP", "°C", SystemInformation.Resource.CPUTemperature),
			TLLCDSensorType.GPUTemp => ("GPU", SystemInformation.Resource.GPUTemperature, "TEMP", "°C", SystemInformation.Resource.GPUTemperature),
			TLLCDSensorType.CPULoad => ("CPU", SystemInformation.Resource.CPULoad, "LOAD", "%", SystemInformation.Resource.CPULoad),
			TLLCDSensorType.GPULoad => ("GPU", SystemInformation.Resource.GPULoad, "LOAD", "%", SystemInformation.Resource.GPULoad),
			TLLCDSensorType.FanRPM => ("FAN", GetFanRPM(), "RPM", "RPM", GetFanRPM() * 100 / 1600),
			// Temperature in the middle, while the ring (Progress) and the Label below it show the load.
			TLLCDSensorType.CPUTempLoad => ("CPU", SystemInformation.Resource.CPUTemperature, $"Load: {SystemInformation.Resource.CPULoad}%", "°C", SystemInformation.Resource.CPULoad),
			_ => null,
		};
		if (!sensor.HasValue)
		{
			return;
		}
		Volatile.Write(ref targetProgress, sensor.Value.Progress);
		SensorScreenRenderer renderer = SensorRenderer;
		if (renderer == null)
		{
			return;
		}
		// Progress is the eased value, not the new reading, so a new renderer starts where the ring was.
		renderer.SetReading(new SensorReading(sensor.Value.Type, sensor.Value.Value, sensor.Value.Label, sensor.Value.Unit, Volatile.Read(ref shownProgress)));
		startProgressAnimation();
	}

	// Readings change every 2 s; easing Progress at the stream rate turns each change into a sweep instead of a jump.
	private void startProgressAnimation()
	{
		if (Interlocked.CompareExchange(ref animatingProgress, 1, 0) == 0)
		{
			_ = Task.Run(animateProgress);
		}
	}

	private async Task animateProgress()
	{
		while (true)
		{
			while (true)
			{
				double target = Volatile.Read(ref targetProgress);
				double shown = Volatile.Read(ref shownProgress);
				shown = (Math.Abs(target - shown) < 0.1) ? target : (shown + (target - shown) * ProgressEasing);
				Volatile.Write(ref shownProgress, shown);
				SensorRenderer?.SetProgress(shown);
				if (shown == target)
				{
					break;
				}
				await Task.Delay(1000 / Constants.StreamFramesPerSecond);
			}
			Volatile.Write(ref animatingProgress, 0);
			// A reading that arrived after the loop settled but before the flag cleared would otherwise be dropped.
			if (Volatile.Read(ref targetProgress) == Volatile.Read(ref shownProgress) || Interlocked.CompareExchange(ref animatingProgress, 1, 0) != 0)
			{
				return;
			}
		}
	}

	public byte[] WriteSensorFrame()
	{
		try
		{
			byte[] latestFrame = SensorRenderer?.LatestFrame;
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
}
