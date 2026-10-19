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

internal class SLInfinityController : IController
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

	private const LightingSpeed PingLightingSpeed = LightingSpeed.Normal;

	private const LightingBrightness PingLightingBrightness = LightingBrightness.Normal;

	private static readonly HashSet<LightingMode> MergeLightingModeLookup = new HashSet<LightingMode>
	{
		LightingMode.Runway_Merge,
		LightingMode.MopUp_Merge,
		LightingMode.Mixing_Merge,
		LightingMode.Stack_Merge,
		LightingMode.Tide_Merge,
		LightingMode.Scan_Merge,
		LightingMode.Door_Merge,
		LightingMode.HeartBeatRunway_Merge,
		LightingMode.ElectricCurrent_Merge
	};

	private static readonly Dictionary<LightingMode, int> LightingModeValueLookup = new Dictionary<LightingMode, int>
	{
		[LightingMode.Runway_Merge] = 70,
		[LightingMode.MopUp_Merge] = 71,
		[LightingMode.Mixing_Merge] = 72,
		[LightingMode.Stack_Merge] = 73,
		[LightingMode.Tide_Merge] = 74,
		[LightingMode.Scan_Merge] = 75,
		[LightingMode.Door_Merge] = 76,
		[LightingMode.HeartBeatRunway_Merge] = 77,
		[LightingMode.ElectricCurrent_Merge] = 78,
		[LightingMode.Rainbow] = 50,
		[LightingMode.RainbowMorph] = 63,
		[LightingMode.StaticColor] = 1,
		[LightingMode.Breathing] = 2,
		[LightingMode.BreathingRainbow] = 6,
		[LightingMode.Runway] = 26,
		[LightingMode.MopUp] = 68,
		[LightingMode.Meteor] = 25,
		[LightingMode.Warning] = 54,
		[LightingMode.Voice] = 55,
		[LightingMode.Mixing] = 56,
		[LightingMode.Stack] = 57,
		[LightingMode.Tide] = 58,
		[LightingMode.Scan] = 59,
		[LightingMode.Door] = 60,
		[LightingMode.HeartBeat] = 66,
		[LightingMode.HeartBeatRunway] = 67,
		[LightingMode.Disco] = 35,
		[LightingMode.ElectricCurrent] = 65,
		[LightingMode.Rainbow_Inner] = 5,
		[LightingMode.RainbowMorph_Inner] = 4,
		[LightingMode.StaticColor_Inner] = 1,
		[LightingMode.Breathing_Inner] = 2,
		[LightingMode.BreathingRainbow_Inner] = 6,
		[LightingMode.Taichi_Inner] = 28,
		[LightingMode.ColorCycle_Inner] = 24,
		[LightingMode.Runway_Inner] = 26,
		[LightingMode.MopUp_Inner] = 27,
		[LightingMode.Meteor_Inner] = 25,
		[LightingMode.MeteorRainbow_Inner] = 8,
		[LightingMode.Lottery_Inner] = 38,
		[LightingMode.Warning_Inner] = 41,
		[LightingMode.Voice_Inner] = 42,
		[LightingMode.Mixing_Inner] = 43,
		[LightingMode.Stack_Inner] = 44,
		[LightingMode.Tide_Inner] = 45,
		[LightingMode.Scan_Inner] = 46,
		[LightingMode.DoubleMeteor_Inner] = 29,
		[LightingMode.MeteorContest_Inner] = 30,
		[LightingMode.MeteorMix_Inner] = 31,
		[LightingMode.ReturnArc_Inner] = 32,
		[LightingMode.DoubleArc_Inner] = 33,
		[LightingMode.Door_Inner] = 34,
		[LightingMode.HeartBeat_Inner] = 36,
		[LightingMode.HeartBeatRunway_Inner] = 69,
		[LightingMode.Disco_Inner] = 35,
		[LightingMode.Rainbow_Outer] = 5,
		[LightingMode.RainbowMorph_Outer] = 4,
		[LightingMode.StaticColor_Outer] = 1,
		[LightingMode.Breathing_Outer] = 2,
		[LightingMode.BreathingRainbow_Outer] = 6,
		[LightingMode.ColorCycle_Outer] = 24,
		[LightingMode.Runway_Outer] = 26,
		[LightingMode.MopUp_Outer] = 27,
		[LightingMode.Meteor_Outer] = 25,
		[LightingMode.MeteorRainbow_Outer] = 8,
		[LightingMode.ColorfulMeteor_Outer] = 39,
		[LightingMode.Lottery_Outer] = 38,
		[LightingMode.Warning_Outer] = 41,
		[LightingMode.Voice_Outer] = 42,
		[LightingMode.Mixing_Outer] = 43,
		[LightingMode.Stack_Outer] = 44,
		[LightingMode.Tide_Outer] = 45,
		[LightingMode.Scan_Outer] = 46,
		[LightingMode.Door_Outer] = 34,
		[LightingMode.HeartBeat_Outer] = 36,
		[LightingMode.Disco_Outer] = 35,
		[LightingMode.Reflect_Outer] = 48
	};

	private static readonly Dictionary<LightingSpeed, int> PingDelayTimeLookup = new Dictionary<LightingSpeed, int>
	{
		[LightingSpeed.Lowest] = 4000,
		[LightingSpeed.Lower] = 3200,
		[LightingSpeed.Normal] = 2500,
		[LightingSpeed.Faster] = 2000,
		[LightingSpeed.Fastest] = 1500
	};

	private static readonly Dictionary<LightingMode, (LightingMode, LightingMode)> NeedFixedLightingModeLookup = new Dictionary<LightingMode, (LightingMode, LightingMode)>
	{
		[LightingMode.StaticColor] = (LightingMode.StaticColor_Inner, LightingMode.StaticColor_Outer),
		[LightingMode.Breathing] = (LightingMode.Breathing_Inner, LightingMode.Breathing_Outer),
		[LightingMode.BreathingRainbow] = (LightingMode.BreathingRainbow_Inner, LightingMode.BreathingRainbow_Outer),
		[LightingMode.Runway] = (LightingMode.Runway_Inner, LightingMode.Runway_Outer),
		[LightingMode.Meteor] = (LightingMode.Meteor_Inner, LightingMode.Meteor_Outer),
		[LightingMode.Disco] = (LightingMode.Disco_Inner, LightingMode.Disco_Outer)
	};

	private static readonly int[] DefaultMergeOrder = new int[4] { 0, 1, 2, 3 };

	private readonly LightingConfig[] currentNormalizedLightingConfigs = new LightingConfig[8];

	private readonly int[] fanQuantityList = Enumerable.Repeat(3, 4).ToArray();

	private bool isMergeModePingRunning;

	public SLInfinityController(IEne6K77FanDevice device)
	{
		this.device = device;
	}

	private void AppSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
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
		setMergeOrder(DefaultMergeOrder);
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
		Settings.Default.PropertyChanged += AppSetting_PropertyChanged;
		isInitialized.Value = true;
	}

	public void Dispose()
	{
		isDisposed = true;
		while (!isInitialized.Value)
		{
			Thread.Sleep(1000);
		}
		Settings.Default.PropertyChanged -= AppSetting_PropertyChanged;
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
			else
			{
				switch (result)
				{
				case SettingType.FanQuantity:
					handleFanQuantity(setting);
					break;
				case SettingType.MergeOrder:
					handleMergeOrder(setting);
					break;
				}
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
			RequestType.MergeOrder => handleMergeOrder(requestData), 
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
				MaxSpeed = 2100,
				Reference = RPMReferenceSource.CPU,
				Phases = new RPMPhase[6]
				{
					new RPMPhase(0, 210),
					new RPMPhase(25, 420),
					new RPMPhase(40, 1050),
					new RPMPhase(55, 1302),
					new RPMPhase(70, 1575),
					new RPMPhase(90, 2100)
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
							int num = currentRPMConfigs[i].CalculateSpeed(temperature);
							int value = ((num == 0) ? 1 : Math.Max(10, (int)Math.Round((double)num / (double)currentRPMConfigs[i].MaxSpeed * 100.0)));
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
			lightingConfig.Mode = (((i & 1) == 0) ? LightingMode.StaticColor_Inner : LightingMode.StaticColor_Outer);
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
			LightingConfig[] configs = JsonSerializer.Deserialize<LightingConfig[]>(requestData.Body);
			bool flag = isMergeMode();
			List<LightingConfig> newConfigs = new List<LightingConfig>();
			foreach (LightingConfig item in prepareLightingConfig(configs))
			{
				if (item != null && item.Port >= 0 && item.Port < 8)
				{
					LightingConfig lightingConfig = normalizeConfig(item);
					if (lightingConfig != null)
					{
						currentNormalizedLightingConfigs[lightingConfig.Port] = lightingConfig;
						saveSetting((SettingType)item.Port, item);
						newConfigs.Add(lightingConfig);
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
					setAllLightingConfig();
				}
				else
				{
					lockedActionExecution(() =>
					{
						foreach (LightingConfig item2 in newConfigs)
						{
							setLightingConfig(item2);
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

	private List<LightingConfig> prepareLightingConfig(LightingConfig[] configs)
	{
		List<LightingConfig> list = new List<LightingConfig>();
		foreach (LightingConfig lightingConfig in configs)
		{
			if (NeedFixedLightingModeLookup.TryGetValue(lightingConfig.Mode, out var value))
			{
				LightingConfig lightingConfig2 = lightingConfig.Clone();
				(lightingConfig2.Mode, _) = value;
				list.Add(lightingConfig2);
				LightingConfig lightingConfig3 = lightingConfig.Clone();
				lightingConfig3.Port++;
				lightingConfig3.Mode = value.Item2;
				list.Add(lightingConfig3);
			}
			else
			{
				list.Add(lightingConfig);
			}
		}
		return list;
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

	private void setMergeOrder(int[] order)
	{
		if (order == null || order.Length != 4)
		{
			return;
		}
		int[] array = order;
		foreach (int num in array)
		{
			if (num < 0 || num > 4)
			{
				return;
			}
		}
		lockedActionExecution(() =>
		{
			device.SetMergeOrder(order);
		});
	}

	private void handleMergeOrder(DeviceSetting<object> setting)
	{
		if (DeviceSettingManager.TryParseData<int[]>(setting, out var data))
		{
			setMergeOrder(data);
		}
	}

	private object handleMergeOrder(RequestData requestData)
	{
		try
		{
			int[] array = JsonSerializer.Deserialize<int[]>(requestData.Body);
			setMergeOrder(array);
			saveSetting(SettingType.MergeOrder, array);
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
			if (isMergeMode() && groupIndex != 0)
			{
				if (isMergeModePingRunning)
				{
					return null;
				}
				isMergeModePingRunning = true;
				lockedActionExecution(() =>
				{
					LightingConfig lightingConfig = new LightingConfig
					{
						Port = 0,
						Mode = LightingMode.Rainbow,
						Speed = LightingSpeed.Normal,
						Direction = LightingDirection.Right,
						Brightness = LightingBrightness.Off,
						Colors = Array.Empty<Color>()
					};
					setLightingConfig(lightingConfig);
					device.Ping(groupIndex, 0, 2, isMerged: true);
				});
				Thread.Sleep(PingDelayTimeLookup[LightingSpeed.Normal]);
				setMergeLighting();
				isMergeModePingRunning = false;
			}
			else
			{
				lockedActionExecution(() =>
				{
					device.Ping(groupIndex, 0, 2, isMerged: false);
				});
				Thread.Sleep(PingDelayTimeLookup[LightingSpeed.Normal]);
				syncLightingFrame();
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
		Color[] colors;
		switch (config.Mode)
		{
		case LightingMode.Breathing_Inner:
		case LightingMode.StaticColor_Inner:
			colors = convertToInnerLEDColor(config.Colors);
			break;
		case LightingMode.Breathing_Outer:
		case LightingMode.StaticColor_Outer:
			colors = convertToOuterLEDColor(config.Colors);
			break;
		default:
			colors = convertToFanGroupColor(config.Colors);
			break;
		}
		lightingConfig2.Colors = colors;
		return lightingConfig;
	}

	private void setLightingConfig(LightingConfig config)
	{
		if (LightingModeValueLookup.ContainsKey(config.Mode) && !Settings.Default.IsMotherboardARGBSync)
		{
			device.SetColorSetting((byte)config.Port, config.Colors);
			device.SetEffectSetting((byte)config.Port, (byte)LightingModeValueLookup[config.Mode], (byte)config.Speed, (byte)config.Direction, (byte)config.Brightness);
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
			for (int num = currentNormalizedLightingConfigs.Length - 1; num >= 0; num--)
			{
				if (currentNormalizedLightingConfigs[num] != null)
				{
					setLightingConfig(currentNormalizedLightingConfigs[num]);
				}
			}
		});
		syncLightingFrame();
	}

	private bool isMergeMode()
	{
		if (currentNormalizedLightingConfigs[0] != null && currentNormalizedLightingConfigs[0].Port == 0)
		{
			return MergeLightingModeLookup.Contains(currentNormalizedLightingConfigs[0].Mode);
		}
		return false;
	}

	private void setMergeLighting()
	{
		if (Settings.Default.IsMotherboardARGBSync)
		{
			return;
		}
		LightingConfig emptyConfig = new LightingConfig
		{
			Mode = LightingMode.Rainbow,
			Speed = LightingSpeed.Normal,
			Direction = LightingDirection.Right,
			Brightness = LightingBrightness.Off,
			Colors = Array.Empty<Color>()
		};
		lockedActionExecution(() =>
		{
			setLightingConfig(currentNormalizedLightingConfigs[0]);
			for (int i = 1; i < 8; i++)
			{
				emptyConfig.Port = i;
				setLightingConfig(emptyConfig);
			}
		});
	}

	private void syncLightingFrame()
	{
		lockedActionExecution(() =>
		{
			device.SetFrame(1);
		});
	}

	private Color[] convertToInnerLEDColor(Color[] uiColors)
	{
		Color[] array = new Color[32];
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				array[i * 8 + j] = ((i < uiColors.Length) ? uiColors[i] : Colors.Black);
			}
		}
		return array.ToArray();
	}

	private Color[] convertToOuterLEDColor(Color[] uiColors)
	{
		Color[] array = new Color[48];
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 12; j++)
			{
				array[i * 12 + j] = ((i < uiColors.Length) ? uiColors[i] : Colors.Black);
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
}
