using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace DrinkWater.Android;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    private const string PreferencesName = "drinkwater.preferences";
    private const int NotificationPermissionRequestCode = 1107;

    private ISharedPreferences? _preferences;
    private AppState _state = AppState.CreateDefault();

    private TextView? _consumedText;
    private TextView? _goalText;
    private TextView? _nextReminderText;
    private TextView? _progressText;
    private TextView? _recommendedGoalText;
    private TextView? _recommendedScheduleText;
    private TextView? _elementProfileText;
    private TextView? _notesText;
    private TextView? _breakdownText;
    private TextView? _safetyText;
    private TextView? _scheduleText;
    private TextView? _stats7DayText;
    private TextView? _stats30DayText;
    private TextView? _historyPreviewText;
    private TextView? _chart7DayText;
    private TextView? _chart30DayText;
    private TextView? _streakTrendText;
    private TextView? _dayPartText;
    private TextView? _reminderStrategyText;
    private ProgressBar? _progressBar;
    private Button? _openInsightsButton;

    private EditText? _manualGoalInput;
    private EditText? _manualRemindersInput;
    private EditText? _manualStartInput;
    private EditText? _manualEndInput;
    private Spinner? _reminderModeSpinner;
    private EditText? _eveningTimeInput;
    private EditText? _nameInput;
    private EditText? _birthDateInput;
    private EditText? _weightInput;
    private EditText? _heightInput;
    private EditText? _workoutMinutesInput;
    private EditText? _coffeeCupsInput;
    private EditText? _alcoholServingsInput;
    private Spinner? _sexSpinner;
    private Spinner? _activitySpinner;
    private Spinner? _climateSpinner;
    private Spinner? _sweatSpinner;
    private Spinner? _bodySignalSpinner;
    private Spinner? _doshaSpinner;
    private CheckBox? _pregnancyCheckbox;
    private CheckBox? _kidneyCheckbox;
    private CheckBox? _heartCheckbox;
    private CheckBox? _fluidRestrictionCheckbox;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        _preferences = GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        _state = AppStateStorage.Load(_preferences);
        _state.ResetForToday();
        EnsureSchedule();

        BindViews();
        HydrateInputs();
        CreateNotificationChannel();
        WireEvents();
        var backupButton = new Button(this) { Text = "Export a zaloha historie" };
        var content = (LinearLayout)FindViewById<Button>(Resource.Id.openInsightsButton)!.Parent!;
        content.AddView(backupButton);
        backupButton.Click += (_, _) => StartActivity(typeof(BackupActivity));
        var customButton = new Button(this) { Text = "Moje pripomienky" };
        content.AddView(customButton);
        customButton.Click += (_, _) => StartActivity(typeof(CustomRemindersActivity));
        PersistAndRender();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == NotificationPermissionRequestCode)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            CustomReminderScheduler.TryRefresh(this);
            PersistAndRender();
            Toast.MakeText(this, granted ? "Upozornenia povolene." : "Upozornenia neboli povolene.", ToastLength.Short)?.Show();
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_preferences is null || _consumedText is null) return;
        CustomReminderScheduler.TryRefresh(this);
        _state = AppStateStorage.Load(_preferences);
        HydrateInputs();
        PersistAndRender();
    }

    private void BindViews()
    {
        _consumedText = FindViewById<TextView>(Resource.Id.consumedText);
        _goalText = FindViewById<TextView>(Resource.Id.goalText);
        _nextReminderText = FindViewById<TextView>(Resource.Id.nextReminderText);
        _progressText = FindViewById<TextView>(Resource.Id.progressText);
        _recommendedGoalText = FindViewById<TextView>(Resource.Id.recommendedGoalText);
        _recommendedScheduleText = FindViewById<TextView>(Resource.Id.recommendedScheduleText);
        _elementProfileText = FindViewById<TextView>(Resource.Id.elementProfileText);
        _notesText = FindViewById<TextView>(Resource.Id.notesText);
        _breakdownText = FindViewById<TextView>(Resource.Id.breakdownText);
        _safetyText = FindViewById<TextView>(Resource.Id.safetyText);
        _scheduleText = FindViewById<TextView>(Resource.Id.scheduleText);
        _stats7DayText = FindViewById<TextView>(Resource.Id.stats7DayText);
        _stats30DayText = FindViewById<TextView>(Resource.Id.stats30DayText);
        _historyPreviewText = FindViewById<TextView>(Resource.Id.historyPreviewText);
        _chart7DayText = FindViewById<TextView>(Resource.Id.chart7DayText);
        _chart30DayText = FindViewById<TextView>(Resource.Id.chart30DayText);
        _streakTrendText = FindViewById<TextView>(Resource.Id.streakTrendText);
        _dayPartText = FindViewById<TextView>(Resource.Id.dayPartText);
        _reminderStrategyText = FindViewById<TextView>(Resource.Id.reminderStrategyText);
        _progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
        _openInsightsButton = FindViewById<Button>(Resource.Id.openInsightsButton);

        _manualGoalInput = FindViewById<EditText>(Resource.Id.manualGoalInput);
        _manualRemindersInput = FindViewById<EditText>(Resource.Id.manualRemindersInput);
        _manualStartInput = FindViewById<EditText>(Resource.Id.manualStartInput);
        _manualEndInput = FindViewById<EditText>(Resource.Id.manualEndInput);
        _reminderModeSpinner = FindViewById<Spinner>(Resource.Id.reminderModeSpinner);
        _eveningTimeInput = FindViewById<EditText>(Resource.Id.eveningTimeInput);
        SetSpinnerItems(_reminderModeSpinner, "Pripominat kazdy pohar", "Raz denne vecer", "Vypnute");
        _nameInput = FindViewById<EditText>(Resource.Id.nameInput);
        _birthDateInput = FindViewById<EditText>(Resource.Id.birthDateInput);
        _weightInput = FindViewById<EditText>(Resource.Id.weightInput);
        _heightInput = FindViewById<EditText>(Resource.Id.heightInput);
        _workoutMinutesInput = FindViewById<EditText>(Resource.Id.workoutMinutesInput);
        _coffeeCupsInput = FindViewById<EditText>(Resource.Id.coffeeCupsInput);
        _alcoholServingsInput = FindViewById<EditText>(Resource.Id.alcoholServingsInput);
        _sexSpinner = FindViewById<Spinner>(Resource.Id.sexSpinner);
        _activitySpinner = FindViewById<Spinner>(Resource.Id.activitySpinner);
        _climateSpinner = FindViewById<Spinner>(Resource.Id.climateSpinner);
        _sweatSpinner = FindViewById<Spinner>(Resource.Id.sweatSpinner);
        _bodySignalSpinner = FindViewById<Spinner>(Resource.Id.bodySignalSpinner);
        _doshaSpinner = FindViewById<Spinner>(Resource.Id.doshaSpinner);
        _pregnancyCheckbox = FindViewById<CheckBox>(Resource.Id.pregnancyCheckbox);
        _kidneyCheckbox = FindViewById<CheckBox>(Resource.Id.kidneyCheckbox);
        _heartCheckbox = FindViewById<CheckBox>(Resource.Id.heartCheckbox);
        _fluidRestrictionCheckbox = FindViewById<CheckBox>(Resource.Id.fluidRestrictionCheckbox);

        SetSpinnerItems(_sexSpinner, "Zena", "Muz", "Inak");
        SetSpinnerItems(_activitySpinner, "Nizka", "Stredna", "Vysoka");
        SetSpinnerItems(_climateSpinner, "Chladne", "Mierne", "Teple");
        SetSpinnerItems(_sweatSpinner, "Nizke", "Stredne", "Vysoke");
        SetSpinnerItems(_bodySignalSpinner, "Skor suchy a rychly", "Vyrovnany", "Skor horuci", "Skor tazsi a pokojny");
        SetSpinnerItems(_doshaSpinner, "Zmiesany profil", "Vata", "Pitta", "Kapha");
    }

    private void HydrateInputs()
    {
        if (_manualGoalInput is null || _manualRemindersInput is null || _manualStartInput is null || _manualEndInput is null)
        {
            return;
        }

        _manualGoalInput.Text = _state.GoalMl.ToString();
        _manualRemindersInput.Text = _state.RemindersPerDay.ToString();
        _manualStartInput.Text = _state.StartTime;
        _manualEndInput.Text = _state.EndTime;
        SetSpinnerSelection(_reminderModeSpinner, !_state.RemindersEnabled ? 2 : _state.EveningReminder ? 1 : 0);
        if (_eveningTimeInput is not null) _eveningTimeInput.Text = _state.EveningReminderTime;
        UpdateReminderInputs();

        if (_weightInput is not null) _weightInput.Text = _state.WeightKg.ToString("0.#");
        if (_heightInput is not null) _heightInput.Text = _state.HeightCm.ToString("0.#");
        if (_workoutMinutesInput is not null) _workoutMinutesInput.Text = _state.WorkoutMinutes.ToString();
        if (_coffeeCupsInput is not null) _coffeeCupsInput.Text = _state.CoffeeCups.ToString();
        if (_alcoholServingsInput is not null) _alcoholServingsInput.Text = _state.AlcoholServings.ToString();
        if (_birthDateInput is not null) _birthDateInput.Text = _state.BirthDate;
        if (_nameInput is not null) _nameInput.Text = _state.Name;
        if (_pregnancyCheckbox is not null) _pregnancyCheckbox.Checked = _state.IsPregnantOrBreastfeeding;
        if (_kidneyCheckbox is not null) _kidneyCheckbox.Checked = _state.HasKidneyCondition;
        if (_heartCheckbox is not null) _heartCheckbox.Checked = _state.HasHeartCondition;
        if (_fluidRestrictionCheckbox is not null) _fluidRestrictionCheckbox.Checked = _state.HasFluidRestriction;

        SetSpinnerSelection(_sexSpinner, _state.Sex switch
        {
            "male" => 1,
            "other" => 2,
            _ => 0
        });
        SetSpinnerSelection(_activitySpinner, _state.Activity switch
        {
            "high" => 2,
            "medium" => 1,
            _ => 0
        });
        SetSpinnerSelection(_climateSpinner, _state.Climate switch
        {
            "hot" => 2,
            "temperate" => 1,
            _ => 0
        });
        SetSpinnerSelection(_bodySignalSpinner, _state.BodySignal switch
        {
            "balanced" => 1,
            "heated" => 2,
            "heavy" => 3,
            _ => 0
        });
        SetSpinnerSelection(_sweatSpinner, _state.SweatLevel switch
        {
            "high" => 2,
            "medium" => 1,
            _ => 0
        });
        SetSpinnerSelection(_doshaSpinner, _state.Dosha switch
        {
            "vata" => 1,
            "pitta" => 2,
            "kapha" => 3,
            _ => 0
        });
    }

    private void WireEvents()
    {
        FindViewById<Button>(Resource.Id.add150Button)!.Click += (_, _) => AddWater(150);
        FindViewById<Button>(Resource.Id.add250Button)!.Click += (_, _) => AddWater(250);
        FindViewById<Button>(Resource.Id.add330Button)!.Click += (_, _) => AddWater(330);
        FindViewById<Button>(Resource.Id.add500Button)!.Click += (_, _) => AddWater(500);
        FindViewById<Button>(Resource.Id.resetTodayButton)!.Click += (_, _) => ResetToday();
        FindViewById<Button>(Resource.Id.saveManualButton)!.Click += (_, _) => SaveManualPlan();
        FindViewById<Button>(Resource.Id.calculateButton)!.Click += (_, _) => CalculateRecommendation();
        FindViewById<Button>(Resource.Id.enableNotificationsButton)!.Click += (_, _) => RequestNotificationPermissionIfNeeded();
        FindViewById<Button>(Resource.Id.scheduleRemindersButton)!.Click += (_, _) => ScheduleReminders();
        if (_reminderModeSpinner is not null)
            _reminderModeSpinner.ItemSelected += (_, _) => UpdateReminderInputs();
        if (_openInsightsButton is not null)
        {
            _openInsightsButton.Click += (_, _) => StartActivity(typeof(InsightsActivity));
        }
    }

    private void AddWater(int amountMl)
    {
        if (_preferences is not null) _state = AppStateStorage.Load(_preferences);
        _state.AddConsumed(amountMl);
        PersistAndRender();
    }

    private void ResetToday()
    {
        new AlertDialog.Builder(this)
            .SetTitle("Vymazat dnesne pitie?")
            .SetMessage("Odstrania sa dnesne davky aj ich casy. Historia starsich dni zostane zachovana.")
            .SetNegativeButton("Ponechat", (_, _) => { })
            .SetPositiveButton("Vymazat dnesok", (_, _) =>
            {
                _state.ResetForToday(forceReset: true);
                PersistAndRender();
            })
            .Show();
    }

    private void SaveManualPlan()
    {
        _state.GoalMl = ParseInt(_manualGoalInput?.Text, 2200, 500, 5000);
        _state.RemindersPerDay = ParseInt(_manualRemindersInput?.Text, 8, 1, 16);
        _state.StartTime = NormalizeTime(_manualStartInput?.Text, "08:00");
        _state.EndTime = NormalizeTime(_manualEndInput?.Text, "20:00");
        _state.ActiveReminderSchedule = [];
        _state.ActiveReminderSummary = string.Empty;
        EnsureSchedule();
        _state.SyncTodayRecord();
        PersistAndRender();
        Toast.MakeText(this, "Manualny rezim ulozeny.", ToastLength.Short)?.Show();
    }

    private void CalculateRecommendation()
    {
        _state.Name = _nameInput?.Text?.Trim() ?? string.Empty;
        _state.BirthDate = _birthDateInput?.Text?.Trim() ?? string.Empty;
        _state.WeightKg = ParseDouble(_weightInput?.Text, 70, 30, 250);
        _state.HeightCm = ParseDouble(_heightInput?.Text, 170, 120, 230);
        _state.WorkoutMinutes = ParseInt(_workoutMinutesInput?.Text, 30, 0, 300);
        _state.CoffeeCups = ParseInt(_coffeeCupsInput?.Text, 0, 0, 12);
        _state.AlcoholServings = ParseInt(_alcoholServingsInput?.Text, 0, 0, 12);
        _state.Sex = GetSexKey(_sexSpinner?.SelectedItemPosition ?? 0);
        _state.Activity = GetActivityKey(_activitySpinner?.SelectedItemPosition ?? 1);
        _state.Climate = GetClimateKey(_climateSpinner?.SelectedItemPosition ?? 1);
        _state.SweatLevel = GetSweatKey(_sweatSpinner?.SelectedItemPosition ?? 1);
        _state.BodySignal = GetBodySignalKey(_bodySignalSpinner?.SelectedItemPosition ?? 1);
        _state.Dosha = GetDoshaKey(_doshaSpinner?.SelectedItemPosition ?? 0);
        _state.IsPregnantOrBreastfeeding = _pregnancyCheckbox?.Checked ?? false;
        _state.HasKidneyCondition = _kidneyCheckbox?.Checked ?? false;
        _state.HasHeartCondition = _heartCheckbox?.Checked ?? false;
        _state.HasFluidRestriction = _fluidRestrictionCheckbox?.Checked ?? false;

        var recommendation = HydrationCalculator.Generate(_state);
        _state.GoalMl = recommendation.GoalMl;
        _state.RemindersPerDay = recommendation.RemindersPerDay;
        _state.StartTime = "07:30";
        _state.EndTime = "20:30";
        _state.Schedule = recommendation.Schedule;
        _state.RecommendedGoalMl = recommendation.GoalMl;
        _state.RecommendedRemindersPerDay = recommendation.RemindersPerDay;
        _state.ElementProfile = recommendation.ElementProfile;
        _state.RecommendationNotes = recommendation.Notes;
        _state.RecommendationBreakdown = recommendation.Breakdown;
        _state.SafetyMessage = recommendation.SafetyMessage;
        _state.ActiveReminderSchedule = [];
        _state.ActiveReminderSummary = string.Empty;
        _state.SyncTodayRecord();

        HydrateInputs();
        PersistAndRender();
        Toast.MakeText(this, "Odporucanie vypocitane.", ToastLength.Short)?.Show();
    }

    private void ScheduleReminders()
    {
        var mode = _reminderModeSpinner?.SelectedItemPosition ?? 2;
        if (mode == 1 && !TimeSpan.TryParseExact(_eveningTimeInput?.Text, @"hh\:mm",
            System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            Toast.MakeText(this, "Zadaj vecerny cas vo formate HH:mm (00:00 az 23:59).", ToastLength.Long)?.Show();
            return;
        }
        if (mode == 0)
        {
            if (!TimeSpan.TryParseExact(_manualStartInput?.Text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var start) ||
                !TimeSpan.TryParseExact(_manualEndInput?.Text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var end) || end <= start ||
                !int.TryParse(_manualRemindersInput?.Text, out var count) || count < 1 || count > 16)
            {
                Toast.MakeText(this, "Zadaj casy HH:mm, koniec neskor ako zaciatok a 1 az 16 pripomienok.", ToastLength.Long)?.Show();
                return;
            }
            _state.StartTime = start.ToString(@"hh\:mm");
            _state.EndTime = end.ToString(@"hh\:mm");
            _state.RemindersPerDay = count;
            EnsureSchedule();
        }
        _state.RemindersEnabled = mode != 2;
        _state.EveningReminder = mode == 1;
        _state.NextReminderUtc = 0;
        if (mode == 1) _state.EveningReminderTime = _eveningTimeInput!.Text!;
        if (_state.RemindersEnabled) RequestNotificationPermissionIfNeeded();
        PersistAndRender();
        Toast.MakeText(this, "Nastavenie upozorneni ulozene.", ToastLength.Short)?.Show();
    }

    private void UpdateReminderInputs()
    {
        var mode = _reminderModeSpinner?.SelectedItemPosition ?? 2;
        foreach (var input in new[] { _manualRemindersInput, _manualStartInput, _manualEndInput })
            if (input is not null) input.Visibility = mode == 0 ? ViewStates.Visible : ViewStates.Gone;
        if (_eveningTimeInput is not null) _eveningTimeInput.Visibility = mode == 1 ? ViewStates.Visible : ViewStates.Gone;
        FindViewById<Button>(Resource.Id.enableNotificationsButton)!.Visibility = mode == 2 ? ViewStates.Gone : ViewStates.Visible;
    }

    private void CancelReminders(bool showToast = true)
    {
        _state.RemindersEnabled = false;
        PersistAndRender();
        if (showToast) Toast.MakeText(this, "Pripomienky vypnute.", ToastLength.Short)?.Show();
    }

    private void Render()
    {
        if (_consumedText is null || _goalText is null || _progressBar is null || _progressText is null ||
            _recommendedGoalText is null || _recommendedScheduleText is null || _elementProfileText is null ||
            _notesText is null || _breakdownText is null || _safetyText is null || _scheduleText is null ||
            _stats7DayText is null || _stats30DayText is null || _historyPreviewText is null ||
            _chart7DayText is null || _chart30DayText is null || _streakTrendText is null || _dayPartText is null ||
            _reminderStrategyText is null ||
            _nextReminderText is null)
        {
            return;
        }

        _state.ResetForToday();
        EnsureSchedule();

        _consumedText.Text = $"{_state.ConsumedMl} ml";
        _goalText.Text = $"{_state.GoalMl} ml";

        var progress = _state.GoalMl > 0 ? (int)Math.Min(100, Math.Round(_state.ConsumedMl * 100d / _state.GoalMl)) : 0;
        _progressBar.Progress = progress;
        _progressText.Text = $"{progress} % denneho ciela";
        _nextReminderText.Text = GetNextReminderLabel();

        var recommendedGoal = _state.RecommendedGoalMl > 0 ? _state.RecommendedGoalMl : _state.GoalMl;
        var recommendedSchedule = _state.RecommendedRemindersPerDay > 0 ? _state.RecommendedRemindersPerDay : _state.RemindersPerDay;

        _recommendedGoalText.Text = $"{recommendedGoal} ml";
        _recommendedScheduleText.Text = $"{recommendedSchedule}x denne";
        _elementProfileText.Text = string.IsNullOrWhiteSpace(_state.ElementProfile)
            ? "Vzduch 20 % | Ohen 20 % | Voda 20 % | Zem 20 % | Priestor 20 %"
            : _state.ElementProfile;
        _notesText.Text = string.IsNullOrWhiteSpace(_state.RecommendationNotes)
            ? "Vypln dotaznik a appka pripravi orientacny wellness plan hydratacie."
            : _state.RecommendationNotes;
        _breakdownText.Text = string.IsNullOrWhiteSpace(_state.RecommendationBreakdown)
            ? "Tu sa po vypocte objavi rozpis, z coho sa vysledok sklada."
            : _state.RecommendationBreakdown;
        _safetyText.Text = string.IsNullOrWhiteSpace(_state.SafetyMessage)
            ? "Bezpecnostne upozornenia sa zobrazia po vyplneni rizikovych faktorov."
            : _state.SafetyMessage;

        var doseMl = _state.Schedule.Count > 0 ? (int)Math.Round((double)_state.GoalMl / _state.Schedule.Count) : 0;
        var displayedSchedule = _state.ActiveReminderSchedule;
        _scheduleText.Text = displayedSchedule.Count == 0
            ? "Zatial nie je vytvoreny harmonogram."
            : string.Join(System.Environment.NewLine, displayedSchedule.Select((time, index) =>
                $"Kontrola pitia okolo {time}"));
        _reminderStrategyText.Text = string.IsNullOrWhiteSpace(_state.ActiveReminderSummary)
            ? "Pripomienky su zatial fixne podla rezimu. Po spusteni planu appka vytvori adaptivny notifikacny plan."
            : _state.ActiveReminderSummary;

        var stats7 = _state.CalculateStats(7);
        var stats30 = _state.CalculateStats(30);
        _stats7DayText.Text = FormatStats("Poslednych 7 dni", stats7);
        _stats30DayText.Text = FormatStats("Poslednych 30 dni", stats30);
        _historyPreviewText.Text = FormatHistoryPreview(_state.History);
        _chart7DayText.Text = BuildMiniChart("Mini graf 7 dni", stats7.Records);
        _chart30DayText.Text = BuildMiniChart("Mini graf 30 dni", stats30.Records, maxItems: 10);
        _streakTrendText.Text = BuildStreakTrendText(stats7, stats30);
        _dayPartText.Text = BuildDayPartText(stats30.DayParts);
    }

    private void PersistAndRender()
    {
        _state.ResetForToday();
        ReminderScheduler.Refresh(this, _state);
        Render();

        if (_preferences is not null)
        {
            AppStateStorage.Save(_preferences, _state);
        }
    }

    private void EnsureSchedule()
    {
        _state.Schedule = HydrationCalculator.BuildSchedule(_state.StartTime, _state.EndTime, _state.RemindersPerDay);
    }

    private string GetNextReminderLabel()
    {
        return _state.NextReminderUtc > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(_state.NextReminderUtc).LocalDateTime.ToString("dd.MM HH:mm")
            : "Vypnute / nenaplanovane";
    }

    private string GetLegacyReminderLabel()
    {
        var source = _state.ActiveReminderSchedule.Count > 0 ? _state.ActiveReminderSchedule : _state.Schedule;
        var next = source
            .Select(time => new { Time = time, DateTime = DateTime.Today.Add(TimeSpan.Parse(time)) })
            .FirstOrDefault(entry => entry.DateTime > DateTime.Now);

        return next?.Time ?? "Dnes uz ziadna";
    }

    private PendingIntent? CreateReminderPendingIntent(int requestCode, string time, int doseMl, string message = "", PendingIntentFlags extraFlags = PendingIntentFlags.UpdateCurrent)
    {
        var intent = new Intent(this, typeof(ReminderReceiver));
        intent.SetAction(ReminderReceiver.ActionReminder);
        intent.PutExtra(ReminderReceiver.ExtraTime, time);
        intent.PutExtra(ReminderReceiver.ExtraDoseMl, doseMl);
        intent.PutExtra(ReminderReceiver.ExtraMessage, message);
        var flags = extraFlags | PendingIntentFlags.Immutable;
        return PendingIntent.GetBroadcast(this, requestCode, intent, flags);
    }

    private void RequestNotificationPermissionIfNeeded()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
        {
            return;
        }

        if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions([Manifest.Permission.PostNotifications], NotificationPermissionRequestCode);
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null)
        {
            return;
        }

        var channel = new NotificationChannel(ReminderReceiver.ChannelId, "DrinkWater pripomienky", NotificationImportance.High)
        {
            Description = "Upozornenia na pitie vody"
        };

        manager.CreateNotificationChannel(channel);
    }

    private static void SetSpinnerItems(Spinner? spinner, params string[] items)
    {
        if (spinner is null)
        {
            return;
        }

        var adapter = new ArrayAdapter<string>(spinner.Context!, 17367048, items);
        adapter.SetDropDownViewResource(17367049);
        spinner.Adapter = adapter;
    }

    private static void SetSpinnerSelection(Spinner? spinner, int position)
    {
        if (spinner is null || spinner.Adapter is null || spinner.Adapter.Count <= position)
        {
            return;
        }

        spinner.SetSelection(position);
    }

    private static int ParseInt(string? value, int fallback, int min, int max)
    {
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;
    }

    private static double ParseDouble(string? value, double fallback, double min, double max)
    {
        return double.TryParse(value?.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : fallback;
    }

    private static string NormalizeTime(string? value, string fallback)
    {
        return TimeSpan.TryParse(value, out var time) ? time.ToString(@"hh\:mm") : fallback;
    }

    private static string GetSexKey(int selectedIndex) => selectedIndex switch
    {
        1 => "male",
        2 => "other",
        _ => "female"
    };

    private static string GetActivityKey(int selectedIndex) => selectedIndex switch
    {
        2 => "high",
        1 => "medium",
        _ => "low"
    };

    private static string GetClimateKey(int selectedIndex) => selectedIndex switch
    {
        2 => "hot",
        1 => "temperate",
        _ => "cool"
    };

    private static string GetBodySignalKey(int selectedIndex) => selectedIndex switch
    {
        3 => "heavy",
        2 => "heated",
        1 => "balanced",
        _ => "dry"
    };

    private static string GetSweatKey(int selectedIndex) => selectedIndex switch
    {
        2 => "high",
        1 => "medium",
        _ => "low"
    };

    private static string GetDoshaKey(int selectedIndex) => selectedIndex switch
    {
        3 => "kapha",
        2 => "pitta",
        1 => "vata",
        _ => "mixed"
    };

    private static string FormatStats(string title, HydrationStats stats)
    {
        return string.Join(
            System.Environment.NewLine,
            [
                title,
                $"Spolu: {stats.TotalConsumedMl} ml",
                $"Priemer na den: {stats.AveragePerDayMl} ml",
                $"Aktivne dni: {stats.ActiveDays}/{stats.WindowDays}",
                $"Splneny ciel: {stats.GoalHitDays}/{stats.WindowDays}",
                $"Konzistentnost: {stats.ConsistencyPercent} %"
            ]);
    }

    private static string FormatHistoryPreview(List<DailyHydrationRecord> history)
    {
        var latest = history
            .OrderByDescending(item => item.Date)
            .Take(7)
            .Select(item => $"{item.Date}: {item.ConsumedMl}/{item.GoalMl} ml")
            .ToList();

        if (latest.Count == 0)
        {
            return "Zatial este nie je zaznamenana ziadna historia pitia.";
        }

        return string.Join(System.Environment.NewLine, latest);
    }

    private static string BuildMiniChart(string title, List<DailyHydrationRecord> records, int maxItems = 7)
    {
        var relevant = records
            .OrderByDescending(item => item.Date)
            .Take(maxItems)
            .OrderBy(item => item.Date)
            .ToList();

        if (relevant.Count == 0)
        {
            return $"{title}{System.Environment.NewLine}Zatial bez dat.";
        }

        var maxValue = Math.Max(1, relevant.Max(item => Math.Max(item.ConsumedMl, item.GoalMl)));
        var lines = new List<string> { title };

        foreach (var item in relevant)
        {
            var label = item.Date.Length >= 5 ? item.Date[^5..] : item.Date;
            var barLength = (int)Math.Round(item.ConsumedMl * 10d / maxValue);
            var bar = new string('█', Math.Max(0, barLength));
            if (string.IsNullOrEmpty(bar))
            {
                bar = "·";
            }

            lines.Add($"{label} {bar} {item.ConsumedMl} ml");
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private static string BuildStreakTrendText(HydrationStats stats7, HydrationStats stats30)
    {
        var shortTrend = DescribeTrend(stats7.TrendMl);
        var longTrend = DescribeTrend(stats30.TrendMl);

        return string.Join(
            System.Environment.NewLine,
            [
                "Streak a trend",
                $"Aktualny streak splneneho ciela: {stats30.CurrentStreak} dni",
                $"Trend 7 dni: {shortTrend}",
                $"Trend 30 dni: {longTrend}"
            ]);
    }

    private static string BuildDayPartText(DayPartStats stats)
    {
        var weakest = "rano";
        var weakestValue = stats.MorningMl;

        if (stats.NoonMl < weakestValue)
        {
            weakest = "obed";
            weakestValue = stats.NoonMl;
        }

        if (stats.EveningMl < weakestValue)
        {
            weakest = "vecer";
        }

        return string.Join(
            System.Environment.NewLine,
            [
                "Rozdelenie pitia",
                $"Rano: {stats.MorningMl} ml",
                $"Obed: {stats.NoonMl} ml",
                $"Vecer: {stats.EveningMl} ml",
                $"Najslabsia cast dna: {weakest}"
            ]);
    }

    private static string DescribeTrend(int trendMl)
    {
        if (trendMl > 120)
        {
            return $"rastie o cca {trendMl} ml";
        }

        if (trendMl < -120)
        {
            return $"klesa o cca {Math.Abs(trendMl)} ml";
        }

        return "stabilny";
    }
}
