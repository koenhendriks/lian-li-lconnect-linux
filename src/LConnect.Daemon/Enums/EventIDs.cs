using System;

namespace LConnectService.Enums;

internal enum EventIDs
{
	None = 0,
	ServiceStart = 1,
	ServiceStop = 2,
	DeviceWatcherErrorOnStart = 6,
	DeviceWatcherErrorOnStop = 7,
	DeviceWatcherInsertedEvent = 8,
	DeviceWatcherRemovedEvent = 9,
	CoreInitFailed = 10,
	CoreInitError = 11,
	SearchDeviceResult = 12,
	OpenDeviceError = 13,
	HttpListenerErrorOnStart = 14,
	InitCPUIDSDKFailed = 15,
	SystemResourceUpdaterErrorOnStart = 16,
	RunFFMpegError = 17,
	InitCefError = 18,
	InitFFMEError = 19,
	InitControllerError = 20,
	ApplyControllerSettingOnStartError = 21,
	InitGPUZFailed = 22,
	InitCPUIDSDKRetry = 23,
	GA2Vision_InitLibAVError = 1700,
	GA2Vision_InitWebBrowser = 1701,
	[Obsolete("風扇韌體 1.00.44 增加群組上限後不使用")]
	TLFan_TooManyLightGroup = 1800,
	TLFan_FanGroupStatus = 1801
}
