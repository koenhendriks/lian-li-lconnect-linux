using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using LConnectCore.Products;
using LConnectCore.Products.TLFan;

namespace LConnect3cs.Products.TLFan;

internal class TLFanProfile : INotifyPropertyChanged
{
	private static readonly RPMSetting defaultFanRPMSetting;

	private static readonly RPMSetting defaultFanRPMSettingLCD;

	private static readonly Dictionary<RPMMode, RPMPhase[]> defaultPhaseData;

	public static readonly RPMMode[] FanModeOrder;

	private static readonly Dictionary<LightingMode, TLFanLightingSetting> defaultLightingSettings;

	public static readonly Dictionary<LightingMode, TLFanLightingViewSetting> LightingViewSettings;

	public static readonly LightingMode[] LightingModeOrder;

	public static readonly LightingMode[] LightingModeSideOrder;

	public static readonly LightingMode[] LightingModeMergeOrder;

	public static readonly LightingMode[] LightingModeSideMergeOrder;

	private readonly int[] portOrder = new int[4];

	private bool isMerged;

	public TLFanPortProfile[] PortProfiles { get; set; } = new TLFanPortProfile[4];

	public int[] PortOrder
	{
		get
		{
			return portOrder;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException();
			}
			if (value.Length != 4)
			{
				throw new ArgumentException();
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (int num in value)
			{
				if (num < 0 || num >= 4)
				{
					throw new ArgumentException();
				}
				hashSet.Add(num);
			}
			if (4 != hashSet.Count)
			{
				throw new ArgumentException();
			}
			Array.Copy(value, portOrder, portOrder.Length);
		}
	}

	public bool IsSelectedAtLightApplyToAll { get; set; }

	public bool IsMerged
	{
		get
		{
			return isMerged;
		}
		set
		{
			isMerged = value;
			OnPropertyChanged("IsMerged");
		}
	}

	public bool IsSyncSideMode { get; set; }

	public bool IsSyncBottomSide { get; set; }

	public bool IsSingleSideMode { get; set; }

	public LightingMode LightingMode { get; set; } = LightingMode.Meteor_Merge;

	public LightingMode LightingModeTop { get; set; } = LightingMode.Meteor_Side_Merge;

