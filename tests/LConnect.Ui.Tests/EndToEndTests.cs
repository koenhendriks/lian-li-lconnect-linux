using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using LConnect3cs.Enums;
using LConnect3cs.Products.Ene6K77Fan;
using LConnect3cs.Products.TLFan;
using LConnectCore.Products;
using LConnectCore.Products.TLFan;
using LConnectCore.Products.TLLCD;
using LConnectUi.Services;
using LConnectUi.ViewModels;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;
using AColor = Avalonia.Media.Color;

namespace LConnect.Ui.Tests;

// Drives the page view models against the simulated daemon and checks what reaches the simulated hardware
// (<data>/sim/*.log and the LCD frame dumps).
public class EndToEndTests
{
	private readonly ITestOutputHelper output;

	public EndToEndTests(ITestOutputHelper output)
	{
		this.output = output;
	}

	private static string sim(string name)
	{
		return Path.Combine(TestEnvironment.SimDir, name);
	}

	private static long length(string file)
	{
		if (!File.Exists(file))
		{
			return 0L;
		}
		return new FileInfo(file).Length;
	}

	private static string readFrom(string file, long offset)
	{
		if (!File.Exists(file))
		{
			return string.Empty;
		}
		using FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		if (offset > fileStream.Length)
		{
			offset = 0L;
		}
		fileStream.Seek(offset, SeekOrigin.Begin);
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
		return streamReader.ReadToEnd();
	}

	private static async Task<string> waitForLog(string file, long offset, Func<string, bool> predicate, int timeoutMs = 15000)
	{
		string text = string.Empty;
		await TestEnvironment.WaitFor(delegate
		{
			text = readFrom(file, offset);
			return predicate(text);
		}, timeoutMs);
		return text;
	}

	private static int frames(string stem)
	{
		string path = sim(stem + "-frames.txt");
		try
		{
			return File.Exists(path) ? int.Parse(File.ReadAllText(path).Trim()) : 0;
		}
		catch
		{
			return 0;
		}
	}

	private async Task<bool> requireDaemon()
	{
		bool flag = await SimulatedSetup.EnsureAsync(output);
		if (!flag)
		{
			Assert.True(TestEnvironment.DaemonExecutable == null, "lconnectd is built but did not start; see " + Path.Combine(TestEnvironment.DataDir, "daemon.log"));
			output.WriteLine("lconnectd not built - end-to-end test skipped.");
		}
		return flag;
	}

	[AvaloniaFact]
	public async Task Lighting_change_reaches_the_TL_controller()
	{
		if (!(await requireDaemon()))
		{
			return;
		}
		TLLightingViewModel vm = new TLLightingViewModel();
		vm.Activate();
		Assert.True(await TestEnvironment.WaitFor(() => vm.HasPorts && vm.SelectedMode != null, 15000), "TL ports did not show up");
		vm.SelectedPort = vm.Ports.FirstOrDefault((PortItem p) => p.Type == PortType.LED) ?? vm.Ports.First();
		await TestEnvironment.WaitFor(() => vm.SelectedGroup != null);
		vm.IsMerged = false;
		vm.SelectedScope = vm.Scopes[0];
		vm.SelectedTarget = vm.Targets[0];
		vm.SelectedMode = vm.Modes.First((TLModeItem m) => m.Mode == LightingMode.StaticColor);
		Assert.Equal(2, vm.Editor.ColorCount);
		vm.Editor.Colors[0].Color = AColor.FromRgb(byte.MaxValue, 0, 0);
		vm.Editor.Colors[1].Color = AColor.FromRgb(0, 0, byte.MaxValue);
		vm.Editor.Brightness = 50.0;
		string text = sim("tl-controller.log");
		long offset = length(text);
		await vm.ApplyAsync();
		Assert.False(vm.StatusIsError, vm.StatusMessage);
		string text2 = await waitForLog(text, offset, (string t) => t.Contains("cmd "));
		output.WriteLine("tl-controller.log after lighting apply:\n" + text2);
		Assert.Contains("cmd ", text2);
		TLFanController tLFanController = ControllerHub.Instance.TLControllers.First();
		TLFanPortProfile tLFanPortProfile = tLFanController.Profile.PortProfiles[vm.SelectedPort.Port];
		TLFanGroupProfile tLFanGroupProfile = ((tLFanPortProfile.PortType == PortType.LCD) ? tLFanPortProfile.LCDGroupProfiles : tLFanPortProfile.LEDGroupProfiles).First((TLFanGroupProfile g) => g.Index == vm.SelectedGroup.Index);
		Assert.Equal(LightingMode.StaticColor, tLFanGroupProfile.LightingMode);
		Assert.Equal(50, tLFanGroupProfile.LightingSettings[LightingMode.StaticColor].Brightness);
		Assert.Equal(byte.MaxValue, tLFanGroupProfile.LightingSettings[LightingMode.StaticColor].Colors[0].R);
		Assert.True(File.Exists(Path.Combine(LConnectCore.LConnectApp.Path.ControllerProfileDirectory, tLFanController.GetProfileName())), "profile was not saved");
		long offset2 = length(text);
		await vm.TestTopAsync();
		string text3 = await waitForLog(text, offset2, (string t) => t.Contains("cmd "), 8000);
		output.WriteLine("after test light:\n" + text3);
		vm.Deactivate();
	}

