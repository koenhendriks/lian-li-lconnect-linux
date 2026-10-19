using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LConnectCore;
using LConnectService.Helpers;
using PuppeteerSharp;

namespace LConnectService.Products.TLLCD;

// Replaces CefSharp.OffScreen. The original rendered assets/tl-sensor/index.html (a Vue app supplied by Lian Li)
// in an off-screen Chromium, pushed values with setConfigs()/setSensorValue() and screenshotted it at ~25 fps.
// We drive a headless system Chromium through the DevTools protocol instead. The bundle is served from a fake
// http origin via request interception (ES modules do not load from file://), and a capture loop keeps the
// latest rendered JPEG ready for the LCD stream.
internal sealed class SensorScreenRenderer : IAsyncDisposable
{
	private const string Origin = "http://tl-sensor.local/";

	private static readonly SemaphoreSlim browserLock = new SemaphoreSlim(1, 1);

	private static IBrowser sharedBrowser;

	private static int pageCount;

	private readonly object gate = new object();

	private IPage page;

	private bool disposed;

	private CancellationTokenSource captureCancel;

	private Task captureTask;

	private volatile byte[] latestFrame;

	private int frameCount;

	public static string AssetDirectory => Path.Combine(LConnectApp.Path.AssetsDirectory, "tl-sensor");

	public byte[] LatestFrame => latestFrame;

	public bool IsReady => page != null;

	// Chromium crashed or the page went away after a successful start; the owner should dispose and start over.
	public bool IsBroken
	{
		get
		{
			IPage page = this.page;
			if (page != null)
			{
				if (!page.IsClosed)
				{
					return !page.Browser.IsConnected;
				}
				return true;
			}
			return false;
		}
	}

