using System.Collections.Generic;
using LConnectCore.Products;

namespace LConnect3cs.Products;

internal class RPMPhaseInfo
{
	public bool Adjustable { get; set; }

	public RPMPhase[] Phases { get; set; }

	public int MinSpeed { get; set; }

	public int MaxSpeed { get; set; }

	public int ChartMinSpeed { get; set; }

	public int ChartMaxSpeed { get; set; }

	public int ChartSpeedTick { get; set; }

	public string[] ChartSpeedLabels { get; set; }

	public RPMPhaseInfo Clone()
	{
		return new RPMPhaseInfo
		{
			Adjustable = Adjustable,
			Phases = (RPMPhase[])Phases?.Clone(),
			MinSpeed = MinSpeed,
			MaxSpeed = MaxSpeed,
			ChartMinSpeed = ChartMinSpeed,
			ChartMaxSpeed = ChartMaxSpeed,
			ChartSpeedTick = ChartSpeedTick,
			ChartSpeedLabels = (string[])ChartSpeedLabels?.Clone()
		};
	}

	public RPMPhase[] ToRPMConfigPhases(bool isStartStop)
	{
		List<RPMPhase> list = new List<RPMPhase>();
		if (Phases.Length == 1)
		{
			list.AddRange(Phases);
		}
		else
		{
			if (isStartStop)
			{
				list.Add(new RPMPhase(Phases[0].Temperature, 0));
			}
			else
			{
				list.Add(new RPMPhase(0, MinSpeed));
			}
			list.AddRange(Phases);
			list.Add(new RPMPhase(100, MaxSpeed));
		}
		return list.ToArray();
	}
}
