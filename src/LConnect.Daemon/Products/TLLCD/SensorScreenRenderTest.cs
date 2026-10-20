using System;
using System.IO;
using LConnectCore.Products.TLLCD;

namespace LConnectService.Products.TLLCD;

// lconnectd --render-sensor <prefix>: renders every style with a few readings and colours to
// <prefix>.style<N>.<case>.jpg (N as the GUI numbers styles). Touches no device.
internal static class SensorScreenRenderTest
{
	private static readonly (string Name, int[] Background, int[] Background2, SensorReading Reading)[] cases = new(string, int[], int[], SensorReading)[4]
	{
		("cputemp", new int[3] { 255, 0, 0 }, new int[3] { 255, 255, 0 }, new SensorReading("CPU", 55, "TEMP", "°C", 65.0)),
		("tempload", new int[3] { 0, 0, 255 }, new int[3] { 0, 255, 255 }, new SensorReading("CPU", 48, "Load: 30%", "°C", 30.0)),
		("zero", new int[3] { 0, 200, 80 }, new int[3] { 200, 0, 80 }, new SensorReading("GPU", 0, "LOAD", "%", 0.0)),
		("full", new int[3] { 123, 45, 210 }, new int[3] { 250, 140, 10 }, new SensorReading("FAN", 1600, "RPM", "RPM", 100.0))
	};

	public static int Run(string prefix)
	{
		if (!File.Exists(Path.Combine(SensorScreenRenderer.AssetDirectory, "index.html")))
		{
			Console.Error.WriteLine("LCD sensor assets missing; the styles draw without their masks and artwork. Run 'lconnectctl assets import --from <L-Connect 3 install dir>'.");
		}
		for (int style = 0; style < 5; style++)
		{
			foreach (var (text, background, background2, reading) in cases)
			{
				SensorScreenRenderer renderer = new SensorScreenRenderer();
				renderer.Configure(new TLLCDSensorSetting
				{
					Type = TLLCDSensorType.CPUTemp,
					Style = style,
					BackgroundColor1 = background,
					BackgroundColor2 = background2,
					TextColor1 = new int[3] { 255, 255, 255 },
					TextColor2 = new int[3] { 255, 255, 255 },
					TextColor3 = new int[3] { 255, 255, 255 }
				});
				renderer.SetReading(reading);
				string path = $"{prefix}.style{style + 1}.{text}.jpg";
				File.WriteAllBytes(path, renderer.LatestFrame);
				Console.WriteLine("wrote " + path);
			}
		}
		return 0;
	}
}
