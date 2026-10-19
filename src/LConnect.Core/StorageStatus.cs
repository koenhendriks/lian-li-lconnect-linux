using System;

namespace LConnectCore;

public class StorageStatus
{
	public long TotalSize { get; set; }

	public long TotalFreeSpace { get; set; }

	public long GetTotalUsedSpace()
	{
		return TotalSize - TotalFreeSpace;
	}

	public int GetLoad()
	{
		return (int)Math.Round((double)GetTotalUsedSpace() / (double)TotalSize * 100.0);
	}
}
