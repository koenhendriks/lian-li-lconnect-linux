using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.ExtensionMethods;
using LConnectCore.Products;
using LConnectCore.Products.TLFan;
using LConnectService.Enums;
using LConnectService.Helpers;
using LConnectService.Properties;

namespace LConnectService.Products.TLFan;

internal class TLFanController : IController
{
	private readonly object locker = new object();

	private readonly ITLFanDevice device;

	private readonly LockData<bool> isInitialized = new LockData<bool>(value: false);

	private bool manualMode;

	private bool isDisposed;

	private static readonly Dictionary<PortType, Dictionary<FanSize, RPMConfig>> rpmToPWMConfigs = new Dictionary<PortType, Dictionary<FanSize, RPMConfig>>
	{
		[PortType.LED] = new Dictionary<FanSize, RPMConfig>
		{
			[FanSize.Size120mm] = new RPMConfig
			{
				Phases = new RPMPhase[19]
				{
					new RPMPhase(200, 10),
					new RPMPhase(360, 15),
					new RPMPhase(530, 20),
					new RPMPhase(680, 25),
					new RPMPhase(830, 30),
					new RPMPhase(980, 35),
					new RPMPhase(1120, 40),
					new RPMPhase(1250, 45),
					new RPMPhase(1400, 50),
					new RPMPhase(1530, 55),
					new RPMPhase(1660, 60),
					new RPMPhase(1800, 65),
					new RPMPhase(1920, 70),
					new RPMPhase(2030, 75),
					new RPMPhase(2160, 80),
					new RPMPhase(2290, 85),
					new RPMPhase(2390, 90),
					new RPMPhase(2490, 95),
					new RPMPhase(2600, 100)
				}
			},
			[FanSize.Size140mm] = new RPMConfig
			{
				Phases = new RPMPhase[19]
				{
					new RPMPhase(200, 10),
					new RPMPhase(310, 15),
					new RPMPhase(420, 20),
					new RPMPhase(530, 25),
					new RPMPhase(630, 30),
					new RPMPhase(720, 35),
					new RPMPhase(820, 40),
					new RPMPhase(920, 45),
					new RPMPhase(1010, 50),
					new RPMPhase(1100, 55),
					new RPMPhase(1190, 60),
					new RPMPhase(1270, 65),
					new RPMPhase(1350, 70),
					new RPMPhase(1430, 75),
					new RPMPhase(1500, 80),
					new RPMPhase(1590, 85),
					new RPMPhase(1660, 90),
					new RPMPhase(1720, 95),
					new RPMPhase(1800, 100)
				}
			}
		},
		[PortType.LCD] = new Dictionary<FanSize, RPMConfig>
		{
			[FanSize.Size120mm] = new RPMConfig
			{
				Phases = new RPMPhase[19]
				{
					new RPMPhase(350, 10),
					new RPMPhase(430, 15),
					new RPMPhase(520, 20),
					new RPMPhase(600, 25),
					new RPMPhase(690, 30),
					new RPMPhase(770, 35),
					new RPMPhase(860, 40),
					new RPMPhase(950, 45),
					new RPMPhase(1040, 50),
					new RPMPhase(1120, 55),
					new RPMPhase(1200, 60),
					new RPMPhase(1290, 65),
					new RPMPhase(1360, 70),
					new RPMPhase(1460, 75),
					new RPMPhase(1550, 80),
					new RPMPhase(1630, 85),
					new RPMPhase(1720, 90),
					new RPMPhase(1800, 95),
					new RPMPhase(1900, 100)
				}
			},
			[FanSize.Size140mm] = new RPMConfig
			{
				Phases = new RPMPhase[19]
				{
					new RPMPhase(400, 10),
					new RPMPhase(480, 15),
					new RPMPhase(540, 20),
					new RPMPhase(600, 25),
					new RPMPhase(680, 30),
					new RPMPhase(750, 35),
					new RPMPhase(800, 40),
					new RPMPhase(880, 45),
					new RPMPhase(940, 50),
					new RPMPhase(1000, 55),
					new RPMPhase(1080, 60),
					new RPMPhase(1140, 65),
					new RPMPhase(1200, 70),
					new RPMPhase(1270, 75),
					new RPMPhase(1340, 80),
					new RPMPhase(1400, 85),
					new RPMPhase(1470, 90),
					new RPMPhase(1540, 95),
					new RPMPhase(1600, 100)
				}
			}
		}
	};

	private static readonly Dictionary<PortType, Dictionary<FanSize, int>> fanMaxSpeeds = new Dictionary<PortType, Dictionary<FanSize, int>>
	{
		[PortType.LED] = new Dictionary<FanSize, int>
		{
			[FanSize.Size120mm] = 2600,
			[FanSize.Size140mm] = 1800
		},
		[PortType.LCD] = new Dictionary<FanSize, int>
		{
			[FanSize.Size120mm] = 1900,
			[FanSize.Size140mm] = 1600
		}
	};

	private static readonly RPMPhase[] defaultRPMPhases = new RPMPhase[7]
	{
		new RPMPhase(0, 20),
		new RPMPhase(25, 20),
		new RPMPhase(40, 50),
		new RPMPhase(55, 62),
		new RPMPhase(70, 75),
		new RPMPhase(90, 100),
		new RPMPhase(100, 100)
	};

	private const int UpdateFanInfoInterval = 1000;

	private const int WriteFanSpeedInterval = 1000;

	private FanGroupRPMConfig fanGroupRPMConfigs = new FanGroupRPMConfig();

	private NonOverlappingTimer writeFanSpeedTimer;

	private readonly int?[,] currentFanPWM = new int?[4, 16];

	private readonly bool?[,] currentFanRPMSync = new bool?[4, 16];

	private static readonly Dictionary<PortType, (int, int)> portTypeCountConstants = new Dictionary<PortType, (int, int)>
	{
		[PortType.LED] = (4, 4),
		[PortType.LCD] = (1, 3)
	};

	private FirmwareVersion firmwareVersion;

	private NonOverlappingTimer updateFanInfoTimer;

	private readonly TLFanLightingGroupManager fanGroupManager = new TLFanLightingGroupManager();

	private FanGroupStatus fanGroupStatus = new FanGroupStatus();

	private FanGroupConfig fanGroupConfig = new FanGroupConfig();

	private FanGroupConfig newFanGroupConfig = new FanGroupConfig();

	private bool isFanGroupConfigChanged;

	private readonly object needUpdateFanGroupConfigsCountLocker = new object();

	private int needUpdateFanGroupConfigsCount;

	private bool isReadyUpdateFanGroupStatus;

	private DirectionConfig directionConfig = new DirectionConfig();