	public LightingMode LightingModeBottom { get; set; } = LightingMode.Meteor_Side_Merge;

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettings { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettingsTop { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public Dictionary<LightingMode, TLFanLightingSetting> LightingSettingsBottom { get; set; } = new Dictionary<LightingMode, TLFanLightingSetting>();

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	static TLFanProfile()
	{
		defaultFanRPMSetting = new RPMSetting();
		defaultFanRPMSettingLCD = new RPMSetting();
		defaultPhaseData = new Dictionary<RPMMode, RPMPhase[]>
		{
			[RPMMode.Quiet] = new RPMPhase[5]
			{
				new RPMPhase(25, 20),
				new RPMPhase(45, 45),
				new RPMPhase(65, 52),
				new RPMPhase(80, 75),
				new RPMPhase(90, 100)
			},
			[RPMMode.StandardSpeed] = new RPMPhase[5]
			{
				new RPMPhase(25, 20),
				new RPMPhase(40, 50),
				new RPMPhase(55, 62),
				new RPMPhase(70, 75),
				new RPMPhase(90, 100)
			},
			[RPMMode.HighSpeed] = new RPMPhase[5]
			{
				new RPMPhase(25, 45),
				new RPMPhase(35, 55),
				new RPMPhase(50, 70),
				new RPMPhase(70, 85),
				new RPMPhase(85, 100)
			},
			[RPMMode.FullSpeed] = new RPMPhase[5]
			{
				new RPMPhase(25, 100),
				new RPMPhase(40, 100),
				new RPMPhase(55, 100),
				new RPMPhase(70, 100),
				new RPMPhase(90, 100)
			},
			[RPMMode.UserDefine1] = new RPMPhase[5]
			{
				new RPMPhase(25, 20),
				new RPMPhase(45, 45),
				new RPMPhase(65, 52),
				new RPMPhase(80, 75),
				new RPMPhase(90, 100)
			},
			[RPMMode.UserDefine2] = new RPMPhase[5]
			{
				new RPMPhase(25, 20),
				new RPMPhase(45, 45),
				new RPMPhase(65, 52),
				new RPMPhase(80, 75),
				new RPMPhase(90, 100)
			},
			[RPMMode.UserDefine3] = new RPMPhase[5]
			{
				new RPMPhase(25, 20),
				new RPMPhase(45, 45),
				new RPMPhase(65, 52),
				new RPMPhase(80, 75),
				new RPMPhase(90, 100)
			},
			[RPMMode.UserDefine4] = new RPMPhase[5]
			{
				new RPMPhase(25, 20),
				new RPMPhase(45, 45),
				new RPMPhase(65, 52),
				new RPMPhase(80, 75),
				new RPMPhase(90, 100)
			}
		};
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
		defaultLightingSettings = new Dictionary<LightingMode, TLFanLightingSetting>
		{
			[LightingMode.Rainbow] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.StaticColor] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Brightness = 100
			},
			[LightingMode.Runway] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.ColorCycle] = new TLFanLightingSetting
			{
				Colors = new Color[3]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Render] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.TailChasing] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Stack] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.CoverCycle] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Wave] = new TLFanLightingSetting
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
			[LightingMode.Racing] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Lottery] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Intertwine] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.MeteorShower] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Rainbow_Merge] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.StaticColor_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Brightness = 100
			},
			[LightingMode.Runway_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.ColorCycle_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[3]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Render_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.TailChasing_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Stack_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.CoverCycle_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Wave_Merge] = new TLFanLightingSetting
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
			[LightingMode.Racing_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Rainbow_Side] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.RainbowMorph_Side] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.StaticColor_Side] = new TLFanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Brightness = 100
			},
			[LightingMode.Breathing_Side] = new TLFanLightingSetting
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
			[LightingMode.Runway_Side] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor_Side] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.ColorCycle_Side] = new TLFanLightingSetting
			{
				Colors = new Color[3]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Staggered_Side] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Tide_Side] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Mixing_Side] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Voice_Side] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Door_Side] = new TLFanLightingSetting
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
			[LightingMode.Render_Side] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Ripple_Side] = new TLFanLightingSetting
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
			[LightingMode.Reflect_Side] = new TLFanLightingSetting
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
			[LightingMode.Paint_Side] = new TLFanLightingSetting
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
			[LightingMode.PingPong_Side] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Stack_Side] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Collide_Side] = new TLFanLightingSetting
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
			[LightingMode.ElectricCurrent_Side] = new TLFanLightingSetting
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
			[LightingMode.Kaleidoscope_Side] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Rainbow_Side_Merge] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.StaticColor_Side_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[1] { Colors.Red },
				Brightness = 100
			},
			[LightingMode.Runway_Side_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Meteor_Side_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.ColorCycle_Side_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[3]
				{
					Colors.Red,
					Colors.Blue,
					Colors.Green
				},
				Speed = 75,
				Brightness = 100,
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Tide_Side_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Mixing_Side_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Voice_Side_Merge] = new TLFanLightingSetting
			{
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Door_Side_Merge] = new TLFanLightingSetting
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
			[LightingMode.Render_Side_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.Ripple_Side_Merge] = new TLFanLightingSetting
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
			[LightingMode.Reflect_Side_Merge] = new TLFanLightingSetting
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
			[LightingMode.Paint_Side_Merge] = new TLFanLightingSetting
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
			[LightingMode.PingPong_Side_Merge] = new TLFanLightingSetting
			{
				Colors = new Color[2]
				{
					Colors.Red,
					Colors.Blue
				},
				Speed = 75,
				Brightness = 100
			},
			[LightingMode.Stack_Side_Merge] = new TLFanLightingSetting
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
				Direction = LightingDirection.RightOrClockwise
			},
			[LightingMode.ElectricCurrent_Side_Merge] = new TLFanLightingSetting
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
			}
		};
		LightingViewSettings = new Dictionary<LightingMode, TLFanLightingViewSetting>();
		LightingModeOrder = new LightingMode[14]
		{
			LightingMode.Rainbow,
			LightingMode.StaticColor,
			LightingMode.Runway,
			LightingMode.Meteor,
			LightingMode.ColorCycle,
			LightingMode.Render,
			LightingMode.TailChasing,
			LightingMode.Stack,
			LightingMode.CoverCycle,
			LightingMode.Wave,
			LightingMode.Racing,
			LightingMode.Lottery,
			LightingMode.Intertwine,
			LightingMode.MeteorShower
		};
		LightingModeSideOrder = new LightingMode[21]
		{
			LightingMode.Rainbow_Side,
			LightingMode.RainbowMorph_Side,
			LightingMode.StaticColor_Side,
			LightingMode.Breathing_Side,
			LightingMode.Runway_Side,
			LightingMode.Meteor_Side,
			LightingMode.ColorCycle_Side,
			LightingMode.Staggered_Side,
			LightingMode.Tide_Side,
			LightingMode.Mixing_Side,
			LightingMode.Voice_Side,
			LightingMode.Door_Side,
			LightingMode.Render_Side,
			LightingMode.Ripple_Side,
			LightingMode.Reflect_Side,
			LightingMode.Paint_Side,
			LightingMode.PingPong_Side,
			LightingMode.Stack_Side,
			LightingMode.Collide_Side,
			LightingMode.ElectricCurrent_Side,
			LightingMode.Kaleidoscope_Side
		};
		LightingModeMergeOrder = new LightingMode[11]
		{
			LightingMode.Rainbow_Merge,
			LightingMode.StaticColor_Merge,
			LightingMode.Runway_Merge,
			LightingMode.Meteor_Merge,
			LightingMode.ColorCycle_Merge,
			LightingMode.Render_Merge,
			LightingMode.TailChasing_Merge,
			LightingMode.Stack_Merge,
			LightingMode.CoverCycle_Merge,
			LightingMode.Wave_Merge,
			LightingMode.Racing_Merge
		};
		LightingModeSideMergeOrder = new LightingMode[15]
		{
			LightingMode.Rainbow_Side_Merge,
			LightingMode.Runway_Side_Merge,
			LightingMode.Meteor_Side_Merge,
			LightingMode.ColorCycle_Side_Merge,
			LightingMode.Tide_Side_Merge,
			LightingMode.Mixing_Side_Merge,
			LightingMode.Voice_Side_Merge,
			LightingMode.Door_Side_Merge,
			LightingMode.Render_Side_Merge,
			LightingMode.Ripple_Side_Merge,
			LightingMode.Reflect_Side_Merge,
			LightingMode.Paint_Side_Merge,
			LightingMode.PingPong_Side_Merge,
			LightingMode.Stack_Side_Merge,
			LightingMode.ElectricCurrent_Side_Merge
		};
		defaultFanRPMSetting.SupportedMotherboardSync = true;
		defaultFanRPMSetting.SupportedStartStop = true;
		defaultFanRPMSetting.SupportedRenameProfileName = true;
		defaultFanRPMSetting.SupportedFanSize = new FanSize[2]
		{
			FanSize.Size120mm,
			FanSize.Size140mm
		};
		defaultFanRPMSetting.SupportedRPMReferenceSource = new RPMReferenceSource[2]
		{
			RPMReferenceSource.CPU,
			RPMReferenceSource.GPU
		};
		defaultFanRPMSetting.SpeedUnit = RPMSpeedUnit.RPM;
		defaultFanRPMSetting.Mode = RPMMode.StandardSpeed;
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
			defaultFanRPMSetting.Profiles.Add(rPMMode, rPMProfile);
			FanSize[] supportedFanSize = defaultFanRPMSetting.SupportedFanSize;
			foreach (FanSize fanSize in supportedFanSize)
			{
				int num = ((fanSize == FanSize.Size120mm) ? 2600 : 1800);
				int minSpeed = ((fanSize == FanSize.Size120mm) ? 200 : 200);
				RPMReferenceSource[] supportedRPMReferenceSource = defaultFanRPMSetting.SupportedRPMReferenceSource;
				foreach (RPMReferenceSource referenceSource in supportedRPMReferenceSource)
				{
					RPMPhaseInfo rPMPhaseInfo = new RPMPhaseInfo
					{
						Adjustable = true,
						ChartMinSpeed = 0,
						ChartMaxSpeed = num,
						ChartSpeedTick = ((fanSize == FanSize.Size120mm) ? 260 : 225),
						MinSpeed = minSpeed,
						MaxSpeed = num,
						ChartSpeedLabels = ((fanSize == FanSize.Size120mm) ? new string[11]
						{
							"0",
							string.Empty,
							"520",
							string.Empty,
							"1040",
							string.Empty,
							"1560",
							string.Empty,
							"2080",
							string.Empty,
							"2600"
						} : new string[9]
						{
							"0",
							string.Empty,
							"450",
							string.Empty,
							"900",
							string.Empty,
							"1350",
							string.Empty,
							"1800"
						})
					};
					RPMPhaseInfo rPMPhaseInfo2 = rPMPhaseInfo.Clone();
					rPMPhaseInfo.Phases = convertPhaseDutyToRPM(defaultPhaseData[rPMMode], rPMPhaseInfo.MaxSpeed);
					rPMPhaseInfo2.Phases = convertPhaseDutyToRPM(new RPMPhase[1]
					{
						new RPMPhase(0, 50)
					}, rPMPhaseInfo.MaxSpeed);
					rPMProfile.PhaseInfos.Add(new RPMPhaseKey(fanSize, referenceSource, RPMControlType.Curve), rPMPhaseInfo);
					rPMProfile.PhaseInfos.Add(new RPMPhaseKey(fanSize, referenceSource, RPMControlType.Fixed), rPMPhaseInfo2);
				}
			}
		}
		defaultFanRPMSettingLCD.SupportedMotherboardSync = true;
		defaultFanRPMSettingLCD.SupportedStartStop = true;
		defaultFanRPMSettingLCD.SupportedRenameProfileName = true;
		defaultFanRPMSettingLCD.SupportedFanSize = new FanSize[2]
		{
			FanSize.Size120mm,
			FanSize.Size140mm
		};
		defaultFanRPMSettingLCD.SupportedRPMReferenceSource = new RPMReferenceSource[2]
		{
			RPMReferenceSource.CPU,
			RPMReferenceSource.GPU
		};
		defaultFanRPMSettingLCD.SpeedUnit = RPMSpeedUnit.RPM;
		defaultFanRPMSettingLCD.Mode = RPMMode.StandardSpeed;
		fanModeOrder = FanModeOrder;
		foreach (RPMMode rPMMode2 in fanModeOrder)
		{
			RPMProfile rPMProfile2 = new RPMProfile(rPMMode2)
			{
				FanSize = FanSize.Size120mm,
				RPMReferenceSource = RPMReferenceSource.CPU,
				ControlType = RPMControlType.Curve,
				PhaseInfos = new Dictionary<RPMPhaseKey, RPMPhaseInfo>()
			};
			defaultFanRPMSettingLCD.Profiles.Add(rPMMode2, rPMProfile2);
			FanSize[] supportedFanSize = defaultFanRPMSettingLCD.SupportedFanSize;
			foreach (FanSize fanSize2 in supportedFanSize)
			{
				int num2 = ((fanSize2 == FanSize.Size120mm) ? 1900 : 1600);
				int minSpeed2 = ((fanSize2 == FanSize.Size120mm) ? 350 : 400);
				RPMReferenceSource[] supportedRPMReferenceSource = defaultFanRPMSettingLCD.SupportedRPMReferenceSource;
				foreach (RPMReferenceSource referenceSource2 in supportedRPMReferenceSource)
				{
					RPMPhaseInfo rPMPhaseInfo3 = new RPMPhaseInfo
					{
						Adjustable = true,
						ChartMinSpeed = 0,
						ChartMaxSpeed = num2,
						ChartSpeedTick = ((fanSize2 == FanSize.Size120mm) ? 237 : 200),
						MinSpeed = minSpeed2,
						MaxSpeed = num2,
						ChartSpeedLabels = ((fanSize2 == FanSize.Size120mm) ? new string[9]
						{
							"0",
							string.Empty,
							"475",
							string.Empty,
							"950",
							string.Empty,
							"1425",
							string.Empty,
							"1900"
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
					RPMPhaseInfo rPMPhaseInfo4 = rPMPhaseInfo3.Clone();
					rPMPhaseInfo3.Phases = convertPhaseDutyToRPM(defaultPhaseData[rPMMode2], rPMPhaseInfo3.MaxSpeed);
					rPMPhaseInfo4.Phases = convertPhaseDutyToRPM(new RPMPhase[1]
					{
						new RPMPhase(0, 50)
					}, rPMPhaseInfo3.MaxSpeed);
					rPMProfile2.PhaseInfos.Add(new RPMPhaseKey(fanSize2, referenceSource2, RPMControlType.Curve), rPMPhaseInfo3);
					rPMProfile2.PhaseInfos.Add(new RPMPhaseKey(fanSize2, referenceSource2, RPMControlType.Fixed), rPMPhaseInfo4);
				}
			}
		}
		foreach (KeyValuePair<LightingMode, TLFanLightingSetting> defaultLightingSetting in defaultLightingSettings)
		{
			defaultLightingSettings[defaultLightingSetting.Key].Mode = defaultLightingSetting.Key;
			LightingViewSettings.Add(defaultLightingSetting.Key, new TLFanLightingViewSetting
			{
				ColorCount = defaultLightingSetting.Value.Colors.Length,
				Speed = defaultLightingSetting.Value.Speed.HasValue,
				Brightness = defaultLightingSetting.Value.Brightness.HasValue,
				Direction = defaultLightingSetting.Value.Direction.HasValue
			});
		}
	}

	public TLFanProfile()
	{
		for (int i = 0; i < PortProfiles.Length; i++)
		{
			portOrder[i] = i;
			PortProfiles[i] = new TLFanPortProfile(i);
			TLFanGroupProfile[] lEDGroupProfiles = PortProfiles[i].LEDGroupProfiles;
			foreach (TLFanGroupProfile tLFanGroupProfile in lEDGroupProfiles)
			{
				defaultFanRPMSetting.CopyTo(tLFanGroupProfile.RPMSetting);
			}
			lEDGroupProfiles = PortProfiles[i].LCDGroupProfiles;
			foreach (TLFanGroupProfile tLFanGroupProfile2 in lEDGroupProfiles)
			{
				defaultFanRPMSettingLCD.CopyTo(tLFanGroupProfile2.RPMSetting);
			}
		}
		LightingMode[] lightingModeMergeOrder = LightingModeMergeOrder;
		foreach (LightingMode key in lightingModeMergeOrder)
		{
			LightingSettings.Add(key, defaultLightingSettings[key].Clone());
		}
		lightingModeMergeOrder = LightingModeSideMergeOrder;
		foreach (LightingMode key2 in lightingModeMergeOrder)
		{
			LightingSettingsTop.Add(key2, defaultLightingSettings[key2].Clone());
			LightingSettingsBottom.Add(key2, defaultLightingSettings[key2].Clone());
		}
		lightingModeMergeOrder = LightingModeOrder;
		foreach (LightingMode key3 in lightingModeMergeOrder)
		{
			TLFanPortProfile[] portProfiles = PortProfiles;
			foreach (TLFanPortProfile tLFanPortProfile in portProfiles)
			{
				TLFanGroupProfile[] lEDGroupProfiles = tLFanPortProfile.LEDGroupProfiles;
				foreach (TLFanGroupProfile obj in lEDGroupProfiles)
				{
					obj.LightingSettings.Add(key3, defaultLightingSettings[key3].Clone());
					TLFanFanProfile[] fanProfiles = obj.FanProfiles;
					for (int m = 0; m < fanProfiles.Length; m++)
					{
						fanProfiles[m].LightingSettings.Add(key3, defaultLightingSettings[key3].Clone());
					}
				}
				lEDGroupProfiles = tLFanPortProfile.LCDGroupProfiles;
				foreach (TLFanGroupProfile obj2 in lEDGroupProfiles)
				{
					obj2.LightingSettings.Add(key3, defaultLightingSettings[key3].Clone());
					TLFanFanProfile[] fanProfiles = obj2.FanProfiles;
					for (int m = 0; m < fanProfiles.Length; m++)
					{
						fanProfiles[m].LightingSettings.Add(key3, defaultLightingSettings[key3].Clone());
					}
				}
			}
		}
		lightingModeMergeOrder = LightingModeSideOrder;
		foreach (LightingMode key4 in lightingModeMergeOrder)
		{
			TLFanPortProfile[] portProfiles = PortProfiles;
			foreach (TLFanPortProfile tLFanPortProfile2 in portProfiles)
			{
				TLFanGroupProfile[] lEDGroupProfiles = tLFanPortProfile2.LEDGroupProfiles;
				foreach (TLFanGroupProfile obj3 in lEDGroupProfiles)
				{
					obj3.LightingSettingsTop.Add(key4, defaultLightingSettings[key4].Clone());
					obj3.LightingSettingsBottom.Add(key4, defaultLightingSettings[key4].Clone());
				}
				lEDGroupProfiles = tLFanPortProfile2.LCDGroupProfiles;
				foreach (TLFanGroupProfile obj4 in lEDGroupProfiles)
				{
					obj4.LightingSettingsTop.Add(key4, defaultLightingSettings[key4].Clone());
					obj4.LightingSettingsBottom.Add(key4, defaultLightingSettings[key4].Clone());
				}
			}
		}
	}

	public static TLFanLightingSetting GetDefaultLightingSetting(LightingMode mode)
	{
		return defaultLightingSettings[mode].Clone();
	}

	public static RPMPhaseInfo GetDefaultFanRPMPhaseInfo(RPMMode mode, RPMPhaseKey phaseKey)
	{
		if (defaultFanRPMSetting.Profiles.TryGetValue(mode, out var value) && value.PhaseInfos.TryGetValue(phaseKey, out var value2))
		{
			return value2.Clone();
		}
		return null;
	}

	public static RPMPhaseInfo GetDefaultFanRPMPhaseInfoLCD(RPMMode mode, RPMPhaseKey phaseKey)
	{
		if (defaultFanRPMSettingLCD.Profiles.TryGetValue(mode, out var value) && value.PhaseInfos.TryGetValue(phaseKey, out var value2))
		{
			return value2.Clone();
		}
		return null;
	}

	private static RPMPhase[] convertPhaseDutyToRPM(RPMPhase[] dutyPhases, int maxSpeed)
	{
		RPMPhase[] array = new RPMPhase[dutyPhases.Length];
		for (int i = 0; i < dutyPhases.Length; i++)
		{
			array[i] = new RPMPhase(dutyPhases[i].Temperature, (int)Math.Round((double)dutyPhases[i].Speed / 100.0 * (double)maxSpeed));
		}
		return array;
	}
}
