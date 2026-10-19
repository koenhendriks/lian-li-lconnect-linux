using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products;
using LConnectCore.Products.Ene6K77Fan;
using LConnectService.Properties;

namespace LConnectService.Products.Ene6K77Fan;

internal class SLFanController : IController
{
	private const int CheckFirmwareVersionInterval = 2000;

	private readonly object locker = new object();

	private readonly LockData<bool> isInitialized = new LockData<bool>(value: false);

	private bool isDisposed;

	private readonly IEne6K77FanDevice device;

	private Task checkFirmwareVersionTask;

	private CancellationTokenSource checkFirmwareVersionTaskToken;

	private bool autoSendToDevice = true;

	private FirmwareVersion firmwareVersion;

	private const int ReadFanSpeedInterval = 1000;

	private const int WriteFanSpeedInterval = 1000;

	private readonly RPMConfig[] currentRPMConfigs = new RPMConfig[4];

	private readonly int[] fanSpeed = new int[4];

	private NonOverlappingTimer readFanSpeedTimer;

	private NonOverlappingTimer writeFanSpeedTimer;

	private const LightingSpeed PingLightingSpeed = LightingSpeed.Faster;

	private const LightingBrightness PingLightingBrightness = LightingBrightness.Normal;

	private static readonly HashSet<LightingMode> mergeLightingModeLookup = new HashSet<LightingMode>
	{
		LightingMode.Runway_Merge,
		LightingMode.Meteor_Merge
	};

	private static readonly Dictionary<LightingMode, int> lightingModeValueLookup = new Dictionary<LightingMode, int>
	{
		[LightingMode.Runway_Merge] = 28,
		[LightingMode.Meteor_Merge] = 36,
		[LightingMode.Rainbow] = 5,
		[LightingMode.RainbowMorph] = 4,
		[LightingMode.StaticColor] = 1,
		[LightingMode.Breathing] = 2,
		[LightingMode.ColorCycle] = 35,
		[LightingMode.Runway] = 28,
		[LightingMode.Staggered] = 24,
		[LightingMode.Tide] = 26,
		[LightingMode.Meteor] = 36,
		[LightingMode.Mixing] = 30,
		[LightingMode.Stack] = 32,
		[LightingMode.StackMulti] = 33,
		[LightingMode.Neon] = 34
	};

	private static readonly Dictionary<LightingSpeed, int> PingDelayTimeLookup = new Dictionary<LightingSpeed, int>
	{
		[LightingSpeed.Lowest] = 4000,
		[LightingSpeed.Lower] = 3200,
		[LightingSpeed.Normal] = 2500,
		[LightingSpeed.Faster] = 2000,
		[LightingSpeed.Fastest] = 1500
	};

	private readonly LightingConfig[] currentNormalizedLightingConfigs = new LightingConfig[4];

	private readonly int[] fanQuantityList = Enumerable.Repeat(3, 4).ToArray();

	private readonly object pingLocker = new object();

	private bool isPingRunning;

	public SLFanController(IEne6K77FanDevice device)
	{
		this.device = device;
	}

