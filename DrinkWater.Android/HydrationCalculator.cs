namespace DrinkWater.Android;

public static class HydrationCalculator
{
    public static HydrationRecommendation Generate(AppState state)
    {
        var age = CalculateAge(state.BirthDate);
        var bmi = state.WeightKg / Math.Pow(state.HeightCm / 100d, 2);
        var breakdown = new List<string>();
        var safetyNotes = new List<string>();

        var goal = state.WeightKg * 30d;
        breakdown.Add($"Zaklad podla vahy: {Math.Round(state.WeightKg * 30d)} ml");

        var ageAdjustment = age >= 55 ? 150 : 0;
        goal += ageAdjustment;
        if (ageAdjustment != 0)
        {
            breakdown.Add($"+{ageAdjustment} ml vek 55+ a vyssie riziko slabsieho pocitu smadu");
        }

        var activityAdjustment = state.Activity switch
        {
            "high" => 500,
            "medium" => 250,
            _ => 0
        };
        goal += activityAdjustment;
        if (activityAdjustment != 0)
        {
            breakdown.Add($"+{activityAdjustment} ml celkova denno-denna aktivita");
        }

        var workoutAdjustment = (int)Math.Round(state.WorkoutMinutes * 4d);
        goal += workoutAdjustment;
        if (workoutAdjustment != 0)
        {
            breakdown.Add($"+{workoutAdjustment} ml trening alebo pohyb ({state.WorkoutMinutes} min)");
        }

        var sweatAdjustment = state.SweatLevel switch
        {
            "high" => 350,
            "medium" => 150,
            _ => 0
        };
        goal += sweatAdjustment;
        if (sweatAdjustment != 0)
        {
            breakdown.Add($"+{sweatAdjustment} ml miera potenia");
        }

        var climateAdjustment = state.Climate switch
        {
            "hot" => 400,
            "temperate" => 100,
            _ => -100
        };
        goal += climateAdjustment;
        breakdown.Add($"{FormatSigned(climateAdjustment)} ml klima a okolita teplota");

        var coffeeAdjustment = state.CoffeeCups * 60;
        goal += coffeeAdjustment;
        if (coffeeAdjustment != 0)
        {
            breakdown.Add($"+{coffeeAdjustment} ml kava a kofein ({state.CoffeeCups}x)");
        }

        var alcoholAdjustment = state.AlcoholServings * 120;
        goal += alcoholAdjustment;
        if (alcoholAdjustment != 0)
        {
            breakdown.Add($"+{alcoholAdjustment} ml alkohol ({state.AlcoholServings}x)");
        }

        if (state.IsPregnantOrBreastfeeding)
        {
            goal += 300;
            breakdown.Add("+300 ml tehotenstvo alebo dojcenie");
            safetyNotes.Add("Pri tehotenstve alebo dojceni ber odporucanie ako orientacne a dolaď ho so svojim lekarom.");
        }

        if (bmi >= 28)
        {
            goal += 100;
            breakdown.Add("+100 ml vyssia telesna zataz");
        }

        var finalGoal = Clamp((int)Math.Round(goal / 50d) * 50, 1400, 4500);
        var hasMedicalRisk = state.HasFluidRestriction || state.HasKidneyCondition || state.HasHeartCondition;
        if (hasMedicalRisk)
        {
            finalGoal = Math.Min(finalGoal, 2500);
            safetyNotes.Add("Mas oznacene zdravotne obmedzenie tekutin alebo ochorenie obliciek/srdca. Appka preto pouziva opatrny rezim.");
            safetyNotes.Add("Pri tychto stavoch nema appka nahradzat lekarom nastavene limity tekutin.");
        }

        if (age < 18)
        {
            safetyNotes.Add("Pre osoby pod 18 rokov je tento model len orientacny a nie je to pediatricke odporucanie.");
        }

        var reminders = Clamp((int)Math.Round(finalGoal / 280d), 5, 12);
        var schedule = BuildSchedule("07:30", "20:30", reminders);
        var profile = BuildElementProfile(state);

        var notes = string.Join(
            System.Environment.NewLine,
            [
                $"{(string.IsNullOrWhiteSpace(state.Name) ? "Pouzivatel" : state.Name)} ma orientacny ciel {finalGoal} ml denne rozdeleny do {reminders} davok.",
                "Hlavny objem vody je pocitany zo zdravotne relevantnych faktorov: vaha, aktivita, trening, potenie, klima, kofein a alkohol.",
                $"Wellness vrstva {state.Dosha}/{state.BodySignal} a elementovy profil sluzia ako doplnkova personalizacia, nie ako hlavny vedecky vypocet."
            ]);

        var breakdownText = string.Join(System.Environment.NewLine, breakdown);
        var safetyText = safetyNotes.Count == 0
            ? "Bezpecnostny status: bez oznacenych rizikovych obmedzeni. Aj tak ide o orientacny model, nie lekarske odporucanie."
            : string.Join(System.Environment.NewLine, safetyNotes);

        return new HydrationRecommendation(
            finalGoal,
            reminders,
            schedule,
            $"Vzduch {profile.Air}% | Ohen {profile.Fire}% | Voda {profile.Water}% | Zem {profile.Earth}% | Priestor {profile.Space}%",
            notes,
            breakdownText,
            safetyText);
    }

