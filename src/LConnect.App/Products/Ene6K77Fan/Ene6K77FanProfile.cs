using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LConnectCore.Products;
using LConnectCore.Products.Ene6K77Fan;

namespace LConnect3cs.Products.Ene6K77Fan;

internal abstract class Ene6K77FanProfile
{
	protected static readonly Dictionary<RPMMode, RPMPhase[]> defaultPhaseData = new Dictionary<RPMMode, RPMPhase[]>
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

	public Ene6K77FanSubProfile[] SubProfiles { get; set; }

	public int[] MergeOrder { get; set; }

	public LightingMode? LightingModeBeforeMerge { get; set; }

	[JsonIgnore]
	public LightingMode? LightingModeMerge
	{
		get
		{
			if (!LightingModeBeforeMerge.HasValue)
			{
				return null;
			}
			return GetMergeLightingMode(LightingModeBeforeMerge.Value);
		}
	}

	public Dictionary<LightingMode, Ene6K77FanLightingSetting> LightingSettings { get; set; }

	public bool IsMerged => LightingModeBeforeMerge.HasValue;

	public abstract RPMMode[] GetFanModeOrder();

	public abstract RPMPhaseInfo GetDefaultFanRPMPhaseInfo(RPMMode mode, RPMPhaseKey phaseKey);

	public abstract LightingMode[] GetLightingModeOrder();

	public abstract LightingMode[] GetLightingModeInnerOrder();

	public abstract LightingMode[] GetLightingModeOuterOrder();

	public abstract bool HasMergeLightingMode(LightingMode mode);

	public abstract LightingMode? GetMergeLightingMode(LightingMode mode);

	public abstract Ene6K77FanLightingViewSetting GetLightingViewSetting(LightingMode mode);

	public abstract Ene6K77FanLightingSetting GetDefaultLightingSetting(LightingMode mode);

	protected static RPMPhase[] convertPhaseDutyToRPM(RPMPhase[] dutyPhases, int maxSpeed)
	{
		RPMPhase[] array = new RPMPhase[dutyPhases.Length];
		for (int i = 0; i < dutyPhases.Length; i++)
		{
			array[i] = new RPMPhase(dutyPhases[i].Temperature, (int)Math.Round((double)dutyPhases[i].Speed / 100.0 * (double)maxSpeed));
		}
		return array;
	}
}