	[AvaloniaFact]
	public async Task Fan_profile_change_reaches_the_TL_controller()
	{
		if (!(await requireDaemon()))
		{
			return;
		}
		TLFanSpeedViewModel vm = new TLFanSpeedViewModel();
		vm.Activate();
		Assert.True(await TestEnvironment.WaitFor(() => vm.HasPorts && vm.Curve.HasCurve, 15000), "fan curve did not load");
		int port = vm.SelectedPort.Port;
		vm.Curve.SelectedMode = vm.Curve.Modes.First((RpmModeItem m) => m.Mode == RPMMode.FullSpeed);
		string file = sim("tl-controller.log");
		long offset = length(file);
		await vm.ApplyAsync();
		Assert.False(vm.StatusIsError, vm.StatusMessage);
		string text = await waitForLog(file, offset, (string t) => t.Contains($"SetFanSpeed port {port}") && t.Contains("pwm 100%"), 20000);
		output.WriteLine("tl-controller.log after FullSpeed:\n" + text);
		Assert.Contains("pwm 100%", text);
		// Back to a curve with a custom point: a quiet profile with the first point moved.
		vm.Curve.SelectedMode = vm.Curve.Modes.First((RpmModeItem m) => m.Mode == RPMMode.UserDefine1);
		vm.Curve.SelectedPointIndex = 2;
		vm.Curve.PointSpeed = vm.Curve.MinSpeed;
		RPMPhase[] phases = vm.Curve.Phases;
		Assert.True(phases[0].Speed <= phases[2].Speed && phases[1].Speed <= phases[2].Speed, "curve must stay non-decreasing");
		long offset2 = length(file);
		await vm.ApplyAsync();
		string text2 = await waitForLog(file, offset2, (string t) => t.Contains($"SetFanSpeed port {port}") && !t.Contains("pwm 100%"), 20000);
		output.WriteLine("after UserDefine1:\n" + text2);
		Assert.Contains("SetFanSpeed", text2);
		TLFanController tLFanController = ControllerHub.Instance.TLControllers.First();
		Assert.Equal(RPMMode.UserDefine1, (tLFanController.Profile.PortProfiles[port].PortType == PortType.LCD ? tLFanController.Profile.PortProfiles[port].LCDGroupProfiles : tLFanController.Profile.PortProfiles[port].LEDGroupProfiles).First((TLFanGroupProfile g) => g.Index == vm.SelectedGroup.Index).RPMSetting.Mode);
		vm.Deactivate();
	}

	private static string makeTestImage(int width, int height, string name)
	{
		string text = Path.Combine(TestEnvironment.DataDir, name);
		using SKBitmap sKBitmap = new SKBitmap(width, height);
		using (SKCanvas sKCanvas = new SKCanvas(sKBitmap))
		{
			using SKPaint sKPaint = new SKPaint
			{
				Shader = SKShader.CreateLinearGradient(new SKPoint(0f, 0f), new SKPoint(width, height), new SKColor[3]
				{
					new SKColor(230, 40, 90),
					new SKColor(40, 120, 230),
					new SKColor(40, 220, 120)
				}, SKShaderTileMode.Clamp)
			};
			sKCanvas.DrawRect(0f, 0f, width, height, sKPaint);
			using SKPaint sKPaint2 = new SKPaint
			{
				Color = SKColors.White,
				TextSize = height / 6f,
				IsAntialias = true
			};
			sKCanvas.DrawText("TEST", width * 0.3f, height * 0.55f, sKPaint2);
		}
		using SKImage sKImage = SKImage.FromBitmap(sKBitmap);
		using SKData sKData = sKImage.Encode(SKEncodedImageFormat.Png, 100);
		File.WriteAllBytes(text, sKData.ToArray());
		return text;
	}

