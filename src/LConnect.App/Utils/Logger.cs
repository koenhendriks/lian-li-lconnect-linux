using System;
using LConnectCore;

namespace LConnect3cs.Utils;

public class Logger : ILogger
{
	private readonly FileLogger logger = new FileLogger
	{
		LogLevel = LogLevel.Info
	};

	private static readonly Lazy<Logger> lazy = new Lazy<Logger>(() => new Logger());

	public string LogPath { get; private set; }

	public LogLevel LogLevel
	{
		get
		{
			return logger.LogLevel;
		}
		set
		{
			logger.LogLevel = value;
		}
	}

	public static Logger Instance => lazy.Value;

	private Logger()
	{
	}

	public void Open(string filepath)
	{
		if (!logger.IsOpen)
		{
			logger.Open(filepath);
			LogPath = filepath;
		}
	}

	public void Close()
	{
		logger.Close();
	}

	public void Debug(string message)
	{
		logger.Debug(message);
	}

	public void Info(string message)
	{
		logger.Info(message);
	}

	public void Warn(string message)
	{
		logger.Warn(message);
	}

	public void Error(string message)
	{
		logger.Error(message);
	}
}
