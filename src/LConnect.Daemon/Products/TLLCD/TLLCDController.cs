using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products.TLLCD;
using LConnectService.Helpers;
using LConnectService.Products.TLFan;
using LConnectService.Properties;

namespace LConnectService.Products.TLLCD;

internal class TLLCDController : IController
{
	public const int MaxPortNumberOfController = 4;

	public string DevicePath;

	private readonly object locker = new object();

	private readonly SemaphoreSlim sensorLock = new SemaphoreSlim(1, 1);

	public readonly List<ITLLCDDevice>[] devices = new List<ITLLCDDevice>[4];

	public readonly string[] portLocationString = new string[4];

	public readonly string controllerLocationString;

	private string controllerLocationInfo;

	private CancellationTokenSource checkFirmwareVersionTaskToken;

	private bool autoSendToDevice = true;

	private const int ScreenSensorUpdateTime = 2000;

	private NonOverlappingTimer screenUpdateSensorValueTimer;

	private const string FolderName = "TL-LCD-Controller-240110";

	private const string FileExtension = "LCD";

	private TLFanController FanController { get; set; }

	private List<TLLCDSetting> Settings { get; set; } = new List<TLLCDSetting>();

	private List<TLLCDVideoThread> VideoThreads { get; set; } = new List<TLLCDVideoThread>();

	public bool ChekcIsGroup(string locationString)
	{
		int chainNumber;
		return TLLCDManager.CheckIsParentLocation(controllerLocationString, locationString, out chainNumber, 2);
	}

	public ITLLCDDevice GetFirstHidDevice()
	{
		for (int i = 0; i < devices.Length; i++)
		{
			if (devices[i].Count > 0)
			{
				return devices[i][0];
			}
		}
		return null;
	}

	public TLLCDController(string controllerLocationPath, string controllerLocationString)
	{
		DevicePath = controllerLocationPath;
		controllerLocationInfo = controllerLocationPath;
		this.controllerLocationString = controllerLocationString;
		startSensorValueUpdater();
		for (int i = 0; i < devices.Length; i++)
		{
			devices[i] = new List<ITLLCDDevice>();
		}
	}

	public bool ContainTLLCDDevice(string devicePath)
	{
		for (int i = 0; i < devices.Length; i++)
		{
			for (int j = 0; j < devices[i].Count; j++)
			{
				if (devices[i][j].HidDevice.DevicePath == devicePath)
				{
					return true;
				}
			}
		}
		return false;
	}

	public void RemoveTLLCDDevice(string devicePath)
	{
		for (int i = 0; i < devices.Length; i++)
		{
			for (int j = 0; j < devices[i].Count; j++)
			{
				if (devices[i][j].HidDevice.DevicePath == devicePath && devices[i][j].HidDevice.IsOpen)
				{
					devices[i][j].HidDevice.Close();
					devices[i].RemoveAt(j);
					return;
				}
			}
		}
	}

	public void Init()
	{
	}

	public void CloseAllDevices()
	{
		ClearThread();
		for (int i = 0; i < devices.Length; i++)
		{
			for (int j = 0; j < devices[i].Count; j++)
			{
				if (devices[i][j].HidDevice.IsOpen)
				{
					devices[i][j].HidDevice.Close();
				}
			}
		}
	}

	public void Dispose()
	{
	}

	public void Apply(DeviceSetting<object> setting)
	{
	}

	public void ApplyAll(IEnumerable<DeviceSetting<object>> settings)
	{
		if (settings == null)
		{
			return;
		}
		autoSendToDevice = false;
		foreach (DeviceSetting<object> setting in settings)
		{
			Apply(setting);
		}
		autoSendToDevice = true;
	}

