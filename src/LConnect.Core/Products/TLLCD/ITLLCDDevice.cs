using LConnectCore.Devices;

namespace LConnectCore.Products.TLLCD;

public interface ITLLCDDevice : IHidDeviceWrapper
{
	event GetHandshakeInfoEventHandler OnGetHandshakeInfo;

	bool Open();

	void SendGetHandshakeInfo();

	HandshakeInfo GetHandshakeInfo();

	string GetPortString();

	FirmwareVersion GetFirmwareVersion();

	FirmwareVersion FirmwareVersionV();

	int LCDControl(LCDControlSetting setting);

	int SendJPG(byte[] bytes);

	int SendAVI(byte[] bytes);

	int SendSyncJPG(byte[] bytes);

	System.TimeSpan LastFirstPacketTime { get; }

	int SendBootVideo(byte[] bytes);

	int SendBootJPG(byte[] bytes);

	int ReadPacket();

	int GetPort();

	int GetLCDIndex();

	bool IsMatched();

	void SetMatched(bool value);
}
