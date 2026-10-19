using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using LConnect3cs.Helpers;
using LConnectCore;
using LConnectCore.Products.TLFan;

namespace LConnectUi.Services;

public sealed class DaemonControllerInfo
{
	public string DevicePath { get; set; }

	public string Type { get; set; }

	public int VendorID { get; set; }

	public int ProductID { get; set; }

	public string Product { get; set; }

	public string SettingsKey { get; set; }
}

public sealed class DaemonLcdInfo
{
	public string ControllerDevicePath { get; set; }

	public string DevicePath { get; set; }

	public int PortNo { get; set; }

	public int LCDIndex { get; set; }

	public string Firmware { get; set; }
}

public sealed class DaemonStatus
{
	public List<DaemonControllerInfo> Controllers { get; } = new List<DaemonControllerInfo>();

	public List<DaemonLcdInfo> Lcds { get; } = new List<DaemonLcdInfo>();

	public SystemResource? Resource { get; set; }

	public string DataDirectory { get; set; }
}

// Blocking calls to lconnectd (Unix socket). Call these off the UI thread.
public static class DaemonClient
{
	public static string SocketPath => LConnectApp.Path.ServiceSocketPath;

	public static DaemonStatus GetStatus(int timeout = 3000)
	{
		string text;
		try
		{
			text = LConnectServiceHttpUtility.Send(HttpMethod.Get, "status", null, timeout);
		}
		catch
		{
			return null;
		}
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		return ParseStatus(text);
	}

	public static DaemonStatus ParseStatus(string json)
	{
		DaemonStatus daemonStatus = new DaemonStatus();
		JsonNode jsonNode = JsonNode.Parse(json);
		if (jsonNode == null)
		{
			return daemonStatus;
		}
		daemonStatus.DataDirectory = (string)jsonNode["DataDirectory"];
		if (jsonNode["Controllers"] is JsonArray jsonArray)
		{
			foreach (JsonNode item in jsonArray)
			{
				daemonStatus.Controllers.Add(new DaemonControllerInfo
				{
					DevicePath = (string)item?["DevicePath"],
					Type = (string)item?["Type"],
					VendorID = (int?)item?["VendorID"] ?? 0,
					ProductID = (int?)item?["ProductID"] ?? 0,
					Product = (string)item?["Product"],
					SettingsKey = (string)item?["SettingsKey"]
				});
			}
		}
		if (jsonNode["LCDs"] is JsonArray jsonArray2)
		{
			foreach (JsonNode item2 in jsonArray2)
			{
				daemonStatus.Lcds.Add(new DaemonLcdInfo
				{
					ControllerDevicePath = (string)item2?["ControllerDevicePath"],
					DevicePath = (string)item2?["DevicePath"],
					PortNo = (int?)item2?["PortNo"] ?? 0,
					LCDIndex = (int?)item2?["LCDIndex"] ?? 0,
					Firmware = (string)item2?["Version"]?["SoftwareVersion"]
				});
			}
		}
		JsonNode jsonNode2 = jsonNode["Resource"];
		if (jsonNode2 != null)
		{
			try
			{
				daemonStatus.Resource = jsonNode2.Deserialize<SystemResource>();
			}
			catch
			{
			}
		}
		return daemonStatus;
	}

	public static SystemResource? GetSystemResource()
	{
		if (LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.SystemResource, out var json, null, null, 3000) && !string.IsNullOrEmpty(json))
		{
			try
			{
				return JsonSerializer.Deserialize<SystemResource>(json);
			}
			catch
			{
			}
		}
		return null;
	}

	public static string EncodePath(string devicePath)
	{
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(devicePath ?? string.Empty));
	}

	public static FanGroupStatus GetFanGroupStatus(string tlDevicePath)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("devicePath", EncodePath(tlDevicePath));
		nameValueCollection.Add("type", RequestType.FanGroupStatus.ToString());
		if (LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.Device, out var json, nameValueCollection, null, 3000) && !string.IsNullOrEmpty(json))
		{
			try
			{
				return JsonSerializer.Deserialize<FanGroupStatus>(json);
			}
			catch
			{
			}
		}
		return null;
	}

	public static int[] GetSLFanSpeeds(string devicePath)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("devicePath", EncodePath(devicePath));
		nameValueCollection.Add("type", LConnectCore.Products.Ene6K77Fan.RequestType.GetFanSpeed.ToString());
		if (LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.Device, out var json, nameValueCollection, null, 3000) && !string.IsNullOrEmpty(json))
		{
			try
			{
				return JsonSerializer.Deserialize<int[]>(json);
			}
			catch
			{
			}
		}
		return null;
	}

	// The TL LCD page protocol (action=TLLCD&control=...&devicePath=<TL controller>&port=N), exactly as the original
	// TLFanUtilityLCDScreenPage.sendRequest. Returns null on failure, "" for requests without a response body.
	public static string SendTLLCD(string control, string tlDevicePath, int? port, object data, int timeout = 10000)
	{
		try
		{
			NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
			nameValueCollection.Add("control", control);
			if (!string.IsNullOrEmpty(tlDevicePath))
			{
				nameValueCollection.Add("devicePath", EncodePath(tlDevicePath));
			}
			if (port.HasValue)
			{
				nameValueCollection.Add("port", port.Value.ToString());
			}
			if (LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.TLLCD, out var json, nameValueCollection, data, timeout))
			{
				return json;
			}
		}
		catch
		{
		}
		return null;
	}

	public static List<DaemonLcdInfo> GetAllLcdDevices()
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("cmd", "getAllDevices");
		List<DaemonLcdInfo> list = new List<DaemonLcdInfo>();
		if (LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.TLLCD, out var json, nameValueCollection, null, 10000) && !string.IsNullOrEmpty(json))
		{
			try
			{
				if (JsonNode.Parse(json) is JsonArray jsonArray)
				{
					foreach (JsonNode item in jsonArray)
					{
						list.Add(new DaemonLcdInfo
						{
							ControllerDevicePath = (string)item?["ControllerDevicePath"],
							DevicePath = (string)item?["DevicePath"],
							PortNo = (int?)item?["PortNo"] ?? 0,
							LCDIndex = (int?)item?["LCDIndex"] ?? 0,
							Firmware = (string)item?["Version"]?["SoftwareVersion"]
						});
					}
				}
			}
			catch
			{
			}
		}
		return list;
	}

	public static bool PostReapply()
	{
		try
		{
			return LConnectServiceHttpUtility.Send(HttpMethod.Post, "reapply", Array.Empty<byte>(), 60000) != null;
		}
		catch
		{
			return false;
		}
	}
}
