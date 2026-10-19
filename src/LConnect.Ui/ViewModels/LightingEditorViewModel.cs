using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LConnectUi.Services;
using AColor = Avalonia.Media.Color;
using WColor = System.Windows.Media.Color;

namespace LConnectUi.ViewModels;

public partial class ColorSlot : ObservableObject
{
	[ObservableProperty]
	private AColor color;

	public int Index { get; }

	public string Label => $"{Index + 1}";

	public ColorSlot(int index, AColor color)
	{
		Index = index;
		this.color = color;
	}
}

// Colours / speed / brightness / direction editor shared by the TL and SL Infinity lighting pages. Which controls
// are shown follows the original LightingViewSettings of the selected mode. Speed and brightness are 0-100 in
// steps of 25 like the original sliders.
public partial class LightingEditorViewModel : ObservableObject
{
	[ObservableProperty]
	private int colorCount;

	[ObservableProperty]
	private bool showSpeed;

	[ObservableProperty]
	private bool showBrightness;

	[ObservableProperty]
	private bool showDirection;

	[ObservableProperty]
	private double speed = 75.0;

	[ObservableProperty]
	private double brightness = 100.0;

	[ObservableProperty]
	private bool directionLeft;

	public ObservableCollection<ColorSlot> Colors { get; } = new ObservableCollection<ColorSlot>();

	public bool ShowColors => ColorCount > 0;

	public bool HasAnySetting
	{
		get
		{
			if (ColorCount <= 0 && !ShowSpeed && !ShowBrightness)
			{
				return ShowDirection;
			}
			return true;
		}
	}

	public string SpeedText => $"{(int)Speed}%";

	public string BrightnessText => $"{(int)Brightness}%";

	partial void OnColorCountChanged(int value)
	{
		OnPropertyChanged("ShowColors");
		OnPropertyChanged("HasAnySetting");
	}

	partial void OnShowSpeedChanged(bool value)
	{
		OnPropertyChanged("HasAnySetting");
	}

	partial void OnShowBrightnessChanged(bool value)
	{
		OnPropertyChanged("HasAnySetting");
	}

	partial void OnShowDirectionChanged(bool value)
	{
		OnPropertyChanged("HasAnySetting");
	}

	partial void OnSpeedChanged(double value)
	{
		OnPropertyChanged("SpeedText");
	}

	partial void OnBrightnessChanged(double value)
	{
		OnPropertyChanged("BrightnessText");
	}

	public void Load(int colorCount, bool speed, bool brightness, bool direction, WColor[] colors, int? speedValue, int? brightnessValue, bool isLeft)
	{
		ColorCount = colorCount;
		ShowSpeed = speed;
		ShowBrightness = brightness;
		ShowDirection = direction;
		Colors.Clear();
		for (int i = 0; i < colorCount; i++)
		{
			AColor color = ((colors != null && i < colors.Length) ? colors[i].ToAvalonia() : Avalonia.Media.Colors.White);
			Colors.Add(new ColorSlot(i, color));
		}
		Speed = Snap(speedValue.GetValueOrDefault());
		Brightness = Snap(brightnessValue.GetValueOrDefault());
		DirectionLeft = isLeft;
	}

	public WColor[] GetColors()
	{
		return Colors.Select((ColorSlot c) => c.Color.ToDevice()).ToArray();
	}

	public int SpeedValue => Snap(Speed);

	public int BrightnessValue => Snap(Brightness);

	public static int Snap(double value)
	{
		return Math.Clamp((int)(Math.Round(value / 25.0) * 25.0), 0, 100);
	}
}
