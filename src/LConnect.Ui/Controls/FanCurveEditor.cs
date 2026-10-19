using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LConnectCore.Products;

namespace LConnectUi.Controls;

// Fan curve chart: temperature 0-100 °C on the x axis, RPM on the y axis (0 .. ChartMaxSpeed). Points can be
// dragged (constraints in CurveMath); the dashed segments show what the controller adds around the curve
// (MinSpeed below the first point, or 0 rpm with start/stop, and MaxSpeed at 100 °C).
public class FanCurveEditor : Control
{
	public static readonly StyledProperty<RPMPhase[]> PhasesProperty = AvaloniaProperty.Register<FanCurveEditor, RPMPhase[]>("Phases", null, inherits: false, BindingMode.TwoWay);

	public static readonly StyledProperty<int> MinSpeedProperty = AvaloniaProperty.Register<FanCurveEditor, int>("MinSpeed", 0);

	public static readonly StyledProperty<int> MaxSpeedProperty = AvaloniaProperty.Register<FanCurveEditor, int>("MaxSpeed", 2000);

	public static readonly StyledProperty<int> ChartMaxSpeedProperty = AvaloniaProperty.Register<FanCurveEditor, int>("ChartMaxSpeed", 2000);

	public static readonly StyledProperty<double?> TemperatureProperty = AvaloniaProperty.Register<FanCurveEditor, double?>("Temperature");

	public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<FanCurveEditor, int>("SelectedIndex", 0, inherits: false, BindingMode.TwoWay);

	public static readonly StyledProperty<bool> IsStartStopProperty = AvaloniaProperty.Register<FanCurveEditor, bool>("IsStartStop");

	public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<FanCurveEditor, bool>("IsReadOnly");

	private const double PadLeft = 54.0;

	private const double PadRight = 18.0;

	private const double PadTop = 14.0;

	private const double PadBottom = 32.0;

	private const double PointRadius = 6.0;

