using Android.App;
using Android.Content;
using Android.OS;

namespace DrinkWater.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public const string ChannelId = "drinkwater.reminders";
    public const string ActionReminder = "drinkwater.action.REMINDER";
    public const string ExtraTime = "extra_time";
    public const string ExtraDoseMl = "extra_dose_ml";
    public const string ExtraMessage = "extra_message";
    public const string ActionDrink = "drinkwater.action.DRINK";
    private const string TokenKey = "notification_drink_token";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        var prefs = context.GetSharedPreferences(ReminderScheduler.Preferences, FileCreationMode.Private)!;
        if (intent.Action == ActionDrink)
        {
            var token = intent.GetStringExtra(TokenKey);
            // A notification can record one drink, even if Android delivers its action twice.
            if (string.IsNullOrEmpty(token) || token != prefs.GetString(TokenKey, null)) return;
            prefs.Edit()!.Remove(TokenKey)!.Commit();
            var drinkState = AppStateStorage.Load(prefs);
            drinkState.AddConsumed(250);
            ReminderScheduler.Refresh(context, drinkState);
            AppStateStorage.Save(prefs, drinkState);
            ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.Cancel(100);
            global::Android.Widget.Toast.MakeText(context, "Zapisanych 250 ml.", global::Android.Widget.ToastLength.Short)?.Show();
            return;
        }
        if (intent.Action != ActionReminder) return;
        var state = AppStateStorage.Load(prefs);
        var now = DateTime.Now;
        state.ResetForToday();
        var due = state.NextReminderUtc > 0 && DateTimeOffset.Now.ToUnixTimeMilliseconds() >= state.NextReminderUtc;
        var inWindow = TimeSpan.TryParse(state.StartTime, out var start) &&
            TimeSpan.TryParse(state.EndTime, out var end) && now.TimeOfDay >= start && now.TimeOfDay <= end;
        var eveningDueToday = state.EveningReminder && state.NextReminderUtc > 0 &&
            EveningReminderPlan.IsDue(now, DateTimeOffset.FromUnixTimeMilliseconds(state.NextReminderUtc).LocalDateTime,
                state.LastEveningReminderDate);
        var show = state.RemindersEnabled && due && (state.EveningReminder ? eveningDueToday : inWindow) &&
            state.ConsumedMl < state.GoalMl && ReminderScheduler.CanNotify(context);
        if (show && state.EveningReminder) state.LastEveningReminderDate = now.ToString("yyyy-MM-dd");
        ReminderScheduler.Refresh(context, state);
        AppStateStorage.Save(prefs, state);
        if (!show) return;
        var actionToken = Guid.NewGuid().ToString("N");
        prefs.Edit()!.PutString(TokenKey, actionToken)!.Apply();
        var drinkIntent = new Intent(context, typeof(ReminderReceiver))
            .SetAction(ActionDrink)
            .PutExtra(TokenKey, actionToken);
        var drinkPending = PendingIntent.GetBroadcast(context, 250, drinkIntent,
            PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
        var openIntent = new Intent(context, typeof(MainActivity))
            .SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var openPending = PendingIntent.GetActivity(context, 100, openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var message = state.EveningReminder
            ? $"Dnes mas zaznamenanych {state.ConsumedMl} ml z ciela {state.GoalMl} ml. Do ciela zostava {Math.Max(0, state.GoalMl - state.ConsumedMl)} ml."
            : $"Dnes mas zaznamenanych {state.ConsumedMl} ml z ciela {state.GoalMl} ml. Cas na kratku prestavku?";
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "DrinkWater pripomienky", NotificationImportance.Default));

        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(context, ChannelId)
            : new Notification.Builder(context);

        var notification = builder
            .SetContentTitle(state.EveningReminder ? "Vecerny prehlad pitia" : "Cas na vodu")
            .SetContentText(message)
            .SetSmallIcon(Resource.Drawable.ic_water_notification)
            .SetContentIntent(openPending)
            .SetAutoCancel(true)
            .AddAction(new Notification.Action.Builder(
                global::Android.Graphics.Drawables.Icon.CreateWithResource(context, Resource.Drawable.ic_water_notification),
                "Vypite 250 ml", drinkPending).Build())
            .Build();

        manager?.Notify(100, notification);
    }
}
