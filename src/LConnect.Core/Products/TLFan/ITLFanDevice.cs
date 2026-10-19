using LConnectCore.Devices;

namespace LConnectCore.Products.TLFan;

public interface ITLFanDevice : IHidDeviceWrapper
{
	TLFanHandshakeInfo GetHandshakeInfo();

	void SetFanLight(int port, int fanIndex, LightingConfig config, bool isSync, bool disable);

	FirmwareVersion GetControllerFirmwareVersion();

	FirmwareVersion GetFanFirmwareVersion(int port, int fanIndex);

	void SetFanSpeed(int port, int fanIndex, int pwm);

	void SetFanGroup(int groupNumber, FanGroupParams[] groupParams);

	void SetFanDirection(int port, int fanIndex, bool isSwapTopBottom, bool isSwapLeftRight);

	void SetPortDirection(int port, bool isSwap);

	void SetGroupLight(int groupNumber, LightingConfig config, bool disable);

	void SetMBRPMSync(int port, int fanIndex, bool isSync);

	void TestFanLighting(int port, int fanIndex, bool isBottom);

	void TestGroupLighting(int groupNumber, bool isBottom);

	void TestPortLighting(int port, bool isBottom);
}
