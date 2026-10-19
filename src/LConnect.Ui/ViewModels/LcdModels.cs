using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LConnect3cs.Enums;
using LConnectCore;
using LConnectCore.Products.TLLCD;
using LConnectUi.Services;

namespace LConnectUi.ViewModels;

// One LCD's setting inside a screen (the original ScreenSetting).
public partial class LcdSlot : ObservableObject
{
	private int previewGeneration;

	[ObservableProperty]
	private int fanRpm;

	[ObservableProperty]
	private int frame;

	[ObservableProperty]
	private Bitmap preview;

	[ObservableProperty]
	private double angle;

	[ObservableProperty]
	private string caption = string.Empty;

	[ObservableProperty]
	private IBrush sensorBackground = Brushes.Black;

	[ObservableProperty]
	private IBrush sensorForeground = Brushes.White;

	[ObservableProperty]
	private string sensorValue = string.Empty;

	[ObservableProperty]
	private string sensorLabel = string.Empty;

	[ObservableProperty]
	private TLLCDScreenSetting setting;

	public int Index => Setting?.Index ?? 0;

	public bool IsSensor => Setting?.MediaType == TLLCDMediaType.Sensor;

	public bool IsNone
	{
		get
		{
			TLLCDScreenSetting tLLCDScreenSetting = Setting;
			if (tLLCDScreenSetting == null || tLLCDScreenSetting.MediaType == TLLCDMediaType.None)
			{
				return true;
			}
			return false;
		}
	}

	public bool ShowPreview
	{
		get
		{
			if (!IsSensor)
			{
				return Preview != null;
			}
			return false;
		}
	}

	public bool ShowPlaceholder
	{
		get
		{
			if (!IsSensor)
			{
				return Preview == null;
			}
			return false;
		}
	}

	public string LcdLabel => $"LCD {Index + 1}";

	public LcdSlot(TLLCDScreenSetting setting)
	{
		this.setting = setting;
		Refresh();
	}

	partial void OnPreviewChanged(Bitmap value)
	{
		OnPropertyChanged("ShowPreview");
		OnPropertyChanged("ShowPlaceholder");
	}

	partial void OnSettingChanged(TLLCDScreenSetting value)
	{
		OnPropertyChanged("Index");
		OnPropertyChanged("IsSensor");
		OnPropertyChanged("IsNone");
		OnPropertyChanged("LcdLabel");
		OnPropertyChanged("ShowPreview");
		OnPropertyChanged("ShowPlaceholder");
	}

	public static double AngleOf(ScreenRotation rotation)
	{
		return rotation switch
		{
			ScreenRotation.Rotate90 => 90.0,
			ScreenRotation.Rotate180 => 180.0,
			ScreenRotation.Rotate270 => 270.0,
			_ => 0.0,
		};
	}

	public void Refresh()
	{
		TLLCDScreenSetting tLLCDScreenSetting = Setting;
		Angle = AngleOf(tLLCDScreenSetting.Rotate);
		string text = tLLCDScreenSetting.MediaType switch
		{
			TLLCDMediaType.None => "None",
			TLLCDMediaType.Image => "Image",
			TLLCDMediaType.Dynamic => "GIF",
			TLLCDMediaType.Video => "Video",
			TLLCDMediaType.Animation => "Animation",
			TLLCDMediaType.Sensor => "Sensor",
			_ => tLLCDScreenSetting.MediaType.ToString(),
		};
		if (tLLCDScreenSetting.MediaType == TLLCDMediaType.Sensor && tLLCDScreenSetting.Sensor != null)
		{
			text = text + ": " + Names.Of(tLLCDScreenSetting.Sensor.Type) + $", style {tLLCDScreenSetting.Sensor.Style + 1}";
		}
		else if (!string.IsNullOrEmpty(tLLCDScreenSetting.FilePath))
		{
			text = text + ": " + Path.GetFileNameWithoutExtension(tLLCDScreenSetting.FilePath);
		}
		if (tLLCDScreenSetting.TotalBlock > 1)
		{
			text += $" (block {tLLCDScreenSetting.Block}/{tLLCDScreenSetting.TotalBlock})";
		}
		Caption = text;
		if (IsSensor)
		{
			TLLCDSensorSetting sensor = tLLCDScreenSetting.Sensor;
			Color color = ColorUtil.FromRgbArray(sensor?.BackgroundColor1, Color.FromRgb(0, 0, 255));
			Color color2 = ColorUtil.FromRgbArray(sensor?.BackgroundColor2, color);
			SensorBackground = new LinearGradientBrush
			{
				StartPoint = new Avalonia.RelativePoint(0.0, 0.0, Avalonia.RelativeUnit.Relative),
				EndPoint = new Avalonia.RelativePoint(1.0, 1.0, Avalonia.RelativeUnit.Relative),
				GradientStops =
				{
					new GradientStop(color, 0.0),
					new GradientStop(color2, 1.0)
				}
			};
			SensorForeground = new SolidColorBrush(ColorUtil.FromRgbArray(sensor?.TextColor1, Colors.White));
			UpdateSensorValue();
			Preview = null;
		}
		else
		{
			loadPreview();
		}
	}

