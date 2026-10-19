using System.Collections.Generic;
using LConnectCore.Devices;

namespace LConnectService.Products;

internal interface IController
{
	void Init();

	void Dispose();

	object HandleRequestData(RequestData requestData);

	void Apply(DeviceSetting<object> setting);

	void ApplyAll(IEnumerable<DeviceSetting<object>> settings);
}
