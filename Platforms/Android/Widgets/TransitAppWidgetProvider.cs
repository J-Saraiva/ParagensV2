using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using ParagensV2.Services;

namespace ParagensV2.Platforms.Android.Widgets
{
    [BroadcastReceiver(Name = "ParagensV2.widgets.TransitAppWidgetProvider", Label = "Próximo Autocarro", Exported = true)]
    [IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate, ActionRefreshWidget })]
    [MetaData("android.appwidget.provider", Resource = "@xml/widget_info")]
    public class TransitAppWidgetProvider : AppWidgetProvider
    {
        public const string ActionRefreshWidget = "com.companyname.ParagensV2.ACTION_REFRESH_WIDGET";
        private static readonly TransitDataService TransitService = new();

        public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
        {
            if (context == null || appWidgetManager == null || appWidgetIds == null)
                return;

            foreach (var widgetId in appWidgetIds)
            {
                UpdateSingleWidget(context, appWidgetManager, widgetId);
            }
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            base.OnReceive(context, intent);

            if (intent?.Action == ActionRefreshWidget && context != null)
            {
                var appWidgetManager = AppWidgetManager.GetInstance(context);
                if (appWidgetManager != null)
                {
                    var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(TransitAppWidgetProvider)));
                    var widgetIds = appWidgetManager.GetAppWidgetIds(component);
                    if (widgetIds != null)
                    {
                        foreach (var id in widgetIds)
                        {
                            UpdateSingleWidget(context, appWidgetManager, id);
                        }
                    }
                }
            }
        }

        private static async void UpdateSingleWidget(Context context, AppWidgetManager appWidgetManager, int widgetId)
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_layout);

            try
            {
                var nextArrival = await TransitService.GetNextArrivalAsync();

                if (nextArrival != null)
                {
                    views.SetTextViewText(Resource.Id.widgetTxtLine, nextArrival.LineNumber);
                    views.SetTextViewText(Resource.Id.widgetTxtDestination, nextArrival.Destination);
                    
                    var rtSuffix = nextArrival.IsRealTime ? "(Tempo Real)" : "(Horário)";
                    views.SetTextViewText(Resource.Id.widgetTxtEta, $"{nextArrival.NextEtaMinutes} min {rtSuffix}");
                    views.SetTextViewText(Resource.Id.widgetTxtUpdated, $"Atualizado às {DateTime.Now:HH:mm:ss}");
                }
                else
                {
                    views.SetTextViewText(Resource.Id.widgetTxtDestination, "Sem chegadas");
                    views.SetTextViewText(Resource.Id.widgetTxtEta, "--");
                }
            }
            catch
            {
                views.SetTextViewText(Resource.Id.widgetTxtDestination, "Erro ao carregar");
            }

            // Version-safe PendingIntent flags
            var flags = PendingIntentFlags.UpdateCurrent;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
            {
#pragma warning disable CA1416
                flags |= PendingIntentFlags.Immutable;
#pragma warning restore CA1416
            }

            // PendingIntent 1: Clicking Refresh button triggers ActionRefreshWidget
            var refreshIntent = new Intent(context, typeof(TransitAppWidgetProvider));
            refreshIntent.SetAction(ActionRefreshWidget);
            var refreshPendingIntent = PendingIntent.GetBroadcast(
                context,
                0,
                refreshIntent,
                flags
            );
            views.SetOnClickPendingIntent(Resource.Id.widgetBtnRefresh, refreshPendingIntent);

            // PendingIntent 2: Clicking widget container opens MainActivity
            var launchAppIntent = new Intent(context, typeof(MainActivity));
            var launchPendingIntent = PendingIntent.GetActivity(
                context,
                0,
                launchAppIntent,
                flags
            );
            views.SetOnClickPendingIntent(Resource.Id.widgetContainer, launchPendingIntent);

            // Commit updates to the widget
            appWidgetManager.UpdateAppWidget(widgetId, views);
        }
    }
}

