// Shared golden scenarios. Compiled against the original assemblies (tools/GoldenHarness, net48) to produce
// tests/Golden/golden.txt, and against the port (tests/LConnect.Core.Tests, net10) to verify it.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using LConnectCore.Devices;
using LConnectCore.Products.Ene6K77Fan;
using LConnectCore.Products.TLFan;
using LConnectCore.Products.TLLCD;
using TL = LConnectCore.Products.TLFan;

namespace Golden
{
	public static class Scenarios
	{
		public delegate void Scenario(Action<string, FakeHidDevice> emit);

		private static readonly Color[][] ColorSets = new Color[4][]
		{
			new Color[0],
			new Color[1] { Color.FromRgb(255, 0, 0) },
			new Color[2]
			{
				Color.FromRgb(0, 0, 255),
				Color.FromRgb(0, 215, 255)
			},
			new Color[4]
			{
				Color.FromRgb(255, 0, 0),
				Color.FromRgb(0, 0, 255),
				Color.FromRgb(0, 128, 0),
				Color.FromRgb(255, 255, 0)
			}
		};

		public static IEnumerable<KeyValuePair<string, Func<FakeHidDevice>>> All(string fixturesDir, Assembly serviceAssembly)
		{
			// ---- TL controller, device level ----
			yield return Case("tl.handshake", SimKind.TLController, delegate(FakeHidDevice d)
			{
				TLFanDevice tLFanDevice = new TLFanDevice(d);
				TLFanHandshakeInfo handshakeInfo = tLFanDevice.GetHandshakeInfo();
				d.Log.Add("=> valid=" + handshakeInfo.IsValid + " q=" + string.Join(",", handshakeInfo.FanQuantity) + " rpm=" + handshakeInfo.FanInfos[2, 0].FanSpeed);
			});
			yield return Case("tl.firmware", SimKind.TLController, delegate(FakeHidDevice d)
			{
				TLFanDevice tLFanDevice = new TLFanDevice(d);
				TL.FirmwareVersion controllerFirmwareVersion = tLFanDevice.GetControllerFirmwareVersion();
				TL.FirmwareVersion fanFirmwareVersion = tLFanDevice.GetFanFirmwareVersion(2, 0);
				d.Log.Add("=> " + controllerFirmwareVersion.ModelB + " " + controllerFirmwareVersion.SoftwareVersion + " | " + fanFirmwareVersion.ModelB + " " + fanFirmwareVersion.SoftwareVersion);
			});
			foreach (TL.LightingMode mode in Enum.GetValues(typeof(TL.LightingMode)))
			{
				TL.LightingMode m = mode;
				yield return Case("tl.fanlight." + m, SimKind.TLController, delegate(FakeHidDevice d)
				{
					TLFanDevice tLFanDevice = new TLFanDevice(d);
					int num = 0;
					Color[][] colorSets = ColorSets;
					foreach (Color[] colors in colorSets)
					{
						TL.LightingConfig config = new TL.LightingConfig
						{
							Mode = m,
							Speed = (byte)(num % 5),
							Brightness = (byte)(4 - num % 5),
							Direction = (TL.LightingDirection)(num % 6),
							Colors = colors
						};
						tLFanDevice.SetFanLight(num % 4, num % 3, config, isSync: false, disable: false);
						tLFanDevice.SetGroupLight(num * 7 % 31, config, disable: false);
						num++;
					}
				});
			}
			yield return Case("tl.fanlight.sync_disable", SimKind.TLController, delegate(FakeHidDevice d)
			{
				TLFanDevice tLFanDevice = new TLFanDevice(d);
				TL.LightingConfig config = new TL.LightingConfig
				{
					Colors = ColorSets[3]
				};
				tLFanDevice.SetFanLight(1, 2, config, isSync: true, disable: false);
				tLFanDevice.SetFanLight(3, 15, config, isSync: false, disable: true);
				tLFanDevice.SetGroupLight(5, config, disable: true);
			});
			yield return Case("tl.speed_group_direction", SimKind.TLController, delegate(FakeHidDevice d)
			{
				TLFanDevice tLFanDevice = new TLFanDevice(d);
				int[] array = new int[6] { 0, 1, 12, 50, 100, 255 };
				foreach (int pwm in array)
				{
					tLFanDevice.SetFanSpeed(2, 0, pwm);
					tLFanDevice.SetFanSpeed(3, 15, pwm);
				}
				tLFanDevice.SetFanGroup(0, new FanGroupParams[0]);
				tLFanDevice.SetFanGroup(3, new FanGroupParams[3]
				{
					new FanGroupParams
					{
						Port = 0,
						FanIndex = 1,
						TopSide = true
					},
					new FanGroupParams
					{
						Port = 2,
						FanIndex = 0,
						BottomSide = true
					},
					new FanGroupParams
					{
						Port = 3,
						FanIndex = 15,
						TopSide = true,
						BottomSide = true
					}
				});
				for (int j = 0; j < 4; j++)
				{
					tLFanDevice.SetFanDirection(j, j * 3, j % 2 == 0, j > 1);
					tLFanDevice.SetPortDirection(j, j % 2 == 1);
					tLFanDevice.SetMBRPMSync(j, j, j % 2 == 0);
				}
				tLFanDevice.TestFanLighting(2, 1, isBottom: true);
				tLFanDevice.TestGroupLighting(30, isBottom: false);
				tLFanDevice.TestPortLighting(3, isBottom: true);
			});
			// ---- TL LCD, device level ----
			yield return Case("lcd.open", SimKind.TLLCD, delegate(FakeHidDevice d)
			{
				TLLCDDevice tLLCDDevice = new TLLCDDevice(d, "usb.0");
				bool flag = tLLCDDevice.Open();
				LConnectCore.Products.TLLCD.FirmwareVersion firmwareVersion = tLLCDDevice.FirmwareVersionV();
				HandshakeInfo handshakeInfo = tLLCDDevice.GetHandshakeInfo();
				d.Log.Add("=> open=" + flag + " port=" + tLLCDDevice.GetPort() + " idx=" + tLLCDDevice.GetLCDIndex() + " fw=" + firmwareVersion.SoftwareVersion + " hs=" + handshakeInfo.Mode + "/" + handshakeInfo.FrameIndex);
			});
			yield return Case("lcd.control", SimKind.TLLCD, delegate(FakeHidDevice d)
			{
				TLLCDDevice tLLCDDevice = new TLLCDDevice(d, "usb.0");
				d.Open();
				foreach (LCDControlMode value in Enum.GetValues(typeof(LCDControlMode)))
				{
					foreach (ScreenRotation value2 in Enum.GetValues(typeof(ScreenRotation)))
					{
						tLLCDDevice.LCDControl(new LCDControlSetting
						{
							Mode = value,
							Rotation = value2,
							Brightness = (byte)((int)value2 * 33),
							JPGIndex = (ushort)((int)value * 300),
							VideoFPS = 25,
							EnableTest = (value == LCDControlMode.LCDTest),
							TestColor = Color.FromRgb(1, 2, 3)
						});
					}
				}
				tLLCDDevice.LCDControl(default(LCDControlSetting));
				tLLCDDevice.LCDControl(new LCDControlSetting());
			});
			yield return Case("lcd.transfers", SimKind.TLLCD, delegate(FakeHidDevice d)
			{
				TLLCDDevice tLLCDDevice = new TLLCDDevice(d, "usb.0");
				d.Open();
				byte[] array = Enumerable.Range(0, 1203).Select((int i) => (byte)(i * 7 + 3)).ToArray();
				byte[] bytes = Enumerable.Range(0, 501).Select((int i) => (byte)(i + 1)).ToArray();
				d.Log.Add("=> " + tLLCDDevice.SendJPG(array));
				d.Log.Add("=> " + tLLCDDevice.SendAVI(bytes));
				d.Log.Add("=> " + tLLCDDevice.SendSyncJPG(array));
				d.Log.Add("=> " + tLLCDDevice.SendBootJPG(array));
				d.Log.Add("=> " + tLLCDDevice.SendBootVideo(new byte[0]));
			});
			// ---- SL Infinity, device level ----
			yield return Case("slinf.all", SimKind.SLInfinity, delegate(FakeHidDevice d)
			{
				SLInfinityDevice sLInfinityDevice = new SLInfinityDevice(d)
				{
					WriteDelayTime = 0
				};
				d.Log.Add("=> fw=" + (sLInfinityDevice.GetFirmwareVersion() != null));
				d.Log.Add("=> rpm=" + string.Join(",", sLInfinityDevice.GetFanSpeed()));
				d.Log.Add("=> frame=" + sLInfinityDevice.GetFrame());
				for (int i = 0; i < 4; i++)
				{
					sLInfinityDevice.SetFanSpeed(i, i * 40 + 5);
					sLInfinityDevice.Ping(i, (byte)i, (byte)(4 - i), i % 2 == 0);
					sLInfinityDevice.SetQuantity(i, i + 1);
					sLInfinityDevice.SetFanMotherboardSync(i, i % 2 == 1);
				}
				for (byte b = 0; b < 8; b++)
				{
					sLInfinityDevice.SetEffectSetting(b, (byte)(b * 9), (byte)b, (byte)(b % 2), (byte)(b % 5));
					sLInfinityDevice.SetColorSetting(b, ColorSets[b % 4]);
				}
				sLInfinityDevice.SetFrame(4660);
				sLInfinityDevice.SetLightingMotherboardSync(isSync: true);
				sLInfinityDevice.SetLightingMotherboardSync(isSync: false);
				sLInfinityDevice.SetMergeOrder(new int[4] { 3, 1, 0, 2 });
			});
			// ---- Service controllers fed with the user's real saved settings ----
			if (serviceAssembly == null || fixturesDir == null || !Directory.Exists(fixturesDir))
			{
				yield break;
			}
			string tlDir = Path.Combine(fixturesDir, "device", "69ddedcae59aaa90668efb45766e139c");
			if (Directory.Exists(tlDir))
			{
				yield return Case("svc.tlfan.applyall", SimKind.TLController, delegate(FakeHidDevice d)
				{
					object obj = Construct(serviceAssembly, "LConnectService.Products.TLFan.TLFanController", new TLFanDevice(d));
					ApplyAll(obj, tlDir);
				});
			}
			string slDir = Path.Combine(fixturesDir, "device", "be7b4726de2cf1d3bd60cb14ef962b04");
			if (Directory.Exists(slDir))
			{
				yield return Case("svc.slinf.applyall", SimKind.SLInfinity, delegate(FakeHidDevice d)
				{
					object obj = Construct(serviceAssembly, "LConnectService.Products.Ene6K77Fan.SLInfinityController", new SLInfinityDevice(d)
					{
						WriteDelayTime = 0
					});
					ApplyAll(obj, slDir);
				});
			}
		}

