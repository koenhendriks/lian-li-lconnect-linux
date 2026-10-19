using System.Collections.Generic;
using System.Windows.Media;
using LConnectCore.Products;
using LConnectCore.Products.Ene6K77Fan;

namespace LConnect3cs.Products.Ene6K77Fan;

internal class SLInfinityProfile : Ene6K77FanProfile
{
	private static readonly RPMSetting DefaultFanRPMSetting;

	private static readonly RPMMode[] FanModeOrder;

	private static readonly Dictionary<LightingMode, Ene6K77FanLightingSetting> DefaultLightingSettings;

	private static readonly Dictionary<LightingMode, Ene6K77FanLightingViewSetting> LightingViewSettings;

	private static readonly Dictionary<LightingMode, LightingMode> HasMergeLightingModeLookup;

	private static readonly LightingMode[] LightingModeOrder;

	private static readonly LightingMode[] LightingModeInnerOrder;

	private static readonly LightingMode[] LightingModeOuterOrder;

	static SLInfinityProfile()
	{
		DefaultFanRPMSetting = new RPMSetting();
		FanModeOrder = new RPMMode[8]
		{
			RPMMode.Quiet,
			RPMMode.StandardSpeed,
			RPMMode.HighSpeed,
			RPMMode.FullSpeed,
			RPMMode.UserDefine1,
			RPMMode.UserDefine2,
			RPMMode.UserDefine3,
			RPMMode.UserDefine4
		};
		DefaultLightingSettings = new Dictionary<LightingMode, Ene6K77FanLightingSetting>
		{
			[LightingMode.Rainbow] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.RainbowMorph] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.StaticColor] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Brightness = 100
			},
			[LightingMode.Breathing] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.BreathingRainbow] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Runway] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.MopUp] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Warning] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 50,
				Brightness = 100
			},
			[LightingMode.Voice] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Mixing] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Stack] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Tide] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Scan] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Door] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.HeartBeat] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.HeartBeatRunway] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Disco] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Blue,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.ElectricCurrent] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.White,
					Colors.White,
					Colors.White,
					Colors.White
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Runway_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.MopUp_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Mixing_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Stack_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Tide_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Scan_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Door_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.HeartBeatRunway_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.ElectricCurrent_Merge] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Rainbow_Inner] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.RainbowMorph_Inner] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.StaticColor_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Brightness = 100
			},
			[LightingMode.Breathing_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.BreathingRainbow_Inner] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Taichi_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Blue,
					Color.FromRgb(byte.MaxValue, 105, 0)
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.ColorCycle_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Runway_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.MopUp_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.MeteorRainbow_Inner] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Lottery_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Warning_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Voice_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Mixing_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Stack_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Tide_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Scan_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.DoubleMeteor_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.MeteorContest_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.MeteorMix_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.ReturnArc_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.DoubleArc_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Door_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.HeartBeat_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.HeartBeatRunway_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Disco_Inner] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Rainbow_Outer] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.RainbowMorph_Outer] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.StaticColor_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Brightness = 100
			},
			[LightingMode.Breathing_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.BreathingRainbow_Outer] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.ColorCycle_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Runway_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.MopUp_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.MeteorRainbow_Outer] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.ColorfulMeteor_Outer] = new Ene6K77FanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Lottery_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Warning_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Voice_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Mixing_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Stack_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Tide_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Scan_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Door_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.HeartBeat_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Disco_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[4]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Blue,
					Colors.Yellow
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.Right
			},
			[LightingMode.Reflect_Outer] = new Ene6K77FanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Speed = 75,
				Brightness = 100
			}
		};
		LightingViewSettings = new Dictionary<LightingMode, Ene6K77FanLightingViewSetting>();
		HasMergeLightingModeLookup = new Dictionary<LightingMode, LightingMode>
		{
			[LightingMode.Runway] = LightingMode.Runway_Merge,
			[LightingMode.MopUp] = LightingMode.MopUp_Merge,
			[LightingMode.Mixing] = LightingMode.Mixing_Merge,
			[LightingMode.Stack] = LightingMode.Stack_Merge,
			[LightingMode.Tide] = LightingMode.Tide_Merge,
			[LightingMode.Scan] = LightingMode.Scan_Merge,
			[LightingMode.Door] = LightingMode.Door_Merge,
			[LightingMode.HeartBeatRunway] = LightingMode.HeartBeatRunway_Merge,
			[LightingMode.ElectricCurrent] = LightingMode.ElectricCurrent_Merge
		};
		LightingModeOrder = new LightingMode[19]
		{
			LightingMode.Rainbow,
			LightingMode.RainbowMorph,
			LightingMode.StaticColor,
			LightingMode.Breathing,
			LightingMode.BreathingRainbow,
			LightingMode.Runway,
			LightingMode.MopUp,
			LightingMode.Meteor,
			LightingMode.Warning,
			LightingMode.Voice,
			LightingMode.Mixing,
			LightingMode.Stack,
			LightingMode.Tide,
			LightingMode.Scan,
			LightingMode.Door,
			LightingMode.HeartBeat,
			LightingMode.HeartBeatRunway,
			LightingMode.Disco,
			LightingMode.ElectricCurrent
		};
		LightingModeInnerOrder = new LightingMode[27]
		{
			LightingMode.Rainbow_Inner,
			LightingMode.RainbowMorph_Inner,
			LightingMode.StaticColor_Inner,
			LightingMode.Breathing_Inner,
			LightingMode.BreathingRainbow_Inner,
			LightingMode.Taichi_Inner,
			LightingMode.ColorCycle_Inner,
			LightingMode.Runway_Inner,
			LightingMode.MopUp_Inner,
			LightingMode.Meteor_Inner,
			LightingMode.MeteorRainbow_Inner,
			LightingMode.Lottery_Inner,
			LightingMode.Warning_Inner,
			LightingMode.Voice_Inner,
			LightingMode.Mixing_Inner,
			LightingMode.Stack_Inner,
			LightingMode.Tide_Inner,
			LightingMode.Scan_Inner,
			LightingMode.DoubleMeteor_Inner,
			LightingMode.MeteorContest_Inner,
			LightingMode.MeteorMix_Inner,
			LightingMode.ReturnArc_Inner,
			LightingMode.DoubleArc_Inner,
			LightingMode.Door_Inner,
			LightingMode.HeartBeat_Inner,
			LightingMode.HeartBeatRunway_Inner,
			LightingMode.Disco_Inner
		};
		LightingModeOuterOrder = new LightingMode[22]
		{
			LightingMode.Rainbow_Outer,
			LightingMode.RainbowMorph_Outer,
			LightingMode.StaticColor_Outer,
			LightingMode.Breathing_Outer,
			LightingMode.BreathingRainbow_Outer,
			LightingMode.ColorCycle_Outer,
			LightingMode.Runway_Outer,
			LightingMode.MopUp_Outer,
			LightingMode.Meteor_Outer,
			LightingMode.MeteorRainbow_Outer,
			LightingMode.ColorfulMeteor_Outer,
			LightingMode.Lottery_Outer,
			LightingMode.Warning_Outer,
			LightingMode.Voice_Outer,
			LightingMode.Mixing_Outer,
			LightingMode.Stack_Outer,
			LightingMode.Tide_Outer,
			LightingMode.Scan_Outer,
			LightingMode.Door_Outer,
			LightingMode.HeartBeat_Outer,
			LightingMode.Disco_Outer,
			LightingMode.Reflect_Outer
		};
		DefaultFanRPMSetting.SupportedMotherboardSync = true;
		DefaultFanRPMSetting.SupportedStartStop = true;
		DefaultFanRPMSetting.SupportedRenameProfileName = true;
		DefaultFanRPMSetting.SupportedFanSize = new FanSize[2]
		{
			FanSize.Size120mm,
			FanSize.Size140mm
		};
		DefaultFanRPMSetting.SupportedRPMReferenceSource = new RPMReferenceSource[2]
		{
			RPMReferenceSource.CPU,
			RPMReferenceSource.GPU
		};
		DefaultFanRPMSetting.SpeedUnit = RPMSpeedUnit.RPM;
		DefaultFanRPMSetting.Mode = RPMMode.StandardSpeed;
		RPMMode[] fanModeOrder = FanModeOrder;
		foreach (RPMMode rPMMode in fanModeOrder)
		{
			RPMProfile rPMProfile = new RPMProfile(rPMMode)
			{
				FanSize = FanSize.Size120mm,
				RPMReferenceSource = RPMReferenceSource.CPU,
				ControlType = RPMControlType.Curve,
				PhaseInfos = new Dictionary<RPMPhaseKey, RPMPhaseInfo>()
			};
			DefaultFanRPMSetting.Profiles.Add(rPMMode, rPMProfile);
			FanSize[] supportedFanSize = DefaultFanRPMSetting.SupportedFanSize;
			foreach (FanSize fanSize in supportedFanSize)
			{
				int num = ((fanSize == FanSize.Size120mm) ? 2100 : 1600);
				int num2 = ((fanSize == FanSize.Size120mm) ? 210 : 200);
				RPMReferenceSource[] supportedRPMReferenceSource = DefaultFanRPMSetting.SupportedRPMReferenceSource;
				foreach (RPMReferenceSource referenceSource in supportedRPMReferenceSource)
				{
					RPMPhaseInfo rPMPhaseInfo = new RPMPhaseInfo
					{
						Adjustable = true,
						ChartMinSpeed = 0,
						ChartMaxSpeed = num,
						ChartSpeedTick = num2,
						MinSpeed = num2,
						MaxSpeed = num,
						ChartSpeedLabels = ((fanSize == FanSize.Size120mm) ? new string[11]
						{
							"0",
							string.Empty,
							"420",
							string.Empty,
							"840",
							string.Empty,
							"1260",
							string.Empty,
							"1680",
							string.Empty,
							"2100"
						} : new string[9]
						{
							"0",
							string.Empty,
							"400",
							string.Empty,
							"800",
							string.Empty,
							"1200",
							string.Empty,
							"1600"
						})
					};
					RPMPhaseInfo rPMPhaseInfo2 = rPMPhaseInfo.Clone();
					rPMPhaseInfo.Phases = Ene6K77FanProfile.convertPhaseDutyToRPM(Ene6K77FanProfile.defaultPhaseData[rPMMode], rPMPhaseInfo.MaxSpeed);
					rPMPhaseInfo2.Phases = Ene6K77FanProfile.convertPhaseDutyToRPM(new RPMPhase[1]
					{
						new RPMPhase(0, 50)
					}, rPMPhaseInfo.MaxSpeed);
					rPMProfile.PhaseInfos.Add(new RPMPhaseKey(fanSize, referenceSource, RPMControlType.Curve), rPMPhaseInfo);
					rPMProfile.PhaseInfos.Add(new RPMPhaseKey(fanSize, referenceSource, RPMControlType.Fixed), rPMPhaseInfo2);
				}
			}
		}
		foreach (KeyValuePair<LightingMode, Ene6K77FanLightingSetting> defaultLightingSetting in DefaultLightingSettings)
		{
			DefaultLightingSettings[defaultLightingSetting.Key].Mode = defaultLightingSetting.Key;
			LightingViewSettings.Add(defaultLightingSetting.Key, new Ene6K77FanLightingViewSetting
			{
				ColorCount = defaultLightingSetting.Value.Colors.Length,
				Speed = defaultLightingSetting.Value.Speed.HasValue,
				Brightness = defaultLightingSetting.Value.Brightness.HasValue,
				Direction = defaultLightingSetting.Value.Direction.HasValue
			});
		}
	}

	public SLInfinityProfile()
	{
		HashSet<LightingMode> hashSet = new HashSet<LightingMode>(HasMergeLightingModeLookup.Values);
		MergeOrder = new int[4];
		SubProfiles = new Ene6K77FanSubProfile[4];
		for (int i = 0; i < SubProfiles.Length; i++)
		{
			MergeOrder[i] = i;
			SubProfiles[i] = new Ene6K77FanSubProfile
			{
				GroupIndex = i,
				GroupName = $"Port{i + 1}",
				FanQuantity = 3,
				RPMSetting = DefaultFanRPMSetting.Clone(),
				LightingSettings = new Dictionary<LightingMode, Ene6K77FanLightingSetting>(),
				LightingMode = LightingMode.StaticColor,
				LightingModeInner = LightingMode.StaticColor_Inner,
				LightingModeOuter = LightingMode.StaticColor_Outer,
				IsIndividualMode = true
			};
			foreach (KeyValuePair<LightingMode, Ene6K77FanLightingSetting> defaultLightingSetting in DefaultLightingSettings)
			{
				if (!hashSet.Contains(defaultLightingSetting.Key))
				{
					SubProfiles[i].LightingSettings.Add(defaultLightingSetting.Key, defaultLightingSetting.Value.Clone());
				}
			}
		}
		LightingSettings = new Dictionary<LightingMode, Ene6K77FanLightingSetting>();
		foreach (LightingMode item in hashSet)
		{
			LightingSettings.Add(item, DefaultLightingSettings[item].Clone());
		}
	}

	public override RPMMode[] GetFanModeOrder()
	{
		return FanModeOrder;
	}

	public override RPMPhaseInfo GetDefaultFanRPMPhaseInfo(RPMMode mode, RPMPhaseKey phaseKey)
	{
		if (DefaultFanRPMSetting.Profiles.TryGetValue(mode, out var value) && value.PhaseInfos.TryGetValue(phaseKey, out var value2))
		{
			return value2.Clone();
		}
		return null;
	}

	public override LightingMode[] GetLightingModeOrder()
	{
		return (LightingMode[])LightingModeOrder.Clone();
	}

	public override LightingMode[] GetLightingModeInnerOrder()
	{
		return (LightingMode[])LightingModeInnerOrder.Clone();
	}

	public override LightingMode[] GetLightingModeOuterOrder()
	{
		return (LightingMode[])LightingModeOuterOrder.Clone();
	}

	public override bool HasMergeLightingMode(LightingMode mode)
	{
		return HasMergeLightingModeLookup.ContainsKey(mode);
	}

	public override Ene6K77FanLightingViewSetting GetLightingViewSetting(LightingMode mode)
	{
		return LightingViewSettings[mode];
	}

	public override LightingMode? GetMergeLightingMode(LightingMode mode)
	{
		if (HasMergeLightingModeLookup.TryGetValue(mode, out var value))
		{
			return value;
		}
		return null;
	}

	public override Ene6K77FanLightingSetting GetDefaultLightingSetting(LightingMode mode)
	{
		return DefaultLightingSettings[mode].Clone();
	}
}
