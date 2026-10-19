using System;
using System.IO;
using System.Reflection;

namespace Golden
{
	internal static class Program
	{
		private static int Main(string[] args)
		{
			string lconnectDir = args.Length > 2 ? args[2] : @"C:\Program Files\Lian-Li\L-Connect 3";
			// Resolve the original's dependencies (System.Text.Json, CefSharp, ...) from the install directory.
			AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
			{
				string path = Path.Combine(lconnectDir, new AssemblyName(e.Name).Name + ".dll");
				return File.Exists(path) ? Assembly.LoadFrom(path) : null;
			};
			string fixtures = args.Length > 0 ? args[0] : null;
			string output = args.Length > 1 ? args[1] : "golden.txt";
			Assembly service = Assembly.LoadFrom(Path.Combine(lconnectDir, "L-Connect-Service.exe"));
			File.WriteAllText(output, Scenarios.Render(fixtures, service).Replace("\r\n", "\n"));
			Console.WriteLine("wrote " + output);
			return 0;
		}
	}
}
