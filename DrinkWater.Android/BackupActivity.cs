using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DrinkWater.Android;

[Activity(Label = "Export a zaloha", Exported = false)]
public class BackupActivity : Activity
{
    private const int Csv = 701, Json = 702, Import = 703;
    private ISharedPreferences Prefs => GetSharedPreferences(ReminderScheduler.Preferences, FileCreationMode.Private)!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding, padding, padding);
        layout.AddView(new TextView(this) {
            Text = "Tvoje data\n\nCSV obsahuje denne sucty a jednotlive davky vody. JSON zaloha obsahuje historiu, manualny rezim a vlastne pripomienky vratane ich poznamok, bez zdravotneho dotaznika. Subory nie su sifrovane.\n\nObnovenie nahradi historiu, rezim aj vlastne pripomienky. Vsetky pripomienky potom zapni znova. Stara zaloha bez vlastnych pripomienok ich zoznam vymaze.",
            TextSize = 18
        });
        AddButton(layout, "Export historie CSV", () => Pick(Csv, "text/csv", "csv"));
        AddButton(layout, "Ulozit zalohu JSON", () => Pick(Json, "application/json", "json"));
        AddButton(layout, "Obnovit zalohu", () => Pick(Import, "application/json", "json"));
        AddButton(layout, "Spat", Finish);
        var scroll = new ScrollView(this);
        scroll.AddView(layout);
        SetContentView(scroll);
    }

    private void AddButton(LinearLayout layout, string label, Action action)
    {
        var button = new Button(this) { Text = label };
        button.Click += (_, _) => action();
        layout.AddView(button);
    }

    private void Pick(int code, string mime, string extension)
    {
        var intent = new Intent(code == Import ? Intent.ActionOpenDocument : Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mime);
        if (code != Import) intent.PutExtra(Intent.ExtraTitle, $"DrinkWater-{DateTime.Now:yyyy-MM-dd-HHmmss}.{extension}");
        try { StartActivityForResult(intent, code); }
        catch (ActivityNotFoundException) { ShowMessage("Vyber suborov nie je dostupny."); }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data?.Data is null) return;
        try
        {
            if (requestCode == Import)
            {
                using var stream = ContentResolver!.OpenInputStream(data.Data) ?? throw new IOException();
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > 4 * 1024 * 1024) throw new InvalidDataException("Zaloha je prilis velka (max. 4 MB).");
                    buffer.Write(chunk, 0, read);
                }
                buffer.Position = 0;
                using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var backup = JsonSerializer.Deserialize<WaterBackup>(reader.ReadToEnd()) ?? throw new InvalidDataException("Prazdna zaloha.");
                backup.Validate();
                var dialog = new AlertDialog.Builder(this);
                dialog.SetTitle("Obnovit zalohu?");
                dialog.SetMessage($"Zaloha obsahuje {backup.History.Count} dni, ciel {backup.GoalMl} ml a {backup.CustomReminders.Count} vlastnych pripomienok. Nahradi aktualnu historiu, rezim aj vlastne pripomienky. Vsetky pripomienky budu vypnute. Najprv si mozes ulozit aktualnu zalohu.");
                dialog.SetNegativeButton("Zrusit", (_, _) => { });
                dialog.SetPositiveButton("Obnovit", (_, _) => Restore(backup));
                dialog.Show();
                return;
            }
            if (requestCode != Csv && requestCode != Json) return;
            var state = AppStateStorage.Load(Prefs);
            state.ResetForToday();
            var backupOut = WaterBackup.From(state);
            if (requestCode == Json) backupOut.CustomReminders = new CustomReminderStore(this).Load();
            var text = requestCode == Json
                ? JsonSerializer.Serialize(backupOut, new JsonSerializerOptions { WriteIndented = true })
                : backupOut.ToCsv();
            using var output = ContentResolver!.OpenOutputStream(data.Data, "wt") ?? throw new IOException();
            using (var writer = new StreamWriter(output, new UTF8Encoding(true))) writer.Write(text);
            ShowMessage("Subor ulozeny.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or Java.Lang.Exception or InvalidOperationException)
        {
            ShowMessage("Subor sa nepodarilo spracovat. Skontroluj format a pristup k suboru.");
        }
    }

    private void Restore(WaterBackup backup)
    {
        try
        {
            var state = AppStateStorage.Load(Prefs);
            state.History = backup.History;
            state.GoalMl = backup.GoalMl;
            state.RemindersPerDay = backup.RemindersPerDay;
            state.StartTime = backup.StartTime;
            state.EndTime = backup.EndTime;
            state.EveningReminderTime = backup.EveningReminderTime;
            state.LastTrackedDate = DateTime.Today.ToString("yyyy-MM-dd");
            state.ConsumedMl = state.History.FirstOrDefault(x => x.Date == state.LastTrackedDate)?.ConsumedMl ?? 0;
            state.RemindersEnabled = false;
            state.NextReminderUtc = 0;
            state.ActiveReminderSchedule = [];
            state.ActiveReminderSummary = "Zaloha obnovena. Pripomienky su vypnute.";
            var previousCustom = new CustomReminderStore(this).Load();
            foreach (var reminder in backup.CustomReminders) reminder.Enabled = false;
            // Commit both collections together before changing alarms.
            if (!Prefs.Edit()!.PutString("app_state_json", JsonSerializer.Serialize(state))!
                .PutString(CustomReminderStore.StorageKey, JsonSerializer.Serialize(backup.CustomReminders))!
                .Remove("notification_drink_token")!.Commit())
                throw new IOException();
            ReminderScheduler.Refresh(this, state);
            CustomReminderScheduler.Refresh(this);
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            foreach (var reminder in previousCustom) manager?.Cancel(reminder.Id, 0);
            ShowMessage("Historia a rezim obnovene. Pripomienky su vypnute.");
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.Exception)
        {
            ShowMessage("Obnovenie sa nepodarilo dokoncit.");
        }
    }

    private void ShowMessage(string message) => Toast.MakeText(this, message, ToastLength.Long)?.Show();
}

