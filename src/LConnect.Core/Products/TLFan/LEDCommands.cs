namespace LConnectCore.Products.TLFan;

public enum LEDCommands : byte
{
	Handshake = 161,
	GetLightModeList = 162,
	SetFanLight = 163,
	GetProductInfo = 166,
	LEDTestMode = 167,
	GetSerialNumber = 168,
	SetSerialNumber = 169,
	SetFanSpeed = 170,
	ActiveFirmware = 171,
	SendFirmware = 172,
	SetFanGrouping = 173,
	SetFanDirection = 174,
	SetPortDirection = 175,
	SetFanGroupLight = 176,
	SetMBFanSync = 177,
	LCDScreenSwitch = 178,
	TestLight = 179
}
