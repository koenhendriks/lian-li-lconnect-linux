using System;
using System.IO;

namespace LConnectCore;

public class FileLogger : ILogger
{
	private FileStream stream;

	private StreamWriter writer;

	public bool IsOpen { get; private set; }

	public LogLevel LogLevel { get; set; } = LogLevel.Info;

	public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

	public void Open(string filepath)
	{
		if (IsOpen)
		{
			return;
		}
		try
		{
			string directoryName = Path.GetDirectoryName(filepath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			stream = new FileStream(filepath, FileMode.Append, FileAccess.Write, FileShare.Read);
			writer = new StreamWriter(stream);
			Info("Logger start");
			IsOpen = true;
		}
		catch (Exception)
		{
		}
	}

	public void Close()
	{
		if (writer != null)
		{
			Info("Logger stop");
			writer.Dispose();
		}
		stream?.Dispose();
		IsOpen = false;
	}

	public void Debug(string message)
	{
		writeLog(message, LogLevel.Debug);
	}

	public void Info(string message)
	{
		writeLog(message);
	}

	public void Warn(string message)
	{
		writeLog(message, LogLevel.Warn);
	}

	public void Error(string message)
	{
		writeLog(message, LogLevel.Error);
	}

	private void writeLog(string message, LogLevel level = LogLevel.Info)
	{
		if (writer == null)
		{
			return;
		}
		string arg = DateTime.Now.ToString(TimestampFormat);
		try
		{
			string value = $"[{arg}][{level}] {message}";
			if (level >= LogLevel)
			{
				writer.WriteLine(value);
				writer.Flush();
			}
		}
		catch
		{
		}
	}
}
