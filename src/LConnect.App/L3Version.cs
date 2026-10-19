using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LConnect3cs;

internal sealed class L3Version
{
	private static readonly Lazy<L3Version> lazy = new Lazy<L3Version>(() => new L3Version());

	private readonly string versionText;

	public static L3Version Instance => lazy.Value;

	public string GetVersionText()
	{
		return versionText;
	}

	private L3Version()
	{
		string path = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "version");
		versionText = string.Empty;
		try
		{
			if (File.Exists(path))
			{
				using StreamReader streamReader = new StreamReader(path);
				char[] array = new char[32];
				int num = streamReader.Read(array, 0, array.Length);
				for (int i = 0; i < num; i++)
				{
					if (array[i] == '\r' || array[i] == '\n')
					{
						num = i;
						break;
					}
				}
				versionText = new string(array.Take(num).ToArray());
			}
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(versionText))
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			versionText = $"v{version.Major}.{version.Minor}.{version.Build}";
		}
	}
}
