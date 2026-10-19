using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LConnectUi;

// Page icon key -> geometry resource "Icon.<key>".
public sealed class IconConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string text && Application.Current != null && Application.Current.TryGetResource("Icon." + text, Application.Current.ActualThemeVariant, out var value2))
		{
			return value2 as Geometry;
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}

public sealed class BoolToBrushConverter : IValueConverter
{
	public IBrush TrueBrush { get; set; } = Brushes.Red;

	public IBrush FalseBrush { get; set; } = Brushes.Gray;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(value is bool flag) || !flag)
		{
			return FalseBrush;
		}
		return TrueBrush;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