	private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(24, 27, 32));

	private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1.0);

	private static readonly IPen AxisPen = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), 1.0);

	private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));

	private static readonly IBrush AccentBrush = new SolidColorBrush(Color.FromRgb(46, 155, 255));

	private static readonly IPen CurvePen = new Pen(AccentBrush, 2.5);

	private static readonly IPen ImplicitPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 46, 155, 255)), 1.5, new DashStyle(new double[2] { 4.0, 3.0 }, 0.0));

	private static readonly IBrush FillBrush = new SolidColorBrush(Color.FromArgb(36, 46, 155, 255));

	private static readonly IPen TemperaturePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 152, 56)), 1.5, new DashStyle(new double[2] { 5.0, 3.0 }, 0.0));

	private static readonly IBrush TemperatureBrush = new SolidColorBrush(Color.FromRgb(255, 152, 56));

	private static readonly IBrush LimitBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));

	private static readonly IPen PointPen = new Pen(AccentBrush, 2.0);

	private int dragIndex = -1;

	public RPMPhase[] Phases
	{
		get => GetValue(PhasesProperty);
		set => SetValue(PhasesProperty, value);
	}

	public int MinSpeed
	{
		get => GetValue(MinSpeedProperty);
		set => SetValue(MinSpeedProperty, value);
	}

	public int MaxSpeed
	{
		get => GetValue(MaxSpeedProperty);
		set => SetValue(MaxSpeedProperty, value);
	}

	public int ChartMaxSpeed
	{
		get => GetValue(ChartMaxSpeedProperty);
		set => SetValue(ChartMaxSpeedProperty, value);
	}

	public double? Temperature
	{
		get => GetValue(TemperatureProperty);
		set => SetValue(TemperatureProperty, value);
	}

	public int SelectedIndex
	{
		get => GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public bool IsStartStop
	{
		get => GetValue(IsStartStopProperty);
		set => SetValue(IsStartStopProperty, value);
	}

	public bool IsReadOnly
	{
		get => GetValue(IsReadOnlyProperty);
		set => SetValue(IsReadOnlyProperty, value);
	}

	static FanCurveEditor()
	{
		AffectsRender<FanCurveEditor>(PhasesProperty, MinSpeedProperty, MaxSpeedProperty, ChartMaxSpeedProperty, TemperatureProperty, SelectedIndexProperty, IsStartStopProperty, IsReadOnlyProperty);
		FocusableProperty.OverrideDefaultValue<FanCurveEditor>(defaultValue: true);
	}

	public FanCurveEditor()
	{
		ClipToBounds = true;
		Cursor = new Cursor(StandardCursorType.Hand);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		double width = double.IsInfinity(availableSize.Width) ? 520.0 : availableSize.Width;
		double height = double.IsInfinity(availableSize.Height) ? 280.0 : Math.Min(availableSize.Height, 320.0);
		return new Size(width, height);
	}

	private Rect plot => new Rect(PadLeft, PadTop, Math.Max(10.0, Bounds.Width - PadLeft - PadRight), Math.Max(10.0, Bounds.Height - PadTop - PadBottom));

	private int chartMax => Math.Max(ChartMaxSpeed, Math.Max(MaxSpeed, 1));

	private double xOf(double temperature)
	{
		Rect rect = plot;
		return rect.X + temperature / 100.0 * rect.Width;
	}

	private double yOf(double speed)
	{
		Rect rect = plot;
		return rect.Bottom - speed / chartMax * rect.Height;
	}

	private Point pointOf(RPMPhase[] phases, int i)
	{
		double temperature = ((phases.Length == 1) ? 50.0 : phases[i].Temperature);
		return new Point(xOf(temperature), yOf(phases[i].Speed));
	}

	public override void Render(DrawingContext context)
	{
		Rect rect = plot;
		context.FillRectangle(Background, new Rect(Bounds.Size), 6f);
		if (MinSpeed > 0)
		{
			context.FillRectangle(LimitBrush, new Rect(rect.X, yOf(MinSpeed), rect.Width, rect.Bottom - yOf(MinSpeed)));
		}
		for (int i = 0; i <= 10; i++)
		{
			double num = xOf(i * 10);
			context.DrawLine(GridPen, new Point(num, rect.Top), new Point(num, rect.Bottom));
			drawText(context, $"{i * 10}", new Point(num, rect.Bottom + 6.0), center: true);
		}
		int num2 = niceStep(chartMax);
		for (int j = 0; j <= chartMax; j += num2)
		{
			double num3 = yOf(j);
			context.DrawLine(GridPen, new Point(rect.Left, num3), new Point(rect.Right, num3));
			drawText(context, j.ToString(CultureInfo.InvariantCulture), new Point(rect.Left - 8.0, num3 - 7.0), center: false, rightAlign: true);
		}
		context.DrawLine(AxisPen, new Point(rect.Left, rect.Top), new Point(rect.Left, rect.Bottom));
		context.DrawLine(AxisPen, new Point(rect.Left, rect.Bottom), new Point(rect.Right, rect.Bottom));
		drawText(context, "°C", new Point(8.0, rect.Bottom + 6.0), center: false);
		drawText(context, "RPM", new Point(4.0, 0.0), center: false);
		RPMPhase[] phases = Phases;
		if (phases != null && phases.Length != 0)
		{
			if (phases.Length == 1)
			{
				double num4 = yOf(phases[0].Speed);
				context.DrawLine(CurvePen, new Point(rect.Left, num4), new Point(rect.Right, num4));
			}
			else
			{
				List<Point> list = new List<Point>();
				for (int k = 0; k < phases.Length; k++)
				{
					list.Add(pointOf(phases, k));
				}
				StreamGeometry streamGeometry = new StreamGeometry();
				using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
				{
					streamGeometryContext.BeginFigure(new Point(list[0].X, rect.Bottom), isFilled: true);
					foreach (Point item in list)
					{
						streamGeometryContext.LineTo(item);
					}
					streamGeometryContext.LineTo(new Point(list[^1].X, rect.Bottom));
					streamGeometryContext.EndFigure(isClosed: true);
				}
				context.DrawGeometry(FillBrush, null, streamGeometry);
				for (int l = 1; l < list.Count; l++)
				{
					context.DrawLine(CurvePen, list[l - 1], list[l]);
				}
				Point point = (IsStartStop ? new Point(list[0].X, yOf(0.0)) : new Point(xOf(0.0), yOf(MinSpeed)));
				context.DrawLine(ImplicitPen, point, list[0]);
				context.DrawLine(ImplicitPen, list[^1], new Point(xOf(100.0), yOf(MaxSpeed)));
			}
			if (!IsReadOnly)
			{
				for (int m = 0; m < phases.Length; m++)
				{
					Point center = pointOf(phases, m);
					bool flag = m == SelectedIndex;
					context.DrawEllipse(flag ? AccentBrush : Brushes.White, PointPen, center, flag ? (PointRadius + 1.5) : PointRadius, flag ? (PointRadius + 1.5) : PointRadius);
				}
				if (SelectedIndex >= 0 && SelectedIndex < phases.Length)
				{
					RPMPhase rPMPhase = phases[SelectedIndex];
					Point point2 = pointOf(phases, SelectedIndex);
					string text = ((phases.Length == 1) ? $"{rPMPhase.Speed} rpm" : $"{rPMPhase.Temperature}°C · {rPMPhase.Speed} rpm");
					double x = Math.Clamp(point2.X + 10.0, rect.Left, rect.Right - 110.0);
					double y = Math.Clamp(point2.Y - 26.0, rect.Top, rect.Bottom - 16.0);
					drawText(context, text, new Point(x, y), center: false, rightAlign: false, Brushes.White);
				}
			}
		}
		double? temperature = Temperature;
		if (temperature.HasValue && !double.IsNaN(temperature.Value))
		{
			double num5 = xOf(Math.Clamp(temperature.Value, 0.0, 100.0));
			context.DrawLine(TemperaturePen, new Point(num5, rect.Top), new Point(num5, rect.Bottom));
			drawText(context, $"{temperature.Value:0}°C", new Point(num5 + 4.0, rect.Top), center: false, rightAlign: false, TemperatureBrush);
		}
	}

	private static int niceStep(int max)
	{
		int[] array = new int[6] { 100, 200, 250, 400, 500, 1000 };
		foreach (int num in array)
		{
			if (max / num <= 6)
			{
				return num;
			}
		}
		return 1000;
	}

	private static void drawText(DrawingContext context, string text, Point origin, bool center, bool rightAlign = false, IBrush brush = null)
	{
		FormattedText formattedText = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11.0, brush ?? LabelBrush);
		double x = origin.X;
		if (center)
		{
			x -= formattedText.Width / 2.0;
		}
		else if (rightAlign)
		{
			x -= formattedText.Width;
		}
		context.DrawText(formattedText, new Point(x, origin.Y));
	}

	private int hitTest(Point p)
	{
		RPMPhase[] phases = Phases;
		if (phases == null)
		{
			return -1;
		}
		int result = -1;
		double num = 14.0;
		for (int i = 0; i < phases.Length; i++)
		{
			Point point = pointOf(phases, i);
			double num2 = Math.Sqrt(Math.Pow(point.X - p.X, 2.0) + Math.Pow(point.Y - p.Y, 2.0));
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);
		if (IsReadOnly || !IsEnabled)
		{
			return;
		}
		int num = hitTest(e.GetPosition(this));
		if (num >= 0)
		{
			SetCurrentValue(SelectedIndexProperty, num);
			dragIndex = num;
			e.Pointer.Capture(this);
			e.Handled = true;
		}
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);
		if (dragIndex < 0 || Phases == null)
		{
			return;
		}
		Point position = e.GetPosition(this);
		Rect rect = plot;
		int temperature = (int)Math.Round((position.X - rect.X) / rect.Width * 100.0);
		int speed = (int)Math.Round((rect.Bottom - position.Y) / rect.Height * chartMax);
		MovePoint(dragIndex, temperature, speed);
		e.Handled = true;
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		if (dragIndex >= 0)
		{
			dragIndex = -1;
			e.Pointer.Capture(null);
			e.Handled = true;
		}
	}

	public void MovePoint(int index, int temperature, int speed)
	{
		RPMPhase[] value = CurveMath.Move(Phases, index, temperature, speed, MinSpeed, MaxSpeed);
		SetCurrentValue(PhasesProperty, value);
	}
}
