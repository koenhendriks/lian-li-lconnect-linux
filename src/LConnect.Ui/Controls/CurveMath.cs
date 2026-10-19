using System;
using LConnectCore.Products;

namespace LConnectUi.Controls;

// Point constraints of the original RPMChart: temperatures strictly increasing (the first point stays within
// 0-50 °C), speeds within the profile's Min/MaxSpeed, and the curve never decreases - moving a point drags
// its neighbours along. A single-phase (fixed speed) profile only changes speed.
public static class CurveMath
{
	public const int MinTemperature = 0;

	public const int MaxTemperature = 100;

	public const int FirstPointMaxTemperature = 50;

	public static RPMPhase[] Move(RPMPhase[] phases, int index, int temperature, int speed, int minSpeed, int maxSpeed)
	{
		if (phases == null || index < 0 || index >= phases.Length)
		{
			return phases;
		}
		RPMPhase[] array = (RPMPhase[])phases.Clone();
		if (maxSpeed < minSpeed)
		{
			maxSpeed = minSpeed;
		}
		speed = Math.Clamp(speed, minSpeed, maxSpeed);
		if (array.Length == 1)
		{
			array[0] = new RPMPhase(array[0].Temperature, speed);
			return array;
		}
		int num = ((index == 0) ? MinTemperature : (array[index - 1].Temperature + 1));
		int num2 = ((index == array.Length - 1) ? MaxTemperature : (array[index + 1].Temperature - 1));
		if (index == 0)
		{
			num2 = Math.Min(num2, FirstPointMaxTemperature);
		}
		if (num2 < num)
		{
			num2 = num;
		}
		temperature = Math.Clamp(temperature, num, num2);
		array[index] = new RPMPhase(temperature, speed);
		for (int num3 = index - 1; num3 >= 0; num3--)
		{
			if (array[num3].Speed > speed)
			{
				array[num3] = new RPMPhase(array[num3].Temperature, speed);
			}
		}
		for (int i = index + 1; i < array.Length; i++)
		{
			if (array[i].Speed < speed)
			{
				array[i] = new RPMPhase(array[i].Temperature, speed);
			}
		}
		return array;
	}

	// Linear interpolation of the speed a curve gives at a temperature (flat outside the points).
	public static int SpeedAt(RPMPhase[] phases, double temperature)
	{
		if (phases == null || phases.Length == 0)
		{
			return 0;
		}
		if (phases.Length == 1 || temperature <= phases[0].Temperature)
		{
			return phases[0].Speed;
		}
		for (int i = 1; i < phases.Length; i++)
		{
			if (temperature <= phases[i].Temperature)
			{
				RPMPhase rPMPhase = phases[i - 1];
				RPMPhase rPMPhase2 = phases[i];
				double num = (temperature - rPMPhase.Temperature) / Math.Max(1, rPMPhase2.Temperature - rPMPhase.Temperature);
				return (int)Math.Round(rPMPhase.Speed + num * (rPMPhase2.Speed - rPMPhase.Speed));
			}
		}
		return phases[^1].Speed;
	}
}
