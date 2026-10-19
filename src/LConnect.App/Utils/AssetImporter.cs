using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LConnectCore;
using SkiaSharp;

namespace LConnect3cs.Utils;

// Copies the vendor assets the daemon and UI need (LCD sensor screens, built-in animations, media library and the
// default LCD logo) from a Windows L-Connect 3 install into the data directory. Shared by 'lconnectctl assets import'
// and the GUI's Settings page.
internal static class AssetImporter
{
	public sealed class Result
	{
		public List<string> Messages { get; } = new List<string>();

		public int FilesCopied { get; set; }

		public bool LogoExtracted { get; set; }
	}

	private static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public static bool LooksLikeInstallDirectory(string installDir)
	{
		return !string.IsNullOrEmpty(installDir) && File.Exists(Path.Combine(installDir, "assets", "tl-sensor", "index.html"));
	}

	// True when the LCD sensor screens have been imported (the daemon renders them for sensor mode).
	public static bool HasSensorAssets => File.Exists(Path.Combine(LConnectApp.Path.AssetsDirectory, "tl-sensor", "index.html"));

	// True when the built-in LCD animations have been imported.
	public static bool HasAnimations => Directory.Exists(Path.Combine(LConnectApp.Path.RepoDirectory, "animation"));

	public static Result Import(string installDir)
	{
		if (!LooksLikeInstallDirectory(installDir))
		{
			throw new ArgumentException(installDir + " does not look like an L-Connect 3 install (assets/tl-sensor/index.html missing)");
		}
		Result result = new Result();
		Dictionary<string, string> manifest = new Dictionary<string, string>();
		int num = copyTree(Path.Combine(installDir, "assets", "tl-sensor"), Path.Combine(LConnectApp.Path.AssetsDirectory, "tl-sensor"), manifest);
		result.FilesCopied += num;
		result.Messages.Add($"LCD sensor screens: {num} file(s)");
		string[] array = new string[4] { "animation", "jpg", "gif", "mp4" };
		foreach (string text in array)
		{
			string text2 = Path.Combine(installDir, "repo", text);
			if (Directory.Exists(text2))
			{
				int num2 = copyTree(text2, Path.Combine(LConnectApp.Path.RepoDirectory, text), manifest);
				result.FilesCopied += num2;
				result.Messages.Add($"repo/{text}: {num2} file(s)");
			}
		}
		string path = Path.Combine(installDir, "L-Connect-Service.exe");
		if (File.Exists(path))
		{
			byte[] array2 = ExtractLogo(File.ReadAllBytes(path));
			if (array2 != null)
			{
				Directory.CreateDirectory(LConnectApp.Path.AssetsDirectory);
				string text3 = Path.Combine(LConnectApp.Path.AssetsDirectory, "logo-400x400.jpg");
				File.WriteAllBytes(text3, array2);
				manifest["logo-400x400.jpg"] = sha256(text3);
				result.LogoExtracted = true;
				result.Messages.Add("Default LCD logo extracted.");
			}
			else
			{
				result.Messages.Add("Default LCD logo not found in L-Connect-Service.exe (a plain screen will be used).");
			}
		}
		Directory.CreateDirectory(LConnectApp.Path.AssetsDirectory);
		File.WriteAllText(Path.Combine(LConnectApp.Path.AssetsDirectory, "manifest.json"), JsonSerializer.Serialize(new
		{
			Source = Path.GetFullPath(installDir),
			Imported = DateTime.Now,
			Files = manifest
		}, Pretty));
		result.Messages.Add("Assets imported into " + LConnectApp.Path.DataRoot);
		return result;
	}

	private static int copyTree(string from, string to, Dictionary<string, string> manifest)
	{
		int num = 0;
		foreach (string item in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
		{
			string relativePath = Path.GetRelativePath(from, item);
			string text = Path.Combine(to, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			FileSystemHelper.CopyShared(item, text);
			manifest[Path.GetRelativePath(LConnectApp.Path.DataRoot, text).Replace('\\', '/')] = sha256(text);
			num++;
		}
		return num;
	}

	private static string sha256(string path)
	{
		using FileStream inputStream = File.OpenRead(path);
		return Convert.ToHexString(SHA256.HashData(inputStream)).ToLowerInvariant();
	}

	// The service embeds its default 400x400 LCD logo as a JPEG inside a .NET resource; find it by its markers.
	public static byte[] ExtractLogo(byte[] exe)
	{
		for (int i = 0; i + 3 < exe.Length; i++)
		{
			if (exe[i] != byte.MaxValue || exe[i + 1] != 216 || exe[i + 2] != byte.MaxValue)
			{
				continue;
			}
			for (int j = i + 3; j + 1 < exe.Length && j < i + 4194304; j++)
			{
				if (exe[j] == byte.MaxValue && exe[j + 1] == 217)
				{
					byte[] array = new byte[j + 2 - i];
					Array.Copy(exe, i, array, 0, array.Length);
					using SKBitmap sKBitmap = SKBitmap.Decode(array);
					if (sKBitmap != null && sKBitmap.Width == 400 && sKBitmap.Height == 400)
					{
						return array;
					}
				}
			}
		}
		return null;
	}
}
