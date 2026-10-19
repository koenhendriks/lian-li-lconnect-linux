using System;
using System.Collections.Generic;
using System.Reflection;
using LConnect3cs.Products;
using LConnect3cs.Products.Ene6K77Fan;
using LConnect3cs.Products.TLFan;
using LConnectCore.Devices;

namespace LConnect3cs.Utils;

internal static class ControllerManager
{
	private class ReflectionInfo
	{
		private object controllers;

		private MethodInfo addMethod;

		private MethodInfo removeMethod;

		private MethodInfo containsKeyMethod;

		private PropertyInfo itemProperty;

		private ConstructorInfo ctor;

		public void InvokeAdd(string devicePath, object controller)
		{
			addMethod?.Invoke(controllers, new object[2] { devicePath, controller });
		}

		public void InvokeRemove(string devicePath)
		{
			removeMethod?.Invoke(controllers, new object[1] { devicePath });
		}

		public bool InvokeContainsKey(string devicePath)
		{
			object obj = containsKeyMethod?.Invoke(controllers, new object[1] { devicePath });
			if (obj is bool)
			{
				return (bool)obj;
			}
			return false;
		}

		public object InvokeItem(string devicePath)
		{
			return itemProperty?.GetValue(controllers, new object[1] { devicePath });
		}

		public object InvokeControllerConstructor(ControllerType type, string devicePath, USBDeviceID deviceID)
		{
			return ctor?.Invoke(new object[3] { type, devicePath, deviceID });
		}

