using System.Windows.Media;

namespace LConnectCore.Products.TLLCD;

public struct LCDControlSetting
{
	public LCDControlMode Mode { get; set; } = LCDControlMode.Reserver;

	public ushort JPGIndex { get; set; } = 0;

	public byte Brightness { get; set; } = 50;

	// The original always sends 30. Above that the firmware is told the faster stream rate, in case it paces by it.
	public byte VideoFPS { get; set; } = (byte)System.Math.Max(30, Constants.StreamFramesPerSecond);

	public ScreenRotation Rotation { get; set; } = ScreenRotation.Rotate0;

	public bool EnableTest { get; set; } = false;

	public Color TestColor { get; set; } = Color.FromRgb(0, 0, 0);

	public byte[] ToBytes()
	{
		return new byte[11]
		{
			(byte)Mode,
			(byte)((JPGIndex >> 8) & 0xFF),
			(byte)(JPGIndex & 0xFF),
			0,
			Brightness,
			VideoFPS,
			(byte)Rotation,
			EnableTest ? ((byte)1) : ((byte)0),
			TestColor.R,
			TestColor.G,
			TestColor.B
		};
	}

	public LCDControlSetting()
	{
	}
}
