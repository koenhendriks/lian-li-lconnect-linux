using System;
using System.IO;

namespace LConnectCore;

public static class FileSystemHelper
{
	public static bool TryDeleteFile(string path)
	{
		Exception exception;
		return TryDeleteFile(path, out exception);
	}

	// Copies into the shared data directory. File.Copy keeps the source's mode on Unix (often read-only when the
	// source is a Windows partition), which would stop the daemon and other group members from replacing the file.
	public static void CopyShared(string source, string destination, bool overwrite = true)
	{
		if (overwrite && File.Exists(destination))
		{
			File.Delete(destination);
		}
		File.Copy(source, destination, overwrite);
		if (!OperatingSystem.IsWindows())
		{
			File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
		}
	}

	public static bool TryDeleteFile(string path, out Exception exception)
	{
		exception = null;
		try
		{
			File.Delete(path);
			return true;
		}
		catch (Exception ex)
		{
			exception = ex;
		}
		return false;
	}
}
