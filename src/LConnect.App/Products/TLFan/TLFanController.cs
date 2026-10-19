using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class TLFanController : Controller
{
	public delegate void FanGroupStatusChangedEventHandler(object sender, FanGroupStatusEventArgs e);

	private const int UpdateFanGroupStatusInterval = 1000;

	private const int CheckFirmwareVersionInterval = 2000;

	private NonOverlappingTimer updateFanGroupStatusTimer;

	private FanGroupStatus currentFanGroupStatus = new FanGroupStatus();

	private CancellationTokenSource checkFirmwareVersionTaskToken;

	private FirmwareVersion firmwareVersion;

	public override Version Version => firmwareVersion.GetVersion();

	public FirmwareVersion FirmwareVersion
	{
		get
		{
			return firmwareVersion;
		}
		private set
		{
			firmwareVersion = value;
			OnPropertyChanged("FirmwareVersion");
			OnPropertyChanged("Version");
		}
	}

	public TLFanProfile Profile { get; set; }

	public event FanGroupStatusChangedEventHandler FanGroupStatusChanged;

	public TLFanController(ControllerType type, string devicePath, USBDeviceID deviceID)
		: base(type, devicePath, deviceID)
	{
		if (loadProfile<TLFanProfile>(out var profile))
		{
			rebindProfile(profile);
			Profile = profile;
		}
		else
		{
			Profile = new TLFanProfile();
		}
	}

	public override void Init()
	{
		startUpdateFanGroupStatus();
		startFirmwareVersionChecker();
	}

	public override void Dispose()
	{
		stopFirmwareVersionChecker();
		stopUpdateFanGroupStatus();
	}

	public override void ReloadProfile()
	{
		if (loadProfile<TLFanProfile>(out var profile))
		{
			rebindProfile(profile);
			Profile = profile;
			onProfileReloaded();
		}
	}

	public override void SaveProfile()
	{
		saveProfile(Profile);
	}

	public void SendDirectionConfigRequest()
	{
		DirectionConfig directionConfig = new DirectionConfig();
		TLFanPortProfile[] portProfiles = Profile.PortProfiles;
		foreach (TLFanPortProfile tLFanPortProfile in portProfiles)
		{
			directionConfig.Port[PortType.LED][tLFanPortProfile.Port] = tLFanPortProfile.LEDIsReverseDirection;
			directionConfig.Port[PortType.LCD][tLFanPortProfile.Port] = tLFanPortProfile.LCDIsReverseDirection;
			setup(directionConfig, tLFanPortProfile.Port, PortType.LED, tLFanPortProfile.LEDGroupProfiles);
			setup(directionConfig, tLFanPortProfile.Port, PortType.LCD, tLFanPortProfile.LCDGroupProfiles);
		}
		sendControllerRequest(RequestType.Direction, directionConfig);
		static void setup(DirectionConfig config, int port, PortType portType, TLFanGroupProfile[] groupProfiles)
		{
			foreach (TLFanGroupProfile tLFanGroupProfile in groupProfiles)
			{
				config.GroupSwapTopBottom[portType][port][tLFanGroupProfile.Index] = tLFanGroupProfile.IsSwapTopBottomDirection;
				config.GroupSwapLeftRight[portType][port][tLFanGroupProfile.Index] = tLFanGroupProfile.IsSwapLeftRightDirection;
				TLFanFanProfile[] fanProfiles = tLFanGroupProfile.FanProfiles;
				foreach (TLFanFanProfile tLFanFanProfile in fanProfiles)
				{
					config.FanSwapTopBottom[portType][port][tLFanGroupProfile.Index][tLFanFanProfile.Index] = tLFanFanProfile.IsSwapTopBottomDirection;
					config.FanSwapLeftRight[portType][port][tLFanGroupProfile.Index][tLFanFanProfile.Index] = tLFanFanProfile.IsSwapLeftRightDirection;
				}
			}
		}
	}

	public void SendFanGroupConfigRequest((int, PortType, int[])[] groupQuantityConfigs)
	{
		FanGroupConfig fanGroupConfig = new FanGroupConfig
		{
			PortOrder = Profile.PortOrder
		};
		TLFanPortProfile[] portProfiles = Profile.PortProfiles;
		foreach (TLFanPortProfile tLFanPortProfile in portProfiles)
		{
			for (int j = 0; j < tLFanPortProfile.LEDGroupProfiles.Length; j++)
			{
				fanGroupConfig.GroupConfigs[PortType.LED][tLFanPortProfile.Port].GroupOrder = tLFanPortProfile.LEDGroupOrder;
			}
			for (int k = 0; k < tLFanPortProfile.LCDGroupProfiles.Length; k++)
			{
				fanGroupConfig.GroupConfigs[PortType.LCD][tLFanPortProfile.Port].GroupOrder = tLFanPortProfile.LCDGroupOrder;
			}
		}
		if (groupQuantityConfigs != null)
		{
			for (int i = 0; i < groupQuantityConfigs.Length; i++)
			{
				var (num, key, groupQuantities) = groupQuantityConfigs[i];
				fanGroupConfig.GroupConfigs[key][num].GroupQuantities = groupQuantities;
			}
		}
		sendControllerRequest(RequestType.FanGroupConfig, fanGroupConfig);
	}

	public void SendFanSpeedConfigRequest()
	{
		FanGroupRPMConfig fanGroupRPMConfig = new FanGroupRPMConfig
		{
			Configs = new Dictionary<PortType, Dictionary<FanSize, RPMConfig>[]>[4]
		};
		for (int i = 0; i < 4; i++)
		{
			fanGroupRPMConfig.Configs[i] = new Dictionary<PortType, Dictionary<FanSize, RPMConfig>[]>
			{
				[PortType.LED] = createRPMConfigs(Profile.PortProfiles[i].LEDGroupProfiles),
				[PortType.LCD] = createRPMConfigs(Profile.PortProfiles[i].LCDGroupProfiles)
			};
		}
		sendControllerRequest(RequestType.FanSpeed, fanGroupRPMConfig);
		static RPMConfig createRPMConfig(RPMProfile rpmProfile, FanSize fanSize)
		{
			RPMPhaseKey key = new RPMPhaseKey(fanSize, rpmProfile.RPMReferenceSource, rpmProfile.ControlType);
			RPMPhaseInfo rPMPhaseInfo = rpmProfile.PhaseInfos[key];
			return new RPMConfig
			{
				MaxSpeed = rPMPhaseInfo.MaxSpeed,
				Reference = rpmProfile.RPMReferenceSource,
				Phases = rPMPhaseInfo.ToRPMConfigPhases(rpmProfile.IsStartStop)
			};
		}
		static Dictionary<FanSize, RPMConfig>[] createRPMConfigs(TLFanGroupProfile[] groupProfiles)
		{
			Dictionary<FanSize, RPMConfig>[] array = new Dictionary<FanSize, RPMConfig>[groupProfiles.Length];
			for (int j = 0; j < groupProfiles.Length; j++)
			{
				array[j] = new Dictionary<FanSize, RPMConfig>();
				RPMSetting rPMSetting = groupProfiles[j].RPMSetting;
				FanSize[] supportedFanSize = rPMSetting.SupportedFanSize;
				foreach (FanSize fanSize in supportedFanSize)
				{
					array[j].Add(fanSize, createRPMConfig(rPMSetting.Profiles[rPMSetting.Mode], fanSize));
				}
			}
			return array;
		}
	}

	public void SendLightingConfigRequest()
	{
		LightingConfigCollection data = new LightingConfigCollection
		{
			IsMerged = Profile.IsMerged,
			MergedLightingConfigs = getMergedLightingConfigs(Profile),
			LightingConfigs = getLightingConfigs(Profile)
		};
		sendControllerRequest(RequestType.LightingConfig, data);
		static LightingConfigCollection.GroupElement adjustGroupElement(LightingConfigCollection.GroupElement groupElement)
		{
			if (groupElement.IsGrouping && !groupElement.IsSingleSideMode && (groupElement.Configs[0].Mode == LightingMode.StaticColor || groupElement.Configs[0].Mode == LightingMode.StaticColor_Merge))
			{
				LightingConfig lightingConfig = groupElement.Configs[0].Clone();
				LightingConfig lightingConfig2 = groupElement.Configs[0].Clone();
				lightingConfig.Colors = new Color[1] { lightingConfig.Colors[0] };
				lightingConfig2.Colors = new Color[1] { lightingConfig2.Colors[1] };
				groupElement.IsSingleSideMode = true;
				groupElement.Configs = new LightingConfig[2] { lightingConfig, lightingConfig2 };
			}
			return groupElement;
		}
		static LightingConfig convertToLightConfig(TLFanLightingSetting setting)
		{
			return new LightingConfig
			{
				Mode = setting.Mode,
				Speed = (byte)(setting.Speed.GetValueOrDefault() / 25),
				Brightness = (byte)(setting.Brightness.GetValueOrDefault() / 25),
				Direction = setting.Direction.GetValueOrDefault(),
				Colors = (setting.Colors ?? Array.Empty<Color>())
			};
		}
		static LightingConfigCollection.GroupElement getGroupLightingConfig(TLFanGroupProfile groupProfile)
		{
			return adjustGroupElement(new LightingConfigCollection.GroupElement
			{
				IsGrouping = groupProfile.IsGrouping,
				IsSingleSideMode = groupProfile.IsSingleSideMode,
				IsSyncSideMode = groupProfile.IsSyncSideMode,
				IsSyncBottomSide = groupProfile.IsSyncBottomSide,
				Configs = getConfigs()
			});
			LightingConfig[] getConfigs()
			{
				if (groupProfile.IsGrouping)
				{
					if (groupProfile.IsSingleSideMode)
					{
						if (groupProfile.IsSyncSideMode)
						{
							if (!groupProfile.IsSyncBottomSide)
							{
								return new LightingConfig[2]
								{
									convertToLightConfig(groupProfile.LightingSettingsTop[groupProfile.LightingModeTop]),
									convertToLightConfig(groupProfile.LightingSettingsTop[groupProfile.LightingModeTop])
								};
							}
							return new LightingConfig[2]
							{
								convertToLightConfig(groupProfile.LightingSettingsBottom[groupProfile.LightingModeBottom]),
								convertToLightConfig(groupProfile.LightingSettingsBottom[groupProfile.LightingModeBottom])
							};
						}
						return new LightingConfig[2]
						{
							convertToLightConfig(groupProfile.LightingSettingsTop[groupProfile.LightingModeTop]),
							convertToLightConfig(groupProfile.LightingSettingsBottom[groupProfile.LightingModeBottom])
						};
					}
					return new LightingConfig[1] { convertToLightConfig(groupProfile.LightingSettings[groupProfile.LightingMode]) };
				}
				LightingConfig[] array = new LightingConfig[groupProfile.FanProfiles.Length];
				for (int i = 0; i < array.Length; i++)
				{
					TLFanFanProfile tLFanFanProfile = groupProfile.FanProfiles[i];
					array[i] = convertToLightConfig(tLFanFanProfile.LightingSettings[tLFanFanProfile.LightingMode]);
				}
				return array;
			}
		}
		static LightingConfigCollection.GroupElement[] getGroupLightingConfigs(TLFanGroupProfile[] groupProfiles)
		{
			LightingConfigCollection.GroupElement[] array = new LightingConfigCollection.GroupElement[groupProfiles.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = getGroupLightingConfig(groupProfiles[i]);
			}
			return array;
		}
		static Dictionary<PortType, LightingConfigCollection.GroupElement[]>[] getLightingConfigs(TLFanProfile profile)
		{
			Dictionary<PortType, LightingConfigCollection.GroupElement[]>[] array = new Dictionary<PortType, LightingConfigCollection.GroupElement[]>[profile.PortProfiles.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new Dictionary<PortType, LightingConfigCollection.GroupElement[]>
				{
					[PortType.LED] = getGroupLightingConfigs(profile.PortProfiles[i].LEDGroupProfiles),
					[PortType.LCD] = getGroupLightingConfigs(profile.PortProfiles[i].LCDGroupProfiles)
				};
			}
			return array;
		}
		static LightingConfigCollection.GroupElement getMergedLightingConfigs(TLFanProfile profile)
		{
			return adjustGroupElement(new LightingConfigCollection.GroupElement
			{
				IsGrouping = true,
				IsSingleSideMode = profile.IsSingleSideMode,
				IsSyncSideMode = profile.IsSyncSideMode,
				IsSyncBottomSide = profile.IsSyncBottomSide,
				Configs = getConfigs()
			});
			LightingConfig[] getConfigs()
			{
				if (profile.IsSingleSideMode)
				{
					if (profile.IsSyncSideMode)
					{
						if (!profile.IsSyncBottomSide)
						{
							return new LightingConfig[2]
							{
								convertToLightConfig(profile.LightingSettingsTop[profile.LightingModeTop]),
								convertToLightConfig(profile.LightingSettingsTop[profile.LightingModeTop])
							};
						}
						return new LightingConfig[2]
						{
							convertToLightConfig(profile.LightingSettingsBottom[profile.LightingModeBottom]),
							convertToLightConfig(profile.LightingSettingsBottom[profile.LightingModeBottom])
						};
					}
					return new LightingConfig[2]
					{
						convertToLightConfig(profile.LightingSettingsTop[profile.LightingModeTop]),
						convertToLightConfig(profile.LightingSettingsBottom[profile.LightingModeBottom])
					};
				}
				return new LightingConfig[1] { convertToLightConfig(profile.LightingSettings[profile.LightingMode]) };
			}
		}
	}

	public void SendTestLightRequest(int port, int groupIndex, bool isBottomSide)
	{
		TestLightParams data = new TestLightParams
		{
			Port = port,
			GroupIndex = groupIndex,
			IsBottomSide = isBottomSide
		};
		sendControllerRequest(RequestType.TestLight, data);
	}

	public void SendEnterManualMode()
	{
		sendControllerRequest(RequestType.EnterManualMode, null);
	}

	public void SendExitManualMode()
	{
		sendControllerRequest(RequestType.ExitManualMode, null);
	}

	private void startUpdateFanGroupStatus()
	{
		try
		{
			updateFanGroupStatusTimer = NonOverlappingTimer.StartNew(() =>
			{
				string responseBody = sendControllerRequest(RequestType.FanGroupStatus, null);
				if (tryGetResponseData<FanGroupStatus>(responseBody, out var data))
				{
					bool flag = false;
					FanGroupStatusEventArgs e = new FanGroupStatusEventArgs();
					for (int i = 0; i < data.PortTypes.Length; i++)
					{
						e.PortStatus[i].PortTypeBefore = currentFanGroupStatus.PortTypes[i];
						e.PortStatus[i].PortTypeAfter = data.PortTypes[i];
						e.PortStatus[i].ConnectedFanQuantityBefore = currentFanGroupStatus.ActualTotalQuantities[i];
						e.PortStatus[i].ConnectedFanQuantityAfter = data.ActualTotalQuantities[i];
						Profile.PortProfiles[i].PortType = data.PortTypes[i];
						Profile.PortProfiles[i].ConnectedFanQuantity = data.ActualTotalQuantities[i];
						TLFanGroupProfile[] array = null;
						if (data.PortTypes[i] == PortType.LED)
						{
							array = Profile.PortProfiles[i].LEDGroupProfiles;
						}
						else if (data.PortTypes[i] == PortType.LCD)
						{
							array = Profile.PortProfiles[i].LCDGroupProfiles;
						}
						if (array != null)
						{
							int num = 0;
							for (int j = 0; j < array.Length; j++)
							{
								int num2 = data.FanQuantities[i][j];
								array[j].FanQuantity = num2;
								array[j].FanSpeed = ((num2 != 0) ? data.FanSpeeds[i][num] : 0);
								foreach (RPMProfile value in array[j].RPMSetting.Profiles.Values)
								{
									value.FanSize = data.FanSizes[i][j];
								}
								num += num2;
							}
						}
						if (currentFanGroupStatus.PortTypes[i] != data.PortTypes[i] || currentFanGroupStatus.FanQuantities[i] == null)
						{
							flag = true;
						}
						else
						{
							for (int k = 0; k < currentFanGroupStatus.FanQuantities[i].Length; k++)
							{
								if (currentFanGroupStatus.FanQuantities[i][k] != data.FanQuantities[i][k])
								{
									flag = true;
									break;
								}
							}
						}
					}
					currentFanGroupStatus = data;
					if (flag)
					{
						FanGroupStatusChanged?.Invoke(this, e);
					}
				}
			}, 1000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	private void stopUpdateFanGroupStatus()
	{
		updateFanGroupStatusTimer?.Dispose();
	}

	private void startFirmwareVersionChecker()
	{
		if (FirmwareVersion.IsValid)
		{
			return;
		}
		try
		{
			checkFirmwareVersionTaskToken = new CancellationTokenSource();
			Task.Run(() =>
			{
				try
				{
					while (!checkFirmwareVersionTaskToken.IsCancellationRequested)
					{
						DateTime now = DateTime.Now;
						try
						{
							string responseBody = sendControllerRequest(RequestType.FirmwareVersion, null);
							if (!tryGetResponseData<FirmwareVersion>(responseBody, out var data) || !data.IsValid)
							{
								break;
							}
							FirmwareVersion = data;
							Logger.Instance.Info("TL F/W Version: " + data.SoftwareVersion);
						}
						catch (Exception)
						{
						}
						if (FirmwareVersion.IsValid)
						{
							break;
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

	private bool tryGetResponseData<T>(string responseBody, out T data)
	{
		data = default;
		try
		{
			if (string.IsNullOrEmpty(responseBody))
			{
				return false;
			}
			data = JsonSerializer.Deserialize<T>(responseBody);
			return true;
		}
		catch (Exception)
		{
		}
		return false;
	}

	private void rebindProfile(TLFanProfile profile)
	{
		TLFanPortProfile[] portProfiles = profile.PortProfiles;
		foreach (TLFanPortProfile tLFanPortProfile in portProfiles)
		{
			TLFanGroupProfile[] lEDGroupProfiles = tLFanPortProfile.LEDGroupProfiles;
			foreach (TLFanGroupProfile groupProfile in lEDGroupProfiles)
			{
				rebindGroupProfile(tLFanPortProfile, groupProfile);
			}
			lEDGroupProfiles = tLFanPortProfile.LCDGroupProfiles;
			foreach (TLFanGroupProfile groupProfile2 in lEDGroupProfiles)
			{
				rebindGroupProfile(tLFanPortProfile, groupProfile2);
			}
		}
		static void rebindGroupProfile(TLFanPortProfile portProfile, TLFanGroupProfile tLFanGroupProfile)
		{
			tLFanGroupProfile.ParentPortProfile = portProfile;
			TLFanFanProfile[] fanProfiles = tLFanGroupProfile.FanProfiles;
			for (int k = 0; k < fanProfiles.Length; k++)
			{
				fanProfiles[k].ParentGroupProfile = tLFanGroupProfile;
			}
		}
	}
}