	public object HandleRequestData(RequestData requestData)
	{
		if (!requestData.Query.TryGetValue("control", out var value) || string.IsNullOrEmpty(value))
		{
			return null;
		}
		if (!requestData.Query.TryGetValue("port", out var value2) || string.IsNullOrEmpty(value2))
		{
			return null;
		}
		int port = int.Parse(value2);
		switch (value)
		{
		case "devices":
			return GetPortDevicesCount(port);
		case "frame":
			return LoadFrame(port);
		case "rpm":
			return LoadFanRPM(port);
		case "config":
			return GetConfig(port);
		case "setting":
			return GetSetting(port);
		case "update_config":
			UpdateConfig(JsonSerializer.Deserialize<TLLCDConfig>(requestData.Body), port);
			SaveSettings();
			break;
		case "update":
			UpdateSetting(JsonSerializer.Deserialize<TLLCDScreenSetting>(requestData.Body), port);
			ExecuteSetting();
			SaveSettings();
			break;
		case "rotate":
			SetRotate(port, JsonSerializer.Deserialize<List<TLLCDScreenRotate>>(requestData.Body));
			ExecuteSetting();
			SaveSettings();
			break;
		}
		return null;
	}

	private void lockedActionExecution(Action action)
	{
		lock (locker)
		{
			try
			{
				action();
			}
			catch
			{
			}
		}
	}