	private DirectionConfig newDirectionConfig = new DirectionConfig();

	private LightingConfigCollection lightingConfigCollection = new LightingConfigCollection();

	private LightingConfigCollection newLightingConfigCollection;

	private readonly bool?[] currentPortDirections = new bool?[4];

	private readonly bool?[,] currentFanTopBottomDirections = new bool?[4, 16];

	private readonly bool?[,] currentFanLeftRightDirections = new bool?[4, 16];

	public string DevicePath => device.HidDevice.DevicePath;

	public TLFanController(ITLFanDevice device)
	{
		this.device = device;
		initLightingConfigCollection();
		initFanGroupRPMConfigs();
		initFanGroupConfig();
	}

	private void AppSetting_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (!(propertyName == "IsMotherboardRPMSync"))
		{
			if (!(propertyName == "IsMotherboardARGBSync"))
			{
				return;
			}
			bool isMotherboardARGBSync = Settings.Default.IsMotherboardARGBSync;
			setMotherboardARGBSync(fanGroupStatus, isMotherboardARGBSync);
			if (!isMotherboardARGBSync)
			{
				lock (needUpdateFanGroupConfigsCountLocker)
				{
					needUpdateFanGroupConfigsCount++;
				}
			}
		}
		else
		{
			setMotherboardRPMSync(fanGroupStatus, Settings.Default.IsMotherboardRPMSync);
		}
	}

	public void Init()
	{
		int readTimeout = device.HidDevice.ReadTimeout;
		device.HidDevice.ReadTimeout = 3000;
		firmwareVersion = device.GetControllerFirmwareVersion();
		updateFanGroupStatus(out var fanGroupStatus);
		if (fanGroupStatus != null)
		{
			this.fanGroupStatus = fanGroupStatus;
		}
		device.HidDevice.ReadTimeout = readTimeout;
		startUpdateFanInfoTimer();
		startWriteFanSpeedTimer();
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
		stopWriteFanSpeedTimer();
		stopUpdateFanInfoTimer();
		Task.WaitAll(new Task[2]
		{
			Task.Run(() =>
			{
				while (!((!(writeFanSpeedTimer?.IsRunning)) ?? true))
				{
					Thread.Sleep(1000);
				}
			}),
			Task.Run(() =>
			{
				while (!((!(updateFanInfoTimer?.IsRunning)) ?? true))
				{
					Thread.Sleep(1000);
				}
			})
		}, 5000);
	}

	public void Apply(DeviceSetting<object> setting)
	{
		try
		{
			if (setting != null && Enum.IsDefined(typeof(SettingType), setting.Type) && Enum.TryParse<SettingType>(setting.Type, out var result))
			{
				switch (result)
				{
				case SettingType.Direction:
					handleSetDirection(setting);
					break;
				case SettingType.FanGroup:
					handleSetFanGroupConfig(setting);
					break;
				case SettingType.FanSpeed:
					handleSetFanSpeed(setting);
					break;
				case SettingType.Lighting:
					handleSetLightingConfig(setting);
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
		foreach (DeviceSetting<object> setting in settings)
		{
			Apply(setting);
		}
		updateFanGroupManager(fanGroupConfig);
		updateFanGroupStatus(out var fanGroupStatus);
		if (fanGroupStatus != null)
		{
			this.fanGroupStatus = fanGroupStatus;
		}
		setDirection(this.fanGroupStatus, directionConfig);
		setMotherboardRPMSync(this.fanGroupStatus, Settings.Default.IsMotherboardRPMSync);
		setLightGroup(this.fanGroupStatus, lightingConfigCollection);
		setLighting(this.fanGroupStatus, lightingConfigCollection);
		isReadyUpdateFanGroupStatus = true;
	}

	public object HandleRequestData(RequestData requestData)
	{
		if (!requestData.Query.TryGetValue("type", out var value) || !Enum.IsDefined(typeof(RequestType), value) || !Enum.TryParse<RequestType>(value, out var result))
		{
			return null;
		}
		return result switch
		{
			RequestType.FanGroupStatus => handleGetFanGroupStatus(requestData), 
			RequestType.FirmwareVersion => handleGetFirmwareVersion(requestData), 
			RequestType.Direction => handleSetDirection(requestData), 
			RequestType.FanGroupConfig => handleSetFanGroupConfig(requestData), 
			RequestType.FanSpeed => handleSetFanSpeed(requestData), 
			RequestType.LightingConfig => handleSetLightingConfig(requestData), 
			RequestType.TestLight => handleTestLight(requestData), 
			RequestType.EnterManualMode => handleEnterManualMode(requestData), 
			RequestType.ExitManualMode => handleExitManualMode(requestData), 
			_ => null, 
		};
	}

	private void discardInputPacket(int times, string tag = "")
	{
		for (int i = 0; i < times; i++)
		{
			if (device.HidDevice.Read(out var _, 64, 50) <= 0)
			{
				break;
			}
		}
	}

	private (FanType, FanSize)? getFanTypeAndSize(FirmwareVersion firmwareVersion)
	{
		if (!firmwareVersion.IsValid)
		{
			return null;
		}
		if (firmwareVersion.ModelB.StartsWith("TLFC"))
		{
			string text = firmwareVersion.ModelB.Substring(4, 2);
			if (text == "12")
			{
				return (FanType.LCDReverse, FanSize.Size120mm);
			}
			if (text == "14")
			{
				return (FanType.LCDReverse, FanSize.Size140mm);
			}
		}
		else if (firmwareVersion.ModelB.StartsWith("TLF"))
		{
			string text = firmwareVersion.ModelB.Substring(3, 2);
			if (text == "12")
			{
				return (FanType.LEDReverse, FanSize.Size120mm);
			}
			if (text == "14")
			{
				return (FanType.LEDReverse, FanSize.Size140mm);
			}
		}
		else if (firmwareVersion.ModelB.StartsWith("TLC"))
		{
			string text = firmwareVersion.ModelB.Substring(3, 2);
			if (text == "12")
			{
				return (FanType.LCD, FanSize.Size120mm);
			}
			if (text == "14")
			{
				return (FanType.LCD, FanSize.Size140mm);
			}
		}
		else if (firmwareVersion.ModelB.StartsWith("TL"))
		{
			string text = firmwareVersion.ModelB.Substring(2, 2);
			if (text == "12")
			{
				return (FanType.LED, FanSize.Size120mm);
			}
			if (text == "14")
			{
				return (FanType.LED, FanSize.Size140mm);
			}
		}
		return null;
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
		if (manualMode || isDisposed)
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

	private void initFanGroupRPMConfigs()
	{
		fanGroupRPMConfigs.Configs = new Dictionary<PortType, Dictionary<FanSize, RPMConfig>[]>[4];
		for (int i = 0; i < 4; i++)
		{
			fanGroupRPMConfigs.Configs[i] = new Dictionary<PortType, Dictionary<FanSize, RPMConfig>[]>();
			foreach (KeyValuePair<PortType, (int, int)> portTypeCountConstant in portTypeCountConstants)
			{
				int item = portTypeCountConstants[portTypeCountConstant.Key].Item1;
				fanGroupRPMConfigs.Configs[i].Add(portTypeCountConstant.Key, new Dictionary<FanSize, RPMConfig>[item]);
				for (int j = 0; j < item; j++)
				{
					fanGroupRPMConfigs.Configs[i][portTypeCountConstant.Key][j] = new Dictionary<FanSize, RPMConfig>();
					foreach (KeyValuePair<FanSize, int> item2 in fanMaxSpeeds[portTypeCountConstant.Key])
					{
						fanGroupRPMConfigs.Configs[i][portTypeCountConstant.Key][j][item2.Key] = new RPMConfig
						{
							MaxSpeed = item2.Value,
							Reference = RPMReferenceSource.CPU,
							Phases = convertPhaseDutyToRPM((RPMPhase[])defaultRPMPhases.Clone(), item2.Value)
						};
					}
				}
			}
		}
	}

	private RPMPhase[] convertPhaseDutyToRPM(RPMPhase[] dutyPhases, int maxSpeed)
	{
		RPMPhase[] array = new RPMPhase[dutyPhases.Length];
		for (int i = 0; i < dutyPhases.Length; i++)
		{
			array[i] = new RPMPhase(dutyPhases[i].Temperature, (int)Math.Round((double)dutyPhases[i].Speed / 100.0 * (double)maxSpeed));
		}
		return array;
	}

	private object handleSetFanSpeed(RequestData requestData)
	{
		try
		{
			FanGroupRPMConfig fanGroupRPMConfig = JsonSerializer.Deserialize<FanGroupRPMConfig>(requestData.Body);
			if (fanGroupRPMConfig != null)
			{
				fanGroupRPMConfigs = fanGroupRPMConfig;
				saveSetting(SettingType.FanSpeed, fanGroupRPMConfig);
			}
		}
		catch
		{
		}
		return null;
	}

	private void handleSetFanSpeed(DeviceSetting<object> setting)
	{
		if (DeviceSettingManager.TryParseData<FanGroupRPMConfig>(setting, out var data))
		{
			fanGroupRPMConfigs = data;
		}
	}

	private void startWriteFanSpeedTimer()
	{
		try
		{
			writeFanSpeedTimer = NonOverlappingTimer.StartNew(() =>
			{
				if (isReadyUpdateFanGroupStatus && !Settings.Default.IsMotherboardRPMSync)
				{
					List<(int, int, int)> values = new List<(int, int, int)>();
					int cPUTemperature = SystemInformation.Resource.CPUTemperature;
					int gPUTemperature = SystemInformation.Resource.GPUTemperature;
					FanGroupStatus fanGroupStatus = this.fanGroupStatus;
					FanGroupRPMConfig fanGroupRPMConfig = fanGroupRPMConfigs;
					for (int i = 0; i < 4; i++)
					{
						if (fanGroupStatus.ActualTotalQuantities[i] != 0)
						{
							PortType key = fanGroupStatus.PortTypes[i];
							int[] array = fanGroupStatus.FanQuantities[i];
							int num = 0;
							for (int j = 0; j < array.Length; j++)
							{
								if (array[j] != 0)
								{
									FanSize key2 = fanGroupStatus.FanSizes[i][j];
									RPMConfig rPMConfig = fanGroupRPMConfig.Configs[i][key][j][key2];
									RPMConfig rPMConfig2 = rpmToPWMConfigs[key][key2];
									int temperature;
									switch (rPMConfig.Reference)
									{
									case RPMReferenceSource.CPU:
										temperature = cPUTemperature;
										break;
									case RPMReferenceSource.GPU:
										temperature = gPUTemperature;
										break;
									default:
										num += array[j];
										continue;
									}
									int num2 = rPMConfig.CalculateSpeed(temperature);
									int num3 = ((num2 == 0) ? 1 : rPMConfig2.CalculateSpeed(num2).Constrain(12, 100));
									for (int k = 0; k < array[j]; k++)
									{
										if (currentFanPWM[i, num] != num3)
										{
											values.Add((i, num, num3));
										}
										currentFanPWM[i, num] = num3;
										num++;
									}
								}
							}
							int num4 = fanGroupStatus.ActualTotalQuantities[i] - fanGroupStatus.TotalQuantities[i];
							if (num4 > 0)
							{
								for (int l = 0; l < num4; l++)
								{
									if (currentFanPWM[i, num] != 1)
									{
										values.Add((i, num, 1));
									}
									currentFanPWM[i, num] = 1;
									num++;
								}
							}
						}
					}
					lockedActionExecution(() =>
					{
						foreach (var (port, fanIndex, pwm) in values)
						{
							device.SetFanSpeed(port, fanIndex, pwm);
						}
					});
				}
			}, 1000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	private void stopWriteFanSpeedTimer()
	{
		writeFanSpeedTimer?.Dispose();
	}

	private void setMotherboardRPMSync(FanGroupStatus fanGroupStatus, bool isSync)
	{
		List<(int, int, bool)> values = new List<(int, int, bool)>();
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < fanGroupStatus.ActualTotalQuantities[i]; j++)
			{
				if (currentFanRPMSync[i, j] != isSync)
				{
					values.Add((i, j, isSync));
				}
				currentFanRPMSync[i, j] = isSync;
			}
		}
		lockedActionExecution(() =>
		{
			foreach (var (port, fanIndex, isSync2) in values)
			{
				device.SetMBRPMSync(port, fanIndex, isSync2);
			}
		});
	}

	public int GetFanSpeed(int port, int fanIndex)
	{
		FanGroupStatus fanGroupStatus = this.fanGroupStatus;
		if (port >= 0 && port < fanGroupStatus.FanSpeeds.Length && fanIndex >= 0 && fanIndex < fanGroupStatus.FanSpeeds[port].Length)
		{
			return fanGroupStatus.FanSpeeds[port][fanIndex];
		}
		return 0;
	}

	private object handleEnterManualMode(RequestData requestData)
	{
		manualMode = true;
		return null;
	}

	private object handleExitManualMode(RequestData requestData)
	{
		manualMode = false;
		return null;
	}

	private object handleGetFanGroupStatus(RequestData requestData)
	{
		return fanGroupStatus;
	}

	private object handleGetFirmwareVersion(RequestData requestData)
	{
		return firmwareVersion;
	}

	private object handleSetFanGroupConfig(RequestData requestData)
	{
		try
		{
			FanGroupConfig fanGroupConfig = JsonSerializer.Deserialize<FanGroupConfig>(requestData.Body);
			if (fanGroupConfig != null)
			{
				PortType[] array = new PortType[2]
				{
					PortType.LED,
					PortType.LCD
				};
				foreach (PortType key in array)
				{
					for (int j = 0; j < 4; j++)
					{
						if (fanGroupConfig.GroupConfigs[key][j].GroupQuantities == null)
						{
							fanGroupConfig.GroupConfigs[key][j].GroupQuantities = (int[])this.fanGroupConfig.GroupConfigs[key][j].GroupQuantities.Clone();
						}
					}
				}
				newFanGroupConfig = fanGroupConfig;
				saveSetting(SettingType.FanGroup, fanGroupConfig);
				isFanGroupConfigChanged = true;
			}
		}
		catch
		{
		}
		return null;
	}

	private void handleSetFanGroupConfig(DeviceSetting<object> setting)
	{
		if (DeviceSettingManager.TryParseData<FanGroupConfig>(setting, out var data))
		{
			fanGroupConfig = data;
		}
	}

	private void startUpdateFanInfoTimer()
	{
		try
		{
			updateFanInfoTimer = NonOverlappingTimer.StartNew(() =>
			{
				if (isReadyUpdateFanGroupStatus && !manualMode)
				{
					bool flag = updateFanGroupStatus(out var fanGroupStatus);
					if (fanGroupStatus != null)
					{
						if (flag || needUpdateFanGroupConfigsCount > 0)
						{
							setDirection(fanGroupStatus, newDirectionConfig ?? directionConfig);
							setMotherboardRPMSync(fanGroupStatus, Settings.Default.IsMotherboardRPMSync);
							setLightGroup(this.fanGroupStatus, lightingConfigCollection, fanGroupStatus, newLightingConfigCollection ?? lightingConfigCollection);
							setLighting(fanGroupStatus, newLightingConfigCollection ?? lightingConfigCollection);
							lock (needUpdateFanGroupConfigsCountLocker)
							{
								needUpdateFanGroupConfigsCount = ((needUpdateFanGroupConfigsCount > 1) ? 1 : 0);
								if (newLightingConfigCollection != null)
								{
									lightingConfigCollection = newLightingConfigCollection;
									newLightingConfigCollection = null;
								}
								if (newDirectionConfig != null)
								{
									directionConfig = newDirectionConfig;
									newDirectionConfig = null;
								}
							}
						}
						this.fanGroupStatus = fanGroupStatus;
					}
				}
			}, 1000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	private void stopUpdateFanInfoTimer()
	{
		updateFanInfoTimer?.Dispose();
	}

	private bool updateFanGroupStatus(out FanGroupStatus fanGroupStatus)
	{
		fanGroupStatus = null;
		TLFanHandshakeInfo handshakeInfo = new TLFanHandshakeInfo();
		lockedActionExecution(() =>
		{
			handshakeInfo = device.GetHandshakeInfo();
			if (!handshakeInfo.IsValid)
			{
				discardInputPacket(5, "Handshake");
				handshakeInfo = device.GetHandshakeInfo();
			}
		});
		if (!handshakeInfo.IsValid)
		{
			return false;
		}
		FirmwareVersion[] array = new FirmwareVersion[4];
		for (int num = 0; num < 4; num++)
		{
			if (handshakeInfo.FanQuantity[num] != 0)
			{
				array[num] = getFirmwareVersion(num, 0);
			}
		}
		bool flag = false;
		if (isFanGroupConfigChanged)
		{
			flag |= updateFanGroupManager(newFanGroupConfig);
			fanGroupConfig = newFanGroupConfig;
			newFanGroupConfig = null;
		}
		fanGroupStatus = new FanGroupStatus
		{
			PortOrder = fanGroupConfig.PortOrder
		};
		for (int num2 = 0; num2 < 4; num2++)
		{
			int num3 = handshakeInfo.FanQuantity[num2];
			PortType portType = PortType.None;
			FanSize fanSize = FanSize.None;
			FanType fanType = FanType.Unknown;
			if (num3 < this.fanGroupStatus.ActualTotalQuantities[num2])
			{
				for (int num4 = num3; num4 < this.fanGroupStatus.ActualTotalQuantities[num2]; num4++)
				{
					currentFanPWM[num2, num4] = null;
					currentFanRPMSync[num2, num4] = null;
					currentFanTopBottomDirections[num2, num4] = null;
					currentFanLeftRightDirections[num2, num4] = null;
				}
				if (num3 == 0)
				{
					currentPortDirections[num2] = null;
				}
			}
			(FanType, FanSize)? fanTypeAndSize = getFanTypeAndSize(array[num2]);
			if (fanTypeAndSize.HasValue)
			{
				(fanType, fanSize) = fanTypeAndSize.Value;
				PortType portType2;
				switch (fanType)
				{
				case FanType.LED:
				case FanType.LEDReverse:
					portType2 = PortType.LED;
					break;
				case FanType.LCD:
				case FanType.LCDReverse:
					portType2 = PortType.LCD;
					break;
				default:
					portType2 = PortType.None;
					break;
				}
				portType = portType2;
			}
			flag |= fanGroupManager.ChangePortType(num2, portType);
			flag |= fanGroupManager[num2]?.UpdateActualTotalQuantity(num3) ?? false;
			fanGroupStatus.PortTypes[num2] = portType;
			if (num3 == 0)
			{
				fanGroupStatus.FanSpeeds[num2] = Array.Empty<int>();
			}
			else
			{
				fanGroupStatus.FanSpeeds[num2] = new int[num3];
				for (int num5 = 0; num5 < num3; num5++)
				{
					fanGroupStatus.FanSpeeds[num2][num5] = handshakeInfo.FanInfos[num2, num5].FanSpeed;
				}
			}
			fanGroupStatus.FanQuantities[num2] = fanGroupManager[num2]?.GroupQuantities ?? Array.Empty<int>();
			fanGroupStatus.TotalQuantities[num2] = fanGroupManager[num2]?.TotalQuantity ?? 0;
			fanGroupStatus.ActualTotalQuantities[num2] = fanGroupManager[num2]?.ActualTotalQuantity ?? 0;
			int[] array2 = fanGroupManager[num2]?.GroupQuantities;
			if (portType == PortType.None)
			{
				fanGroupStatus.FanSizes[num2] = Array.Empty<FanSize>();
				fanGroupStatus.FanTypes[num2] = Array.Empty<FanType>();
				fanGroupStatus.GroupOrders[num2] = Array.Empty<int>();
				continue;
			}
			fanGroupStatus.GroupOrders[num2] = fanGroupConfig.GroupConfigs[portType][num2].GroupOrder;
			fanGroupStatus.FanTypes[num2] = new FanType[array2.Length];
			fanGroupStatus.FanSizes[num2] = new FanSize[array2.Length];
			fanGroupStatus.FanTypes[num2][0] = fanType;
			fanGroupStatus.FanSizes[num2][0] = fanSize;
			int num6 = array2[0];
			for (int num7 = 1; num7 < array2.Length; num7++)
			{
				if (array2[num7] != 0)
				{
					fanTypeAndSize = getFanTypeAndSize(getFirmwareVersion(num2, num6));
					if (fanTypeAndSize.HasValue)
					{
						(fanType, fanSize) = fanTypeAndSize.Value;
						fanGroupStatus.FanTypes[num2][num7] = fanType;
						fanGroupStatus.FanSizes[num2][num7] = fanSize;
					}
					num6 += array2[num7];
				}
			}
		}
		if (flag)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(device.HidDevice.DevicePath);
			stringBuilder.AppendLine("Current port/group status");
			for (int num8 = 0; num8 < fanGroupStatus.PortTypes.Length; num8++)
			{
				stringBuilder.AppendLine($"Port#{num8}, type: {fanGroupStatus.PortTypes[num8]}, {fanGroupStatus.TotalQuantities[num8]}/{fanGroupStatus.ActualTotalQuantities[num8]}");
				if (fanGroupStatus.PortTypes[num8] != PortType.None)
				{
					for (int num9 = 0; num9 < fanGroupStatus.FanQuantities[num8].Length; num9++)
					{
						stringBuilder.AppendLine($"  Group#{num9}, type: {fanGroupStatus.FanTypes[num8][num9]}, size: {fanGroupStatus.FanSizes[num8][num9]}, quantity: {fanGroupStatus.FanQuantities[num8][num9]}");
					}
				}
			}
			EventLogHelper.Info(stringBuilder.ToString(), EventIDs.TLFan_FanGroupStatus);
		}
		isFanGroupConfigChanged = false;
		return flag;
	}

	private bool updateFanGroupManager(FanGroupConfig config)
	{
		bool flag = false;
		for (int i = 0; i < config.PortOrder.Length; i++)
		{
			if (fanGroupManager.Order[i] != config.PortOrder[i])
			{
				flag = true;
				break;
			}
		}
		PortType[] array = new PortType[2]
		{
			PortType.LED,
			PortType.LCD
		};
		fanGroupManager.Order = config.PortOrder;
		for (int j = 0; j < 4; j++)
		{
			PortType[] array2 = array;
			foreach (PortType portType in array2)
			{
				fanGroupManager[j, portType].Order = config.GroupConfigs[portType][j].GroupOrder;
				fanGroupManager[j, portType].GroupQuantities = config.GroupConfigs[portType][j].GroupQuantities;
				bool flag2 = fanGroupManager[j, portType].Adjust();
				flag |= flag2;
			}
		}
		return flag;
	}

	private void initFanGroupConfig()
	{
		foreach (KeyValuePair<PortType, (int, int)> portTypeCountConstant in portTypeCountConstants)
		{
			int item = portTypeCountConstants[portTypeCountConstant.Key].Item1;
			for (int i = 0; i < 4; i++)
			{
				FanGroupConfig.GroupConfig groupConfig = fanGroupConfig.GroupConfigs[portTypeCountConstant.Key][i];
				groupConfig.GroupQuantities = new int[item];
				groupConfig.GroupOrder = new int[item];
				for (int j = 0; j < item; j++)
				{
					groupConfig.GroupQuantities[j] = 3;
					groupConfig.GroupOrder[j] = j;
				}
			}
		}
	}

	private FirmwareVersion getFirmwareVersion(int port, int fanIndex)
	{
		FirmwareVersion firmwareVersion = default;
		lockedActionExecution(() =>
		{
			firmwareVersion = device.GetFanFirmwareVersion(port, fanIndex);
			if (!firmwareVersion.IsValid)
			{
				discardInputPacket(5, "FirmwareVersion");
				firmwareVersion = device.GetFanFirmwareVersion(port, fanIndex);
			}
		});
		return firmwareVersion;
	}

	private void initLightingConfigCollection()
	{
		lightingConfigCollection.LightingConfigs = new Dictionary<PortType, LightingConfigCollection.GroupElement[]>[4];
		for (int i = 0; i < 4; i++)
		{
			lightingConfigCollection.LightingConfigs[i] = new Dictionary<PortType, LightingConfigCollection.GroupElement[]>
			{
				[PortType.LED] = createDefaultLightingConfig(4),
				[PortType.LCD] = createDefaultLightingConfig(1)
			};
		}
	}

	private LightingConfigCollection.GroupElement[] createDefaultLightingConfig(int count)
	{
		LightingConfigCollection.GroupElement[] array = new LightingConfigCollection.GroupElement[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = new LightingConfigCollection.GroupElement
			{
				IsGrouping = true,
				Configs = new LightingConfig[1]
				{
					new LightingConfig
					{
						Mode = LightingMode.Meteor,
						Speed = 2,
						Brightness = 2,
						Direction = LightingDirection.RightOrClockwise,
						Colors = new Color[4]
						{
							Colors.Red,
							Colors.Blue,
							Colors.Green,
							Colors.Yellow
						}
					}
				}
			};
		}
		return array;
	}

	private object handleSetDirection(RequestData requestData)
	{
		try
		{
			DirectionConfig directionConfig = JsonSerializer.Deserialize<DirectionConfig>(requestData.Body);
			if (directionConfig != null)
			{
				newDirectionConfig = directionConfig;
				saveSetting(SettingType.Direction, directionConfig);
				lock (needUpdateFanGroupConfigsCountLocker)
				{
					needUpdateFanGroupConfigsCount++;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void handleSetDirection(DeviceSetting<object> setting)
	{
		if (DeviceSettingManager.TryParseData<DirectionConfig>(setting, out var data))
		{
			directionConfig = data;
		}
	}

	private object handleSetLightingConfig(RequestData requestData)
	{
		try
		{
			LightingConfigCollection lightingConfigCollection = JsonSerializer.Deserialize<LightingConfigCollection>(requestData.Body);
			if (lightingConfigCollection != null)
			{
				newLightingConfigCollection = lightingConfigCollection;
				saveSetting(SettingType.Lighting, lightingConfigCollection);
				lock (needUpdateFanGroupConfigsCountLocker)
				{
					needUpdateFanGroupConfigsCount++;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void handleSetLightingConfig(DeviceSetting<object> setting)
	{
		if (DeviceSettingManager.TryParseData<LightingConfigCollection>(setting, out var data))
		{
			lightingConfigCollection = data;
		}
	}

	private object handleTestLight(RequestData requestData)
	{
		try
		{
			TestLightParams testLightParams = JsonSerializer.Deserialize<TestLightParams>(requestData.Body);
			if (testLightParams == null)
			{
				return null;
			}
			testLight(fanGroupStatus, lightingConfigCollection, testLightParams);
		}
		catch
		{
		}
		return null;
		void testLight(FanGroupStatus fanGroupStatus, LightingConfigCollection configCollection, TestLightParams lightParams)
		{
			if (configCollection.IsMerged)
			{
				int groupNumber = 31;
				int[] array = calcFanIndexes(fanGroupStatus)[lightParams.Port, lightParams.GroupIndex];
				if (array != null && array.Length != 0)
				{
					List<FanGroupParams> groupParams = createFanGroupParams(topSide: true, bottomSide: true, lightParams.Port, array);
					lockedActionExecution(() =>
					{
						device.SetFanGroup(groupNumber, groupParams.ToArray());
						device.TestGroupLighting(groupNumber, lightParams.IsBottomSide);
					});
				}
			}
			else
			{
				PortType portType = fanGroupStatus.PortTypes[lightParams.Port];
				if (portType != PortType.None)
				{
					LightingConfigCollection.GroupElement groupElement = configCollection.LightingConfigs[lightParams.Port][portType][lightParams.GroupIndex];
					int groupNumber2 = calcLightGroupNumber(lightParams.Port, lightParams.GroupIndex, (!groupElement.IsGrouping || !groupElement.IsSingleSideMode) ? ((bool?)null) : new bool?(lightParams.IsBottomSide));
					lockedActionExecution(() =>
					{
						device.TestGroupLighting(groupNumber2, lightParams.IsBottomSide);
					});
				}
			}
		}
	}

	private void setDirection(FanGroupStatus fanGroupStatus, DirectionConfig config)
	{
		List<(int, bool)> portDirections = new List<(int, bool)>();
		List<(int, int, bool, bool)> fanDirections = new List<(int, int, bool, bool)>();
		for (int i = 0; i < 4; i++)
		{
			PortType key = fanGroupStatus.PortTypes[i];
			if (fanGroupStatus.TotalQuantities[i] == 0)
			{
				continue;
			}
			if (currentPortDirections[i] != config.Port[key][i])
			{
				portDirections.Add((i, config.Port[key][i]));
			}
			currentPortDirections[i] = config.Port[key][i];
			int num = 0;
			for (int j = 0; j < fanGroupStatus.FanQuantities[i].Length; j++)
			{
				bool flag = config.GroupSwapTopBottom[key][i][j];
				bool flag2 = config.GroupSwapLeftRight[key][i][j];
				for (int k = 0; k < fanGroupStatus.FanQuantities[i][j]; k++)
				{
					bool flag3 = config.FanSwapTopBottom[key][i][j][k];
					bool flag4 = config.FanSwapLeftRight[key][i][j][k];
					bool flag5 = flag ^ flag3;
					bool flag6 = flag2 ^ flag4;
					if (currentFanTopBottomDirections[i, num] != flag5 || currentFanLeftRightDirections[i, num] != flag6)
					{
						fanDirections.Add((i, num, flag5, flag6));
					}
					currentFanTopBottomDirections[i, num] = flag5;
					currentFanLeftRightDirections[i, num] = flag6;
					num++;
				}
			}
		}
		lockedActionExecution(() =>
		{
			foreach (var (port, isSwap) in portDirections)
			{
				device.SetPortDirection(port, isSwap);
			}
			foreach (var (port2, fanIndex, isSwapTopBottom, isSwapLeftRight) in fanDirections)
			{
				device.SetFanDirection(port2, fanIndex, isSwapTopBottom, isSwapLeftRight);
			}
		});
	}

	private void setLightGroup(FanGroupStatus fanGroupStatus, LightingConfigCollection lightingConfigCollection)
	{
		List<(int, FanGroupParams[])> groupParamsList = new List<(int, FanGroupParams[])>();
		int[,][] fanIndexes = calcFanIndexes(fanGroupStatus);
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				int[] array = fanIndexes[i, j];
				if (array != null && array.Length != 0)
				{
					_ = array.Length;
					_ = 1;
				}
			}
		}
		if (lightingConfigCollection.IsMerged)
		{
			if (lightingConfigCollection.MergedLightingConfigs.IsSingleSideMode)
			{
				addSingleMergedGroup();
			}
			else
			{
				addMergedGroup();
			}
		}
		else
		{
			addGroup();
		}
		lockedActionExecution(() =>
		{
			foreach (var (groupNumber, groupParams) in groupParamsList)
			{
				device.SetFanGroup(groupNumber, groupParams);
			}
		});
		void addGroup()
		{
			for (int k = 0; k < 4; k++)
			{
				if (fanGroupStatus.PortTypes[k] != PortType.None)
				{
					PortType key = fanGroupStatus.PortTypes[k];
					for (int l = 0; l < fanGroupStatus.FanQuantities[k].Length; l++)
					{
						if (fanGroupStatus.FanQuantities[k][l] != 0)
						{
							LightingConfigCollection.GroupElement groupElement = lightingConfigCollection.LightingConfigs[k][key][l];
							if (!groupElement.IsGrouping || !groupElement.IsSingleSideMode)
							{
								List<FanGroupParams> list = createFanGroupParams(topSide: true, bottomSide: true, k, fanIndexes[k, l]);
								if (list.Count > 0)
								{
									groupParamsList.Add((calcLightGroupNumber(k, l), list.ToArray()));
								}
							}
							else
							{
								List<FanGroupParams> list2 = createFanGroupParams(topSide: true, bottomSide: false, k, fanIndexes[k, l]);
								List<FanGroupParams> list3 = createFanGroupParams(topSide: false, bottomSide: true, k, fanIndexes[k, l]);
								if (list2.Count > 0)
								{
									groupParamsList.Add((calcLightGroupNumber(k, l, false), list2.ToArray()));
									groupParamsList.Add((calcLightGroupNumber(k, l, true), list3.ToArray()));
								}
							}
						}
					}
				}
			}
		}
		void addMergedGroup()
		{
			List<FanGroupParams> list = new List<FanGroupParams>();
			int[] order = fanGroupManager.Order;
			foreach (int num in order)
			{
				if (fanGroupStatus.PortTypes[num] != PortType.None)
				{
					int[] order2 = fanGroupManager[num].Order;
					foreach (int num2 in order2)
					{
						if (fanGroupManager[num].GroupQuantities[num2] != 0)
						{
							list.AddRange(createFanGroupParams(topSide: true, bottomSide: true, num, fanIndexes[num, num2]));
						}
					}
				}
			}
			if (list.Count != 0)
			{
				groupParamsList.Add((0, list.ToArray()));
			}
		}
		void addSingleMergedGroup()
		{
			List<FanGroupParams> list = new List<FanGroupParams>();
			List<FanGroupParams> list2 = new List<FanGroupParams>();
			int[] order = fanGroupManager.Order;
			foreach (int num in order)
			{
				if (fanGroupStatus.PortTypes[num] != PortType.None)
				{
					int[] order2 = fanGroupManager[num].Order;
					foreach (int num2 in order2)
					{
						if (fanGroupManager[num].GroupQuantities[num2] != 0)
						{
							list.AddRange(createFanGroupParams(topSide: true, bottomSide: false, num, fanIndexes[num, num2]));
							list2.AddRange(createFanGroupParams(topSide: false, bottomSide: true, num, fanIndexes[num, num2]));
						}
					}
				}
			}
			if (list.Count != 0)
			{
				groupParamsList.Add((0, list.ToArray()));
				groupParamsList.Add((1, list2.ToArray()));
			}
		}
	}

	private void setLightGroup(FanGroupStatus oldFanGroupStatus, LightingConfigCollection oldLightingConfigCollection, FanGroupStatus newFanGroupStatus, LightingConfigCollection newLightingConfigCollection)
	{
		if (oldLightingConfigCollection.IsMerged != newLightingConfigCollection.IsMerged)
		{
			setLightGroup(newFanGroupStatus, newLightingConfigCollection);
			return;
		}
		if (newLightingConfigCollection.IsMerged)
		{
			foreach (Func<bool> item in new List<Func<bool>>
			{
				() => oldLightingConfigCollection.MergedLightingConfigs.IsSingleSideMode != newLightingConfigCollection.MergedLightingConfigs.IsSingleSideMode,
				() =>
				{
					for (int i = 0; i < 4; i++)
					{
						if (oldFanGroupStatus.PortTypes[i] != newFanGroupStatus.PortTypes[i])
						{
							return true;
						}
					}
					return false;
				},
				() =>
				{
					for (int i = 0; i < 4; i++)
					{
						if (oldFanGroupStatus.PortOrder[i] != newFanGroupStatus.PortOrder[i])
						{
							return true;
						}
					}
					return false;
				},
				() =>
				{
					for (int i = 0; i < 4; i++)
					{
						if (oldFanGroupStatus.GroupOrders[i].Length != newFanGroupStatus.GroupOrders[i].Length)
						{
							return true;
						}
						for (int j = 0; j < oldFanGroupStatus.GroupOrders[i].Length; j++)
						{
							if (oldFanGroupStatus.GroupOrders[i][j] != newFanGroupStatus.GroupOrders[i][j])
							{
								return true;
							}
						}
					}
					return false;
				},
				() =>
				{
					for (int i = 0; i < 4; i++)
					{
						if (oldFanGroupStatus.FanQuantities[i].Length != newFanGroupStatus.FanQuantities[i].Length)
						{
							return true;
						}
						for (int j = 0; j < oldFanGroupStatus.FanQuantities[i].Length; j++)
						{
							if (oldFanGroupStatus.FanQuantities[i][j] != newFanGroupStatus.FanQuantities[i][j])
							{
								return true;
							}
						}
					}
					return false;
				}
			})
			{
				if (item())
				{
					setLightGroup(newFanGroupStatus, newLightingConfigCollection);
					break;
				}
			}
			return;
		}
		List<(int, FanGroupParams[])> groupParamsList = new List<(int, FanGroupParams[])>();
		for (int num = 0; num < 4; num++)
		{
			PortType portType = newFanGroupStatus.PortTypes[num];
			if (portType == PortType.None)
			{
				continue;
			}
			if (oldFanGroupStatus.PortTypes[num] != newFanGroupStatus.PortTypes[num])
			{
				int num2 = 0;
				for (int num3 = 0; num3 < newFanGroupStatus.FanQuantities[num].Length; num3++)
				{
					LightingConfigCollection.GroupElement groupElement = newLightingConfigCollection.LightingConfigs[num][portType][num3];
					int num4 = newFanGroupStatus.FanQuantities[num][num3];
					if (!groupElement.IsGrouping || !groupElement.IsSingleSideMode)
					{
						List<FanGroupParams> list = createFanGroupParams(topSide: true, bottomSide: true, num, num2, num4);
						if (list.Count > 0)
						{
							groupParamsList.Add((calcLightGroupNumber(num, num3), list.ToArray()));
						}
					}
					else
					{
						List<FanGroupParams> list2 = createFanGroupParams(topSide: true, bottomSide: false, num, num2, num4);
						List<FanGroupParams> list3 = createFanGroupParams(topSide: false, bottomSide: true, num, num2, num4);
						if (list2.Count > 0)
						{
							groupParamsList.Add((calcLightGroupNumber(num, num3, false), list2.ToArray()));
							groupParamsList.Add((calcLightGroupNumber(num, num3, true), list3.ToArray()));
						}
					}
					num2 += num4;
				}
				continue;
			}
			int num5 = int.MinValue;
			for (int num6 = 0; num6 < newFanGroupStatus.FanQuantities[num].Length; num6++)
			{
				int num7 = oldFanGroupStatus.FanQuantities[num][num6];
				int num8 = newFanGroupStatus.FanQuantities[num][num6];
				if (num7 != num8)
				{
					num5 = num6;
					break;
				}
			}
			if (num5 == int.MinValue)
			{
				num5 = newFanGroupStatus.FanQuantities[num].Length;
			}
			int num9 = 0;
			for (int num10 = 0; num10 < newFanGroupStatus.FanQuantities[num].Length; num10++)
			{
				LightingConfigCollection.GroupElement groupElement2 = oldLightingConfigCollection.LightingConfigs[num][portType][num10];
				LightingConfigCollection.GroupElement groupElement3 = newLightingConfigCollection.LightingConfigs[num][portType][num10];
				int num11 = newFanGroupStatus.FanQuantities[num][num10];
				if (num10 < num5 && groupElement2.IsSingleSideMode == groupElement3.IsSingleSideMode)
				{
					num9 += num11;
					continue;
				}
				if (groupElement3.IsSingleSideMode)
				{
					List<FanGroupParams> list4 = createFanGroupParams(topSide: true, bottomSide: false, num, num9, num11);
					List<FanGroupParams> list5 = createFanGroupParams(topSide: false, bottomSide: true, num, num9, num11);
					if (list4.Count > 0)
					{
						groupParamsList.Add((calcLightGroupNumber(num, num10, false), list4.ToArray()));
						groupParamsList.Add((calcLightGroupNumber(num, num10, true), list5.ToArray()));
					}
				}
				else
				{
					List<FanGroupParams> list6 = createFanGroupParams(topSide: true, bottomSide: true, num, num9, num11);
					if (list6.Count > 0)
					{
						groupParamsList.Add((calcLightGroupNumber(num, num10), list6.ToArray()));
					}
				}
				num9 += num11;
			}
		}
		lockedActionExecution(() =>
		{
			foreach (var (groupNumber, groupParams) in groupParamsList)
			{
				device.SetFanGroup(groupNumber, groupParams);
			}
		});
	}

	private void setLighting(FanGroupStatus fanGroupStatus, LightingConfigCollection lightingConfigCollection)
	{
		if (Settings.Default.IsMotherboardARGBSync)
		{
			return;
		}
		List<(int, int, LightingConfig, bool)> fanLightingConfigs = new List<(int, int, LightingConfig, bool)>();
		List<(int, LightingConfig, bool)> groupLightingConfigs = new List<(int, LightingConfig, bool)>();
		if (lightingConfigCollection.IsMerged)
		{
			setMergedLightConfig();
		}
		else
		{
			setLightConfig();
		}
		for (int i = 0; i < 4; i++)
		{
			if (fanGroupStatus.PortTypes[i] == PortType.None)
			{
				continue;
			}
			int num = fanGroupStatus.TotalQuantities[i];
			int num2 = fanGroupStatus.ActualTotalQuantities[i];
			if (num < num2)
			{
				for (int j = num; j < num2; j++)
				{
					fanLightingConfigs.Add((i, j, null, true));
				}
			}
		}
		groupLightingConfigs.Reverse();
		lockedActionExecution(() =>
		{
			foreach (var (groupNumber, config, disable) in groupLightingConfigs)
			{
				device.SetGroupLight(groupNumber, config, disable);
			}
			foreach (var (port, fanIndex, config2, disable2) in fanLightingConfigs)
			{
				device.SetFanLight(port, fanIndex, config2, isSync: false, disable2);
			}
		});
		void setLightConfig()
		{
			for (int k = 0; k < 4; k++)
			{
				PortType portType = fanGroupStatus.PortTypes[k];
				if (portType != PortType.None)
				{
					int num3 = 0;
					LightingConfigCollection.GroupElement[] array = lightingConfigCollection.LightingConfigs[k][portType];
					for (int l = 0; l < array.Length; l++)
					{
						int num4 = fanGroupStatus.FanQuantities[k][l];
						if (num4 != 0)
						{
							LightingConfigCollection.GroupElement groupElement = array[l];
							if (!groupElement.IsGrouping)
							{
								for (int m = 0; m < num4; m++)
								{
									LightingConfig lightingConfig = groupElement.Configs[m];
									int item = num3 + m;
									fanLightingConfigs.Add((k, item, lightingConfig, lightingConfig.Brightness == 0));
								}
							}
							else if (groupElement.IsSingleSideMode)
							{
								groupLightingConfigs.Add((calcLightGroupNumber(k, l, false), groupElement.Configs[0], groupElement.Configs[0].Brightness == 0));
								groupLightingConfigs.Add((calcLightGroupNumber(k, l, true), groupElement.Configs[1], groupElement.Configs[1].Brightness == 0));
							}
							else
							{
								groupLightingConfigs.Add((calcLightGroupNumber(k, l), groupElement.Configs[0], groupElement.Configs[0].Brightness == 0));
							}
							num3 += num4;
						}
					}
				}
			}
		}
		void setMergedLightConfig()
		{
			LightingConfigCollection.GroupElement mergedLightingConfigs = lightingConfigCollection.MergedLightingConfigs;
			if (mergedLightingConfigs.IsSingleSideMode)
			{
				groupLightingConfigs.Add((0, mergedLightingConfigs.Configs[0], mergedLightingConfigs.Configs[0].Brightness == 0));
				groupLightingConfigs.Add((1, mergedLightingConfigs.Configs[1], mergedLightingConfigs.Configs[1].Brightness == 0));
			}
			else
			{
				groupLightingConfigs.Add((0, mergedLightingConfigs.Configs[0], mergedLightingConfigs.Configs[0].Brightness == 0));
			}
		}
	}

	private void setMotherboardARGBSync(FanGroupStatus fanGroupStatus, bool isSync)
	{
		lockedActionExecution(() =>
		{
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < fanGroupStatus.ActualTotalQuantities[i]; j++)
				{
					device.SetFanLight(i, j, null, isSync, !isSync);
				}
			}
		});
	}

	private int calcLightGroupNumber(int port, int groupIndex, bool? isBottomSide = null)
	{
		int num = (port * 4 + groupIndex) * 2;
		if (isBottomSide ?? false)
		{
			num++;
		}
		return num;
	}

	private int[,][] calcFanIndexes(FanGroupStatus fanGroupStatus)
	{
		int[,][] array = new int[4, 4][];
		for (int i = 0; i < 4; i++)
		{
			if (fanGroupStatus.PortTypes[i] == PortType.None)
			{
				continue;
			}
			int num = 0;
			for (int j = 0; j < fanGroupStatus.FanQuantities[i].Length; j++)
			{
				int num2 = fanGroupStatus.FanQuantities[i][j];
				if (num2 != 0)
				{
					array[i, j] = new int[num2];
					for (int k = 0; k < num2; k++)
					{
						array[i, j][k] = num + k;
					}
					num += num2;
				}
			}
		}
		return array;
	}

	private List<FanGroupParams> createFanGroupParams(bool topSide, bool bottomSide, int port, int fanStartIndex, int quantity)
	{
		return createFanGroupParams(topSide, bottomSide, port, getEnumerator());
		IEnumerable<int> getEnumerator()
		{
			int i = 0;
			while (i < quantity)
			{
				yield return fanStartIndex + i;
				int num = i + 1;
				i = num;
			}
		}
	}

	private List<FanGroupParams> createFanGroupParams(bool topSide, bool bottomSide, int port, IEnumerable<int> fanIndexes)
	{
		List<FanGroupParams> list = new List<FanGroupParams>();
		foreach (int fanIndex in fanIndexes)
		{
			list.Add(new FanGroupParams
			{
				TopSide = topSide,
				BottomSide = bottomSide,
				Port = port,
				FanIndex = fanIndex
			});
		}
		return list;
	}
}
