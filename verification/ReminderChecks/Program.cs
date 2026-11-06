using DrinkWater.Android;

var today = new DateTime(2026, 10, 12);
var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
}
Check(EveningReminderPlan.Next(today.AddHours(18), "20:00", false, "") == today.AddHours(20), "Before evening");
Check(EveningReminderPlan.Next(today.AddHours(21), "20:00", false, "") == today.AddDays(1).AddHours(20), "After evening");
Check(EveningReminderPlan.Next(today.AddHours(20), "20:00", false, "") == today.AddDays(1).AddHours(20), "At evening boundary");
Check(EveningReminderPlan.Next(today.AddHours(18), "20:00", true, "") == today.AddDays(1).AddHours(20), "Goal reached");
Check(EveningReminderPlan.Next(today.AddHours(18), "20:00", false, "2026-10-12") == today.AddDays(1).AddHours(20), "Already shown today");
Check(EveningReminderPlan.Next(today.AddHours(18), "20:00", false, "2026-10-11") == today.AddHours(20), "Previous day does not block");
foreach (var invalid in new[] { "24:00", "-01:00", "wrong", "20:60" })
    Check(EveningReminderPlan.Next(today, invalid, false, "") is null, "Reject " + invalid);
Check(EveningReminderPlan.IsDue(today.AddHours(21), today.AddHours(20), ""), "Delayed same-day delivery");
Check(!EveningReminderPlan.IsDue(today.AddHours(19), today.AddHours(20), ""), "Early delivery");
Check(!EveningReminderPlan.IsDue(today.AddHours(21), today.AddHours(20), "2026-10-12"), "Duplicate delivery");
Check(!EveningReminderPlan.IsDue(today.AddDays(1), today.AddHours(20), ""), "Stale previous-day delivery");
var monday = new DateTime(2026, 10, 12);
CustomReminder Reminder() => new() { Title = "Vitamin D", Times = ["20:00", "08:00"], Days = Enum.GetValues<DayOfWeek>().ToList() };
var custom = Reminder();
custom.Validate();
Check(CustomReminderPlan.Next(custom, monday.AddHours(7)) == monday.AddHours(8), "Unsorted times choose earliest");
Check(CustomReminderPlan.Next(custom, monday.AddHours(8)) == monday.AddHours(20), "Boundary advances to evening");
Check(CustomReminderPlan.Next(custom, monday.AddHours(21)) == monday.AddDays(1).AddHours(8), "Next day");
custom.Enabled = false;
Check(CustomReminderPlan.Next(custom, monday) is null, "Disabled custom reminder");
custom.Enabled = true;
custom.Days = [DayOfWeek.Monday];
Check(CustomReminderPlan.Next(custom, monday.AddHours(21)) == monday.AddDays(7).AddHours(8), "Weekly rollover");
custom.Days = [DayOfWeek.Sunday];
Check(CustomReminderPlan.Next(custom, monday) == monday.AddDays(6).AddHours(8), "Selected day only");
custom.Days = [DayOfWeek.Thursday];
Check(CustomReminderPlan.Next(custom, new DateTime(2026, 12, 31, 21, 0, 0)) == new DateTime(2027, 1, 7, 8, 0, 0), "Year rollover");
var invalidEdits = new Action<CustomReminder>[] {
    r => r.Title = " ", r => r.Title = new string('x', 101), r => r.Note = new string('x', 501),
    r => r.Id = "bad", r => r.Times = [], r => r.Times = ["24:00"], r => r.Times = ["8:00"],
    r => r.Times = ["08:00", "08:00"], r => r.Times = ["20:60"],
    r => r.Days = [], r => r.Days = [(DayOfWeek)9], r => r.Days = [DayOfWeek.Monday, DayOfWeek.Monday]
};
foreach (var edit in invalidEdits)
{
    var invalid = Reminder();
    edit(invalid);
    var rejected = false;
    try { invalid.Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Reject invalid custom reminder");
}
var serialized = System.Text.Json.JsonSerializer.Serialize(Reminder());
var restored = System.Text.Json.JsonSerializer.Deserialize<CustomReminder>(serialized)!;
restored.Validate();
Check(CustomReminderPlan.Next(restored, monday.AddHours(7)) == monday.AddHours(8), "JSON roundtrip preserves schedule");
var bratislava = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bratislava");
var dst = Reminder();
dst.Times = ["03:15", "02:30"];
Check(CustomReminderPlan.Next(dst, new DateTime(2026, 3, 29, 1, 0, 0), bratislava) == new DateTime(2026, 3, 29, 3, 0, 0), "Spring missing time moves forward");
Check(CustomReminderPlan.Next(dst, new DateTime(2026, 3, 29, 3, 0, 0), bratislava) == new DateTime(2026, 3, 29, 3, 15, 0), "Next spring occurrence");
Check(CustomReminderPlan.Next(dst, new DateTime(2026, 10, 25, 1, 0, 0), bratislava) == new DateTime(2026, 10, 25, 2, 30, 0), "Autumn local time retained");
Console.WriteLine($"{checks}/{checks} reminder checks passed.");
