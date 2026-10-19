namespace LConnectCore.Products.TLLCD;

public static class Constants
{
	public const int ScreenBrightnessMin = 0;

	public const int ScreenBrightnessMax = 100;

	public const int ScreenWidth = 400;

	public const int ScreenHeight = 400;

	public const int MaxScreenFramePerSecond = 30;

	// Rate the daemon streams frames at. Windows uses a fixed 25; LCONNECT_LCD_FPS overrides it. The TL LCD
	// firmware completes at most one frame per ~29.5 ms (~34 fps), whatever is requested.
	public static int StreamFramesPerSecond { get; } = int.TryParse(System.Environment.GetEnvironmentVariable("LCONNECT_LCD_FPS"), out int fps) ? System.Math.Clamp(fps, 1, 120) : 25;

	public const int PortCount = 4;

	public const int MaxLCDFanCount = 3;
}
