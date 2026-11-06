using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using System.Text.Json;

namespace DrinkWater.Android;

[Activity(Label = "Moje pripomienky", Exported = false)]
public sealed class CustomRemindersActivity : Activity
{
    private static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    private static readonly string[] DayNames = ["Po", "Ut", "St", "Stv", "Pi", "So", "Ne"];
    private CustomReminderStore Store => new(this);
    private LinearLayout _list = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        _list.SetPadding(padding, padding, padding, padding);
        var scroll = new ScrollView(this);
        scroll.AddView(_list);
        SetContentView(scroll);
    }

    protected override void OnResume()
    {
        base.OnResume();
        Guard(() => CustomReminderScheduler.Refresh(this));
        Render();
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or JsonException or Java.Lang.Exception)
        {
            Toast.MakeText(this, ex is InvalidDataException ? ex.Message : "Operacia sa nepodarila. Skus ju zopakovat.", ToastLength.Long)?.Show();
        }
    }

    private void Render() => Guard(() =>
    {
        _list.RemoveAllViews();
        _list.AddView(new TextView(this) { Text = "Moje pripomienky", TextSize = 26 });
        _list.AddView(new TextView(this) { Text = "Vlastne upozornenia funguju nezavisle od pitneho rezimu. Android ich moze oneskorit; nejde o presny budik.\n" });
        if (!ReminderScheduler.CanNotify(this))
        {
            _list.AddView(new TextView(this) { Text = "Upozornenia su v telefone zablokovane." });
            Button(_list, "Povolit upozornenia", EnableNotifications);
        }
        Button(_list, "Pridat pripomienku", () => Edit(new CustomReminder { Days = Week.ToList(), Times = ["08:00"] }));
        var reminders = Store.Load();
        if (reminders.Count == 0) _list.AddView(new TextView(this) { Text = "Zatial nemas ziadne vlastne pripomienky." });
        foreach (var reminder in reminders)
        {
            var next = CustomReminderPlan.Next(reminder, DateTime.Now);
            var enabled = new CheckBox(this) { Text = reminder.Title, Checked = reminder.Enabled };
            _list.AddView(enabled);
            _list.AddView(new TextView(this) { Text = string.Join(", ", reminder.Times) + " | " +
                string.Join(", ", Week.Select((day, index) => reminder.Days.Contains(day) ? DayNames[index] : null).Where(d => d is not null)) +
                (string.IsNullOrWhiteSpace(reminder.Note) ? "" : "\n" + reminder.Note) +
                (next.HasValue ? $"\nDalsi termin: {next:dd.MM HH:mm}" : "\nVypnuta") });
            enabled.CheckedChange += (_, args) => Guard(() =>
            {
                var current = Store.Load();
                var found = current.FirstOrDefault(r => r.Id == reminder.Id);
                if (found is null) return;
                found.Enabled = args.IsChecked;
                Save(current);
            });
            Button(_list, "Upravit", () => Edit(reminder));
            Button(_list, "Odstranit", () => new AlertDialog.Builder(this).SetTitle("Odstranit pripomienku?")!
                .SetMessage(reminder.Title)!.SetNegativeButton("Zrusit", (_, _) => { })!
                .SetPositiveButton("Odstranit", (_, _) => Guard(() =>
                {
                    var current = Store.Load();
                    current.RemoveAll(r => r.Id == reminder.Id);
                    Store.Save(current);
                    ((NotificationManager?)GetSystemService(NotificationService))?.Cancel(reminder.Id, 0);
                    CustomReminderScheduler.Refresh(this);
                    Render();
                }))!.Show());
        }
        Button(_list, "Spat", Finish);
    });

    private void Save(List<CustomReminder> reminders)
    {
        Store.Save(reminders);
        CustomReminderScheduler.Refresh(this);
        Render();
    }

    private void Edit(CustomReminder reminder)
    {
        var form = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(16 * Resources!.DisplayMetrics!.Density);
        form.SetPadding(padding, padding, padding, padding);
        var title = new EditText(this) { Hint = "Nazov (napr. Vitamin D)", Text = reminder.Title };
        var note = new EditText(this) { Hint = "Poznamka (nepovinna)", Text = reminder.Note };
        var times = new EditText(this) { Hint = "Casy: 08:00, 20:00", Text = string.Join(", ", reminder.Times) };
        form.AddView(title);
        form.AddView(note);
        form.AddView(new TextView(this) { Text = "Casy HH:mm oddel ciarkou (najviac 16)." });
        form.AddView(times);
        var days = Week.Select((day, index) => new CheckBox(this) { Text = DayNames[index], Checked = reminder.Days.Contains(day) }).ToArray();
        foreach (var day in days) form.AddView(day);
        var error = new TextView(this);
        form.AddView(error);
        var scroll = new ScrollView(this);
        scroll.AddView(form);
        var dialog = new AlertDialog.Builder(this).SetTitle("Pripomienka")!.SetView(scroll)!
            .SetNegativeButton("Zrusit", (_, _) => { })!.Create()!;
        Button(form, "Ulozit", () => Guard(() =>
        {
            var updated = new CustomReminder
            {
                Id = reminder.Id, Title = title.Text?.Trim() ?? "", Note = note.Text?.Trim() ?? "", Enabled = reminder.Enabled,
                Times = (times.Text ?? "").Split(',').Select(t => t.Trim()).OrderBy(t => t, StringComparer.Ordinal).ToList(),
                Days = Week.Where((_, index) => days[index].Checked).ToList()
            };
            try { updated.Validate(); }
            catch (InvalidDataException ex) { error.Text = ex.Message; return; }
            var current = Store.Load();
            current.RemoveAll(r => r.Id == updated.Id);
            current.Add(updated);
            Save(current);
            dialog.Dismiss();
        }));
        dialog.Show();
    }

    private void EnableNotifications()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
            RequestPermissions(["android.permission.POST_NOTIFICATIONS"], 2001);
        else
            StartActivity(new Intent("android.settings.APP_NOTIFICATION_SETTINGS").PutExtra("android.provider.extra.APP_PACKAGE", PackageName));
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != 2001) return;
        Guard(() => CustomReminderScheduler.Refresh(this));
        Render();
    }

    private void Button(LinearLayout parent, string text, Action action)
    {
        var button = new Button(this) { Text = text };
        button.Click += (_, _) => Guard(action);
        parent.AddView(button);
    }
}
