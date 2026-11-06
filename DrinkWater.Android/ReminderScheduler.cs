using Android.App;
using Android.Content;
using Android.Content.PM;

namespace DrinkWater.Android;

public static class ReminderScheduler
{
    public const string Preferences = "drinkwater.preferences";
    public static bool CanNotify(Context context) =>
        (!OperatingSystem.IsAndroidVersionAtLeast(33) ||
         context.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") == Permission.Granted) &&
        ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.AreNotificationsEnabled() == true;

    public static void Refresh(Context context, AppState state)
    {
        var alarms = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarms is null) return;
        // Cancel the old multi-alarm schedule as well as the current single alarm.
        for (var i = 0; i < 16; i++)
        {
            var old = PendingIntent.GetBroadcast(context, i,
                new Intent(context, typeof(ReminderReceiver)).SetAction(ReminderReceiver.ActionReminder),
                PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
            if (old is null) continue;
            alarms.Cancel(old);
            old.Cancel();
        }
        var previousReminderUtc = state.NextReminderUtc;
        state.ActiveReminderSchedule = [];
        state.NextReminderUtc = 0;
        if (!state.RemindersEnabled || !CanNotify(context))
        {
            state.ActiveReminderSummary = state.RemindersEnabled
                ? "Povol upozornenia v nastaveniach telefonu."
                : "Pripomienky su vypnute.";
            ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.Cancel(100);
            return;
        }
        var now = DateTime.Now;
        if (state.EveningReminder)
        {
            var nextEvening = EveningReminderPlan.Next(now, state.EveningReminderTime,
                state.ConsumedMl >= state.GoalMl, state.LastEveningReminderDate);
            if (nextEvening is null)
            {
                state.ActiveReminderSummary = "Nastav cas vecerneho upozornenia vo formate HH:mm.";
                return;
            }
            var evening = nextEvening.Value;
            // Keep a same-day alarm pending if Android delayed its delivery while the app was opened.
            if (previousReminderUtc > 0 && state.ConsumedMl < state.GoalMl)
            {
                var previous = DateTimeOffset.FromUnixTimeMilliseconds(previousReminderUtc).LocalDateTime;
                if (previous.ToString("HH:mm") == state.EveningReminderTime &&
                    EveningReminderPlan.IsDue(now, previous, state.LastEveningReminderDate))
                    evening = previous;
            }
            if (state.ConsumedMl >= state.GoalMl)
                ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.Cancel(100);
            state.NextReminderUtc = new DateTimeOffset(evening).ToUnixTimeMilliseconds();
            state.ActiveReminderSchedule = [evening.ToString("HH:mm")];
            state.ActiveReminderSummary = $"Vecerna kontrola {evening:dd.MM HH:mm}. Pri splnenom cieli sa nezobrazi. Android moze cas posunut.";
            Schedule(context, alarms, state.NextReminderUtc);
            return;
        }
        var start = TimeSpan.TryParse(state.StartTime, out var s) ? s : TimeSpan.FromHours(8);
        var end = TimeSpan.TryParse(state.EndTime, out var e) ? e : TimeSpan.FromHours(20);
        if (start < TimeSpan.Zero || end >= TimeSpan.FromDays(1) || end <= start)
        {
            state.ActiveReminderSummary = "Nastav koniec rezimu neskor ako zaciatok v rovnakom dni.";
            return;
        }
        var next = now.AddMinutes(Math.Max(45, (end - start).TotalMinutes / Math.Clamp(state.RemindersPerDay, 1, 16)));
        if (now < now.Date.Add(start)) next = now.Date.Add(start);
        if (state.ConsumedMl >= state.GoalMl || next > now.Date.Add(end))
        {
            next = now.Date.AddDays(1).Add(start);
            ((NotificationManager?)context.GetSystemService(Context.NotificationService))?.Cancel(100);
        }
        state.NextReminderUtc = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        state.ActiveReminderSchedule = [next.ToString("HH:mm")];
        state.ActiveReminderSummary = $"Dalsia kontrola {next:dd.MM HH:mm}. Po zapise vody sa odlozi; po splneni ciela az na dalsi den. Android moze cas posunut.";
        Schedule(context, alarms, state.NextReminderUtc);
    }

    private static void Schedule(Context context, AlarmManager alarms, long nextReminderUtc)
    {
        var pending = PendingIntent.GetBroadcast(context, 0,
            new Intent(context, typeof(ReminderReceiver)).SetAction(ReminderReceiver.ActionReminder),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (pending is not null)
            alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, nextReminderUtc, pending);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged })]
public class RestoreRemindersReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        var prefs = context.GetSharedPreferences(ReminderScheduler.Preferences, FileCreationMode.Private)!;
        var state = AppStateStorage.Load(prefs);
        state.ResetForToday();
        ReminderScheduler.Refresh(context, state);
        AppStateStorage.Save(prefs, state);
        CustomReminderScheduler.TryRefresh(context, preservePending: false);
    }
}
