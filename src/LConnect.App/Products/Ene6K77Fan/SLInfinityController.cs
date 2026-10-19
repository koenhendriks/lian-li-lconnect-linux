using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products;
using LConnectCore.Products.Ene6K77Fan;

namespace LConnect3cs.Products.Ene6K77Fan;

internal class SLInfinityController : Ene6K77FanController
{
	private static readonly Dictionary<LightingBrightness, int> LightingBrightnessLookup = new Dictionary<LightingBrightness, int>
	{
		[LightingBrightness.Highest] = 100,
		[LightingBrightness.Higher] = 75,
		[LightingBrightness.Normal] = 50,
		[LightingBrightness.Lower] = 25,
		[LightingBrightness.Lowest] = 0
	};

	private static readonly HashSet<LightingMode> NeedAdjustColorLightingModes = new HashSet<LightingMode>
	{
		LightingMode.StaticColor,
		LightingMode.StaticColor_Inner,
		LightingMode.StaticColor_Outer
	};

	private const int CheckFirmwareVersionInterval = 2000;

	private const int GetFanSpeedInterval = 1000;

	private NonOverlappingTimer getFanSpeedTimer;

	private CancellationTokenSource checkFirmwareVersionTaskToken;

	private SLInfinityFirmwareVersion firmwareVersion = new SLInfinityFirmwareVersion();

	public override Version Version => firmwareVersion.GetVersion();

	public SLInfinityFirmwareVersion FirmwareVersion
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

	public SLInfinityProfile Profile { get; set; }

	public SLInfinityController(ControllerType type, string devicePath, USBDeviceID deviceId)
		: base(type, devicePath, deviceId)
	{
		if (loadProfile<SLInfinityProfile>(out var profile))
		{
			Profile = profile;
		}
		else
		{
			Profile = new SLInfinityProfile();
		}
	}

	public override void Init()
	{
		startGetFanSpeedTimer();
		startFirmwareVersionChecker();
		Task.Run(() =>
		{
			Thread.Sleep(1000);
			sendAllConfigRequest();
		});
	}

	public override void Dispose()
	{
		stopFirmwareVersionChecker();
		stopGetFanSpeedTimer();
	}

	public override void SaveProfile()
	{
		saveProfile(Profile);
	}

	public override void ReloadProfile()
	{
		if (loadProfile<SLInfinityProfile>(out var profile))
		{
			Profile = profile;
			Task.Run((Action)sendAllConfigRequest);
			onProfileReloaded();
		}
	}

	public override void SendPing(int groupIndex)
	{
		if (groupIndex >= 0 && groupIndex <= 4)
		{
			sendPingRequest(groupIndex);
		}
	}

	public override void SendFanQuantity()
	{
		int[] array = new int[Profile.SubProfiles.Length];
		Ene6K77FanSubProfile[] subProfiles = Profile.SubProfiles;
		foreach (Ene6K77FanSubProfile ene6K77FanSubProfile in subProfiles)
		{
			array[ene6K77FanSubProfile.GroupIndex] = ene6K77FanSubProfile.FanQuantity;
		}
		sendSetFanQuantityRequest(array);
	}

	public override void SendFanConfig()
	{
		List<EneRPMConfig> list = new List<EneRPMConfig>();
		Ene6K77FanSubProfile[] subProfiles = Profile.SubProfiles;
		foreach (Ene6K77FanSubProfile ene6K77FanSubProfile in subProfiles)
		{
			RPMConfig rPMConfig = ene6K77FanSubProfile.RPMSetting.GetCurrentProfile().ToRPMConfig();
			if (rPMConfig != null)
			{
				list.Add(new EneRPMConfig
				{
					FanGroupIndex = ene6K77FanSubProfile.GroupIndex,
					Config = rPMConfig
				});
			}
		}
		sendSetFanSpeedRequest(list.ToArray());
	}

	public override void SendFanConfig(int groupIndex)
	{
		sendFanConfigRequest(groupIndex);
	}

	public override void SendLightingConfig()
	{
		List<LightingConfig> list = new List<LightingConfig>();
		if (Profile.IsMerged)
		{
			LightingConfig lightingConfig = convertToLightingConfig(Profile.LightingSettings[Profile.LightingModeMerge.Value]);
			lightingConfig.Port = 0;
			list.Add(lightingConfig);
		}
		else
		{
			Ene6K77FanSubProfile[] subProfiles = Profile.SubProfiles;
			foreach (Ene6K77FanSubProfile ene6K77FanSubProfile in subProfiles)
			{
				if (ene6K77FanSubProfile.IsIndividualMode)
				{
					LightingConfig lightingConfig2 = convertToLightingConfig(ene6K77FanSubProfile.LightingSettings[ene6K77FanSubProfile.LightingModeInner]);
					lightingConfig2.Port = ene6K77FanSubProfile.GroupIndex * 2;
					list.Add(lightingConfig2);
					lightingConfig2 = convertToLightingConfig(ene6K77FanSubProfile.LightingSettings[ene6K77FanSubProfile.LightingModeOuter]);
					lightingConfig2.Port = ene6K77FanSubProfile.GroupIndex * 2 + 1;
					list.Add(lightingConfig2);
				}
				else
				{
					LightingConfig lightingConfig3 = convertToLightingConfig(ene6K77FanSubProfile.LightingSettings[ene6K77FanSubProfile.LightingMode]);
					lightingConfig3.Port = ene6K77FanSubProfile.GroupIndex * 2;
					list.Add(lightingConfig3);
				}
			}
			adjustLightingConfigColor(list);
		}
		sendLightingSetting(list.ToArray());
	}

