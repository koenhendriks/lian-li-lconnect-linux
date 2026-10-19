namespace LConnectCore;

public interface ILogger
{
	LogLevel LogLevel { get; set; }

	void Debug(string message);

	void Info(string message);

	void Warn(string message);

	void Error(string message);
}
