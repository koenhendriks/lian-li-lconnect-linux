using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LConnectCore;
using LConnectService.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LConnectService;

// lconnectd: the ported L-Connect service. Runs under systemd (Type=notify) and serves the original
// service's request protocol over HTTP on a Unix socket instead of an encrypted localhost TCP listener.
internal static class Program
{
	private static async Task<int> Main(string[] args)
	{
		if (args.Contains("--version"))
		{
			Console.WriteLine("lconnectd (port of L-Connect 3 service v2.0.11)");
			return 0;
		}
		int num = Array.IndexOf(args, "--render-sensor");
		if (num >= 0)
		{
			// Diagnostic: render one LCD sensor screen to a JPEG without touching any device.
			EventLogHelper.Verbose = true;
			return await Products.TLLCD.SensorScreenRenderer.RenderTestAsync((num + 1 < args.Length) ? args[num + 1] : "sensor-test.jpg");
		}
		string socketPath = LConnectApp.Path.ServiceSocketPath;
		Directory.CreateDirectory(LConnectApp.Path.DataRoot);
		Directory.CreateDirectory(Path.GetDirectoryName(socketPath));
		if (File.Exists(socketPath))
		{
			File.Delete(socketPath);
		}
		// The content root defaults to the working directory, which is "/" under systemd; the config file watchers
		// would then crawl the whole filesystem. Pin it to the install directory and don't watch config files.
		Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
		{
			Args = args,
			ContentRootPath = AppContext.BaseDirectory
		});
		builder.Host.UseSystemd();
		builder.Logging.ClearProviders();
		builder.WebHost.ConfigureKestrel(delegate(Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions o)
		{
			o.ListenUnixSocket(socketPath);
			o.Limits.MaxRequestBodySize = 64 * 1024 * 1024;
		});
		builder.Services.AddSingleton<MainService>();
		builder.Services.AddHostedService<ServiceHost>();
		WebApplication app = builder.Build();
		MainService service = app.Services.GetRequiredService<MainService>();
		app.MapPost("/api", (Func<HttpContext, Task>)async delegate(HttpContext ctx)
		{
			Dictionary<string, string> query = ctx.Request.Query.ToDictionary((KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> q) => q.Key, (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> q) => q.Value.ToString());
			string body;
			using (StreamReader reader = new StreamReader(ctx.Request.Body))
			{
				body = await reader.ReadToEndAsync();
			}
			object result = await Task.Run(() => service.HandleRequest(new RequestData
			{
				Query = query,
				Body = body
			}));
			await writeJson(ctx, result);
		});
		app.MapGet("/status", (Func<HttpContext, Task>)((HttpContext ctx) => writeJson(ctx, service.GetStatus())));
		app.MapPost("/reapply", (Func<HttpContext, Task>)async delegate(HttpContext ctx)
		{
			await Task.Run(service.ReapplyAll);
			await writeJson(ctx, "OK");
		});
		app.MapPost("/resume", (Func<HttpContext, Task>)async delegate(HttpContext ctx)
		{
			await Task.Run(service.Resume);
			await writeJson(ctx, "OK");
		});
		app.Lifetime.ApplicationStarted.Register(delegate
		{
			try
			{
				// Group members (the lconnect group) may talk to the daemon.
				if (!OperatingSystem.IsWindows())
				{
					File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
				}
			}
			catch
			{
			}
			EventLogHelper.Info("Listening on " + socketPath);
		});
		await app.RunAsync();
		return 0;
	}

	private static async Task writeJson(HttpContext ctx, object result)
	{
		ctx.Response.ContentType = "application/json;charset=UTF-8";
		if (result != null)
		{
			await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result));
		}
	}
}

internal sealed class ServiceHost : IHostedService
{
	private readonly MainService service;

	public ServiceHost(MainService service)
	{
		this.service = service;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (!service.Start())
		{
			throw new InvalidOperationException("Core initialisation failed (is libhidapi-hidraw installed?)");
		}
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.Run(service.Stop, cancellationToken);
	}
}
