using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using LConnect3cs.Helpers;
using LConnectCore;
using LConnectCore.Devices;

namespace LConnect3cs.Products;

internal abstract class Controller : INotifyPropertyChanged
{
	protected static readonly string DirectoryPath;

	public ControllerType Type { get; protected set; }

	public string DevicePath { get; protected set; }

	public USBDeviceID DeviceID { get; protected set; }

	public abstract Version Version { get; }

	public bool AutoSaveProfile { get; set; }

	public bool AutoSendRequestAfterSave { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler ProfileReloaded;

	protected void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	static Controller()
	{
		DirectoryPath = LConnectApp.Path.ControllerProfileDirectory;
	}

	public Controller(ControllerType type, string devicePath, USBDeviceID deviceID)
	{
		Type = type;
		DevicePath = devicePath;
		DeviceID = deviceID;
	}

	public abstract void Init();

	public abstract void Dispose();

	public abstract void SaveProfile();

	public abstract void ReloadProfile();

	public string GetProfileName()
	{
		return getMD5String(DevicePath.ToLowerInvariant());
	}

	private string getMD5String(string str)
	{
		try
		{
			using MD5 mD = MD5.Create();
			byte[] array = mD.ComputeHash(Encoding.UTF8.GetBytes(str ?? string.Empty));
			StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
			byte[] array2 = array;
			foreach (byte b in array2)
			{
				stringBuilder.Append($"{b:x02}");
			}
			return stringBuilder.ToString();
		}
		catch
		{
		}
		return string.Empty;
	}

	protected string saveProfile(object profile)
	{
		if (profile == null)
		{
			return null;
		}
		string profileName = GetProfileName();
		if (string.IsNullOrEmpty(profileName))
		{
			return null;
		}
		try
		{
			if (!Directory.Exists(DirectoryPath))
			{
				Directory.CreateDirectory(DirectoryPath);
			}
			string text = Path.Combine(DirectoryPath, profileName);
			using FileStream stream = new FileStream(text, FileMode.Create, FileAccess.Write);
			using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Compress);
			byte[] array = JsonSerializer.SerializeToUtf8Bytes(profile);
			gZipStream.Write(array, 0, array.Length);
			return text;
		}
		catch
		{
		}
		return null;
	}

	protected bool loadProfile<T>(out T profile)
	{
		profile = default;
		string mD5String = getMD5String(DevicePath.ToLowerInvariant());
		if (string.IsNullOrEmpty(mD5String))
		{
			return false;
		}
		string path = Path.Combine(DirectoryPath, mD5String);
		if (!File.Exists(path))
		{
			return false;
		}
		try
		{
			using MemoryStream memoryStream = new MemoryStream();
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
			using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
			gZipStream.CopyTo(memoryStream);
			profile = JsonSerializer.Deserialize<T>(memoryStream.ToArray());
			return true;
		}
		catch (Exception)
		{
		}
		return false;
	}

	protected string sendControllerRequest(Enum type, object data)
	{
		try
		{
			NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
			byte[] bytes = Encoding.UTF8.GetBytes(DevicePath);
			nameValueCollection.Add("devicePath", Convert.ToBase64String(bytes));
			nameValueCollection.Add("type", type.ToString());
			if (LConnectServiceHttpUtility.TrySendRequest(HttpRequestActionType.Device, out var json, nameValueCollection, data))
			{
				return json;
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	protected void onProfileReloaded()
	{
		ProfileReloaded?.Invoke(this, EventArgs.Empty);
	}
}
