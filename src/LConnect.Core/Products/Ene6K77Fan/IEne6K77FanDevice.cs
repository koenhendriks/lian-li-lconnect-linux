using System.Windows.Media;
using LConnectCore.Devices;

namespace LConnectCore.Products.Ene6K77Fan;

public interface IEne6K77FanDevice : IHidDeviceWrapper
{
	int WriteDelayTime { get; set; }

	void SetEffectSetting(byte port, byte mode, byte speed, byte direction, byte brightness);

	void SetColorSetting(byte port, Color[] colors);

	FirmwareVersion GetFirmwareVersion();

	int GetFrame();

	void SetFrame(int frame);

	int[] GetFanSpeed();

	void SetFanSpeed(int groupIndex, int duty);

	void SetFanMotherboardSync(int groupIndex, bool isSync);

	void SetLightingMotherboardSync(bool isSync);

	void Ping(int groupIndex, byte speed, byte brightness, bool isMerged);

	void SetQuantity(int groupIndex, int quantity);

	void SetMergeOrder(int[] groupIndexOrder);
}