	private static string getMD5String(string str)
	{
		try
		{
			using MD5 mD = MD5.Create();
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			return BitConverter.ToString(mD.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
		}
		catch
		{
		}
		return string.Empty;
	}

	public int GetTotalDeviceCount()
	{
		int num = 0;
		for (int i = 0; i < 4; i++)
		{
			num += devices[i].Count;
		}
		return num;
	}

	public int GetUsedPortCount()
	{
		int num = 0;
		for (int i = 0; i < 4; i++)
		{
			if (devices[i].Count > 0)
			{
				num++;
			}
		}
		return num;
	}

	public int GetPortDevicesCount(int port)
	{
		return devices[port].Count;
	}

	public bool IsDeviceExist(int port, int index)
	{
		List<ITLLCDDevice> list = GetDevices(port);
		if (list == null)
		{
			return false;
		}
		return list.Count > index;
	}

	public List<ITLLCDDevice> GetDevices(int port)
	{
		if (port >= 0 && port < 4)
		{
			return devices[port];
		}
		return null;
	}

	private bool CheckDevice(int port, int index)
	{
		if (GetDevices(port) == null)
		{
			return false;
		}
		return devices.Length > index;
	}

	private void setScreenRotation(int port, int index, ScreenRotation rotation)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					LCDControlSetting setting = new LCDControlSetting
					{
						Mode = LCDControlMode.LCDSetting,
						Rotation = rotation
					};
					d.LCDControl(setting);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				LCDControlSetting setting = new LCDControlSetting
				{
					Mode = LCDControlMode.LCDSetting,
					Rotation = rotation
				};
				GetDevices(port)[index].LCDControl(setting);
			});
		}
	}

	private void setSwitchJPG(int port, int index)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					LCDControlSetting setting = new LCDControlSetting
					{
						Mode = LCDControlMode.ShowJPG,
						Rotation = ScreenRotation.Rotate0
					};
					d.LCDControl(setting);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				LCDControlSetting setting = new LCDControlSetting
				{
					Mode = LCDControlMode.ShowJPG,
					Rotation = ScreenRotation.Rotate0
				};
				GetDevices(port)[index].LCDControl(setting);
			});
		}
	}

	private void setSwitchAVI(int port, int index)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					LCDControlSetting setting = new LCDControlSetting
					{
						Mode = LCDControlMode.ShowAVI,
						Rotation = ScreenRotation.Rotate0
					};
					d.LCDControl(setting);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				LCDControlSetting setting = new LCDControlSetting
				{
					Mode = LCDControlMode.ShowAVI,
					Rotation = ScreenRotation.Rotate0
				};
				GetDevices(port)[index].LCDControl(setting);
			});
		}
	}

	private void setJPG(int port, int index, byte[] bytes)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					d.SendJPG(bytes);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				GetDevices(port)[index].SendJPG(bytes);
			});
		}
	}

	private void setBootJPG(int port, int index, byte[] bytes)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					d.SendBootJPG(bytes);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				GetDevices(port)[index].SendBootJPG(bytes);
			});
		}
	}

	private void setAVI(int port, int index, byte[] bytes)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					d.SendAVI(bytes);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				GetDevices(port)[index].SendAVI(bytes);
			});
		}
	}

	private void setSyncJPG(int port, int index, byte[] bytes)
	{
		if (index == -1)
		{
			foreach (ITLLCDDevice d in GetDevices(port))
			{
				lockedActionExecution(() =>
				{
					d.SendSyncJPG(bytes);
				});
			}
			return;
		}
		if (index < GetDevices(port).Count)
		{
			lockedActionExecution(() =>
			{
				GetDevices(port)[index].SendSyncJPG(bytes);
			});
		}
	}

	public List<ProductInfo> GetProductInfo(int index)
	{
		List<ProductInfo> list = new List<ProductInfo>();
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < GetPortDevicesCount(i); j++)
			{
				ProductInfo productInfo = new ProductInfo();
				productInfo.ControllerIndex = index;
				productInfo.ControllerDevicePath = DevicePath;
				productInfo.DevicePath = devices[i][j].HidDevice.DevicePath;
				productInfo.Version = devices[i][j].FirmwareVersionV();
				productInfo.PortNo = ((TLLCDDevice)devices[i][j]).PortNo;
				productInfo.LCDIndex = ((TLLCDDevice)devices[i][j]).LCDIndex;
				list.Add(productInfo);
			}
		}
		return list;
	}

	// Settings requests arrive back to back (update, then rotate) and each runs ExecuteSetting; serialising them keeps
	// one call from Reset()ting the thread while the other is still setting up its sensor screen.
	public async void SetSensorStreaming(int port, int index, TLLCDSensorSetting setting)
	{
		await sensorLock.WaitAsync();
		try
		{
			TLLCDVideoThread thread = VideoThreads.FirstOrDefault((TLLCDVideoThread p) => p.Port == port && p.Index == index);
			if (thread == null)
			{
				thread = new TLLCDVideoThread(port, index);
				VideoThreads.Add(thread);
			}
			if (CheckDevice(thread.Port, thread.Index))
			{
				if (thread.Enabled && thread.Type == 1)
				{
					thread.SetSensor(setting, FanController);
					return;
				}
				thread.Reset();
				if (thread.SetSensor(setting, FanController) && VideoThreads.Contains(thread))
				{
					SetVideoThread(thread);
				}
			}
		}
		catch (Exception ex)
		{
			EventLogHelper.Error($"LCD sensor screen on port {port} index {index} failed. {ex.Message}");
		}
		finally
		{
			sensorLock.Release();
		}
	}

	private void startSensorValueUpdater()
	{
		try
		{
			screenUpdateSensorValueTimer = NonOverlappingTimer.StartNew(() =>
			{
				foreach (TLLCDVideoThread item in VideoThreads.Where((TLLCDVideoThread p) => p.Enabled && p.Type == 1).ToList())
				{
					item.SensorValueUpdater();
				}
			}, 2000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	public void SetTLFanController(TLFanController controller)
	{
		FanController = controller;
	}

	private List<TLLCDFanRPM> LoadFanRPM(int port)
	{
		return (from p in VideoThreads
			where p.Port == port
			select new TLLCDFanRPM
			{
				Index = p.Index,
				RPM = p.GetFanRPM()
			}).ToList();
	}

	public void LoadSettings()
	{
		string mD5String = getMD5String("TL-LCD-Controller-240110");
		string mD5String2 = getMD5String(DevicePath);
		string text = Path.Combine(LConnectApp.Path.DeviceSettingDirectory, mD5String);
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		string path = Path.Combine(text, mD5String2 + ".LCD");
		Settings.Clear();
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			using MemoryStream memoryStream = new MemoryStream();
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
			using GZipStream gZipStream = new GZipStream(fileStream, CompressionMode.Decompress);
			gZipStream.CopyTo(memoryStream);
			Settings = JsonSerializer.Deserialize<List<TLLCDSetting>>(memoryStream.ToArray());
			gZipStream.Close();
			fileStream.Close();
		}
		catch
		{
		}
	}

	public void SaveSettings()
	{
		string mD5String = getMD5String("TL-LCD-Controller-240110");
		string mD5String2 = getMD5String(DevicePath);
		string text = Path.Combine(LConnectApp.Path.DeviceSettingDirectory, mD5String);
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		string path = Path.Combine(text, mD5String2 + ".LCD");
		while (true)
		{
			try
			{
				using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
				using GZipStream gZipStream = new GZipStream(fileStream, CompressionMode.Compress);
				byte[] array = JsonSerializer.SerializeToUtf8Bytes(Settings);
				gZipStream.Write(array, 0, array.Length);
				gZipStream.Close();
				fileStream.Close();
				break;
			}
			catch
			{
				Thread.Sleep(100);
			}
		}
	}

	public List<TLLCDScreenSetting> GetSetting(int port)
	{
		TLLCDSetting tLLCDSetting = Settings.FirstOrDefault((TLLCDSetting p) => p.Port == port);
		if (tLLCDSetting == null)
		{
			return new List<TLLCDScreenSetting>();
		}
		return tLLCDSetting.Screens;
	}

	public TLLCDConfig GetConfig(int port)
	{
		TLLCDSetting tLLCDSetting = Settings.FirstOrDefault((TLLCDSetting p) => p.Port == port);
		if (tLLCDSetting == null)
		{
			return new TLLCDConfig
			{
				Rotate = ScreenRotation.Rotate0,
				Group = TLLCDGroupType.Group1
			};
		}
		return tLLCDSetting.Config;
	}

	public void UpdateConfig(TLLCDConfig config, int port)
	{
		TLLCDSetting tLLCDSetting = Settings.FirstOrDefault((TLLCDSetting p) => p.Port == port);
		if (tLLCDSetting == null)
		{
			tLLCDSetting = new TLLCDSetting
			{
				Port = port,
				Screens = new List<TLLCDScreenSetting>()
			};
			Settings.Add(tLLCDSetting);
		}
		tLLCDSetting.Config = config;
	}

	public void UpdateSetting(TLLCDScreenSetting screenSetting, int port)
	{
		TLLCDSetting tLLCDSetting = Settings.FirstOrDefault((TLLCDSetting p) => p.Port == port);
		if (tLLCDSetting == null)
		{
			tLLCDSetting = new TLLCDSetting
			{
				Port = port,
				Screens = new List<TLLCDScreenSetting>()
			};
			Settings.Add(tLLCDSetting);
		}
		TLLCDScreenSetting tLLCDScreenSetting = tLLCDSetting.Screens.FirstOrDefault((TLLCDScreenSetting p) => p.Index == screenSetting.Index);
		if (tLLCDScreenSetting != null)
		{
			foreach (TLLCDScreenSetting item in tLLCDSetting.Screens.Where((TLLCDScreenSetting p) => p.Sync.HasValue && p.Sync.Value == screenSetting.Index && p.Index != screenSetting.Index).ToList())
			{
				item.Sync = null;
			}
			tLLCDSetting.Screens.Remove(tLLCDScreenSetting);
		}
		tLLCDSetting.Screens.Add(screenSetting);
	}

	public void ExecuteSetting()
	{
		int port;
		for (port = 0; port < 4; port++)
		{
			TLLCDSetting tLLCDSetting = Settings.FirstOrDefault((TLLCDSetting p) => p.Port == port);
			int portDevicesCount = GetPortDevicesCount(port);
			int i;
			for (i = 0; i < portDevicesCount; i++)
			{
				if (!IsDeviceExist(port, i))
				{
					RemoveVideoThread(port, i);
					continue;
				}
				if (tLLCDSetting == null)
				{
					SetNoneImage(port, i);
					continue;
				}
				TLLCDScreenSetting tLLCDScreenSetting = tLLCDSetting.Screens.FirstOrDefault((TLLCDScreenSetting p) => p.Index == i);
				if (tLLCDScreenSetting == null)
				{
					SetNoneImage(port, i);
					continue;
				}
				setScreenRotation(port, tLLCDScreenSetting.Index, tLLCDScreenSetting.Rotate);
				switch (tLLCDScreenSetting.MediaType)
				{
				case TLLCDMediaType.None:
					SetNoneImage(port, i);
					break;
				case TLLCDMediaType.Image:
					SetImage(port, tLLCDScreenSetting);
					SetBootImage(port, tLLCDScreenSetting);
					break;
				case TLLCDMediaType.Dynamic:
					SetVideo(port, tLLCDScreenSetting);
					break;
				case TLLCDMediaType.Video:
					SetVideo(port, tLLCDScreenSetting);
					break;
				case TLLCDMediaType.Sensor:
					if (tLLCDScreenSetting.Sensor == null)
					{
						SetNoneImage(port, i);
					}
					else
					{
						SetSensorStreaming(port, tLLCDScreenSetting.Index, tLLCDScreenSetting.Sensor);
					}
					break;
				case TLLCDMediaType.Animation:
					SetVideo(port, tLLCDScreenSetting);
					break;
				}
			}
		}
	}

	public void SetNoneImage(int port, int index)
	{
		RemoveVideoThread(port, index);
		byte[] bytes = DefaultScreen.GetJpeg();
		setJPG(port, index, bytes);
		setSwitchJPG(port, index);
		setBootJPG(port, index, bytes);
	}

	public void SetImage(int port, TLLCDScreenSetting screenSetting)
	{
		RemoveVideoThread(port, screenSetting.Index);
		byte[] imageBlock = GetImageBlock(screenSetting.FilePath, screenSetting.Block, screenSetting.Horizontal);
		setJPG(port, screenSetting.Index, imageBlock);
		setSwitchJPG(port, screenSetting.Index);
	}

	public void SetBootImage(int port, TLLCDScreenSetting screenSetting)
	{
		RemoveVideoThread(port, screenSetting.Index);
		byte[] imageBlock = GetImageBlock(screenSetting.FilePath, screenSetting.Block, screenSetting.Horizontal);
		setBootJPG(port, screenSetting.Index, imageBlock);
	}

	public void SetRotate(int port, List<TLLCDScreenRotate> screenRotates)
	{
		TLLCDSetting tLLCDSetting = Settings.FirstOrDefault((TLLCDSetting p) => p.Port == port);
		if (tLLCDSetting != null)
		{
			foreach (TLLCDScreenRotate rotateSetting in screenRotates)
			{
				TLLCDScreenSetting tLLCDScreenSetting = tLLCDSetting.Screens.FirstOrDefault((TLLCDScreenSetting p) => p.Index == rotateSetting.Index);
				if (tLLCDScreenSetting != null)
				{
					tLLCDScreenSetting.Rotate = rotateSetting.Rotate;
				}
			}
			SaveSettings();
		}
		foreach (TLLCDScreenRotate screenRotate in screenRotates)
		{
			setScreenRotation(port, screenRotate.Index, screenRotate.Rotate);
		}
	}

	private byte[] GetImageBlock(string filePath, int block, bool horizontal)
	{
		return MediaConverter.GetImageBlock(filePath, block, horizontal);
	}

	public void SyncVideoThread()
	{
		try
		{
			foreach (TLLCDVideoThread videoThread in VideoThreads)
			{
				if (videoThread.Enabled && videoThread.Locked == 2)
				{
					videoThread.Locked = 0;
				}
				else if (videoThread.Enabled && videoThread.Locked == 1)
				{
					videoThread.TickMissed = true;
				}
			}
		}
		catch
		{
		}
	}

	public void ClearThread()
	{
		try
		{
			foreach (TLLCDVideoThread videoThread in VideoThreads)
			{
				videoThread.Reset();
			}
			VideoThreads.Clear();
		}
		catch
		{
		}
	}

	private void RemoveVideoThread(int port, int index)
	{
		TLLCDVideoThread tLLCDVideoThread = VideoThreads.FirstOrDefault((TLLCDVideoThread p) => p.Port == port && p.Index == index);
		if (tLLCDVideoThread != null)
		{
			tLLCDVideoThread.Reset();
			VideoThreads.Remove(tLLCDVideoThread);
		}
	}

	private List<TLLCDFrame> LoadFrame(int port)
	{
		return (from p in VideoThreads
			where p.Port == port
			select new TLLCDFrame
			{
				Index = p.Index,
				Frame = ((p.Sync == null) ? p.Frame : p.Sync.Frame)
			}).ToList();
	}

	private void SetVideoThread(TLLCDVideoThread thread)
	{
		Guid guid = thread.Guid;
		Task.Run(async () =>
		{
			while (thread.Enabled && guid == thread.Guid)
			{
				if (thread.Locked == 0)
				{
					thread.Locked = 1;
					try
					{
						if (GetDevices(thread.Port).Count <= thread.Index)
						{
							thread.Reset();
						}
						if (thread.Device == null)
						{
							thread.Device = GetDevices(thread.Port)[thread.Index];
						}
						byte[] image = null;
						if (thread.Sync == null)
						{
							if (thread.Type == 1)
							{
								image = thread.WriteSensorFrame();
							}
							if (thread.Type == 0)
							{
								if (thread.Frame < thread.Images.Count)
								{
									image = thread.Images[thread.Frame];
									thread.Frame++;
								}
								if (thread.Frame >= thread.Images.Count)
								{
									thread.Frame = 0;
								}
							}
						}
						else
						{
							if (thread.Sync.Type == 1)
							{
								image = thread.Sync.Images[0];
							}
							if (thread.Sync.Type == 0)
							{
								if (thread.Sync.FilePath == thread.FilePath && thread.Sync.Block == thread.Block)
								{
									image = thread.Sync.Images[thread.Sync.Frame];
								}
								else
								{
									image = thread.Images[thread.Sync.Frame];
								}
							}
						}
						if (image != null && image.Length != 0)
						{
							lockedActionExecution(() =>
							{
								if (thread.Device != null)
								{
									long start = System.Diagnostics.Stopwatch.GetTimestamp();
									thread.Device.SendSyncJPG(image);
									thread.RecordFrame(image.Length, System.Diagnostics.Stopwatch.GetElapsedTime(start), thread.Device.LastFirstPacketTime);
								}
							});
						}
					}
					catch
					{
					}
					finally
					{
						// A send that overran its tick may start the next frame right away; waiting for the following
						// tick would turn every slow frame into two ticks (60 fps target, ~30 fps delivered).
						thread.Locked = (ushort)(thread.TickMissed ? 0 : 2);
						thread.TickMissed = false;
					}
				}
				// The original polled every 20 ms, which silently caps the stream near 50 fps.
				await Task.Delay(Math.Min(20, 250 / Constants.StreamFramesPerSecond + 1));
			}
		});
	}

	private void SetVideo(int port, TLLCDScreenSetting screenSetting)
	{
		TLLCDVideoThread tLLCDVideoThread = VideoThreads.FirstOrDefault((TLLCDVideoThread p) => p.Port == port && p.Index == screenSetting.Index);
		if (tLLCDVideoThread == null)
		{
			tLLCDVideoThread = new TLLCDVideoThread(port, screenSetting.Index);
			VideoThreads.Add(tLLCDVideoThread);
		}
		else if (tLLCDVideoThread.FilePath == screenSetting.FilePath && tLLCDVideoThread.Sync?.Index == screenSetting.Sync)
		{
			return;
		}
		tLLCDVideoThread.Reset();
		if (!CheckDevice(tLLCDVideoThread.Port, tLLCDVideoThread.Index))
		{
			return;
		}
		if (screenSetting.Sync.HasValue && screenSetting.Index != screenSetting.Sync.Value && VideoThreads.Any((TLLCDVideoThread p) => p.Port == port && p.Index == screenSetting.Sync.Value))
		{
			TLLCDVideoThread syncThread = VideoThreads.FirstOrDefault((TLLCDVideoThread p) => p.Port == port && p.Index == screenSetting.Sync.Value);
			tLLCDVideoThread.SetSyncVideo(syncThread, screenSetting.FilePath, screenSetting.Block, screenSetting.Horizontal);
		}
		else
		{
			tLLCDVideoThread.SetVideo(screenSetting.FilePath, screenSetting.Block, screenSetting.Horizontal);
		}
		SetVideoThread(tLLCDVideoThread);
	}
}
