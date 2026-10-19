namespace LConnectCore.Products.TLFan;

public enum ControllerCommands : byte
{
	Handshake = 1,
	EnterUpgradeMode = 11,
	WriteFirmware = 12,
	GetProductInfo = 13,
	GetSerialNumber = 21
}