	private static List<TLLCDScreenSetting> lcdSettings(string tlPath, int port)
	{
		string text = DaemonClient.SendTLLCD("setting", tlPath, port, null);
		if (!string.IsNullOrEmpty(text))
		{
			return JsonSerializer.Deserialize<List<TLLCDScreenSetting>>(text);
		}
		return new List<TLLCDScreenSetting>();
	}

	[AvaloniaFact]
	public async Task Lcd_image_sensor_and_canvas_layout_reach_the_LCDs()
	{
		if (!(await requireDaemon()))
		{
			return;
		}
		LcdViewModel vm = new LcdViewModel();
		vm.Activate();
		Assert.True(await TestEnvironment.WaitFor(() => vm.Screens.Count > 0 && !vm.IsLoadingMedia, 20000), "LCD screens did not load");
		int port = vm.SelectedPort.Port;
		string tl = vm.SelectedController.DevicePath;
		string stem = $"tl-lcd-p{port}-i0";
		vm.SelectScreen(vm.Screens[0]);
		vm.SelectedMediaType = vm.MediaTypes.First((LcdMediaTypeOption t) => t.Type == TLLCDTypeEnums.Picture);
		await TestEnvironment.WaitFor(() => !vm.IsLoadingMedia, 20000);
		string file = makeTestImage(640, 480, "lcd-test.png");
		string jpg = sim(stem + "-jpg.jpg");
		DateTime before = (File.Exists(jpg) ? File.GetLastWriteTimeUtc(jpg) : DateTime.MinValue);
		int framesBefore = frames(stem);
		string converted = await vm.AddFileAsync(file);
		Assert.False(string.IsNullOrEmpty(converted), vm.StatusMessage);
		await vm.WhenSent();
		Assert.True(await TestEnvironment.WaitFor(() => File.Exists(jpg) && File.GetLastWriteTimeUtc(jpg) > before, 20000), "the LCD did not receive the image");
		output.WriteLine($"{stem}: jpg received, frames {framesBefore} -> {frames(stem)}");
		TLLCDScreenSetting tLLCDScreenSetting = lcdSettings(tl, port).First((TLLCDScreenSetting s) => s.Index == 0);
		Assert.Equal(TLLCDMediaType.Image, tLLCDScreenSetting.MediaType);
		Assert.Equal(converted, tLLCDScreenSetting.FilePath);
		Assert.Equal(1, tLLCDScreenSetting.TotalBlock);
		Assert.True(vm.MediaItems.Single((LcdMediaItem m) => m.Media?.Path == converted).IsSelected);
		// Sensor mode: switching the type applies CPU temperature, style 1 (as the original page did).
		vm.SelectedMediaType = vm.MediaTypes.First((LcdMediaTypeOption t) => t.Type == TLLCDTypeEnums.Sensor);
		vm.SensorStyleIndex = 3;
		await vm.WhenSent();
		TLLCDScreenSetting tLLCDScreenSetting2 = lcdSettings(tl, port).First((TLLCDScreenSetting s) => s.Index == 0);
		Assert.Equal(TLLCDMediaType.Sensor, tLLCDScreenSetting2.MediaType);
		Assert.Equal(TLLCDSensorType.CPUTemp, tLLCDScreenSetting2.Sensor.Type);
		Assert.Equal(3, tLLCDScreenSetting2.Sensor.Style);
		Assert.NotNull(tLLCDScreenSetting2.Sensor.BackgroundColor2);
		int sensorFrames = frames(stem);
		bool flag = await TestEnvironment.WaitFor(() => frames(stem) > sensorFrames, 30000);
		output.WriteLine($"sensor frames streaming: {flag} ({sensorFrames} -> {frames(stem)})");
		// Rotation of a single LCD.
		vm.RotateScreen(vm.Screens[0]);
		await vm.WhenSent();
		Assert.Equal(ScreenRotation.Rotate270, lcdSettings(tl, port).First((TLLCDScreenSetting s) => s.Index == 0).Rotate);
		if (vm.DeviceCount >= 3)
		{
			// One picture across all three LCDs: three updates with Block 1..3 of a 1200x400 canvas.
			await vm.GroupChangedAsync(TLLCDGroupType.Group3);
			Assert.True(await TestEnvironment.WaitFor(() => vm.Screens.Count == 1 && vm.Screens[0].ScreenCount == 3, 15000));
			vm.SelectedMediaType = vm.MediaTypes.First((LcdMediaTypeOption t) => t.Type == TLLCDTypeEnums.Picture);
			await TestEnvironment.WaitFor(() => !vm.IsLoadingMedia, 20000);
			string canvas = await vm.AddFileAsync(makeTestImage(1200, 400, "lcd-canvas.png"));
			Assert.False(string.IsNullOrEmpty(canvas), vm.StatusMessage);
			await vm.WhenSent();
			List<TLLCDScreenSetting> source = lcdSettings(tl, port);
			List<TLLCDScreenSetting> list = source.OrderBy((TLLCDScreenSetting s) => s.Index).ToList();
			output.WriteLine(JsonSerializer.Serialize(list));
			Assert.Equal(3, list.Count((TLLCDScreenSetting s) => s.FilePath == canvas));
			Assert.All(list, delegate(TLLCDScreenSetting s)
			{
				Assert.Equal(3, s.TotalBlock);
				Assert.True(s.Horizontal);
				Assert.Equal(0, s.Sync);
			});
			Assert.Equal(new int[3] { 1, 2, 3 }, list.Select((TLLCDScreenSetting s) => s.Block).ToArray());
			string config = DaemonClient.SendTLLCD("config", tl, port, null);
			Assert.Equal(TLLCDGroupType.Group3, JsonSerializer.Deserialize<TLLCDConfig>(config).Group);
			await vm.GroupChangedAsync(TLLCDGroupType.Group111);
			await vm.WhenSent();
		}
		vm.Deactivate();
	}

