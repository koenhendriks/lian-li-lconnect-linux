using System.Text.Json;
using System.Windows.Media;
using LConnectCore.Devices;
using LConnectCore.Products;
using LConnectCore.Products.Ene6K77Fan;

namespace LConnect3cs.Products.Ene6K77Fan;

internal abstract class Ene6K77FanController : Controller
{
	public Ene6K77FanController(ControllerType type, string devicePath, USBDeviceID deviceId)
		: base(type, devicePath, deviceId)
	{
	}

	public abstract void SendPing(int groupIndex);

	public abstract void SendFanConfig();

	public abstract void SendFanConfig(int groupIndex);

	public abstract void SendFanQuantity();

	public abstract void SendLightingConfig();

	public abstract void SendLightingConfig(int groupIndex);

	public abstract void SendMergeOrder();

	public abstract Ene6K77FanProfile GetProfile();

	protected string sendFirmwareVersionRequest()
	{
		return sendControllerRequest(RequestType.FirmwareVersion, null);
	}

	protected void sendPingRequest(int groupIndex)
	{
		sendControllerRequest(RequestType.Ping, groupIndex);
	}

	protected void sendSetFanQuantityRequest(int[] quantityList)
	{
		sendControllerRequest(RequestType.FanQuantity, quantityList);
	}

	protected void sendSetFanSpeedRequest(int groupIndex, RPMConfig config)
	{
		sendSetFanSpeedRequest(new EneRPMConfig[1]
		{
			new EneRPMConfig
			{
				FanGroupIndex = groupIndex,
				Config = config
			}
		});
	}

	protected void sendSetFanSpeedRequest(EneRPMConfig[] configs)
	{
		sendControllerRequest(RequestType.SetFanSpeed, configs);
	}

	protected int[] sendGetFanSpeedRequest()
	{
		string responseBody = sendControllerRequest(RequestType.GetFanSpeed, null);
		if (tryGetResponseData<int[]>(responseBody, out var data))
		{
			return data;
		}
		return null;
	}

	protected void sendMergeOrder(int[] order)
	{
		sendControllerRequest(RequestType.MergeOrder, order);
	}

	protected void sendLightingSetting(LightingConfig[] configs)
	{
		sendControllerRequest(RequestType.LightingSetting, configs);
	}

	protected bool tryGetResponseData<T>(string responseBody, out T data)
	{
		data = default;
		try
		{
			if (string.IsNullOrEmpty(responseBody))
			{
				return false;
			}
			data = JsonSerializer.Deserialize<T>(responseBody);
			return true;
		}
		catch
		{
		}
		return false;
	}

	protected LightingConfig convertToLightingConfig(Ene6K77FanLightingSetting setting)
	{
		LightingConfig lightingConfig = new LightingConfig();
		lightingConfig.Mode = setting.Mode;
		lightingConfig.Direction = setting.Direction.GetValueOrDefault();
		lightingConfig.Colors = (Color[])setting.Colors.Clone();
		LightingConfig lightingConfig2 = lightingConfig;
		lightingConfig2.Speed = setting.Speed switch
		{
			0 => LightingSpeed.Lowest, 
			25 => LightingSpeed.Lower, 
			50 => LightingSpeed.Normal, 
			75 => LightingSpeed.Faster, 
			100 => LightingSpeed.Fastest, 
			_ => LightingSpeed.Normal, 
		};
		LightingConfig lightingConfig3 = lightingConfig;
		lightingConfig3.Brightness = setting.Brightness switch
		{
			0 => LightingBrightness.Lowest, 
			25 => LightingBrightness.Lower, 
			50 => LightingBrightness.Normal, 
			75 => LightingBrightness.Higher, 
			100 => LightingBrightness.Highest, 
			_ => LightingBrightness.Normal, 
		};
		return lightingConfig;
	}
}