		public static bool TryParse(object store, out ReflectionInfo info)
		{
			info = null;
			try
			{
				PropertyInfo property = store.GetType().GetProperty("Controllers");
				if ((object)property == null)
				{
					return false;
				}
				object value = property.GetValue(store, null);
				Type type = value.GetType();
				if (type.GetGenericTypeDefinition() != typeof(ObservableDictionary<, >))
				{
					return false;
				}
				Type type2 = type.GetGenericArguments()[1];
				ReflectionInfo reflectionInfo = new ReflectionInfo();
				reflectionInfo.controllers = value;
				// Explicit signatures: on .NET Core Dictionary also has Remove(key, out value), and the
				// ObservableDictionary 'new' members hide the base ones, so name-only lookups are ambiguous.
				Type type3 = type.GetGenericArguments()[0];
				reflectionInfo.addMethod = type.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, new Type[2] { type3, type2 }, null);
				reflectionInfo.removeMethod = type.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, new Type[1] { type3 }, null);
				reflectionInfo.containsKeyMethod = type.GetMethod("ContainsKey", new Type[1] { type3 });
				reflectionInfo.itemProperty = type.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, type2, new Type[1] { type3 }, null);
				reflectionInfo.ctor = type2.GetConstructor(new Type[3]
				{
					typeof(ControllerType),
					typeof(string),
					typeof(USBDeviceID)
				});
				ReflectionInfo reflectionInfo2 = reflectionInfo;
				if ((object)reflectionInfo2.addMethod == null || (object)reflectionInfo2.removeMethod == null || (object)reflectionInfo2.containsKeyMethod == null || (object)reflectionInfo2.ctor == null)
				{
					return false;
				}
				info = reflectionInfo2;
				return true;
			}
			catch
			{
			}
			return false;
		}
	}

	private static readonly Dictionary<ControllerType, ReflectionInfo> ReflectionInfos;

	static ControllerManager()
	{
		ReflectionInfos = new Dictionary<ControllerType, ReflectionInfo>();
		foreach (KeyValuePair<ControllerType, object> item in new Dictionary<ControllerType, object>
		{
			[ControllerType.SLInfinity] = SLInfinityStore.Instance,
			[ControllerType.TLFan] = TLFanStore.Instance,
			[ControllerType.TLLCD] = TLLCDStore.Instance
		})
		{
			if (ReflectionInfo.TryParse(item.Value, out var info))
			{
				ReflectionInfos.Add(item.Key, info);
			}
		}
	}

	public static void Add(ControllerType type, string devicePath, USBDeviceID deviceID)
	{
		try
		{
			if (!ReflectionInfos.ContainsKey(type))
			{
				Logger.Instance.Warn($"Unsupported device type. (type={type}, path={devicePath})");
				return;
			}
		}
		catch (Exception ex)
		{
			Logger.Instance.Error($"Unable to perform the addition action. (type={type}, path={devicePath}) {ex.Message}");
			return;
		}
		ReflectionInfo reflectionInfo;
		try
		{
			reflectionInfo = ReflectionInfos[type];
			if (reflectionInfo.InvokeContainsKey(devicePath))
			{
				Logger.Instance.Error($"Device exists. (type={type}, path={devicePath})");
				return;
			}
		}
		catch (Exception ex2)
		{
			Logger.Instance.Error($"Error occurred while checking for the existence of the device. (type={type}, path={devicePath}) {ex2.Message}");
			return;
		}
		Controller controller;
		try
		{
			object obj = reflectionInfo.InvokeControllerConstructor(type, devicePath, deviceID);
			if (!(obj is Controller))
			{
				Logger.Instance.Error($"Device creation failed. (type={type}, path={devicePath})");
				return;
			}
			controller = (Controller)obj;
		}
		catch (Exception ex3)
		{
			Logger.Instance.Error($"Error occurred during device creation. (type={type}, path={devicePath}) {ex3.Message}");
			return;
		}
		try
		{
			controller.Init();
		}
		catch (Exception ex4)
		{
			Logger.Instance.Error($"Error occurred during device initialization. (type={type}, path={devicePath}) {ex4.Message}\n{ex4.StackTrace}");
			return;
		}
		try
		{
			reflectionInfo.InvokeAdd(devicePath, controller);
		}
		catch (Exception ex5)
		{
			Logger.Instance.Error($"Error occurred when adding the device. (type={type}, path={devicePath}) {ex5.Message}");
			return;
		}
		Logger.Instance.Info($"Device added successfully. (type={type}, path={devicePath})");
	}

	public static void Remove(ControllerType type, string devicePath)
	{
		try
		{
			if (!ReflectionInfos.ContainsKey(type))
			{
				Logger.Instance.Warn($"Unsupported device type. (type={type}, path={devicePath})");
				return;
			}
		}
		catch (Exception ex)
		{
			Logger.Instance.Error($"Unable to perform the removal action. (type={type}, path={devicePath}) {ex.Message}");
			return;
		}
		ReflectionInfo reflectionInfo;
		try
		{
			reflectionInfo = ReflectionInfos[type];
			if (!reflectionInfo.InvokeContainsKey(devicePath))
			{
				Logger.Instance.Error($"Device not exists. (type={type}, path={devicePath})");
				return;
			}
		}
		catch (Exception ex2)
		{
			Logger.Instance.Error($"Error occurred while checking for the existence of the device. (type={type}, path={devicePath}) {ex2.Message}");
			return;
		}
		Controller controller;
		try
		{
			object obj = reflectionInfo.InvokeItem(devicePath);
			if (!(obj is Controller))
			{
				Logger.Instance.Error($"Failed to retrieve the device. (type={type}, path={devicePath})");
				return;
			}
			controller = (Controller)obj;
		}
		catch (Exception ex3)
		{
			Logger.Instance.Error($"Error occurred while retrieving the device. (type={type}, path={devicePath}) {ex3.Message}");
			return;
		}
		try
		{
			reflectionInfo.InvokeRemove(devicePath);
		}
		catch (Exception ex4)
		{
			Logger.Instance.Error($"Error occurred while removing the device. (type={type}, path={devicePath}) {ex4.Message}");
			return;
		}
		try
		{
			controller.Dispose();
		}
		catch (Exception ex5)
		{
			Logger.Instance.Error($"Error occurred while releasing device resources. (type={type}, path={devicePath}) {ex5.Message}\n{ex5.StackTrace}");
			return;
		}
		Logger.Instance.Info($"Device removed successfully. (type={type}, path={devicePath})");
	}
}
