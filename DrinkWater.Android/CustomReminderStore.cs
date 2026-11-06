using Android.Content;
using System.Text.Json;

namespace DrinkWater.Android;

public sealed class CustomReminderStore(Context context)
{
    private readonly ISharedPreferences _prefs = context.GetSharedPreferences(ReminderScheduler.Preferences, FileCreationMode.Private)!;
    public const string StorageKey = "custom_reminders_json";

    public List<CustomReminder> Load()
    {
        var reminders = JsonSerializer.Deserialize<List<CustomReminder>>(_prefs.GetString(StorageKey, "[]")!)
            ?? throw new InvalidDataException("Pripomienky sa nepodarilo nacitat.");
        Validate(reminders);
        return reminders;
    }

    public void Save(List<CustomReminder> reminders)
    {
        Validate(reminders);
        if (!_prefs.Edit()!.PutString(StorageKey, JsonSerializer.Serialize(reminders))!.Commit())
            throw new IOException("Pripomienky sa nepodarilo ulozit.");
    }

    public static void Validate(List<CustomReminder> reminders)
    {
        if (reminders is null || reminders.Count > 100 || reminders.Any(r => r is null) ||
            reminders.Select(r => r.Id).Distinct().Count() != reminders.Count)
            throw new InvalidDataException("Neplatny zoznam pripomienok (maximum 100).");
        foreach (var reminder in reminders) reminder.Validate();
    }
}
