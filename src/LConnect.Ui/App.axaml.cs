using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LConnectUi.Services;
using LConnectUi.ViewModels;
using LConnectUi.Views;

namespace LConnectUi;

public partial class App : Application
{
	private MainWindow mainWindow;

	private bool quitting;

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// Headless tests have no desktop lifetime; they create windows and call AppRuntime themselves.
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			AppRuntime.Start();
			mainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(),
				Icon = AppIcon.Create()
			};
			AppRuntime.MainWindow = mainWindow;
			mainWindow.Closing += delegate(object sender, WindowClosingEventArgs e)
			{
				if (!quitting)
				{
					if (UiSettings.Current.CloseToTray && TrayIcon.GetIcons(this)?.Count > 0)
					{
						e.Cancel = true;
						mainWindow.Hide();
						(mainWindow.DataContext as MainWindowViewModel)?.CurrentPage?.Deactivate();
					}
					else
					{
						quit();
					}
				}
			};
			setupTray();
			if (AppRuntime.SingleInstance != null)
			{
				AppRuntime.SingleInstance.ShowRequested += delegate
				{
					Dispatcher.UIThread.Post(showMainWindow);
				};
			}
			if (!AppRuntime.StartHidden)
			{
				desktop.MainWindow = mainWindow;
			}
			else
			{
				// Started into the tray: no page polling until the window is shown.
				(mainWindow.DataContext as MainWindowViewModel)?.CurrentPage?.Deactivate();
			}
		}
		base.OnFrameworkInitializationCompleted();
	}

	private void setupTray()
	{
		try
		{
			NativeMenuItem nativeMenuItem = new NativeMenuItem(Loc.Get("I18n.TrayText.Open", "Show"));
			nativeMenuItem.Click += delegate
			{
				showMainWindow();
			};
			NativeMenuItem nativeMenuItem2 = new NativeMenuItem(Loc.Get("I18n.TrayText.Quit", "Quit"));
			nativeMenuItem2.Click += delegate
			{
				quit();
			};
			TrayIcon trayIcon = new TrayIcon
			{
				Icon = AppIcon.Create(),
				ToolTipText = (OperatingSystem.IsLinux() ? null : "L-Connect"),
				Menu = new NativeMenu { nativeMenuItem, nativeMenuItem2 },
				IsVisible = true
			};
			trayIcon.Clicked += delegate
			{
				showMainWindow();
			};
			TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
			if (OperatingSystem.IsLinux())
			{
				fixLinuxTrayStatus(trayIcon);
			}
		}
		catch (Exception ex)
		{
			LConnect3cs.Utils.Logger.Instance.Warn("Tray icon unavailable: " + ex.Message);
		}
	}

	// Avalonia 11.3 copies ToolTipText into the StatusNotifierItem Status too (fixed in Avalonia 12), and hosts such
	// as Omarchy's quickshell hide any item whose Status is not "Active", so the icon never appeared. Without a
	// tooltip Avalonia leaves these properties alone, also when the host re-registers the item, so they are set once
	// here on its D-Bus object. Remove when moving to Avalonia 12.
	private static void fixLinuxTrayStatus(TrayIcon trayIcon)
	{
		const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		try
		{
			object impl = typeof(TrayIcon).GetField("_impl", flags)?.GetValue(trayIcon);
			if (impl == null)
			{
				return;
			}
			object item = impl.GetType().GetField("_statusNotifierItemDbusObj", flags)?.GetValue(impl);
			if (item == null)
			{
				LConnect3cs.Utils.Logger.Instance.Warn("Tray status fix not applied: " + impl.GetType().FullName + " has no StatusNotifierItem object.");
				return;
			}
			Type type = item.GetType();
			type.GetProperty("Id")?.SetValue(item, "L-Connect");
			type.GetProperty("Title")?.SetValue(item, "L-Connect");
			type.GetProperty("Category")?.SetValue(item, "ApplicationStatus");
			type.GetProperty("Status")?.SetValue(item, "Active");
		}
		catch (Exception ex)
		{
			LConnect3cs.Utils.Logger.Instance.Warn("Tray status fix failed: " + ex.Message);
		}
	}

	private void showMainWindow()
	{
		if (mainWindow == null)
		{
			return;
		}
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime && classicDesktopStyleApplicationLifetime.MainWindow == null)
		{
			classicDesktopStyleApplicationLifetime.MainWindow = mainWindow;
		}
		mainWindow.Show();
		if (mainWindow.WindowState == WindowState.Minimized)
		{
			mainWindow.WindowState = WindowState.Normal;
		}
		mainWindow.Activate();
		(mainWindow.DataContext as MainWindowViewModel)?.CurrentPage?.Activate();
	}

	private void quit()
	{
		quitting = true;
		SensorService.Instance.Stop();
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime)
		{
			classicDesktopStyleApplicationLifetime.Shutdown();
		}
	}
}
