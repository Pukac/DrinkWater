using Android.App;
using Android.Content;
using Android.OS;
using System.Text.Json;

namespace DrinkWater.Android;

public static class CustomReminderScheduler
{
    private const string DueKey = "custom_reminders_due";
    private const string Action = "drinkwater.action.CUSTOM_REMINDER";
    private const string Channel = "drinkwater.custom_reminders";

    private static ISharedPreferences Prefs(Context context) =>
        context.GetSharedPreferences(ReminderScheduler.Preferences, FileCreationMode.Private)!;

    public static void TryRefresh(Context context, bool preservePending = true)
    {
        try { Refresh(context, preservePending); }
        catch (Exception ex) when (ex is IOException or JsonException or Java.Lang.Exception)
        {
            global::Android.Util.Log.Error("DrinkWater", "Vlastne pripomienky sa nepodarilo obnovit: " + ex.GetType().Name);
        }
    }

    public static void Refresh(Context context, bool preservePending = true)
    {
        var reminders = new CustomReminderStore(context).Load();
        var prefs = Prefs(context);
        var previous = JsonSerializer.Deserialize<Dictionary<string, long>>(prefs.GetString(DueKey, "{}")!) ?? [];
        var signatures = JsonSerializer.Deserialize<Dictionary<string, string>>(prefs.GetString("custom_reminder_signatures", "{}")!) ?? [];
        var currentSignatures = reminders.ToDictionary(r => r.Id, r => JsonSerializer.Serialize(r));
        var due = new Dictionary<string, long>();
        var now = DateTime.Now;
        foreach (var reminder in reminders)
        {
            var next = CustomReminderPlan.Next(reminder, now);
            if (next.HasValue) due[reminder.Id] = new DateTimeOffset(next.Value).ToUnixTimeMilliseconds();
            if (preservePending && reminder.Enabled && signatures.GetValueOrDefault(reminder.Id) == currentSignatures[reminder.Id] &&
                previous.TryGetValue(reminder.Id, out var pending) &&
                DateTimeOffset.FromUnixTimeMilliseconds(pending).LocalDateTime.Date >= now.Date)
                due[reminder.Id] = pending;
        }
        if (!prefs.Edit()!.PutString("custom_reminder_signatures", JsonSerializer.Serialize(currentSignatures))!.Commit())
            throw new IOException("Plan sa nepodarilo ulozit.");
        SaveAndSchedule(context, due);
        // Editing or disabling a reminder also removes its previous notification.
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        foreach (var reminder in reminders.Where(r => !r.Enabled || signatures.GetValueOrDefault(r.Id) != currentSignatures[r.Id]))
            manager?.Cancel(reminder.Id, 0);
    }

    private static void SaveAndSchedule(Context context, Dictionary<string, long> due)
    {
        var prefs = Prefs(context);
        var generation = Guid.NewGuid().ToString("N");
        if (!prefs.Edit()!.PutString(DueKey, JsonSerializer.Serialize(due))!
            .PutString("custom_reminder_generation", generation)!.Commit())
            throw new IOException("Plan pripomienok sa nepodarilo ulozit.");
        var alarms = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        var pending = PendingIntent.GetBroadcast(context, 2000,
            new Intent(context, typeof(CustomReminderReceiver)).SetAction(Action).PutExtra("generation", generation),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (alarms is null || pending is null) return;
        alarms.Cancel(pending);
        if (due.Count > 0 && ReminderScheduler.CanNotify(context))
            alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, due.Values.Min(), pending);
    }

    internal static void Receive(Context context, Intent intent)
    {
        var prefs = Prefs(context);
        if (intent.Action != Action || intent.GetStringExtra("generation") != prefs.GetString("custom_reminder_generation", "")) return;
        var reminders = new CustomReminderStore(context).Load();
        var due = JsonSerializer.Deserialize<Dictionary<string, long>>(prefs.GetString(DueKey, "{}")!) ?? [];
        var now = DateTimeOffset.Now;
        var ready = reminders.Where(r => r.Enabled && due.TryGetValue(r.Id, out var time) && time <= now.ToUnixTimeMilliseconds()).ToList();
        foreach (var reminder in ready)
        {
            var next = CustomReminderPlan.Next(reminder, now.LocalDateTime);
            if (next.HasValue) due[reminder.Id] = new DateTimeOffset(next.Value).ToUnixTimeMilliseconds();
            else due.Remove(reminder.Id);
        }
        // Persist advancement before showing notifications, so duplicate broadcasts cannot show them twice.
        var previous = JsonSerializer.Deserialize<Dictionary<string, long>>(prefs.GetString(DueKey, "{}")!) ?? [];
        SaveAndSchedule(context, due);
        if (!ReminderScheduler.CanNotify(context)) return;
        foreach (var reminder in ready)
        {
            // Do not present yesterday's missed reminder as a current one.
            if (DateTimeOffset.FromUnixTimeMilliseconds(previous[reminder.Id]).LocalDateTime.Date != now.LocalDateTime.Date) continue;
            Show(context, reminder);
        }
    }

    private static void Show(Context context, CustomReminder reminder)
    {
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            manager?.CreateNotificationChannel(new NotificationChannel(Channel, "Moje pripomienky", NotificationImportance.Default));
        var open = PendingIntent.GetActivity(context, 2000,
            new Intent(context, typeof(CustomRemindersActivity)).SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
#pragma warning disable CA1422 // API 24-25 require the legacy constructor.
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(context, Channel) : new Notification.Builder(context);
#pragma warning restore CA1422
        manager?.Notify(reminder.Id, 0, builder.SetContentTitle(reminder.Title)
            .SetContentText(string.IsNullOrWhiteSpace(reminder.Note) ? "Tvoja nastavena pripomienka." : reminder.Note)
            .SetStyle(new Notification.BigTextStyle().BigText(reminder.Note))
            .SetSmallIcon(Resource.Drawable.ic_water_notification).SetContentIntent(open).SetAutoCancel(true).Build());
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class CustomReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;
        try { CustomReminderScheduler.Receive(context, intent); }
        catch (Exception ex) when (ex is IOException or JsonException or Java.Lang.Exception)
        {
            global::Android.Util.Log.Error("DrinkWater", "Vlastne pripomienky sa nepodarilo spracovat: " + ex.GetType().Name);
        }
    }
}
