using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using LConnect3cs.Products;
using LConnect3cs.Products.Ene6K77Fan;
using LConnect3cs.Products.TLFan;
using LConnectCore;

namespace LConnect3cs.Utils;

internal class ProfileManager
{
	private class Metadata
	{
		public string Version { get; set; }

		public DateTime? CreatedAt { get; set; }
	}

	private const string FileDialogFilter = "Zip (*.zip)|*.zip";

	private const string FileDialogDefaultExt = ".zip";

	private const string MetadataJsonName = "metadata.json";

	private static readonly Lazy<ProfileManager> lazy = new Lazy<ProfileManager>(() => new ProfileManager());

	public static ProfileManager Instance => lazy.Value;

	public bool Import(string path)
	{
		try
		{
			List<(Controller, byte[])> list = readZip(path, getConnectedController());
			if (list == null)
			{
				return false;
			}
			foreach (var (controller, profile) in list)
			{
				writeAndReloadProfile(controller, profile);
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance.Error("Import error. '" + ex.Message + "'");
		}
		return false;
	}

	public bool Export(string path)
	{
		try
		{
			createZip(path, getConnectedController());
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance.Error("Export error. '" + ex.Message + "'");
		}
		return false;
	}

	private List<Controller> getConnectedController()
	{
		List<Controller> list = new List<Controller>();
		list.AddRange(SLInfinityStore.Instance.Controllers.Values);
		list.AddRange(TLFanStore.Instance.Controllers.Values);
		return list;
	}

	private void createZip(string path, List<Controller> controllers)
	{
		using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
		{
			using ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create);
			foreach (Controller controller in controllers)
			{
				string profileName = controller.GetProfileName();
				string text = Path.Combine(LConnectApp.Path.ControllerProfileDirectory, profileName);
				if (File.Exists(text))
				{
					addFileEntry(zip, profileName, text);
				}
			}
			addEntry(zip, "metadata.json", getMetadataJson());
		}
		static void addEntry(ZipArchive zipArchive, string entryName, byte[] data)
		{
			using Stream stream2 = zipArchive.CreateEntry(entryName, CompressionLevel.Optimal).Open();
			stream2.Write(data, 0, data.Length);
		}
		static void addFileEntry(ZipArchive zip2, string entryName, string filePath)
		{
			if (File.Exists(filePath))
			{
				addEntry(zip2, entryName, File.ReadAllBytes(filePath));
			}
		}
		static byte[] getMetadataJson()
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			return JsonSerializer.SerializeToUtf8Bytes(new Metadata
			{
				Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
				CreatedAt = DateTime.Now
			});
		}
	}

	private List<(Controller, byte[])> readZip(string path, List<Controller> controllers)
	{
		using FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read);
		using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
		if (readMetadata(readZipEntry(zipArchive.GetEntry("metadata.json"))) == null)
		{
			return null;
		}
		List<(Controller, byte[])> list = new List<(Controller, byte[])>();
		foreach (Controller controller in controllers)
		{
			string profileName = controller.GetProfileName();
			byte[] array = readZipEntry(zipArchive.GetEntry(profileName));
			if (array != null && array.Length != 0)
			{
				list.Add((controller, array));
			}
		}
		return list;
	}

	private byte[] readZipEntry(ZipArchiveEntry entry)
	{
		if (entry == null || entry.Length == 0L)
		{
			return null;
		}
		byte[] array = new byte[entry.Length];
		using Stream stream = entry.Open();
		stream.Read(array, 0, array.Length);
		return array;
	}

	private Metadata readMetadata(byte[] bytes)
	{
		if (bytes == null)
		{
			return null;
		}
		try
		{
			Metadata metadata = JsonSerializer.Deserialize<Metadata>(bytes);
			if (Version.TryParse(metadata.Version, out var _) && metadata.CreatedAt.HasValue)
			{
				return metadata;
			}
		}
		catch
		{
		}
		return null;
	}

	private void writeAndReloadProfile(Controller controller, byte[] profile)
	{
		try
		{
			File.WriteAllBytes(Path.Combine(LConnectApp.Path.ControllerProfileDirectory, controller.GetProfileName()), profile);
			controller.ReloadProfile();
		}
		catch (Exception)
		{
		}
	}

	private ProfileManager()
	{
	}
}
