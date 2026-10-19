using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LConnect3cs.Enums;
using LConnect3cs.Products.TLFan;
using LConnect3cs.Utils;
using LConnectCore;
using LConnectCore.Products.TLFan;
using LConnectCore.Products.TLLCD;
using LConnectUi.Services;
using AColor = Avalonia.Media.Color;

namespace LConnectUi.ViewModels;

// TL LCD screens. A port of TLFanUtilityLCDScreenPage: the group layouts (screens spanning several LCDs with
// Block/TotalBlock/Horizontal), rotation mapping, media library + conversion, sensor styles and the exact
// update / rotate / update_config requests the original page sent (action=TLLCD).
public partial class LcdViewModel : TLPageBase
{
	private readonly object sendLock = new object();

	private Task sendChain = Task.CompletedTask;

	private string selectedControllerPath;

	private int selectedPortNo = -1;

	private int selectedScreenIndex = -1;

	private int selectedSensorStyle;

	private int selectedSensorType = -1;

	private bool selectedLock;

	private bool suppressTypeHandler;

	private bool settingColors;

	private int deviceCount;

	private ScreenRotation groupRotation;

	private TLLCDGroupType screenGroup;

	private string portKey;

	private int mediaGeneration;

	private bool polling;

	private readonly List<TLLCDSensorStyle> sensorStyles = new List<TLLCDSensorStyle>();

	private readonly DispatcherTimer sensorDebounce;

	[ObservableProperty]
	private LcdGroupOption selectedGroupOption;

	[ObservableProperty]
	private LcdMediaTypeOption selectedMediaType;

	[ObservableProperty]
	private SensorTypeOption selectedSensorTypeOption;

	[ObservableProperty]
	private int sensorStyleIndex;

	[ObservableProperty]
	private bool showMediaList;

	[ObservableProperty]
	private bool showSensor;

	[ObservableProperty]
	private bool showAdd;

	[ObservableProperty]
	private bool showSync;

	[ObservableProperty]
	private bool showGroupPicker;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private int progress;

	[ObservableProperty]
	private string busyText = string.Empty;

	[ObservableProperty]
	private bool isLoadingMedia;

	[ObservableProperty]
	private double groupAngle;

	[ObservableProperty]
	private string screenTitle = string.Empty;

	public override string Title => "TL · LCD";

	public override string IconKey => "Screen";

	public ObservableCollection<LcdScreen> Screens { get; } = new ObservableCollection<LcdScreen>();

	public ObservableCollection<LcdGroupOption> GroupOptions { get; } = new ObservableCollection<LcdGroupOption>();

	public ObservableCollection<LcdMediaTypeOption> MediaTypes { get; } = new ObservableCollection<LcdMediaTypeOption>();

	public ObservableCollection<LcdMediaItem> MediaItems { get; } = new ObservableCollection<LcdMediaItem>();

	public ObservableCollection<SensorTypeOption> SensorTypes { get; } = new ObservableCollection<SensorTypeOption>();

	public ObservableCollection<string> SensorStyles { get; } = new ObservableCollection<string> { "Style 1", "Style 2", "Style 3", "Style 4", "Style 5" };

	public ObservableCollection<ColorSlot> SensorTextColors { get; } = new ObservableCollection<ColorSlot>();

	public ObservableCollection<ColorSlot> SensorBackgroundColors { get; } = new ObservableCollection<ColorSlot>();

	private readonly LcdMediaTypeOption[] allMediaTypes;

	public int DeviceCount => deviceCount;

	public TLLCDGroupType ScreenGroup => screenGroup;

	public ScreenRotation GroupRotation => groupRotation;

	public string GroupRotationText => Names.Rotation(groupRotation);

	public bool HasLcd => deviceCount > 0;

	public LcdScreen SelectedScreen => (selectedScreenIndex >= 0 && selectedScreenIndex < Screens.Count) ? Screens[selectedScreenIndex] : null;

	public string AssetsHint
	{
		get
		{
			List<string> list = new List<string>();
			if (!AssetImporter.HasSensorAssets)
			{
				list.Add("sensor screens (assets/tl-sensor)");
			}
			if (!AssetImporter.HasAnimations)
			{
				list.Add("built-in animations (repo/animation)");
			}
			if (list.Count == 0)
			{
				return null;
			}
			return "Missing " + string.Join(" and ", list) + ". Import them with 'lconnectctl assets import --from <L-Connect 3 install dir>' or Settings › Import assets.";
		}
	}

	public bool ShowAssetsHint => AssetsHint != null;

	public string NoLcdHint => HasController ? "No TL LCD fans detected on this controller." : NoDeviceHint;