	private void appSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "IsMotherboardARGBSync")
		{
			bool isMotherboardARGBSync = Settings.Default.IsMotherboardARGBSync;
			setMotherboardARGBSync(isMotherboardARGBSync);
			if (!isMotherboardARGBSync)
			{
				setAllLightingConfig();
			}
		}
		else if (e.PropertyName == "IsMotherboardRPMSync")
		{
			bool isMotherboardRPMSync = Settings.Default.IsMotherboardRPMSync;
			setMotherboardRPMSync(isMotherboardRPMSync);
		}
	}

	public void Init()
	{
		firmwareVersion = device.GetFirmwareVersion();
		setupDefaultRPMConfig();
		setupDefaultLightingConfig();
		startReadFanSpeed();
		startFirmwareVersionChecker();
		startWriteFanSpeed();
		setFanQuantity(fanQuantityList);
		bool isMotherboardARGBSync = Settings.Default.IsMotherboardARGBSync;
		bool isMotherboardRPMSync = Settings.Default.IsMotherboardRPMSync;
		if (isMotherboardARGBSync)
		{
			setMotherboardARGBSync(isMotherboardARGBSync);
		}
		if (isMotherboardRPMSync)
		{
			setMotherboardRPMSync(isMotherboardRPMSync);
		}
		Settings.Default.PropertyChanged += appSetting_PropertyChanged;
		isInitialized.Value = true;
	}

	public void Dispose()
	{
		isDisposed = true;
		while (!isInitialized.Value)
		{
			Thread.Sleep(1000);
		}
		Settings.Default.PropertyChanged -= appSetting_PropertyChanged;
		stopWriteFanSpeed();
		stopFirmwareVersionChecker();
		stopReadFanSpeed();
		while (!((!(writeFanSpeedTimer?.IsRunning)) ?? true) || !((!(readFanSpeedTimer?.IsRunning)) ?? true) || !(checkFirmwareVersionTask?.IsCompleted ?? true))
		{
			Thread.Sleep(1000);
		}
	}

	public void Apply(DeviceSetting<object> setting)
	{
		try
		{
			if (setting == null || !Enum.IsDefined(typeof(SettingType), setting.Type) || !Enum.TryParse<SettingType>(setting.Type, out var result))
			{
				return;
			}
			if (SettingType.LightingPort0 <= result && result <= SettingType.LightingPort7)
			{
				if (!DeviceSettingManager.TryParseData<LightingConfig>(setting, out var data))
				{
					return;
				}
				LightingConfig newConfig = normalizeConfig(data);
				if (newConfig == null)
				{
					return;
				}
				currentNormalizedLightingConfigs[newConfig.Port] = newConfig;
				if (autoSendToDevice)
				{
					lockedActionExecution(() =>
					{
						setLightingConfig(newConfig);
					});
				}
			}
			else if (SettingType.FanGroupSpeed0 <= result && result <= SettingType.FanGroupSpeed3)
			{
				if (DeviceSettingManager.TryParseData<RPMConfig>(setting, out var data2))
				{
					int num = (int)(result - 8);
					currentRPMConfigs[num] = data2;
				}
			}
			else if (result == SettingType.FanQuantity)
			{
				handleFanQuantity(setting);
			}
		}
		catch
		{
		}
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
		if (isMergeMode())
		{
			setMergeLighting();
		}
		else
		{
			setAllLightingConfig();
		}
	}

	public object HandleRequestData(RequestData requestData)
	{
		if (!requestData.Query.TryGetValue("type", out var value) || !Enum.IsDefined(typeof(RequestType), value) || !Enum.TryParse<RequestType>(value, out var result))
		{
			return null;
		}
		return result switch
		{
			RequestType.FirmwareVersion => handleGetFirmwareVersion(requestData), 
			RequestType.GetFanSpeed => handleGetFanSpeed(requestData), 
			RequestType.SetFanSpeed => handleSetFanSpeed(requestData), 
			RequestType.LightingSetting => handleLightingSetting(requestData), 
			RequestType.FanQuantity => handleFanQuantity(requestData), 
			RequestType.Ping => handlePing(requestData), 
			_ => null, 
		};
	}

	private object handleGetFirmwareVersion(RequestData requestData)
	{
		return firmwareVersion;
	}

	private void setMotherboardARGBSync(bool isSync)
	{
		lockedActionExecution(() =>
		{
			device.SetLightingMotherboardSync(isSync);
		});
	}

	private void setMotherboardRPMSync(bool isSync)
	{
		lockedActionExecution(() =>
		{
			for (int i = 0; i < 4; i++)
			{
				device.SetFanMotherboardSync(i, isSync);
			}
		});
	}

	private void startFirmwareVersionChecker()
	{
		try
		{
			checkFirmwareVersionTaskToken = new CancellationTokenSource();
			checkFirmwareVersionTask = Task.Run(() =>
			{
				try
				{
					while (!checkFirmwareVersionTaskToken.IsCancellationRequested)
					{
						DateTime now = DateTime.Now;
						try
						{
							lockedActionExecution(() =>
							{
								firmwareVersion = device.GetFirmwareVersion();
							});
							if (firmwareVersion.IsValid)
							{
								break;
							}
						}
						catch
						{
						}
						double num = 2000.0 - (DateTime.Now - now).TotalMilliseconds;
						if (num > 0.0)
						{
							Thread.Sleep((int)Math.Min(num, 2147483647.0));
						}
					}
				}
				catch
				{
				}
			}, checkFirmwareVersionTaskToken.Token);
		}
		catch
		{
		}
	}

	private void stopFirmwareVersionChecker()
	{
		try
		{
			checkFirmwareVersionTaskToken?.Cancel();
		}
		catch
		{
		}
	}

	private void saveSetting<T>(SettingType type, T data)
	{
		try
		{
			DeviceSettingManager.Save(new DeviceSetting<T>
			{
				DevicePath = device.HidDevice.DevicePath,
				VendorID = device.HidDevice.VendorID,
				ProductID = device.HidDevice.ProductID,
				Type = type.ToString(),
				Data = data
			});
		}
		catch
		{
		}
	}

	private void lockedActionExecution(Action action)
	{
		if (isDisposed)
		{
			return;
		}
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

	private void setupDefaultRPMConfig()
	{
		for (int i = 0; i < currentRPMConfigs.Length; i++)
		{
			currentRPMConfigs[i] = new RPMConfig
			{
				MaxSpeed = 100,
				Reference = RPMReferenceSource.CPU,
				Phases = new RPMPhase[6]
				{
					new RPMPhase(0, 10),
					new RPMPhase(25, 20),
					new RPMPhase(40, 50),
					new RPMPhase(55, 62),
					new RPMPhase(70, 75),
					new RPMPhase(90, 100)
				}
			};
		}
	}

	private object handleGetFanSpeed(RequestData requestData)
	{
		return fanSpeed;
	}

	private object handleSetFanSpeed(RequestData requestData)
	{
		try
		{
			EneRPMConfig[] array = JsonSerializer.Deserialize<EneRPMConfig[]>(requestData.Body);
			if (array == null)
			{
				return null;
			}
			HashSet<int> hashSet = new HashSet<int>();
			EneRPMConfig[] array2 = array;
			foreach (EneRPMConfig eneRPMConfig in array2)
			{
				if (eneRPMConfig != null && eneRPMConfig.FanGroupIndex >= 0 && eneRPMConfig.FanGroupIndex < 4 && eneRPMConfig.Config != null)
				{
					currentRPMConfigs[eneRPMConfig.FanGroupIndex] = eneRPMConfig.Config;
					hashSet.Add(eneRPMConfig.FanGroupIndex);
				}
			}
			foreach (int item in hashSet)
			{
				try
				{
					saveSetting((SettingType)(8 + item), currentRPMConfigs[item]);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void startReadFanSpeed()
	{
		try
		{
			readFanSpeedTimer = NonOverlappingTimer.StartNew(() =>
			{
				lockedActionExecution(() =>
				{
					int[] array = device.GetFanSpeed();
					if (array != null && array.Length >= fanSpeed.Length)
					{
						for (int i = 0; i < fanSpeed.Length; i++)
						{
							fanSpeed[i] = array[i];
						}
					}
				});
			}, 1000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	private void stopReadFanSpeed()
	{
		readFanSpeedTimer?.Dispose();
	}

	private void startWriteFanSpeed()
	{
		try
		{
			writeFanSpeedTimer = NonOverlappingTimer.StartNew(() =>
			{
				if (!Settings.Default.IsMotherboardRPMSync)
				{
					int?[] dutyList = new int?[currentRPMConfigs.Length];
					for (int i = 0; i < currentRPMConfigs.Length; i++)
					{
						if (currentRPMConfigs[i] != null)
						{
							RPMReferenceSource reference = currentRPMConfigs[i].Reference;
							int temperature;
							if (reference != RPMReferenceSource.CPU)
							{
								if (reference != RPMReferenceSource.GPU)
								{
									continue;
								}
								temperature = SystemInformation.Resource.GPUTemperature;
							}
							else
							{
								temperature = SystemInformation.Resource.CPUTemperature;
							}
							int value = currentRPMConfigs[i].CalculateSpeed(temperature);
							dutyList[i] = value;
						}
					}
					lockedActionExecution(() =>
					{
						for (int j = 0; j < dutyList.Length; j++)
						{
							if (dutyList[j].HasValue)
							{
								device.SetFanSpeed(j, dutyList[j].Value);
							}
						}
					});
				}
			}, 1000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	private void stopWriteFanSpeed()
	{
		writeFanSpeedTimer?.Dispose();
	}

	private void setupDefaultLightingConfig()
	{
		for (int i = 0; i < currentNormalizedLightingConfigs.Length; i++)
		{
			LightingConfig lightingConfig = new LightingConfig();
			lightingConfig.Port = i;
			lightingConfig.Mode = LightingMode.StaticColor;
			lightingConfig.Colors = new Color[4]
			{
				Colors.Red,
				Colors.Blue,
				Colors.Green,
				Colors.Yellow
			};
			lightingConfig.Brightness = LightingBrightness.Highest;
			lightingConfig.Speed = LightingSpeed.Normal;
			lightingConfig.Direction = LightingDirection.Right;
			LightingConfig config = lightingConfig;
			currentNormalizedLightingConfigs[i] = normalizeConfig(config);
		}
	}

	private object handleLightingSetting(RequestData requestData)
	{
		try
		{
			LightingConfig[]? array = JsonSerializer.Deserialize<LightingConfig[]>(requestData.Body);
			bool flag = isMergeMode();
			List<LightingConfig> newConfigs = new List<LightingConfig>();
			LightingConfig[] array2 = array;
			foreach (LightingConfig lightingConfig in array2)
			{
				if (lightingConfig != null && lightingConfig.Port >= 0 && lightingConfig.Port < 4)
				{
					LightingConfig lightingConfig2 = normalizeConfig(lightingConfig);
					if (lightingConfig2 != null)
					{
						currentNormalizedLightingConfigs[lightingConfig2.Port] = lightingConfig2;
						saveSetting((SettingType)lightingConfig.Port, lightingConfig);
						newConfigs.Add(lightingConfig2);
					}
				}
			}
			if (isMergeMode())
			{
				setMergeLighting();
			}
			else
			{
				if (flag)
				{
					sendMergeCommand(enable: false);
					setAllLightingConfig();
				}
				else
				{
					lockedActionExecution(() =>
					{
						foreach (LightingConfig item in newConfigs)
						{
							setLightingConfig(item);
						}
					});
				}
				syncLightingFrame();
			}
		}
		catch
		{
		}
		return null;
	}

	private void setFanQuantity(int[] quantity)
	{
		if (quantity == null || quantity.Length != 4)
		{
			return;
		}
		int[] array = quantity;
		foreach (int num in array)
		{
			if (num < 0 || num > 4)
			{
				return;
			}
		}
		lockedActionExecution(() =>
		{
			for (int j = 0; j < quantity.Length; j++)
			{
				fanQuantityList[j] = quantity[j];
				device.SetQuantity(j, quantity[j]);
			}
		});
		setAllLightingConfig();
	}

	private void handleFanQuantity(DeviceSetting<object> setting)
	{
		if (DeviceSettingManager.TryParseData<int[]>(setting, out var data))
		{
			setFanQuantity(data);
		}
	}

	private object handleFanQuantity(RequestData requestData)
	{
		try
		{
			int[] fanQuantity = JsonSerializer.Deserialize<int[]>(requestData.Body);
			setFanQuantity(fanQuantity);
			saveSetting(SettingType.FanQuantity, fanQuantityList);
		}
		catch
		{
		}
		return null;
	}

	private object handlePing(RequestData requestData)
	{
		try
		{
			int groupIndex = JsonSerializer.Deserialize<int>(requestData.Body);
			if (groupIndex < 0 || groupIndex >= 4)
			{
				return null;
			}
			lock (pingLocker)
			{
				if (isPingRunning)
				{
					return null;
				}
				isPingRunning = true;
			}
			bool flag = isMergeMode();
			if (flag)
			{
				sendMergeCommand(enable: false);
			}
			lockedActionExecution(() =>
			{
				device.Ping(groupIndex, byte.MaxValue, 2, isMerged: false);
			});
			Thread.Sleep(PingDelayTimeLookup[LightingSpeed.Faster]);
			if (flag)
			{
				setMergeLighting();
			}
			syncLightingFrame();
			lock (pingLocker)
			{
				isPingRunning = false;
			}
		}
		catch
		{
		}
		return null;
	}

	private LightingConfig normalizeConfig(LightingConfig config)
	{
		if (config == null)
		{
			return null;
		}
		LightingConfig lightingConfig = new LightingConfig();
		lightingConfig.Port = config.Port;
		lightingConfig.Mode = config.Mode;
		lightingConfig.Speed = config.Speed;
		LightingBrightness brightness = ((config.Brightness != LightingBrightness.Lowest) ? config.Brightness : LightingBrightness.Off);
		lightingConfig.Brightness = brightness;
		lightingConfig.Direction = config.Direction;
		LightingConfig lightingConfig2 = lightingConfig;
		LightingMode mode = config.Mode;
		Color[] colors = ((mode != LightingMode.Breathing && mode != LightingMode.StaticColor) ? convertToFanGroupColor(config.Colors) : convertToLEDColor(config.Colors));
		lightingConfig2.Colors = colors;
		return lightingConfig;
	}

	private void setLightingConfig(LightingConfig config)
	{
		if (lightingModeValueLookup.ContainsKey(config.Mode) && !Settings.Default.IsMotherboardARGBSync)
		{
			device.SetColorSetting((byte)config.Port, config.Colors);
			device.SetEffectSetting((byte)config.Port, (byte)lightingModeValueLookup[config.Mode], (byte)config.Speed, (byte)config.Direction, (byte)config.Brightness);
		}
	}

	private void setAllLightingConfig()
	{
		if (Settings.Default.IsMotherboardARGBSync)
		{
			return;
		}
		lockedActionExecution(() =>
		{
			LightingConfig[] array = currentNormalizedLightingConfigs;
			foreach (LightingConfig lightingConfig in array)
			{
				if (lightingConfig != null)
				{
					setLightingConfig(lightingConfig);
				}
			}
		});
		syncLightingFrame();
	}

	private bool isMergeMode()
	{
		if (currentNormalizedLightingConfigs[0] != null && currentNormalizedLightingConfigs[0].Port == 0)
		{
			return mergeLightingModeLookup.Contains(currentNormalizedLightingConfigs[0].Mode);
		}
		return false;
	}

	private void setMergeLighting()
	{
		if (!Settings.Default.IsMotherboardARGBSync)
		{
			sendMergeCommand(enable: true);
			lockedActionExecution(() =>
			{
				setLightingConfig(currentNormalizedLightingConfigs[0]);
			});
		}
	}

	private void syncLightingFrame()
	{
		lockedActionExecution(() =>
		{
			device.SetFrame(1);
		});
	}

	private Color[] convertToLEDColor(Color[] uiColors)
	{
		Color[] array = new Color[64];
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				array[i * 16 + j] = ((i < uiColors.Length) ? uiColors[i] : Colors.Black);
			}
		}
		return array;
	}

	private Color[] convertToFanGroupColor(Color[] uiColors)
	{
		Color[] array = new Color[16];
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				array[i * 4 + j] = ((j < uiColors.Length) ? uiColors[j] : Colors.Black);
			}
		}
		return array;
	}

	private void sendMergeCommand(bool enable)
	{
		IEne6K77FanDevice ene6K77FanDevice = device;
		SLFanDevice slFanDevice = ene6K77FanDevice as SLFanDevice;
		if (slFanDevice == null)
		{
			return;
		}
		lockedActionExecution(() =>
		{
			if (enable)
			{
				slFanDevice.StartMerge();
			}
			else
			{
				slFanDevice.StopMerge();
			}
		});
	}
}
