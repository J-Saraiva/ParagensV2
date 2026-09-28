using Microsoft.Extensions.Logging;

namespace ParagensV2;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			var ex = args.ExceptionObject as Exception;
			var msg = $"[CRITICAL UNHANDLED EXCEPTION] {ex?.ToString() ?? "Unknown exception"}";
			System.Diagnostics.Debug.WriteLine(msg);
#if ANDROID
			Android.Util.Log.Error("ParagensV2_Crash", msg);
#endif
		};

		TaskScheduler.UnobservedTaskException += (sender, args) =>
		{
			var msg = $"[UNOBSERVED TASK EXCEPTION] {args.Exception}";
			System.Diagnostics.Debug.WriteLine(msg);
#if ANDROID
			Android.Util.Log.Error("ParagensV2_Crash", msg);
#endif
			args.SetObserved();
		};

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