	public static string FindChromium()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("LCONNECT_CHROMIUM");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			return environmentVariable;
		}
		string[] array = (OperatingSystem.IsWindows() ? new string[3] { "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe" } : new string[5] { "/usr/bin/chromium", "/usr/bin/chromium-browser", "/usr/bin/google-chrome-stable", "/usr/bin/google-chrome", "/usr/lib/chromium/chromium" });
		foreach (string text in array)
		{
			if (File.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	private static async Task<IBrowser> getBrowser()
	{
		await browserLock.WaitAsync();
		try
		{
			if (sharedBrowser != null && sharedBrowser.IsConnected)
			{
				return sharedBrowser;
			}
			string text = FindChromium();
			if (text == null)
			{
				throw new InvalidOperationException("No Chromium/Chrome found. Install the 'chromium' package or set LCONNECT_CHROMIUM.");
			}
			string text2 = Path.Combine(Environment.GetEnvironmentVariable("CACHE_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "lconnect"), "chromium-profile");
			Directory.CreateDirectory(text2);
			LaunchOptions options = new LaunchOptions
			{
				Headless = true,
				ExecutablePath = text,
				UserDataDir = text2,
				DefaultViewport = new ViewPortOptions
				{
					Width = 400,
					Height = 400,
					DeviceScaleFactor = 1.0
				},
				Args = new string[8] { "--no-sandbox", "--disable-gpu", "--hide-scrollbars", "--mute-audio", "--disable-extensions", "--disable-background-timer-throttling", "--disable-renderer-backgrounding", "--autoplay-policy=no-user-gesture-required" }
			};
			sharedBrowser = await Puppeteer.LaunchAsync(options);
			EventLogHelper.Info("Started headless browser for LCD sensor screens: " + text);
			return sharedBrowser;
		}
		finally
		{
			browserLock.Release();
		}
	}

	// A DisposeAsync that arrives while this is still running (a newer setting replaced the screen) must neither
	// null the page under it nor let the last-page shutdown close the shared browser, so the page stays local until
	// it is fully set up and pageCount is taken before the browser is touched.
	public async Task InitAsync()
	{
		if (page != null)
		{
			return;
		}
		if (!File.Exists(Path.Combine(AssetDirectory, "index.html")))
		{
			throw new FileNotFoundException("LCD sensor assets missing. Run 'lconnectctl assets import --from <L-Connect 3 install dir>'.", Path.Combine(AssetDirectory, "index.html"));
		}
		Interlocked.Increment(ref pageCount);
		IPage newPage = null;
		try
		{
			IBrowser browser = await getBrowser();
			EventLogHelper.Debug("sensor renderer: opening page");
			newPage = await browser.NewPageAsync();
			await newPage.SetViewportAsync(new ViewPortOptions
			{
				Width = 400,
				Height = 400,
				DeviceScaleFactor = 1.0
			});
			await newPage.SetRequestInterceptionAsync(value: true);
			newPage.Request += onRequest;
			await newPage.GoToAsync(Origin + "index.html#/home", new NavigationOptions
			{
				WaitUntil = new WaitUntilNavigation[1] { WaitUntilNavigation.Networkidle0 },
				Timeout = 20000
			});
			EventLogHelper.Debug("sensor renderer: page loaded");
			await newPage.EvaluateExpressionAsync("document.body.style.overflow = 'hidden'");
			lock (gate)
			{
				if (!disposed)
				{
					page = newPage;
					newPage = null;
					captureCancel = new CancellationTokenSource();
					CancellationToken token = captureCancel.Token;
					captureTask = Task.Run(() => captureLoop(token));
				}
			}
		}
		catch
		{
			await releasePage(newPage);
			throw;
		}
		if (newPage != null)
		{
			await releasePage(newPage);
			return;
		}
		EventLogHelper.Debug("sensor renderer: capture loop started");
	}

	private static async Task releasePage(IPage page)
	{
		try
		{
			if (page != null)
			{
				await page.CloseAsync();
			}
		}
		catch
		{
		}
		if (Interlocked.Decrement(ref pageCount) == 0)
		{
			await ShutdownAsync();
		}
	}

	// Screenshots at up to the stream rate (25 fps, as the original grabbed frames from CEF), never overlapping.
	private async Task captureLoop(CancellationToken token)
	{
		ScreenshotOptions options = new ScreenshotOptions
		{
			Type = ScreenshotType.Jpeg,
			Quality = 90,
			Clip = new PuppeteerSharp.Media.Clip
			{
				X = 0m,
				Y = 0m,
				Width = 400m,
				Height = 400m
			}
		};
		int num = 0;
		while (!token.IsCancellationRequested)
		{
			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				IPage page = this.page;
				if (page == null)
				{
					break;
				}
				latestFrame = await page.ScreenshotDataAsync(options);
				Interlocked.Increment(ref frameCount);
				num = 0;
			}
			catch (Exception ex) when (!token.IsCancellationRequested)
			{
				if (++num == 1 || num % 100 == 0)
				{
					EventLogHelper.Warn("LCD sensor screen capture failed: " + ex.Message);
				}
			}
			int num2 = 1000 / LConnectCore.Products.TLLCD.Constants.StreamFramesPerSecond - (int)stopwatch.ElapsedMilliseconds;
			try
			{
				await Task.Delay(Math.Max(num2, 5), token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	private async void onRequest(object sender, RequestEventArgs e)
	{
		try
		{
			string url = e.Request.Url;
			if (!url.StartsWith(Origin, StringComparison.Ordinal))
			{
				await e.Request.AbortAsync();
				return;
			}
			string text = Uri.UnescapeDataString(new Uri(url).AbsolutePath.TrimStart('/'));
			string fullPath = Path.GetFullPath(Path.Combine(AssetDirectory, text));
			if (!fullPath.StartsWith(Path.GetFullPath(AssetDirectory), StringComparison.Ordinal) || !File.Exists(fullPath))
			{
				await e.Request.RespondAsync(new ResponseData
				{
					Status = System.Net.HttpStatusCode.NotFound
				});
				return;
			}
			await e.Request.RespondAsync(new ResponseData
			{
				Status = System.Net.HttpStatusCode.OK,
				ContentType = contentType(fullPath),
				BodyData = await File.ReadAllBytesAsync(fullPath)
			});
		}
		catch (Exception ex)
		{
			EventLogHelper.Debug("sensor asset request failed: " + ex.Message);
		}
	}

	private static string contentType(string path)
	{
		return Path.GetExtension(path).ToLowerInvariant() switch
		{
			".html" => "text/html; charset=utf-8",
			".js" => "text/javascript; charset=utf-8",
			".css" => "text/css; charset=utf-8",
			".svg" => "image/svg+xml",
			".png" => "image/png",
			".ttf" => "font/ttf",
			".ico" => "image/x-icon",
			".json" => "application/json",
			_ => "application/octet-stream",
		};
	}

	public static async Task<int> RenderTestAsync(string outputPath)
	{
		SensorScreenRenderer renderer = new SensorScreenRenderer();
		try
		{
			await renderer.InitAsync();
			EventLogHelper.Debug("page loaded; configuring");
			await renderer.CallAsync("setConfigs", "{\"Effect\":\"RingOne\",\"BackgroundColor\":[0,0,255],\"TextColors\":[[255,255,255],[255,255,255],[255,255,255]],\"Sensor\":\"CPUTemp\"}");
			await renderer.CallAsync("setSensorValue", "{\"Type\":\"CPU\",\"Value\":55,\"Label\":\"TEMP\",\"Unit\":\"°C\",\"Progress\":55}");
			for (int i = 0; i < 50; i++)
			{
				if (renderer.LatestFrame != null && i > 10)
				{
					break;
				}
				await Task.Delay(100);
			}
			byte[] array = renderer.LatestFrame;
			if (array == null)
			{
				EventLogHelper.Debug("no screencast frame; falling back to a screenshot");
				array = await renderer.page.ScreenshotDataAsync(new ScreenshotOptions
				{
					Type = ScreenshotType.Jpeg,
					Quality = 90
				});
			}
			await File.WriteAllBytesAsync(outputPath, array);
			Console.WriteLine($"wrote {outputPath} ({array.Length} bytes, screencast frames: {renderer.frameCount})");
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("render test failed: " + ex);
			return 1;
		}
		finally
		{
			await renderer.DisposeAsync();
		}
	}

	// Same calls the original made through CefSharp's GetScriptForJavascriptMethodWithArgs: one JSON string arg.
	public async Task CallAsync(string function, string jsonArgument)
	{
		if (page != null)
		{
			await page.EvaluateFunctionAsync("(f, a) => window[f] && window[f](a)", function, jsonArgument);
		}
	}

	public async ValueTask DisposeAsync()
	{
		IPage page;
		lock (gate)
		{
			disposed = true;
			page = this.page;
			this.page = null;
		}
		// Still starting (InitAsync releases its own page when it sees the flag) or never started.
		if (page == null)
		{
			return;
		}
		try
		{
			captureCancel?.Cancel();
			if (captureTask != null)
			{
				await Task.WhenAny(captureTask, Task.Delay(2000));
			}
		}
		catch
		{
		}
		await releasePage(page);
	}

	// Closing the last page also stops Chromium so it does not keep ~200 MB around when no sensor screen is used.
	public static async Task ShutdownAsync()
	{
		await browserLock.WaitAsync();
		try
		{
			if (sharedBrowser != null && pageCount <= 0)
			{
				try
				{
					await sharedBrowser.CloseAsync();
				}
				catch
				{
				}
				sharedBrowser = null;
			}
		}
		finally
		{
			browserLock.Release();
		}
	}
}
