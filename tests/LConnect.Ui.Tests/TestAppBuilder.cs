using Avalonia;
using Avalonia.Headless;
using LConnect.Ui.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LConnect.Ui.Tests;

// Headless Avalonia with real Skia rendering so pages can be captured as PNGs.
public class TestAppBuilder
{
	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<LConnectUi.App>().UseSkia().WithInterFont()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions
			{
				UseHeadlessDrawing = false
			});
	}
}
