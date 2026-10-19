using System;
using System.Collections.Specialized;
using System.Text.Json;
using System.Web;
using LConnect3cs.Helpers;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Devices;
using LConnectCore.Products.TLLCD;

namespace LConnect3cs.Products.TLFan;

internal class TLLCDController : Controller
{
	public override Version Version
	{
		get
		{
			if (TLFanStore.Instance.Controllers.Count == 0 || !trySendGetAllTLLCDFirmwareVersion(out var productInfos) || productInfos.Length == 0)
			{
				return null;
			}
			Version version = null;
			ProductInfo[] array = productInfos;
			foreach (ProductInfo productInfo in array)
			{
				try
				{
					if (Version.TryParse(productInfo.Version.SoftwareVersion?.Substring(1), out var result) && ((object)version == null || result < version))
					{
						version = result;
					}
				}
				catch
				{
				}
			}
			if ((object)version == null)
			{
				return null;
			}
			return new Version(version.Minor, version.Build);
		}
	}

	public TLLCDController()
		: this(ControllerType.TLLCD, "TLLCD", new USBDeviceID(1276, 29587))
	{
	}

	public TLLCDController(ControllerType type, string devicePath, USBDeviceID deviceID)
		: base(type, devicePath, deviceID)
	{
	}

	public override void Init()
	{
	}

	public override void Dispose()
	{
	}

	public override void ReloadProfile()
	{
	}

	public override void SaveProfile()
	{
	}

	private static bool trySendTLLCDRequest(string cmd, out string json)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("cmd", cmd);
		return LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.TLLCD, out json, nameValueCollection, null, 10000);
	}

	private bool trySendGetAllTLLCDFirmwareVersion(out ProductInfo[] productInfos)
	{
		productInfos = Array.Empty<ProductInfo>();
		if (trySendTLLCDRequest("getAllDevices", out var json))
		{
			Logger.Instance.Info("-- TL LCD Firmware Version: " + json);
			try
			{
				productInfos = JsonSerializer.Deserialize<ProductInfo[]>(json);
				return productInfos != null;
			}
			catch
			{
			}
		}
		return false;
	}

	public bool SendStartAllTLLCD()
	{
		string json;
		return trySendTLLCDRequest("start", out json);
	}

	public bool SendStopAllTLLCD()
	{
		string json;
		return trySendTLLCDRequest("stop", out json);
	}
}
