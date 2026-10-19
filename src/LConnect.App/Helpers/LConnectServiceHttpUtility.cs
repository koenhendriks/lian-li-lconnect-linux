using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using LConnectCore;

namespace LConnect3cs.Helpers;

// Same API as the original UI helper, but talks to lconnectd over its Unix socket instead of reading a port file
// and POSTing to localhost. Requests and responses are the original plain JSON.
internal static class LConnectServiceHttpUtility
{
	private static readonly Lazy<HttpClient> client = new Lazy<HttpClient>(createClient);

	public static string SocketPath => LConnectApp.Path.ServiceSocketPath;

	private static HttpClient createClient()
	{
		SocketsHttpHandler handler = new SocketsHttpHandler
		{
			ConnectCallback = async delegate(SocketsHttpConnectionContext context, CancellationToken token)
			{
				Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
				try
				{
					await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), token);
					return new NetworkStream(socket, ownsSocket: true);
				}
				catch
				{
					socket.Dispose();
					throw;
				}
			}
		};
		return new HttpClient(handler)
		{
			BaseAddress = new Uri("http://lconnectd/"),
			Timeout = Timeout.InfiniteTimeSpan
		};
	}

	public static bool LoadPortFile()
	{
		return System.IO.File.Exists(SocketPath);
	}

	public static bool IsServiceAvailable()
	{
		string json;
		return TrySendRequest(HttpRequestActionType.Ping, out json, null, null, 1000);
	}

	public static bool TrySendRequest(HttpRequestActionType action, out string json, NameValueCollection query = null, object data = null, int timeout = 2000)
	{
		json = null;
		try
		{
			NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
			nameValueCollection.Add("action", action.ToString());
			if (query != null)
			{
				string[] allKeys = query.AllKeys;
				foreach (string text in allKeys)
				{
					if (!(text == "action"))
					{
						nameValueCollection.Add(text, query[text]);
					}
				}
			}
			byte[] content = ((data == null) ? Array.Empty<byte>() : JsonSerializer.SerializeToUtf8Bytes(data));
			json = Send(HttpMethod.Post, "api?" + nameValueCollection.ToString(), content, timeout);
			return json != null;
		}
		catch (Exception)
		{
		}
		return false;
	}

	// Raw access for the extra endpoints (/status, /reapply, /resume).
	public static string Send(HttpMethod method, string relativeUri, byte[] content = null, int timeout = 2000)
	{
		using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(Math.Max(timeout, 1000));
		using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(method, relativeUri);
		if (content != null)
		{
			httpRequestMessage.Content = new ByteArrayContent(content);
			httpRequestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
			{
				CharSet = "UTF-8"
			};
		}
		using HttpResponseMessage httpResponseMessage = client.Value.Send(httpRequestMessage, cancellationTokenSource.Token);
		if (!httpResponseMessage.IsSuccessStatusCode)
		{
			return null;
		}
		using System.IO.StreamReader streamReader = new System.IO.StreamReader(httpResponseMessage.Content.ReadAsStream(), Encoding.UTF8);
		return streamReader.ReadToEnd();
	}

	private static void trySendMotherboardSyncRequest(HttpRequestActionType syncType, bool isSync)
	{
		NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
		nameValueCollection.Add("value", isSync.ToString());
		TrySendRequest(syncType, out var _, nameValueCollection);
	}

	public static void TrySendMotherboardRPMSyncRequest(bool isSync)
	{
		trySendMotherboardSyncRequest(HttpRequestActionType.MotherboardRPMSync, isSync);
	}

	public static Task TrySendMotherboardRPMSyncRequestAsync(bool isSync)
	{
		return Task.Run(delegate
		{
			TrySendMotherboardRPMSyncRequest(isSync);
		});
	}

	public static void TrySendMotherboardARGBSyncRequest(bool isSync)
	{
		trySendMotherboardSyncRequest(HttpRequestActionType.MotherboardARGBSync, isSync);
	}

	public static Task TrySendMotherboardARGBSyncRequestAsync(bool isSync)
	{
		return Task.Run(delegate
		{
			TrySendMotherboardARGBSyncRequest(isSync);
		});
	}
}