public sealed class WaterBackup
{
    public string Format { get; set; } = "DrinkWater";
    public int Version { get; set; } = 2;
    public List<CustomReminder> CustomReminders { get; set; } = [];
    public int GoalMl { get; set; }
    public int RemindersPerDay { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string EveningReminderTime { get; set; } = "20:00";
    public List<DailyHydrationRecord> History { get; set; } = [];

    public static WaterBackup From(AppState state) => new() {
        GoalMl = state.GoalMl, RemindersPerDay = state.RemindersPerDay,
        StartTime = state.StartTime, EndTime = state.EndTime, History = state.History,
        EveningReminderTime = state.EveningReminderTime
    };

    public void Validate()
    {
        bool Time(string value, out TimeSpan time) => TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time);
        if (Format != "DrinkWater" || Version is < 1 or > 2 || GoalMl < 500 || GoalMl > 5000 ||
            RemindersPerDay < 1 || RemindersPerDay > 16 || !Time(StartTime, out var start) ||
            !Time(EndTime, out var end) || end <= start || !Time(EveningReminderTime, out _) || History is null || History.Count > 120)
            throw new InvalidDataException("Neplatna zaloha.");
        if (Version == 1) CustomReminders = [];
        CustomReminderStore.Validate(CustomReminders);
        var dates = new HashSet<string>();
        foreach (var day in History)
        {
            if (day is null || !DateTime.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) || date > DateTime.Today || !dates.Add(day.Date) ||
                day.ConsumedMl < 0 || day.ConsumedMl > 1000000 || day.GoalMl < 500 || day.GoalMl > 5000 ||
                day.Events is null || day.Events.Count > 5000)
                throw new InvalidDataException("Neplatny den.");
            if (day.Events.Any(e => e is null || !Time(e.Time, out _) || e.AmountMl <= 0 || e.AmountMl > 1000000) ||
                day.Events.Sum(e => (long)e.AmountMl) > day.ConsumedMl)
                throw new InvalidDataException("Neplatne davky.");
        }
    }

    public string ToCsv()
    {
        var text = new StringBuilder("typ;datum;cas;objem_ml;ciel_ml\r\n");
        foreach (var day in History.OrderBy(x => x.Date))
        {
            text.AppendLine($"den;{day.Date};;{day.ConsumedMl};{day.GoalMl}");
            foreach (var e in day.Events)
                text.AppendLine($"davka;{day.Date};{e.Time};{e.AmountMl};");
        }
        return text.ToString();
    }
}
