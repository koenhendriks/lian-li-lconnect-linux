using System;

namespace LConnectCore.Products.TLLCD;

public struct FirmwareVersion
{
	public bool IsValid { get; set; }

	public string ChipCode { get; set; }

	public string BootCode { get; set; }

	public string Type { get; set; }

	public string ModelA { get; set; }

	public string ModelB { get; set; }

	public string SoftwareVersion { get; set; }

	public string UserVersion { get; set; }

	public DateTime DateTime { get; set; }

	public static bool TryParse(string versionStr, string dateTimeStr, out FirmwareVersion firmwareVersion)
	{
		versionStr = versionStr?.Trim()?.Trim(default(char));
		dateTimeStr = dateTimeStr?.Trim()?.Trim(default(char));
		firmwareVersion = default;
		if (string.IsNullOrEmpty(versionStr) || string.IsNullOrEmpty(dateTimeStr))
		{
			return false;
		}
		string[] array = versionStr.Split(',');
		bool flag = DateTime.TryParse(dateTimeStr, out var result);
		if (array.Length < 7 || !flag)
		{
			return false;
		}
		firmwareVersion = new FirmwareVersion
		{
			IsValid = true,
			ChipCode = array[0],
			BootCode = array[1],
			Type = array[2],
			ModelA = array[3],
			ModelB = array[4],
			SoftwareVersion = array[5],
			UserVersion = array[6],
			DateTime = result
		};
		return true;
	}
}
