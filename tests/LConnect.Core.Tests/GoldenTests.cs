using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Golden;
using Xunit;

namespace LConnect.Core.Tests;

// Replays the scenarios that tools/GoldenHarness ran against the original L-Connect 3 assemblies and checks the
// port produces byte-identical HID traffic.
public class GoldenTests
{
	// Deliberate differences from the original, applied to its recorded traffic before comparing so everything else
	// stays byte-checked. Each one was verified on real hardware.
	private static readonly Dictionary<string, Func<string, string>> Deviations = new Dictionary<string, Func<string, string>>
	{
		// SLInfinityController.setMergeLighting ends with a frame sync (SetFrame 1); without it a merged effect runs
		// out of phase across the ports after a service start or settings import.
		["svc.slinf.applyall"] = (string expected) => expected + "\nF:4/e0600001"
	};

	internal static string RepoRoot
	{
		get
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory);
			while (directoryInfo != null && !File.Exists(Path.Combine(directoryInfo.FullName, "LConnect.sln")))
			{
				directoryInfo = directoryInfo.Parent;
			}
			return directoryInfo?.FullName ?? throw new InvalidOperationException("repo root not found");
		}
	}

	static GoldenTests()
	{
		// Controllers save settings while applying them; keep that out of the real data directory.
		string text = Path.Combine(Path.GetTempPath(), "lconnect-tests-" + Guid.NewGuid().ToString("N"));
		Environment.SetEnvironmentVariable("LCONNECT_DATA_DIR", text);
		LConnectCore.LConnectApp.ReloadPaths();
	}

	private static Dictionary<string, string> parse(string text)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string text2 = null;
		List<string> list = new List<string>();
		foreach (string item in text.Replace("\r\n", "\n").Split('\n'))
		{
			if (item.StartsWith("## "))
			{
				if (text2 != null)
				{
					dictionary[text2] = string.Join("\n", list);
				}
				text2 = item.Substring(3);
				list.Clear();
			}
			else if (text2 != null && item.Length > 0)
			{
				list.Add(item);
			}
		}
		if (text2 != null)
		{
			dictionary[text2] = string.Join("\n", list);
		}
		return dictionary;
	}

	public static IEnumerable<object[]> ScenarioNames()
	{
		return parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.txt"))).Keys.Select((string k) => new object[1] { k });
	}

	private static readonly Lazy<Dictionary<string, string>> actual = new Lazy<Dictionary<string, string>>(delegate
	{
		Assembly serviceAssembly = Assembly.Load("lconnectd");
		return parse(Scenarios.Render(Path.Combine(RepoRoot, "tests", "fixtures", "appdata"), serviceAssembly));
	});

	[Theory]
	[MemberData(nameof(ScenarioNames))]
	public void MatchesOriginal(string scenario)
	{
		Dictionary<string, string> dictionary = parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.txt")));
		Assert.True(actual.Value.ContainsKey(scenario), "scenario missing in port: " + scenario);
		string expected = Deviations.TryGetValue(scenario, out var deviation) ? deviation(dictionary[scenario]) : dictionary[scenario];
		Assert.Equal(expected, actual.Value[scenario]);
	}

	[Fact]
	public void NoExtraScenarios()
	{
		Dictionary<string, string> dictionary = parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.txt")));
		Assert.Empty(actual.Value.Keys.Except(dictionary.Keys));
	}
}
