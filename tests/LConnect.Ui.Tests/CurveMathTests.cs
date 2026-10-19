using LConnectCore.Products;
using LConnectUi.Controls;
using Xunit;

namespace LConnect.Ui.Tests;

public class CurveMathTests
{
	private static readonly RPMPhase[] Curve = new RPMPhase[5]
	{
		new RPMPhase(25, 400),
		new RPMPhase(40, 900),
		new RPMPhase(55, 1200),
		new RPMPhase(70, 1500),
		new RPMPhase(90, 1900)
	};

	[Fact]
	public void Temperature_stays_between_neighbours()
	{
		RPMPhase[] array = CurveMath.Move(Curve, 2, 95, 1200, 200, 1900);
		Assert.Equal(69, array[2].Temperature);
		array = CurveMath.Move(Curve, 2, 10, 1200, 200, 1900);
		Assert.Equal(41, array[2].Temperature);
	}

	[Fact]
	public void First_point_is_limited_to_50_degrees()
	{
		RPMPhase[] array = CurveMath.Move(new RPMPhase[2]
		{
			new RPMPhase(25, 400),
			new RPMPhase(90, 1900)
		}, 0, 80, 400, 200, 1900);
		Assert.Equal(50, array[0].Temperature);
	}

	[Fact]
	public void Speed_is_clamped_and_curve_stays_monotonic()
	{
		RPMPhase[] array = CurveMath.Move(Curve, 2, 55, 5000, 200, 1900);
		Assert.Equal(1900, array[2].Speed);
		Assert.Equal(1900, array[3].Speed);
		Assert.Equal(900, array[1].Speed);
		array = CurveMath.Move(Curve, 2, 55, 0, 200, 1900);
		Assert.Equal(200, array[2].Speed);
		Assert.Equal(200, array[0].Speed);
		Assert.Equal(200, array[1].Speed);
		Assert.Equal(1500, array[3].Speed);
	}

	[Fact]
	public void Fixed_speed_only_changes_speed()
	{
		RPMPhase[] array = CurveMath.Move(new RPMPhase[1]
		{
			new RPMPhase(0, 1000)
		}, 0, 70, 1300, 350, 1900);
		Assert.Equal(0, array[0].Temperature);
		Assert.Equal(1300, array[0].Speed);
	}

	[Fact]
	public void Speed_at_interpolates()
	{
		Assert.Equal(400, CurveMath.SpeedAt(Curve, 10.0));
		Assert.Equal(1050, CurveMath.SpeedAt(Curve, 47.5));
		Assert.Equal(1900, CurveMath.SpeedAt(Curve, 99.0));
	}
}