	public override void SendLightingConfig(int groupIndex)
	{
		sendLightingConfigRequest(groupIndex);
	}

	public override void SendMergeOrder()
	{
		sendMergeOrder(Profile.MergeOrder);
	}

	public override Ene6K77FanProfile GetProfile()
	{
		return Profile;
	}

	private Task getFirmwareVersion()
	{
		return Task.Run(() =>
		{
			try
			{
				string responseBody = sendFirmwareVersionRequest();
				if (tryGetResponseData<SLInfinityFirmwareVersion>(responseBody, out var data) && data.IsValid)
				{
					FirmwareVersion = data;
					Version version = data.GetVersion();
					Logger.Instance.Info($"SL-INF F/W Version: {version.Major}.{version.Minor}");
				}
			}
			catch (Exception)
			{
			}
		});
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
			Task.Run(async () =>
			{
				try
				{
					while (!checkFirmwareVersionTaskToken.IsCancellationRequested)
					{
						DateTime startTime = DateTime.Now;
						await getFirmwareVersion();
						if (FirmwareVersion.IsValid)
						{
							break;
						}
						double num = 2000.0 - (DateTime.Now - startTime).TotalMilliseconds;
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

	private void sendFanConfigRequest(int groupIndex)
	{
		if (groupIndex >= 0 && groupIndex <= 4)
		{
			RPMConfig rPMConfig = Profile.SubProfiles[groupIndex].RPMSetting.GetCurrentProfile().ToRPMConfig();
			if (rPMConfig != null)
			{
				sendSetFanSpeedRequest(groupIndex, rPMConfig);
			}
		}
	}

	private void sendLightingConfigRequest(int groupIndex)
	{
		if (groupIndex < 0 || groupIndex > 4)
		{
			return;
		}
		List<LightingConfig> list = new List<LightingConfig>();
		if (Profile.IsMerged)
		{
			LightingConfig lightingConfig = convertToLightingConfig(Profile.LightingSettings[Profile.LightingModeMerge.Value]);
			lightingConfig.Port = 0;
			list.Add(lightingConfig);
		}
		else
		{
			Ene6K77FanSubProfile ene6K77FanSubProfile = Profile.SubProfiles[groupIndex];
			if (ene6K77FanSubProfile.IsIndividualMode)
			{
				LightingConfig lightingConfig2 = convertToLightingConfig(ene6K77FanSubProfile.LightingSettings[ene6K77FanSubProfile.LightingModeInner]);
				lightingConfig2.Port = groupIndex * 2;
				list.Add(lightingConfig2);
				lightingConfig2 = convertToLightingConfig(ene6K77FanSubProfile.LightingSettings[ene6K77FanSubProfile.LightingModeOuter]);
				lightingConfig2.Port = groupIndex * 2 + 1;
				list.Add(lightingConfig2);
			}
			else
			{
				LightingConfig lightingConfig3 = convertToLightingConfig(ene6K77FanSubProfile.LightingSettings[ene6K77FanSubProfile.LightingMode]);
				lightingConfig3.Port = groupIndex * 2;
				list.Add(lightingConfig3);
			}
			adjustLightingConfigColor(list);
		}
		sendLightingSetting(list.ToArray());
	}

	private List<LightingConfig> adjustLightingConfigColor(List<LightingConfig> configs)
	{
		foreach (LightingConfig config in configs)
		{
			if (NeedAdjustColorLightingModes.Contains(config.Mode))
			{
				if (!LightingBrightnessLookup.TryGetValue(config.Brightness, out var value))
				{
					value = 0;
				}
				double num = (double)value / 100.0;
				for (int i = 0; i < config.Colors.Length; i++)
				{
					config.Colors[i].R = (byte)Math.Floor((double)(int)config.Colors[i].R * num);
					config.Colors[i].G = (byte)Math.Floor((double)(int)config.Colors[i].G * num);
					config.Colors[i].B = (byte)Math.Floor((double)(int)config.Colors[i].B * num);
				}
			}
		}
		return configs;
	}

	private void startGetFanSpeedTimer()
	{
		try
		{
			getFanSpeedTimer = NonOverlappingTimer.StartNew(() =>
			{
				string responseBody = sendControllerRequest(RequestType.GetFanSpeed, null);
				if (tryGetResponseData<int[]>(responseBody, out var data) && data.Length == 4)
				{
					for (int i = 0; i < data.Length; i++)
					{
						Profile.SubProfiles[i].FanSpeed = data[i];
					}
				}
			}, 1000, continueOnCallbackError: true);
		}
		catch
		{
		}
	}

	private void stopGetFanSpeedTimer()
	{
		try
		{
			getFanSpeedTimer?.Dispose();
		}
		catch
		{
		}
	}

	private void sendAllConfigRequest()
	{
		SendFanQuantity();
		SendMergeOrder();
		SendFanConfig();
		SendLightingConfig();
	}
}