	public LcdViewModel()
	{
		allMediaTypes = new LcdMediaTypeOption[5]
		{
			new LcdMediaTypeOption
			{
				Type = TLLCDTypeEnums.Picture,
				Name = "Picture (JPG/PNG)"
			},
			new LcdMediaTypeOption
			{
				Type = TLLCDTypeEnums.Dynamic,
				Name = "Dynamic (GIF)"
			},
			new LcdMediaTypeOption
			{
				Type = TLLCDTypeEnums.Video,
				Name = "Video (MP4)"
			},
			new LcdMediaTypeOption
			{
				Type = TLLCDTypeEnums.Sensor,
				Name = Loc.Get("I18n.Text.Sensor", "Sensor")
			},
			new LcdMediaTypeOption
			{
				Type = TLLCDTypeEnums.Animation,
				Name = "Animation"
			}
		};
		foreach (LcdMediaTypeOption item in allMediaTypes)
		{
			MediaTypes.Add(item);
		}
		selectedMediaType = allMediaTypes[0];
		foreach (TLLCDSensorType value in Enum.GetValues<TLLCDSensorType>())
		{
			SensorTypes.Add(new SensorTypeOption
			{
				Value = (int)value,
				Name = Names.Of(value)
			});
		}
		sensorDebounce = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(350.0)
		};
		sensorDebounce.Tick += delegate
		{
			sensorDebounce.Stop();
			UpdateSensor();
		};
		SensorService.Instance.Updated += delegate
		{
			foreach (LcdScreen screen in Screens)
			{
				foreach (LcdSlot slot in screen.Slots)
				{
					slot.UpdateSensorValue();
				}
			}
		};
		setBackgroundColorCount(1);
		setColors(SensorBackgroundColors, new AColor[1] { AColor.FromRgb(0, 0, byte.MaxValue) });
		setColors(SensorTextColors, new AColor[3]
		{
			AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
			AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
			AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue)
		});
	}

	private protected override bool IncludePort(TLFanPortProfile port)
	{
		return port.PortType == PortType.LCD;
	}

	protected override void OnSelectionChanged()
	{
		string text = SelectedController?.DevicePath + "#" + SelectedPort?.Port;
		OnPropertyChanged("NoLcdHint");
		OnPropertyChanged("AssetsHint");
		OnPropertyChanged("ShowAssetsHint");
		if (text == portKey)
		{
			return;
		}
		portKey = text;
		if (SelectedController == null || SelectedPort == null)
		{
			selectedPortNo = -1;
			deviceCount = 0;
			Screens.Clear();
			MediaItems.Clear();
			OnPropertyChanged("HasLcd");
			return;
		}
		Port_Changed(SelectedPort.Port, SelectedController.DevicePath);
	}

	// ---- daemon requests (sendRequest in the original; writes are queued so they reach the daemon in order) ----

	private void send(string control, object data)
	{
		string path = selectedControllerPath;
		int port = selectedPortNo;
		JsonElement? body = ((data == null) ? null : new JsonElement?(JsonSerializer.SerializeToElement(data)));
		lock (sendLock)
		{
			sendChain = sendChain.ContinueWith((Task _) => DaemonClient.SendTLLCD(control, path, port, body), TaskScheduler.Default);
		}
	}

	private Task<string> query(string control)
	{
		string path = selectedControllerPath;
		int port = selectedPortNo;
		Task task;
		lock (sendLock)
		{
			task = sendChain;
		}
		return task.ContinueWith((Task _) => DaemonClient.SendTLLCD(control, path, port, null), TaskScheduler.Default);
	}

	// Completes when every queued write has been handed to the daemon.
	public Task WhenSent()
	{
		lock (sendLock)
		{
			return sendChain;
		}
	}

	// ---- port / group ----

	public async void Port_Changed(int port, string devicePath)
	{
		await PortChangedAsync(port, devicePath);
	}

	public async Task PortChangedAsync(int port, string devicePath)
	{
		selectedControllerPath = devicePath;
		selectedPortNo = port;
		selectedScreenIndex = -1;
		selectedSensorType = -1;
		selectedSensorStyle = 0;
		setSensorTypeSilently(-1);
		setSensorStyleSilently(0);
		SetSensorStyleColorCount();
		SetSensorStyleDefaultColor();
		ClearScreen();
		SetScreenTypeDefault();
		await LoadDeviceCount();
		await SyncConfig();
	}

	private async Task LoadDeviceCount()
	{
		string text = await query("devices");
		deviceCount = ((!string.IsNullOrEmpty(text) && int.TryParse(text, out var result)) ? result : 0);
		buildGroupOptions();
		OnPropertyChanged("DeviceCount");
		OnPropertyChanged("HasLcd");
	}

	// TLLCDGroupPicker.SetDeviceCount: which layouts exist for 1, 2 or 3 LCDs on a port.
	private void buildGroupOptions()
	{
		TLLCDGroupType[] array = deviceCount switch
		{
			2 => new TLLCDGroupType[2]
			{
				TLLCDGroupType.Group11,
				TLLCDGroupType.Group2
			},
			3 => new TLLCDGroupType[4]
			{
				TLLCDGroupType.Group111,
				TLLCDGroupType.Group21,
				TLLCDGroupType.Group12,
				TLLCDGroupType.Group3
			},
			_ => new TLLCDGroupType[1],
		};
		bool flag = selectedLock;
		selectedLock = true;
		try
		{
			GroupOptions.Clear();
			TLLCDGroupType[] array2 = array;
			foreach (TLLCDGroupType tLLCDGroupType in array2)
			{
				GroupOptions.Add(new LcdGroupOption
				{
					Type = tLLCDGroupType,
					Name = Names.Of(tLLCDGroupType),
					Description = describe(tLLCDGroupType)
				});
			}
			SelectedGroupOption = GroupOptions.FirstOrDefault((LcdGroupOption g) => g.Type == screenGroup);
		}
		finally
		{
			selectedLock = flag;
		}
		ShowGroupPicker = deviceCount > 1;
		static string describe(TLLCDGroupType t)
		{
			return t switch
			{
				TLLCDGroupType.Group1 => "One LCD",
				TLLCDGroupType.Group11 => "Each LCD separately",
				TLLCDGroupType.Group111 => "Each LCD separately",
				TLLCDGroupType.Group2 => "One picture across both LCDs",
				TLLCDGroupType.Group21 => "LCD 1+2 combined, LCD 3 separate",
				TLLCDGroupType.Group12 => "LCD 1 separate, LCD 2+3 combined",
				TLLCDGroupType.Group3 => "One picture across all three LCDs",
				_ => string.Empty,
			};
		}
	}

	public void ClearScreen()
	{
		Screens.Clear();
		selectedScreenIndex = -1;
		OnPropertyChanged("SelectedScreen");
	}

	private async Task SyncConfig()
	{
		TLLCDConfig tLLCDConfig = null;
		try
		{
			string text = await query("config");
			if (!string.IsNullOrEmpty(text))
			{
				tLLCDConfig = JsonSerializer.Deserialize<TLLCDConfig>(text);
			}
		}
		catch
		{
		}
		if (tLLCDConfig == null)
		{
			groupRotation = ScreenRotation.Rotate0;
			screenGroup = deviceCount switch
			{
				1 => TLLCDGroupType.Group1,
				2 => TLLCDGroupType.Group11,
				_ => TLLCDGroupType.Group3,
			};
			selectGroupOptionSilently();
			await SyncSetting();
			return;
		}
		screenGroup = tLLCDConfig.Group;
		groupRotation = tLLCDConfig.Rotate;
		if (deviceCount == 1)
		{
			screenGroup = TLLCDGroupType.Group1;
		}
		if (deviceCount == 2 && (screenGroup == TLLCDGroupType.Group1 || screenGroup == TLLCDGroupType.Group21 || screenGroup == TLLCDGroupType.Group12 || screenGroup == TLLCDGroupType.Group111 || screenGroup == TLLCDGroupType.Group3))
		{
			screenGroup = TLLCDGroupType.Group11;
		}
		if (deviceCount == 3 && (screenGroup == TLLCDGroupType.Group1 || screenGroup == TLLCDGroupType.Group11 || screenGroup == TLLCDGroupType.Group2))
		{
			screenGroup = TLLCDGroupType.Group111;
		}
		selectGroupOptionSilently();
		await GroupChangedAsync(screenGroup);
	}

	private void selectGroupOptionSilently()
	{
		bool flag = selectedLock;
		selectedLock = true;
		SelectedGroupOption = GroupOptions.FirstOrDefault((LcdGroupOption g) => g.Type == screenGroup);
		selectedLock = flag;
		OnPropertyChanged("ScreenGroup");
		OnPropertyChanged("GroupRotationText");
	}

	partial void OnSelectedGroupOptionChanged(LcdGroupOption value)
	{
		if (!selectedLock && value != null)
		{
			GroupChangedAsync(value.Type);
		}
	}

	// GroupButton_CheckedEventHandler.
	public async Task GroupChangedAsync(TLLCDGroupType type)
	{
		screenGroup = type;
		selectedScreenIndex = -1;
		ClearScreen();
		await LoadDeviceCount();
		UpdateConfig();
		await SyncSetting();
		// Syncing one LCD's content to the others only makes sense with more than one separate LCD.
		ShowSync = screenGroup == TLLCDGroupType.Group11 || screenGroup == TLLCDGroupType.Group111;
		OnPropertyChanged("ScreenGroup");
	}

	private void UpdateConfig()
	{
		send("update_config", new TLLCDConfig
		{
			Group = screenGroup,
			Rotate = groupRotation
		});
	}

	private async Task SyncSetting()
	{
		List<TLLCDScreenSetting> settings;
		try
		{
			string text = await query("setting");
			settings = (string.IsNullOrEmpty(text) ? null : JsonSerializer.Deserialize<List<TLLCDScreenSetting>>(text));
		}
		catch
		{
			settings = null;
		}
		if (settings == null)
		{
			return;
		}
		Screens.Clear();
		TLLCDScreenSetting get(int index)
		{
			return settings.FirstOrDefault((TLLCDScreenSetting p) => p.Index == index) ?? new TLLCDScreenSetting
			{
				Index = index,
				MediaType = TLLCDMediaType.None
			};
		}
		LcdScreen add(List<TLLCDScreenSetting> list)
		{
			LcdScreen lcdScreen = new LcdScreen(Screens.Count);
			lcdScreen.SetSetting(list);
			Screens.Add(lcdScreen);
			return lcdScreen;
		}
		if (screenGroup == TLLCDGroupType.Group1 || screenGroup == TLLCDGroupType.Group11 || screenGroup == TLLCDGroupType.Group111)
		{
			for (int num = 0; num < deviceCount; num++)
			{
				add(new List<TLLCDScreenSetting> { get(num) });
			}
		}
		if (screenGroup == TLLCDGroupType.Group2 || screenGroup == TLLCDGroupType.Group21)
		{
			add(new List<TLLCDScreenSetting>
			{
				get(0),
				get(1)
			});
			if (deviceCount == 3)
			{
				add(new List<TLLCDScreenSetting> { get(2) });
			}
		}
		if (screenGroup == TLLCDGroupType.Group12)
		{
			add(new List<TLLCDScreenSetting> { get(0) });
			add(new List<TLLCDScreenSetting>
			{
				get(1),
				get(2)
			});
		}
		if (screenGroup == TLLCDGroupType.Group3)
		{
			List<TLLCDScreenSetting> list2 = new List<TLLCDScreenSetting>();
			for (int num2 = 0; num2 < deviceCount; num2++)
			{
				list2.Add(get(num2));
			}
			add(list2);
		}
		ScreenTitle = Names.Of(screenGroup);
		await SetGroupRotate(rotate: false);
		if (Screens.Count > 0)
		{
			SelectScreen(Screens[0]);
		}
	}

	// ---- screen selection (Screen_CheckedEventHandler) ----

	[RelayCommand]
	public void SelectScreen(LcdScreen screen)
	{
		if (screen == null)
		{
			return;
		}
		selectedScreenIndex = Screens.IndexOf(screen);
		foreach (LcdScreen screen2 in Screens)
		{
			screen2.IsSelected = screen2 == screen;
		}
		OnPropertyChanged("SelectedScreen");
		SetReferenceMediaTypeComboBoxItems();
		SetScreenTypeComboBox();
	}

	// Dynamic (GIF) and Sensor are single-LCD only.
	private void SetReferenceMediaTypeComboBoxItems()
	{
		bool flag = Screens[selectedScreenIndex].ScreenCount > 1;
		LcdMediaTypeOption lcdMediaTypeOption = SelectedMediaType;
		suppressTypeHandler = true;
		try
		{
			MediaTypes.Clear();
			LcdMediaTypeOption[] array = allMediaTypes;
			foreach (LcdMediaTypeOption lcdMediaTypeOption2 in array)
			{
				if (!flag || (lcdMediaTypeOption2.Type != TLLCDTypeEnums.Dynamic && lcdMediaTypeOption2.Type != TLLCDTypeEnums.Sensor))
				{
					MediaTypes.Add(lcdMediaTypeOption2);
				}
			}
			SelectedMediaType = lcdMediaTypeOption;
		}
		finally
		{
			suppressTypeHandler = false;
		}
	}

	private static TLLCDTypeEnums typeFor(TLLCDMediaType mediaType)
	{
		return mediaType switch
		{
			TLLCDMediaType.Dynamic => TLLCDTypeEnums.Dynamic,
			TLLCDMediaType.Video => TLLCDTypeEnums.Video,
			TLLCDMediaType.Sensor => TLLCDTypeEnums.Sensor,
			TLLCDMediaType.Animation => TLLCDTypeEnums.Animation,
			_ => TLLCDTypeEnums.Picture,
		};
	}

	private LcdMediaTypeOption option(TLLCDTypeEnums type)
	{
		return allMediaTypes.First((LcdMediaTypeOption o) => o.Type == type);
	}

	private TLLCDTypeEnums GetLCDSelectedType()
	{
		return SelectedMediaType?.Type ?? TLLCDTypeEnums.Picture;
	}

	private void SetScreenTypeComboBox()
	{
		if (Screens.Count == 0 || selectedScreenIndex == -1)
		{
			return;
		}
		selectedLock = true;
		LcdScreen lcdScreen = Screens[selectedScreenIndex];
		TLLCDMediaType mediaType = lcdScreen.Slots[0].Setting.MediaType;
		if (lcdScreen.ScreenCount > 1 && (mediaType == TLLCDMediaType.Sensor || mediaType == TLLCDMediaType.Dynamic))
		{
			SetScreenTypeDefault();
			selectedLock = false;
		}
		else if (lcdScreen.Slots.All((LcdSlot p) => p.Setting.MediaType == mediaType))
		{
			LcdMediaTypeOption lcdMediaTypeOption = option(typeFor(mediaType));
			if (SelectedMediaType == lcdMediaTypeOption)
			{
				if (mediaType != TLLCDMediaType.Sensor)
				{
					UpdateMediaList();
				}
				LoadScreenSetting();
			}
			else
			{
				SelectedMediaType = lcdMediaTypeOption;
			}
		}
		else
		{
			SetScreenTypeDefault();
			selectedLock = false;
		}
	}

	private void SetScreenTypeDefault()
	{
		SelectedMediaType = MediaTypes.FirstOrDefault() ?? allMediaTypes[0];
		if (selectedScreenIndex >= 0)
		{
			UpdateMediaList();
		}
	}

	partial void OnSelectedMediaTypeChanged(LcdMediaTypeOption value)
	{
		if (!suppressTypeHandler && value != null)
		{
			ReferenceMediaTypeChanged();
		}
	}

	// ReferenceMediaTypeComboBox_SelectionChanged.
	private void ReferenceMediaTypeChanged()
	{
		ShowSensor = false;
		ShowMediaList = false;
		ShowAdd = false;
		bool showSync = false;
		setSensorTypeSilently(-1);
		switch (GetLCDSelectedType())
		{
		case TLLCDTypeEnums.Picture:
		case TLLCDTypeEnums.Dynamic:
		case TLLCDTypeEnums.Video:
			ShowMediaList = true;
			ShowAdd = true;
			if (selectedScreenIndex >= 0)
			{
				UpdateMediaList();
			}
			break;
		case TLLCDTypeEnums.Sensor:
			ShowSensor = true;
			if (!selectedLock)
			{
				SelectedSensorTypeOption = SensorTypes[0];
				SensorStyleIndex = 0;
			}
			break;
		case TLLCDTypeEnums.Animation:
			ShowMediaList = true;
			if (selectedScreenIndex >= 0)
			{
				UpdateMediaList();
			}
			break;
		}
		if (screenGroup == TLLCDGroupType.Group11 || screenGroup == TLLCDGroupType.Group111)
		{
			showSync = true;
		}
		ShowSync = showSync;
		LoadScreenSetting();
	}

	// LoadScreenSetting: reflect the selected screen's current setting in the media list / sensor controls.
	private void LoadScreenSetting()
	{
		if (selectedScreenIndex < 0)
		{
			return;
		}
		selectedLock = true;
		try
		{
			LcdScreen lcdScreen = Screens[selectedScreenIndex];
			highlightMedia();
			TLLCDMediaType mediaType = lcdScreen.Slots[0].Setting.MediaType;
			if (!lcdScreen.Slots.All((LcdSlot p) => p.Setting.MediaType == mediaType) || mediaType != TLLCDMediaType.Sensor)
			{
				return;
			}
			setSensorTypeSilently(-1);
			TLLCDSensorSetting sensor = lcdScreen.Slots[0].Setting.Sensor;
			if (sensor != null)
			{
				selectedSensorType = (int)sensor.Type;
				setSensorTypeSilently(selectedSensorType);
				selectedSensorStyle = sensor.Style;
				setSensorStyleSilently(sensor.Style);
				AColor[] colors = new AColor[3]
				{
					ColorUtil.FromRgbArray(sensor.TextColor1, Avalonia.Media.Colors.White),
					ColorUtil.FromRgbArray(sensor.TextColor2, Avalonia.Media.Colors.White),
					ColorUtil.FromRgbArray(sensor.TextColor3, Avalonia.Media.Colors.White)
				};
				List<AColor> list = new List<AColor>();
				if (sensor.BackgroundColor1 != null)
				{
					list.Add(ColorUtil.FromRgbArray(sensor.BackgroundColor1, Avalonia.Media.Colors.Blue));
				}
				if (sensor.BackgroundColor2 != null)
				{
					list.Add(ColorUtil.FromRgbArray(sensor.BackgroundColor2, Avalonia.Media.Colors.Blue));
				}
				SetSensorStyleColorCount();
				SetSensorStyleDefaultColor();
				setColors(SensorTextColors, colors);
				setColors(SensorBackgroundColors, list.ToArray());
			}
		}
		finally
		{
			selectedLock = false;
		}
	}

	private bool isHighlighted(Media media)
	{
		if (selectedScreenIndex < 0 || selectedScreenIndex >= Screens.Count)
		{
			return false;
		}
		LcdScreen lcdScreen = Screens[selectedScreenIndex];
		if (lcdScreen.Slots.Count == 0)
		{
			return false;
		}
		TLLCDMediaType mediaType = lcdScreen.Slots[0].Setting.MediaType;
		if (!lcdScreen.Slots.All((LcdSlot p) => p.Setting.MediaType == mediaType))
		{
			return false;
		}
		switch (mediaType)
		{
		case TLLCDMediaType.None:
			return media == null;
		case TLLCDMediaType.Image:
		case TLLCDMediaType.Dynamic:
		case TLLCDMediaType.Video:
		case TLLCDMediaType.Animation:
			if (media != null)
			{
				return lcdScreen.Slots.All((LcdSlot p) => p.Setting.FileMD5 == media.MD5);
			}
			return false;
		default:
			return false;
		}
	}

	private void highlightMedia()
	{
		foreach (LcdMediaItem mediaItem in MediaItems)
		{
			mediaItem.IsSelected = isHighlighted(mediaItem.Media);
		}
	}

	// ---- media library (UpdateMediaList) ----

	public Task UpdateMediaList()
	{
		TLLCDTypeEnums type = GetLCDSelectedType();
		if (type == TLLCDTypeEnums.Sensor)
		{
			return Task.CompletedTask;
		}
		int generation = ++mediaGeneration;
		MediaItems.Clear();
		if (type != TLLCDTypeEnums.Animation)
		{
			MediaItems.Add(new LcdMediaItem(null)
			{
				IsSelected = isHighlighted(null)
			});
		}
		int sizeCount = ((selectedScreenIndex < 0 || selectedScreenIndex >= Screens.Count) ? 1 : Screens[selectedScreenIndex].ScreenCount);
		bool horizontal = IsHorizontal();
		IsLoadingMedia = true;
		return loadMediaAsync(type, sizeCount, horizontal, generation);
	}

	private async Task loadMediaAsync(TLLCDTypeEnums type, int sizeCount, bool horizontal, int generation)
	{
		List<(Media, Bitmap)> list;
		try
		{
			list = await Task.Run(delegate
			{
				List<Media> obj = type switch
				{
					TLLCDTypeEnums.Picture => MediaConverter.GetImageList(sizeCount, horizontal),
					TLLCDTypeEnums.Dynamic => MediaConverter.GetDynamicList(sizeCount, horizontal),
					TLLCDTypeEnums.Video => MediaConverter.GetVideoList(sizeCount, horizontal),
					TLLCDTypeEnums.Animation => MediaConverter.GetAnimationList(sizeCount, horizontal),
					_ => new List<Media>(),
				};
				return obj.Select((Media m) => (m, MediaPreview.ToBitmap(m.Bytes, 160))).ToList();
			});
		}
		catch (Exception ex)
		{
			list = new List<(Media, Bitmap)>();
			ShowStatus("Could not read the media library: " + ex.Message, isError: true);
		}
		if (generation != mediaGeneration)
		{
			return;
		}
		foreach (var (media, thumbnail) in list)
		{
			MediaItems.Add(new LcdMediaItem(media)
			{
				Thumbnail = thumbnail,
				CanDelete = (type != TLLCDTypeEnums.Animation),
				IsSelected = isHighlighted(media)
			});
		}
		IsLoadingMedia = false;
	}

	[RelayCommand]
	public void SelectMedia(LcdMediaItem item)
	{
		if (item != null)
		{
			FileSelected(item.Media);
		}
	}

	private void FileSelected(string path)
	{
		if (!string.IsNullOrEmpty(path))
		{
			LcdMediaItem lcdMediaItem = MediaItems.FirstOrDefault((LcdMediaItem p) => p.Media != null && p.Media.Path == path);
			if (lcdMediaItem != null)
			{
				FileSelected(lcdMediaItem.Media);
			}
		}
	}

	// FileSelected(int index): build the screen settings for the selected screen and send them.
	private void FileSelected(Media media)
	{
		if (selectedScreenIndex == -1 || selectedLock)
		{
			return;
		}
		LcdScreen lcdScreen = Screens[selectedScreenIndex];
		ScreenRotation rotate = groupRotation switch
		{
			ScreenRotation.Rotate90 => ScreenRotation.Rotate270,
			ScreenRotation.Rotate180 => ScreenRotation.Rotate180,
			ScreenRotation.Rotate270 => ScreenRotation.Rotate90,
			_ => ScreenRotation.Rotate0,
		};
		if (media == null)
		{
			List<TLLCDScreenSetting> list = new List<TLLCDScreenSetting>();
			foreach (LcdSlot slot in lcdScreen.Slots)
			{
				TLLCDScreenSetting tLLCDScreenSetting = new TLLCDScreenSetting
				{
					Index = slot.Setting.Index,
					Block = 1,
					MediaType = TLLCDMediaType.None,
					Rotate = ScreenRotation.Rotate0
				};
				list.Add(tLLCDScreenSetting);
				send("update", tLLCDScreenSetting);
			}
			lcdScreen.SetSetting(list);
			lcdScreen.SetRotate(rotate);
		}
		else if (lcdScreen.ScreenCount > 1)
		{
			int num = ((groupRotation != ScreenRotation.Rotate270 && groupRotation != ScreenRotation.Rotate180) ? 1 : lcdScreen.ScreenCount);
			List<TLLCDScreenSetting> list2 = new List<TLLCDScreenSetting>();
			int index = lcdScreen.Slots[0].Setting.Index;
			foreach (LcdSlot slot2 in lcdScreen.Slots)
			{
				TLLCDScreenSetting tLLCDScreenSetting2 = new TLLCDScreenSetting
				{
					Index = slot2.Setting.Index,
					TotalBlock = lcdScreen.ScreenCount,
					Block = num,
					Horizontal = IsHorizontal(),
					MediaType = media.Type,
					Rotate = rotate,
					FileMD5 = media.MD5,
					FilePath = media.Path,
					Sync = index
				};
				list2.Add(tLLCDScreenSetting2);
				send("update", tLLCDScreenSetting2);
				num = ((groupRotation == ScreenRotation.Rotate270 || groupRotation == ScreenRotation.Rotate180) ? (num - 1) : (num + 1));
			}
			lcdScreen.SetSetting(list2);
			lcdScreen.SetRotate(rotate);
		}
		else
		{
			TLLCDScreenSetting tLLCDScreenSetting3 = new TLLCDScreenSetting
			{
				Index = lcdScreen.Slots[0].Setting.Index,
				TotalBlock = 1,
				Block = 1,
				Horizontal = IsHorizontal(),
				MediaType = media.Type,
				FilePath = media.Path,
				Rotate = rotate,
				FileMD5 = media.MD5
			};
			lcdScreen.SetSetting(new List<TLLCDScreenSetting> { tLLCDScreenSetting3 });
			lcdScreen.SetRotate(rotate);
			send("update", tLLCDScreenSetting3);
		}
		highlightMedia();
		ShowStatus((media == null) ? (lcdScreen.Title + ": cleared.") : (lcdScreen.Title + ": showing " + Path.GetFileNameWithoutExtension(media.Name) + "."));
	}

	[RelayCommand]
	public void DeleteMedia(LcdMediaItem item)
	{
		if (item?.Media == null)
		{
			return;
		}
		Media media = item.Media;
		foreach (LcdScreen item2 in Screens.Where((LcdScreen p) => p.Slots.Any((LcdSlot x) => x.Setting.FilePath == media.Path)).ToList())
		{
			List<TLLCDScreenSetting> list = new List<TLLCDScreenSetting>();
			for (int num = 0; num < item2.ScreenCount; num++)
			{
				TLLCDScreenSetting tLLCDScreenSetting = new TLLCDScreenSetting
				{
					Index = item2.Slots[num].Setting.Index,
					Block = 1,
					TotalBlock = 1,
					Rotate = ScreenRotation.Rotate0,
					MediaType = TLLCDMediaType.None
				};
				list.Add(tLLCDScreenSetting);
				send("update", tLLCDScreenSetting);
			}
			item2.SetSetting(list);
		}
		try
		{
			File.Delete(media.Path);
			UpdateMediaList();
		}
		catch (Exception ex)
		{
			ShowStatus("Could not delete " + media.Name + ": " + ex.Message, isError: true);
		}
	}

	[RelayCommand]
	public async Task PickAndAddFileAsync()
	{
		TLLCDTypeEnums lCDSelectedType = GetLCDSelectedType();
		IReadOnlyList<(string, string[])> filters = lCDSelectedType switch
		{
			TLLCDTypeEnums.Picture => new (string, string[])[1] { ("Image File (*.jpg, *.png)", new string[3] { "*.jpg", "*.jpeg", "*.png" }) },
			TLLCDTypeEnums.Dynamic => new (string, string[])[1] { ("Image File (*.gif)", new string[1] { "*.gif" }) },
			TLLCDTypeEnums.Video => new (string, string[])[1] { ("Video File (*.mp4)", new string[1] { "*.mp4" }) },
			_ => null,
		};
		if (filters != null)
		{
			string text = await PickAsync(() => UiDialogs.OpenFile("Add " + SelectedMediaType?.Name, filters));
			if (!string.IsNullOrEmpty(text))
			{
				await AddFileAsync(text);
			}
		}
	}

	// AddButton_ClickEventHandler after the file dialog: size checks, conversion with progress, select the result.
	public async Task<string> AddFileAsync(string file)
	{
		if (selectedScreenIndex < 0)
		{
			return null;
		}
		if (MediaItems.Count >= 30)
		{
			ShowStatus("The storage space is full. Please delete files to free up space.", isError: true);
			return null;
		}
		TLLCDTypeEnums type = GetLCDSelectedType();
		FileInfo fileInfo = new FileInfo(file);
		if (!fileInfo.Exists)
		{
			ShowStatus(file + " does not exist.", isError: true);
			return null;
		}
		double num = ((type == TLLCDTypeEnums.Video) ? (await Task.Run(() => MediaConverter.VideoDuration(file))) : 0.0);
		if (fileInfo.Length > 1073741824 || (type == TLLCDTypeEnums.Video && num / 1000.0 > 600.0))
		{
			ShowStatus("The uploaded MP4 file should not exceed 10 minutes / 1GB.", isError: true);
			return null;
		}
		int screenCount = Screens[selectedScreenIndex].ScreenCount;
		bool horizontal = IsHorizontal();
		IsBusy = true;
		Progress = 0;
		BusyText = "Converting " + Path.GetFileName(file) + "…";
		Action<int> progress = delegate(int rate)
		{
			Dispatcher.UIThread.Post(delegate
			{
				Progress = Math.Clamp(rate, 0, 100);
			});
		};
		string path;
		try
		{
			path = await Task.Run(() => type switch
			{
				TLLCDTypeEnums.Picture => MediaConverter.SaveImage(file, screenCount, horizontal),
				TLLCDTypeEnums.Dynamic => MediaConverter.SaveGif(file, screenCount, horizontal, progress),
				TLLCDTypeEnums.Video => MediaConverter.SaveMP4(file, screenCount, horizontal, progress),
				_ => string.Empty,
			});
		}
		finally
		{
			IsBusy = false;
		}
		if (string.IsNullOrEmpty(path))
		{
			ShowStatus("File are not in standard format, please use other files. (Is ffmpeg installed?)", isError: true);
			return null;
		}
		await UpdateMediaList();
		FileSelected(path);
		return path;
	}

	// ---- sync / rotation ----

	[RelayCommand]
	public void SyncToOthers()
	{
		if (selectedScreenIndex == -1)
		{
			return;
		}
		LcdScreen lcdScreen = Screens[selectedScreenIndex];
		TLLCDScreenSetting first = lcdScreen.First;
		if (first == null)
		{
			return;
		}
		foreach (LcdScreen screen in Screens)
		{
			if (screen.Index != selectedScreenIndex)
			{
				TLLCDScreenSetting tLLCDScreenSetting = new TLLCDScreenSetting
				{
					Index = screen.Index,
					MediaType = first.MediaType,
					FilePath = first.FilePath,
					Rotate = first.Rotate,
					Sensor = first.Sensor,
					FileMD5 = first.FileMD5,
					Block = first.Block,
					TotalBlock = first.TotalBlock,
					Horizontal = first.Horizontal,
					Sync = selectedScreenIndex
				};
				screen.SetSetting(new List<TLLCDScreenSetting> { tLLCDScreenSetting });
				screen.SetRotate(first.Rotate);
				send("update", tLLCDScreenSetting);
			}
		}
		ShowStatus("Synchronised " + lcdScreen.Title + " to the other LCDs.");
	}

	private static ScreenRotation rotateLeft(ScreenRotation r)
	{
		return r switch
		{
			ScreenRotation.Rotate0 => ScreenRotation.Rotate270,
			ScreenRotation.Rotate90 => ScreenRotation.Rotate0,
			ScreenRotation.Rotate180 => ScreenRotation.Rotate90,
			_ => ScreenRotation.Rotate180,
		};
	}

	// RotateButton_Click of a single-LCD screen.
	[RelayCommand]
	public void RotateScreen(LcdScreen screen)
	{
		if (screen != null && screen.ScreenCount == 1)
		{
			ScreenRotation rotate = rotateLeft(screen.First.Rotate);
			screen.SetRotate(rotate);
			send("rotate", new List<TLLCDScreenRotate>
			{
				new TLLCDScreenRotate
				{
					Index = screen.First.Index,
					Rotate = rotate
				}
			});
		}
	}

	[RelayCommand]
	public async Task RotateGroupAsync()
	{
		groupRotation = rotateLeft(groupRotation);
		await SetGroupRotate();
		UpdateConfig();
		UpdateMediaList();
		OnPropertyChanged("GroupRotation");
		OnPropertyChanged("GroupRotationText");
	}

	private Task SetGroupRotate(bool rotate = true)
	{
		GroupAngle = LcdSlot.AngleOf(groupRotation);
		OnPropertyChanged("GroupRotationText");
		ScreenRotation rotate2 = groupRotation switch
		{
			ScreenRotation.Rotate90 => ScreenRotation.Rotate270,
			ScreenRotation.Rotate180 => ScreenRotation.Rotate180,
			ScreenRotation.Rotate270 => ScreenRotation.Rotate90,
			_ => ScreenRotation.Rotate0,
		};
		if (!rotate)
		{
			return Task.CompletedTask;
		}
		foreach (LcdScreen screen in Screens)
		{
			if (screen.ScreenCount == 1 && screen.Slots.All((LcdSlot p) => p.Setting.TotalBlock == 1))
			{
				screen.SetRotate(rotate2);
				continue;
			}
			foreach (LcdSlot item in screen.Slots.ToList())
			{
				if (item.Setting.MediaType == TLLCDMediaType.None || item.Setting.TotalBlock == 1)
				{
					screen.SetRotate(rotate2, item.Index);
				}
			}
		}
		List<TLLCDScreenRotate> list = new List<TLLCDScreenRotate>();
		foreach (LcdScreen screen2 in Screens)
		{
			foreach (LcdSlot slot in screen2.Slots)
			{
				if ((screen2.ScreenCount == 1 && slot.Setting.TotalBlock == 1) || slot.Setting.MediaType == TLLCDMediaType.None || slot.Setting.TotalBlock == 1)
				{
					list.Add(new TLLCDScreenRotate
					{
						Index = slot.Setting.Index,
						Rotate = rotate2
					});
				}
			}
		}
		send("rotate", list);
		return Task.CompletedTask;
	}

	private bool IsHorizontal()
	{
		if (groupRotation != ScreenRotation.Rotate0)
		{
			return groupRotation == ScreenRotation.Rotate180;
		}
		return true;
	}

	// ---- sensor ----

	public int SelectedSensorType => selectedSensorType;

	public int SelectedSensorStyle => selectedSensorStyle;

	public bool ShowSecondBackground => SensorBackgroundColors.Count > 1;

	private void setSensorTypeSilently(int value)
	{
		bool flag = selectedLock;
		selectedLock = true;
		SelectedSensorTypeOption = ((value < 0) ? null : SensorTypes.FirstOrDefault((SensorTypeOption s) => s.Value == value));
		selectedLock = flag;
	}

	private void setSensorStyleSilently(int value)
	{
		bool flag = selectedLock;
		selectedLock = true;
		SensorStyleIndex = value;
		selectedLock = flag;
	}

	partial void OnSelectedSensorTypeOptionChanged(SensorTypeOption value)
	{
		if (selectedLock)
		{
			return;
		}
		if (selectedScreenIndex == -1)
		{
			setSensorTypeSilently(-1);
			return;
		}
		selectedSensorType = value?.Value ?? (-1);
		UpdateSensor();
	}

	partial void OnSensorStyleIndexChanged(int value)
	{
		if (!selectedLock && value >= 0)
		{
			selectedSensorStyle = value;
			SetSensorStyleColorCount();
			SetSensorStyleDefaultColor();
			UpdateSensor();
		}
	}

	private void SetSensorStyleColorCount()
	{
		setBackgroundColorCount((selectedSensorStyle == 3 || selectedSensorStyle == 4) ? 2 : 1);
	}

	private void setBackgroundColorCount(int count)
	{
		settingColors = true;
		try
		{
			while (SensorBackgroundColors.Count > count)
			{
				SensorBackgroundColors.RemoveAt(SensorBackgroundColors.Count - 1);
			}
			while (SensorBackgroundColors.Count < count)
			{
				ColorSlot colorSlot = new ColorSlot(SensorBackgroundColors.Count, Avalonia.Media.Colors.White);
				colorSlot.PropertyChanged += onSensorColorChanged;
				SensorBackgroundColors.Add(colorSlot);
			}
			while (SensorTextColors.Count < 3)
			{
				ColorSlot colorSlot2 = new ColorSlot(SensorTextColors.Count, Avalonia.Media.Colors.White);
				colorSlot2.PropertyChanged += onSensorColorChanged;
				SensorTextColors.Add(colorSlot2);
			}
		}
		finally
		{
			settingColors = false;
		}
		OnPropertyChanged("ShowSecondBackground");
	}

	private void setColors(ObservableCollection<ColorSlot> slots, AColor[] colors)
	{
		settingColors = true;
		try
		{
			int num = Math.Min(colors.Length, slots.Count);
			for (int i = 0; i < num; i++)
			{
				slots[i].Color = colors[i];
			}
		}
		finally
		{
			settingColors = false;
		}
	}

	private void onSensorColorChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (!settingColors && !selectedLock && e.PropertyName == "Color")
		{
			sensorDebounce.Stop();
			sensorDebounce.Start();
		}
	}

	// Applies pending colour edits immediately (tests / before leaving the page).
	public void FlushSensorColors()
	{
		if (sensorDebounce.IsEnabled)
		{
			sensorDebounce.Stop();
			UpdateSensor();
		}
	}

	private void SetSensorStyleDefaultColor()
	{
		TLLCDSensorStyle tLLCDSensorStyle = sensorStyles.FirstOrDefault((TLLCDSensorStyle p) => p.Style == selectedSensorStyle);
		if (tLLCDSensorStyle == null)
		{
			if (selectedSensorStyle == 3)
			{
				setColors(SensorBackgroundColors, new AColor[2]
				{
					AColor.FromRgb(0, 0, byte.MaxValue),
					AColor.FromRgb(0, 198, 63)
				});
				setColors(SensorTextColors, new AColor[3]
				{
					AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
					AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
					AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue)
				});
			}
			else if (selectedSensorStyle == 4)
			{
				setColors(SensorBackgroundColors, new AColor[2]
				{
					AColor.FromRgb(byte.MaxValue, 0, 161),
					AColor.FromRgb(42, 0, byte.MaxValue)
				});
				setColors(SensorTextColors, new AColor[3]
				{
					AColor.FromRgb(byte.MaxValue, 0, 161),
					AColor.FromRgb(0, 0, 0),
					AColor.FromRgb(0, 0, 0)
				});
			}
			else
			{
				setColors(SensorBackgroundColors, new AColor[1] { AColor.FromRgb(0, 0, byte.MaxValue) });
				setColors(SensorTextColors, new AColor[3]
				{
					AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
					AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
					AColor.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue)
				});
			}
		}
		else
		{
			setColors(SensorBackgroundColors, tLLCDSensorStyle.BackgroundColors.Select((System.Windows.Media.Color c) => c.ToAvalonia()).ToArray());
			setColors(SensorTextColors, tLLCDSensorStyle.TextColors.Select((System.Windows.Media.Color c) => c.ToAvalonia()).ToArray());
		}
	}

	// UpdateSensor: single-LCD screens only.
	public void UpdateSensor()
	{
		if (selectedScreenIndex == -1 || selectedScreenIndex >= Screens.Count)
		{
			return;
		}
		LcdScreen lcdScreen = Screens[selectedScreenIndex];
		if (lcdScreen.ScreenCount > 1)
		{
			return;
		}
		AColor[] array = SensorTextColors.Select((ColorSlot c) => c.Color).ToArray();
		AColor[] array2 = SensorBackgroundColors.Select((ColorSlot c) => c.Color).ToArray();
		if (selectedSensorType != -1 && array.Length == 3 && array2.Length >= 1)
		{
			TLLCDSensorStyle tLLCDSensorStyle = sensorStyles.FirstOrDefault((TLLCDSensorStyle p) => p.Style == selectedSensorStyle);
			if (tLLCDSensorStyle != null)
			{
				sensorStyles.Remove(tLLCDSensorStyle);
			}
			sensorStyles.Add(new TLLCDSensorStyle
			{
				Style = selectedSensorStyle,
				TextColors = array.Select((AColor c) => c.ToDevice()).ToArray(),
				BackgroundColors = array2.Select((AColor c) => c.ToDevice()).ToArray()
			});
			TLLCDScreenSetting first = lcdScreen.First;
			TLLCDScreenSetting tLLCDScreenSetting = new TLLCDScreenSetting
			{
				Index = first.Index,
				MediaType = TLLCDMediaType.Sensor,
				FilePath = string.Empty,
				Rotate = first.Rotate,
				Block = 1,
				TotalBlock = 1,
				Sensor = new TLLCDSensorSetting
				{
					Type = (TLLCDSensorType)selectedSensorType,
					Style = selectedSensorStyle,
					TextColor1 = ColorUtil.ToRgbArray(array[0]),
					TextColor2 = ColorUtil.ToRgbArray(array[1]),
					TextColor3 = ColorUtil.ToRgbArray(array[2]),
					BackgroundColor1 = ColorUtil.ToRgbArray(array2[0])
				}
			};
			if ((selectedSensorStyle == 3 || selectedSensorStyle == 4) && array2.Length >= 2)
			{
				tLLCDScreenSetting.Sensor.BackgroundColor2 = ColorUtil.ToRgbArray(array2[1]);
			}
			lcdScreen.SetSetting(new List<TLLCDScreenSetting> { tLLCDScreenSetting });
			send("update", tLLCDScreenSetting);
			ShowStatus(lcdScreen.Title + ": " + Names.Of((TLLCDSensorType)selectedSensorType) + $" sensor, style {selectedSensorStyle + 1}.");
		}
	}

	// ---- live status (updateScreenStatusTimer) ----

	protected override void OnPoll()
	{
		base.OnPoll();
		pollScreens();
	}

	private async void pollScreens()
	{
		if (selectedPortNo < 0 || polling || IsBusy)
		{
			return;
		}
		polling = true;
		try
		{
			string text = await query("devices");
			int num = ((!string.IsNullOrEmpty(text) && int.TryParse(text, out var result)) ? result : 0);
			if (text != null && num != deviceCount)
			{
				portKey = null;
				await PortChangedAsync(selectedPortNo, selectedControllerPath);
				return;
			}
			string text2 = await query("frame");
			string text3 = await query("rpm");
			List<TLLCDFrame> list = tryParse<List<TLLCDFrame>>(text2) ?? new List<TLLCDFrame>();
			List<TLLCDFanRPM> list2 = tryParse<List<TLLCDFanRPM>>(text3) ?? new List<TLLCDFanRPM>();
			foreach (LcdScreen screen in Screens)
			{
				foreach (LcdSlot slot in screen.Slots)
				{
					TLLCDFrame tLLCDFrame = list.FirstOrDefault((TLLCDFrame f) => f.Index == slot.Index);
					if (tLLCDFrame != null)
					{
						slot.Frame = tLLCDFrame.Frame;
					}
					TLLCDFanRPM tLLCDFanRPM = list2.FirstOrDefault((TLLCDFanRPM r) => r.Index == slot.Index);
					if (tLLCDFanRPM != null)
					{
						slot.FanRpm = tLLCDFanRPM.RPM;
						slot.UpdateSensorValue();
					}
				}
			}
		}
		catch
		{
		}
		finally
		{
			polling = false;
		}
	}

	private static T tryParse<T>(string json) where T : class
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<T>(json);
		}
		catch
		{
			return null;
		}
	}
}