    public static List<string> BuildSchedule(string startTime, string endTime, int remindersPerDay)
    {
        if (!TimeSpan.TryParse(startTime, out var start))
        {
            start = new TimeSpan(8, 0, 0);
        }

        if (!TimeSpan.TryParse(endTime, out var end))
        {
            end = new TimeSpan(20, 0, 0);
        }

        if (remindersPerDay <= 1 || end <= start)
        {
            return [start.ToString(@"hh\:mm")];
        }

        var totalMinutes = (end - start).TotalMinutes;
        var interval = totalMinutes / (remindersPerDay - 1);

        return Enumerable.Range(0, remindersPerDay)
            .Select(index => start.Add(TimeSpan.FromMinutes(Math.Round(interval * index))).ToString(@"hh\:mm"))
            .ToList();
    }

    private static ElementProfile BuildElementProfile(AppState state)
    {
        var air = 20;
        var fire = 20;
        var water = 20;
        var earth = 20;
        var space = 20;

        if (state.Dosha == "vata")
        {
            air += 18;
            space += 16;
            water -= 10;
            earth -= 8;
        }

        if (state.Dosha == "pitta")
        {
            fire += 22;
            water += 8;
            air -= 6;
        }

        if (state.Dosha == "kapha")
        {
            water += 16;
            earth += 18;
            fire -= 8;
            space -= 6;
        }

        if (state.BodySignal == "dry")
        {
            air += 8;
            water -= 5;
        }

        if (state.BodySignal == "heated")
        {
            fire += 8;
            water += 4;
        }

        if (state.BodySignal == "heavy")
        {
            earth += 6;
            water += 5;
            air -= 4;
        }

        if (state.Climate == "hot")
        {
            fire += 6;
            water += 3;
        }

        if (state.Climate == "cool")
        {
            earth += 4;
            water -= 2;
        }

        var birthInfluence = DeriveBirthInfluence(state.BirthDate);
        air += birthInfluence.Air;
        fire += birthInfluence.Fire;
        water += birthInfluence.Water;
        earth += birthInfluence.Earth;
        space += birthInfluence.Space;

        var total = air + fire + water + earth + space;
        return new ElementProfile(
            (int)Math.Round(air * 100d / total),
            (int)Math.Round(fire * 100d / total),
            (int)Math.Round(water * 100d / total),
            (int)Math.Round(earth * 100d / total),
            (int)Math.Round(space * 100d / total));
    }

    private static ElementProfile DeriveBirthInfluence(string birthDate)
    {
        if (!DateTime.TryParse(birthDate, out var date))
        {
            return new ElementProfile(0, 0, 0, 0, 0);
        }

        var seed = ((date.Month * 31) + date.Day) % 5;
        return seed switch
        {
            0 => new ElementProfile(3, 1, 0, 1, 2),
            1 => new ElementProfile(1, 3, 1, 0, 2),
            2 => new ElementProfile(0, 1, 3, 2, 0),
            3 => new ElementProfile(1, 0, 1, 3, 1),
            _ => new ElementProfile(2, 1, 1, 0, 3)
        };
    }

    private static int CalculateAge(string birthDate)
    {
        if (!DateTime.TryParse(birthDate, out var dob))
        {
            return 30;
        }

        var today = DateTime.Today;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();
}

public sealed record HydrationRecommendation(
    int GoalMl,
    int RemindersPerDay,
    List<string> Schedule,
    string ElementProfile,
    string Notes,
    string Breakdown,
    string SafetyMessage);

public sealed record ElementProfile(int Air, int Fire, int Water, int Earth, int Space);
