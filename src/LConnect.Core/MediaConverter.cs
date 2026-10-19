using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FFMpegCore;
using LConnectCore.Products.TLLCD;
using SkiaSharp;

namespace LConnectCore;

// Port of L-Connect's MediaConverter. The original decoded video with FFmpeg.AutoGen (bound to the bundled
// ffmpeg 6 DLLs) and used System.Drawing for scaling/cropping. Here decoding runs through the ffmpeg CLI
// (no ABI coupling to the distro's libav* versions) and image work through SkiaSharp.
public sealed class MediaConverter
{
	// The LCD firmware rejects frames larger than this (the original enforced it the same way).
	private const int TargetSizeInBytes = 65535;

	private const int ScreenSize = 400;

	public static string FFmpegBinary => resolveBinary("ffmpeg");

	public static string FFprobeBinary => resolveBinary("ffprobe");

	private static string resolveBinary(string name)
	{
		string environmentVariable = Environment.GetEnvironmentVariable("LCONNECT_FFMPEG_DIR");
		string text = (OperatingSystem.IsWindows() ? (name + ".exe") : name);
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			return Path.Combine(environmentVariable, text);
		}
		return text;
	}

	private static (int Width, int Height) canvasSize(int sizeCount, bool horizontal)
	{
		(int, int) result = sizeCount switch
		{
			2 => (400, 800),
			3 => (400, 1200),
			_ => (400, 400),
		};
		if (horizontal)
		{
			result = (result.Item2, result.Item1);
		}
		return result;
	}

	public static string MediaDirectory(TLLCDMediaType type, int sizeCount, bool horizontal)
	{
		string path = type switch
		{
			TLLCDMediaType.Image => "jpg",
			TLLCDMediaType.Dynamic => "gif",
			TLLCDMediaType.Video => "mp4",
			TLLCDMediaType.Animation => "animation",
			_ => "other",
		};
		return Path.Combine(LConnectApp.Path.RepoDirectory, path, sizeCount.ToString(), horizontal ? "h" : "v");
	}

	public static string SaveImage(string file, int sizeCount, bool horizontal)
	{
		string result = string.Empty;
		(int Width, int Height) tuple = canvasSize(sizeCount, horizontal);
		try
		{
			using SKBitmap sKBitmap = SKBitmap.Decode(file);
			if (sKBitmap == null)
			{
				return result;
			}
			// Same as the original: stretch to the canvas, no aspect-ratio preservation (the UI crops first).
			using SKBitmap sKBitmap2 = sKBitmap.Resize(new SKImageInfo(tuple.Width, tuple.Height, SKColorType.Rgba8888, SKAlphaType.Premul), SKFilterQuality.High);
			string text = MediaDirectory(TLLCDMediaType.Image, sizeCount, horizontal);
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, Guid.NewGuid().ToString() + ".jpg");
			File.WriteAllBytes(text2, EncodeJpeg(sKBitmap2, 90));
			result = text2;
		}
		catch
		{
		}
		return result;
	}

	public static double VideoDuration(string file)
	{
		TimeSpan? videoDuration = GetVideoDuration(file);
		if (!videoDuration.HasValue)
		{
			return 0.0;
		}
		return videoDuration.Value.TotalMilliseconds;
	}

	public static string SaveGif(string file, int sizeCount, bool horizontal, Action<int> progress)
	{
		return saveVideo(file, MediaDirectory(TLLCDMediaType.Dynamic, sizeCount, horizontal), sizeCount, horizontal, progress, gif: true);
	}

	public static string SaveMP4(string file, int sizeCount, bool horizontal, Action<int> progress)
	{
		return saveVideo(file, MediaDirectory(TLLCDMediaType.Video, sizeCount, horizontal), sizeCount, horizontal, progress);
	}

	private static string saveVideo(string file, string path, int sizeCount, bool horizontal, Action<int> progress, bool gif = false)
	{
		bool flag = false;
		string text = string.Empty;
		(int Width, int Height) size = canvasSize(sizeCount, horizontal);
		try
		{
			TimeSpan? duration = GetVideoDuration(file);
			Directory.CreateDirectory(path);
			text = Path.Combine(path, Guid.NewGuid().ToString() + ".avi");
			// Keeps the source rate (up to 60) instead of the original's fixed -r 25: playback resamples to the
			// daemon's stream rate, and a rate forced here would depend on the environment of whichever client
			// converted the file (the GUI dropped a 60 fps video to 25 while the daemon streamed at 60).
			flag = FFMpegArguments.FromFileInput(file).OutputToFile(text, overwrite: true, delegate(FFMpegArgumentOptions options)
			{
				options.WithCustomArgument($"-an -fpsmax 60 -vf scale={size.Width}:{size.Height} -c:v libx264 -crf 23 -pix_fmt yuv420p");
			}).NotifyOnProgress(delegate(TimeSpan p)
			{
				if (duration.HasValue && duration.Value.TotalMilliseconds > 0.0 && progress != null)
				{
					progress(Convert.ToInt32(Math.Round(p.TotalMilliseconds / duration.Value.TotalMilliseconds * 100.0)));
				}
			}).ProcessSynchronously();
		}
		catch
		{
		}
		if (!flag)
		{
			return string.Empty;
		}
		return text;
	}

	public static List<Media> GetImageList(int sizeCount, bool horizontal)
	{
		return getMediaList(TLLCDMediaType.Image, sizeCount, horizontal);
	}

	public static List<Media> GetDynamicList(int sizeCount, bool horizontal)
	{
		return getMediaList(TLLCDMediaType.Dynamic, sizeCount, horizontal);
	}

	public static List<Media> GetVideoList(int sizeCount, bool horizontal)
	{
		return getMediaList(TLLCDMediaType.Video, sizeCount, horizontal);
	}

	public static List<Media> GetAnimationList(int sizeCount, bool horizontal)
	{
		return getMediaList(TLLCDMediaType.Animation, sizeCount, horizontal);
	}

	private static List<Media> getMediaList(TLLCDMediaType type, int sizeCount, bool horizontal)
	{
		string text = MediaDirectory(type, sizeCount, horizontal);
		bool flag = type != TLLCDMediaType.Image;
		string searchPattern = (flag ? "*.avi" : "*.jpg");
		Directory.CreateDirectory(text);
		List<Media> list = new List<Media>();
		foreach (string item in Directory.GetFiles(text, searchPattern))
		{
			try
			{
				byte[] bytes = (flag ? ConvertVideoFirstFrameToJpegs(item) : File.ReadAllBytes(item));
				list.Add(new Media
				{
					Bytes = bytes,
					Name = Path.GetFileName(item),
					Path = item,
					Type = type,
					MD5 = calculateSHA256(item)
				});
			}
			catch (Exception)
			{
			}
		}
		return list.OrderBy((Media p) => File.GetCreationTime(p.Path)).ToList();
	}

	public static Media GetMedia(TLLCDMediaType type, string file)
	{
		try
		{
			if (File.Exists(file))
			{
				return (type == TLLCDMediaType.Image) ? new Media
				{
					Bytes = File.ReadAllBytes(file),
					Name = Path.GetFileName(file),
					Path = file,
					Type = TLLCDMediaType.Image
				} : new Media
				{
					Bytes = ConvertVideoFirstFrameToJpegs(file),
					Name = Path.GetFileName(file),
					Path = file,
					Type = type
				};
			}
		}
		catch
		{
		}
		return null;
	}

	// Decodes every frame of a video to a JPEG (cropped to one 400x400 block for multi-fan layouts) and hands it
	// to the callback; the callback returns false to abort. Returns the index of the last frame, like the original.
	public static int? ConvertVideoToJpegs(string inputVideoPath, int block, bool horizontal, Func<byte[], bool> callback)
	{
		if (!File.Exists(inputVideoPath))
		{
			return null;
		}
		(int Width, int Height)? tuple = probeSize(inputVideoPath);
		string text = null;
		if (block > 0 && tuple.HasValue && ((tuple.Value.Width > 400 && horizontal) || (tuple.Value.Height > 400 && !horizontal)))
		{
			int num = (horizontal ? ((block - 1) * 400) : 0);
			int num2 = (horizontal ? 0 : ((block - 1) * 400));
			text = $"crop=400:400:{num}:{num2},";
		}
		// One frame per stream tick, so media stored at another rate still plays at its real speed (a no-op for
		// the 25 fps files the original produces when streaming at the default rate).
		text += $"fps={Constants.StreamFramesPerSecond}";
		int num3 = 0;
		bool flag = true;
		foreach (byte[] item in streamJpegFrames(inputVideoPath, text, null))
		{
			flag = callback(compressToTargetSize(item, 65535, 100));
			if (!flag)
			{
				break;
			}
			num3++;
		}
		return num3 - 1;
	}

	public static byte[] ConvertVideoFirstFrameToJpegs(string inputVideoPath)
	{
		foreach (byte[] item in streamJpegFrames(inputVideoPath, null, 1))
		{
			return compressToTargetSize(item, 65535, 100);
		}
		return null;
	}

	public static TimeSpan? GetVideoDuration(string videoFilePath)
	{
		try
		{
			string text = runCapture(FFprobeBinary, new string[7] { "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", videoFilePath });
			if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return TimeSpan.FromSeconds(result);
			}
		}
		catch
		{
		}
		return null;
	}

	// Milliseconds per frame, as the original returned (den * 1000 / num of avg_frame_rate).
	public static int? GetVideoFrameRate(string inputVideoPath)
	{
		try
		{
			string text = runCapture(FFprobeBinary, new string[9] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=avg_frame_rate", "-of", "default=nw=1:nk=1", inputVideoPath })?.Trim();
			string[] array = text?.Split('/');
			if (array != null && array.Length == 2 && int.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2) && result > 0)
			{
				return result2 * 1000 / result;
			}
		}
		catch
		{
		}
		return null;
	}

	private static (int Width, int Height)? probeSize(string file)
	{
		try
		{
			string text = runCapture(FFprobeBinary, new string[9] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0:s=x", file })?.Trim();
			string[] array = text?.Split('x');
			if (array != null && array.Length >= 2 && int.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2))
			{
				return (result, result2);
			}
		}
		catch
		{
		}
		return null;
	}

	private static string runCapture(string fileName, IEnumerable<string> args)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		foreach (string arg in args)
		{
			processStartInfo.ArgumentList.Add(arg);
		}
		using Process process = Process.Start(processStartInfo);
		string result = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return result;
	}

	// Runs ffmpeg and splits its MJPEG image2pipe output into individual JPEG files (SOI ... EOI).
	private static IEnumerable<byte[]> streamJpegFrames(string input, string videoFilter, int? maxFrames)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(FFmpegBinary)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		List<string> list = new List<string> { "-nostdin", "-v", "error", "-i", input, "-an" };
		if (!string.IsNullOrEmpty(videoFilter))
		{
			list.AddRange(new string[2] { "-vf", videoFilter });
		}
		if (maxFrames.HasValue)
		{
			list.AddRange(new string[2]
			{
				"-frames:v",
				maxFrames.Value.ToString(CultureInfo.InvariantCulture)
			});
		}
		list.AddRange(new string[11] { "-fps_mode", "passthrough", "-f", "image2pipe", "-c:v", "mjpeg", "-q:v", "3", "-pix_fmt", "yuvj420p", "-" });
		foreach (string item in list)
		{
			processStartInfo.ArgumentList.Add(item);
		}
		Process process;
		try
		{
			process = Process.Start(processStartInfo);
		}
		catch
		{
			yield break;
		}
		process.ErrorDataReceived += delegate
		{
		};
		process.BeginErrorReadLine();
		Stream baseStream = process.StandardOutput.BaseStream;
		MemoryStream memoryStream = new MemoryStream();
		byte[] array = new byte[65536];
		int num = -1;
		bool flag = false;
		try
		{
			while (true)
			{
				int num2 = baseStream.Read(array, 0, array.Length);
				if (num2 <= 0)
				{
					break;
				}
				for (int i = 0; i < num2; i++)
				{
					byte b = array[i];
					memoryStream.WriteByte(b);
					if (num == 255 && b == 217)
					{
						byte[] buffer = memoryStream.GetBuffer();
						int num3 = (int)memoryStream.Length;
						int num4 = indexOfSoi(buffer, num3);
						if (num4 >= 0)
						{
							byte[] array2 = new byte[num3 - num4];
							Array.Copy(buffer, num4, array2, 0, array2.Length);
							flag = true;
							yield return array2;
							flag = false;
						}
						memoryStream.SetLength(0L);
						num = -1;
					}
					else
					{
						num = b;
					}
				}
			}
		}
		finally
		{
			if (!process.HasExited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
				}
			}
			process.Dispose();
			_ = flag;
		}
	}

	private static int indexOfSoi(byte[] buffer, int length)
	{
		for (int i = 0; i + 1 < length; i++)
		{
			if (buffer[i] == byte.MaxValue && buffer[i + 1] == 216)
			{
				return i;
			}
		}
		return -1;
	}

	public static byte[] EncodeJpeg(SKBitmap bitmap, int quality)
	{
		using SKImage sKImage = SKImage.FromBitmap(bitmap);
		using SKData sKData = sKImage.Encode(SKEncodedImageFormat.Jpeg, quality);
		return sKData.ToArray();
	}

	// Crops block N (1-based) of a multi-fan image the same way TLLCDController.GetImageBlock did; block 0 means
	// the whole image.
	public static byte[] GetImageBlock(string filePath, int block, bool horizontal)
	{
		using SKBitmap sKBitmap = SKBitmap.Decode(filePath);
		if (sKBitmap == null)
		{
			return null;
		}
		if (block >= 1)
		{
			SKRectI sKRectI = (horizontal ? new SKRectI((block - 1) * 400, 0, block * 400, 400) : new SKRectI(0, (block - 1) * 400, 400, block * 400));
			using SKBitmap sKBitmap2 = new SKBitmap(400, 400);
			if (sKBitmap.ExtractSubset(sKBitmap2, sKRectI))
			{
				return compressToTargetSize(EncodeJpeg(sKBitmap2, 90), 65535, 90);
			}
		}
		return compressToTargetSize(EncodeJpeg(sKBitmap, 90), 65535, 90);
	}

	public static byte[] EncodeJpeg(byte[] rgbaPixels, int width, int height, int quality)
	{
		SKImageInfo info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
		using SKBitmap sKBitmap = new SKBitmap(info);
		System.Runtime.InteropServices.Marshal.Copy(rgbaPixels, 0, sKBitmap.GetPixels(), Math.Min(rgbaPixels.Length, info.BytesSize));
		return EncodeJpeg(sKBitmap, quality);
	}

	private static byte[] compressToTargetSize(byte[] inputImageBytes, int targetSizeInBytes, int quality)
	{
		if (inputImageBytes == null || inputImageBytes.Length <= targetSizeInBytes)
		{
			return inputImageBytes;
		}
		using SKBitmap sKBitmap = SKBitmap.Decode(inputImageBytes);
		if (sKBitmap == null)
		{
			return inputImageBytes;
		}
		byte[] array = EncodeJpeg(sKBitmap, quality);
		while (array.Length > targetSizeInBytes && quality > 0)
		{
			quality -= 5;
			array = EncodeJpeg(sKBitmap, Math.Max(quality, 1));
		}
		return array;
	}

	public static string calculateSHA256(string input)
	{
		using SHA256 sHA = SHA256.Create();
		byte[] bytes = Encoding.UTF8.GetBytes(input);
		byte[] array = sHA.ComputeHash(bytes);
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < array.Length; i++)
		{
			stringBuilder.Append(array[i].ToString("x2"));
		}
		return stringBuilder.ToString();
	}
}