	public void UpdateSensorValue()
	{
		TLLCDSensorSetting tLLCDSensorSetting = Setting?.Sensor;
		if (tLLCDSensorSetting != null)
		{
			SystemResource? latest = SensorService.Instance.Latest;
			(string, string) tuple = tLLCDSensorSetting.Type switch
			{
				TLLCDSensorType.CPUTemp => ("CPU TEMP", latest.HasValue ? $"{latest.Value.CPUTemperature}°C" : "--"),
				TLLCDSensorType.GPUTemp => ("GPU TEMP", latest.HasValue ? $"{latest.Value.GPUTemperature}°C" : "--"),
				TLLCDSensorType.CPULoad => ("CPU LOAD", latest.HasValue ? $"{latest.Value.CPULoad}%" : "--"),
				TLLCDSensorType.GPULoad => ("GPU LOAD", latest.HasValue ? $"{latest.Value.GPULoad}%" : "--"),
				TLLCDSensorType.FanRPM => ("FAN", $"{FanRpm}"),
				_ => (string.Empty, string.Empty),
			};
			SensorLabel = tuple.Item1;
			SensorValue = tuple.Item2;
		}
	}

	private void loadPreview()
	{
		TLLCDScreenSetting s = Setting;
		int generation = ++previewGeneration;
		if (s.MediaType == TLLCDMediaType.None || string.IsNullOrEmpty(s.FilePath))
		{
			Preview = null;
			return;
		}
		Task.Run(delegate
		{
			byte[] jpeg = MediaPreview.FirstFrame(s.MediaType, s.FilePath);
			Bitmap bmp = MediaPreview.ToBitmap(MediaPreview.CropBlock(jpeg, s.Block, s.TotalBlock, s.Horizontal), 200);
			Dispatcher.UIThread.Post(delegate
			{
				if (generation == previewGeneration)
				{
					Preview = bmp;
				}
			});
		});
	}
}

// A screen tile of the LCD page: one LCD, or several LCDs sharing one canvas (the original LCDScreen).
public partial class LcdScreen : ObservableObject
{
	[ObservableProperty]
	private bool isSelected;

	public int Index { get; }

	public ObservableCollection<LcdSlot> Slots { get; } = new ObservableCollection<LcdSlot>();

	public int ScreenCount => Slots.Count;

	public bool CanRotate => ScreenCount == 1;

	public string Title
	{
		get
		{
			if (Slots.Count == 0)
			{
				return $"Screen {Index + 1}";
			}
			return "LCD " + string.Join(" + ", Slots.Select((LcdSlot s) => s.Index + 1));
		}
	}

	public LcdScreen(int index)
	{
		Index = index;
	}

	public void SetSetting(List<TLLCDScreenSetting> settings)
	{
		Slots.Clear();
		foreach (TLLCDScreenSetting setting in settings)
		{
			Slots.Add(new LcdSlot(setting));
		}
		OnPropertyChanged("ScreenCount");
		OnPropertyChanged("CanRotate");
		OnPropertyChanged("Title");
	}

	// LCDScreen.SetRotate: index <= 0 applies to every LCD of the screen (quirk kept from the original).
	public void SetRotate(ScreenRotation rotate, int index = 0)
	{
		foreach (LcdSlot slot in Slots)
		{
			if (index <= 0 || slot.Index == index)
			{
				slot.Setting.Rotate = rotate;
				slot.Angle = LcdSlot.AngleOf(rotate);
				slot.Refresh();
			}
		}
	}

	public TLLCDScreenSetting First => Slots.FirstOrDefault()?.Setting;
}

public partial class LcdMediaItem : ObservableObject
{
	[ObservableProperty]
	private Bitmap thumbnail;

	[ObservableProperty]
	private bool isSelected;

	public Media Media { get; }

	public bool IsNone => Media == null;

	public bool CanDelete { get; init; }

	public string Name => (Media == null) ? "None" : Path.GetFileNameWithoutExtension(Media.Name);

	public bool ShowThumbnail => Thumbnail != null;

	public LcdMediaItem(Media media)
	{
		Media = media;
	}

	partial void OnThumbnailChanged(Bitmap value)
	{
		OnPropertyChanged("ShowThumbnail");
	}
}

public sealed class LcdGroupOption
{
	public TLLCDGroupType Type { get; init; }

	public string Name { get; init; }

	public string Description { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public sealed class LcdMediaTypeOption
{
	public TLLCDTypeEnums Type { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}

public sealed class SensorTypeOption
{
	public int Value { get; init; }

	public string Name { get; init; }

	public override string ToString()
	{
		return Name;
	}
}