		private static object Construct(Assembly assembly, string typeName, object device)
		{
			Type type = assembly.GetType(typeName, throwOnError: true);
			ConstructorInfo constructorInfo = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First((ConstructorInfo c) => c.GetParameters().Length == 1);
			return constructorInfo.Invoke(new object[1] { device });
		}

		private static void ApplyAll(object controller, string settingsDir)
		{
			List<DeviceSetting<object>> list = (from f in Directory.GetFiles(settingsDir, "*.0")
				orderby f
				select DeviceSettingManager.Load(f)).ToList();
			MethodInfo method = controller.GetType().GetMethod("ApplyAll", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			// Dispose is not called: the original TLFanController.Dispose spins until Init() has run.
			method.Invoke(controller, new object[1] { list });
		}

		private static KeyValuePair<string, Func<FakeHidDevice>> Case(string name, SimKind kind, Action<FakeHidDevice> body)
		{
			return new KeyValuePair<string, Func<FakeHidDevice>>(name, delegate
			{
				FakeHidDevice fakeHidDevice = new FakeHidDevice(kind);
				try
				{
					body(fakeHidDevice);
				}
				catch (Exception ex)
				{
					Exception ex2 = ((ex is TargetInvocationException && ex.InnerException != null) ? ex.InnerException : ex);
					fakeHidDevice.Log.Add("!! " + ex2.GetType().Name + ": " + ex2.Message);
				}
				return fakeHidDevice;
			});
		}

		public static string Render(string fixturesDir, Assembly serviceAssembly)
		{
			System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
			foreach (KeyValuePair<string, Func<FakeHidDevice>> item in All(fixturesDir, serviceAssembly))
			{
				FakeHidDevice fakeHidDevice = item.Value();
				stringBuilder.Append("## ").Append(item.Key).Append('\n');
				foreach (string item2 in fakeHidDevice.Log)
				{
					stringBuilder.Append(item2).Append('\n');
				}
			}
			return stringBuilder.ToString();
		}
	}
}