	[AvaloniaFact]
	public async Task SL_Infinity_lighting_and_fan_reach_the_hub()
	{
		if (!(await requireDaemon()))
		{
			return;
		}
		SLInfinityViewModel vm = new SLInfinityViewModel();
		vm.Activate();
		Assert.True(await TestEnvironment.WaitFor(() => vm.HasController && vm.Modes.Count > 0, 15000), "SL Infinity did not show up");
		vm.SelectedScope = vm.Scopes[0];
		vm.SelectedMode = vm.Modes.First((SLModeItem m) => m.Mode == LConnectCore.Products.Ene6K77Fan.LightingMode.Rainbow);
		vm.IsMerged = false;
		vm.Editor.Speed = 100.0;
		string file = sim("sl-infinity.log");
		long offset = length(file);
		await vm.ApplyLightingAsync();
		Assert.False(vm.StatusIsError, vm.StatusMessage);
		string text = await waitForLog(file, offset, (string t) => t.Length > 0);
		output.WriteLine("sl-infinity.log after lighting:\n" + text);
		Assert.NotEmpty(text);
		SLInfinityController sLInfinityController = ControllerHub.Instance.SLInfinityControllers.First();
		Assert.Equal(LConnectCore.Products.Ene6K77Fan.LightingMode.Rainbow, sLInfinityController.Profile.SubProfiles[vm.SelectedGroup.Index].LightingMode);
		Assert.False(sLInfinityController.Profile.SubProfiles[vm.SelectedGroup.Index].IsIndividualMode);
		vm.Curve.SelectedMode = vm.Curve.Modes.First((RpmModeItem m) => m.Mode == RPMMode.HighSpeed);
		long offset2 = length(file);
		await vm.ApplyFanAsync();
		string text2 = await waitForLog(file, offset2, (string t) => t.Length > 0);
		output.WriteLine("sl-infinity.log after fan profile:\n" + text2);
		Assert.NotEmpty(text2);
		Assert.Equal(RPMMode.HighSpeed, sLInfinityController.Profile.SubProfiles[vm.SelectedGroup.Index].RPMSetting.Mode);
		vm.Deactivate();
	}
}
