using System;

namespace LConnectCore.Products;

public class RPMConfig
{
	public int MaxSpeed { get; set; }

	public RPMReferenceSource Reference { get; set; }

	public RPMPhase[] Phases { get; set; }

	public int CalculateSpeed(int temperature)
	{
		return Math.Max((int)calculate(temperature), 0);
	}

	private double calculate(int temperature)
	{
		if (Phases == null || Phases.Length == 0)
		{
			throw new InvalidOperationException();
		}
		if (Phases.Length == 1)
		{
			return Phases[0].Speed;
		}
		RPMPhase rPMPhase = Phases[0];
		for (int i = 1; i < Phases.Length; i++)
		{
			if (temperature <= rPMPhase.Temperature)
			{
				return rPMPhase.Speed;
			}
			if (rPMPhase.Temperature < temperature && temperature < Phases[i].Temperature)
			{
				int num = Phases[i].Temperature - rPMPhase.Temperature;
				double num2 = Phases[i].Speed - rPMPhase.Speed;
				if (num == 0)
				{
					return rPMPhase.Speed;
				}
				return num2 / (double)num * (double)(temperature - rPMPhase.Temperature) + (double)rPMPhase.Speed;
			}
			rPMPhase = Phases[i];
		}
		return rPMPhase.Speed;
	}
}
