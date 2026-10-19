namespace LConnectCore.Products.TLLCD;

public enum LCDCommands : byte
{
	ActiveFirmware = 58,
	WriteFirmware = 59,
	GetHandshakeInfo = 60,
	GetProductInfo = 61,
	ReadSerialNumber = 62,
	LCDControl = 64,
	WriteJPG = 65,
	WriteAVI = 69,
	WriteSYNCJPG = 70,
	WriteBootAVI = 71,
	WriteBootJPG = 72
}
