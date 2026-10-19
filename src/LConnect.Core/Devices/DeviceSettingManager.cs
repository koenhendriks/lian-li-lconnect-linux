using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LConnectCore.Devices;

public static class DeviceSettingManager
{
	private static string DirectoryPath => LConnectApp.Path.DeviceSettingDirectory;

	public static string FileExtension { get; private set; }

	static DeviceSettingManager()
	{
		FileExtension = "0";
	}

	public static string Save<T>(DeviceSetting<T> setting)
	{
		if (setting == null)
		{
			return null;
		}
		string mD5String = getMD5String(setting.DevicePath.ToLowerInvariant());
		string mD5String2 = getMD5String(setting.Type);
		if (string.IsNullOrEmpty(mD5String) || string.IsNullOrEmpty(mD5String2))
		{
			return null;
		}
		string text = Path.Combine(DirectoryPath, mD5String);
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		string text2 = Path.Combine(text, mD5String2 + "." + FileExtension);
		using FileStream stream = new FileStream(text2, FileMode.Create, FileAccess.Write);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Compress);
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(setting);
		gZipStream.Write(array, 0, array.Length);
		return text2;
	}

	// Folder name used for a device's settings (md5 of the lower-cased device path).
	public static string GetDeviceKey(string devicePath)
	{
		return getMD5String(devicePath.ToLowerInvariant());
	}

	public static string GetMD5String(string str)
	{
		return getMD5String(str);
	}

	private static string getMD5String(string str)
	{
		try
		{
			using MD5 mD = MD5.Create();
			byte[] bytes = Encoding.UTF8.GetBytes(str);
			return BitConverter.ToString(mD.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
		}
		catch
		{
		}
		return string.Empty;
	}

	public static DeviceSetting<object> Load(string path)
	{
		return Load<object>(path);
	}

	public static IEnumerable<DeviceSetting<object>> LoadAll(string devicePath)
	{
		devicePath = devicePath.ToLowerInvariant();
		string mD5String = getMD5String(devicePath);
		string path = Path.Combine(DirectoryPath, mD5String);
		if (!Directory.Exists(path))
		{
			yield break;
		}
		string searchPattern = "*." + FileExtension;
		foreach (string item in Directory.EnumerateFiles(path, searchPattern))
		{
			DeviceSetting<object> deviceSetting = null;
			try
			{
				deviceSetting = Load(item);
				_ = deviceSetting.DevicePath != devicePath;
			}
			catch
			{
			}
			if (deviceSetting != null)
			{
				yield return deviceSetting;
			}
		}
	}

	public static DeviceSetting<T> Load<T>(string path)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		gZipStream.CopyTo(memoryStream);
		return JsonSerializer.Deserialize<DeviceSetting<T>>(memoryStream.ToArray());
	}

	public static bool TryParseData<T>(DeviceSetting<object> setting, out T data)
	{
		JsonElement? jsonElement = setting.Data as JsonElement?;
		if (jsonElement.HasValue)
		{
			data = jsonElement.Value.Deserialize<T>();
			return true;
		}
		data = default;
		return false;
	}
}
