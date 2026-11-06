const STORAGE_KEY = "drinkwater-ritual-state";

const defaultState = {
  lastTrackedDate: new Date().toISOString().slice(0, 10),
  consumed: 0,
  goal: 2200,
  remindersPerDay: 8,
  startTime: "08:00",
  endTime: "20:00",
  recommendation: null,
  schedule: [],
  notificationPermission: "default",
  reminderTimerIds: []
};

let state = loadState();
resetDailyStateIfNeeded();
ensureSchedule();

const consumedTodayEl = document.getElementById("consumedToday");
const goalTodayEl = document.getElementById("goalToday");
const nextReminderEl = document.getElementById("nextReminder");
const progressFillEl = document.getElementById("progressFill");
const progressTextEl = document.getElementById("progressText");
const recommendedGoalEl = document.getElementById("recommendedGoal");
const recommendedScheduleEl = document.getElementById("recommendedSchedule");
const elementProfileEl = document.getElementById("elementProfile");
const recommendationNotesEl = document.getElementById("recommendationNotes");
const scheduleListEl = document.getElementById("scheduleList");

const manualForm = document.getElementById("manualForm");
const quizForm = document.getElementById("quizForm");
const enableNotificationsBtn = document.getElementById("enableNotifications");
const startRemindersBtn = document.getElementById("startReminders");
const stopRemindersBtn = document.getElementById("stopReminders");

if ("serviceWorker" in navigator) {
  window.addEventListener("load", () => {
    navigator.serviceWorker.register("sw.js").catch(() => {});
  });
}

manualForm.addEventListener("submit", (event) => {
  event.preventDefault();

  state.goal = Number(document.getElementById("manualGoal").value || 2200);
  state.remindersPerDay = Number(document.getElementById("manualReminders").value || 8);
  state.startTime = document.getElementById("manualStart").value || "08:00";
  state.endTime = document.getElementById("manualEnd").value || "20:00";
  state.schedule = buildSchedule(state.startTime, state.endTime, state.remindersPerDay);

  saveAndRender();
});

quizForm.addEventListener("submit", (event) => {
  event.preventDefault();

  const payload = {
    name: document.getElementById("name").value.trim(),
    birthDate: document.getElementById("birthDate").value,
    sex: document.getElementById("sex").value,
    weight: Number(document.getElementById("weight").value || 70),
    height: Number(document.getElementById("height").value || 170),
    activity: document.getElementById("activity").value,
    climate: document.getElementById("climate").value,
    bodySignal: document.getElementById("bodySignal").value,
    dosha: document.getElementById("dosha").value
  };

  const recommendation = generateRecommendation(payload);
  state.goal = recommendation.goal;
  state.remindersPerDay = recommendation.reminders;
  state.startTime = "07:30";
  state.endTime = "20:30";
  state.recommendation = recommendation;
  state.schedule = buildSchedule(state.startTime, state.endTime, state.remindersPerDay);

  document.getElementById("manualGoal").value = String(state.goal);
  document.getElementById("manualReminders").value = String(state.remindersPerDay);
  document.getElementById("manualStart").value = state.startTime;
  document.getElementById("manualEnd").value = state.endTime;

  saveAndRender();
});

document.querySelectorAll(".water-btn").forEach((button) => {
  button.addEventListener("click", () => {
    const amount = Number(button.dataset.amount || 0);
    state.consumed += amount;
    saveAndRender();
  });
});

enableNotificationsBtn.addEventListener("click", async () => {
  if (!("Notification" in window)) {
    alert("Tento prehliadac nepodporuje lokalne upozornenia.");
    return;
  }

  const permission = await Notification.requestPermission();
  state.notificationPermission = permission;
  saveAndRender();
});

startRemindersBtn.addEventListener("click", () => {
  clearReminderTimers();

  if (!state.schedule.length) {
    state.schedule = buildSchedule(state.startTime, state.endTime, state.remindersPerDay);
  }

  const now = new Date();
  const timerIds = [];

  state.schedule.forEach((timeLabel) => {
    const target = createTodayTime(timeLabel);
    if (target <= now) {
      return;
    }

    const timeout = target.getTime() - now.getTime();
    const id = window.setTimeout(() => {
      triggerReminder(timeLabel);
    }, timeout);
    timerIds.push(id);
  });

  state.reminderTimerIds = timerIds;
  saveAndRender();
});

stopRemindersBtn.addEventListener("click", () => {
  clearReminderTimers();
  saveAndRender();
});

hydrateForms();
render();

function loadState() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    return saved ? { ...defaultState, ...JSON.parse(saved) } : { ...defaultState };
  } catch {
    return { ...defaultState };
  }
}

function resetDailyStateIfNeeded() {
  const today = new Date().toISOString().slice(0, 10);
  if (state.lastTrackedDate !== today) {
    state.lastTrackedDate = today;
    state.consumed = 0;
  }
}

function ensureSchedule() {
  if (!Array.isArray(state.schedule) || !state.schedule.length) {
    state.schedule = buildSchedule(state.startTime, state.endTime, state.remindersPerDay);
  }
}

function hydrateForms() {
  document.getElementById("manualGoal").value = String(state.goal);
  document.getElementById("manualReminders").value = String(state.remindersPerDay);
  document.getElementById("manualStart").value = state.startTime;
  document.getElementById("manualEnd").value = state.endTime;
}

function saveAndRender() {
  localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      ...state,
      reminderTimerIds: []
    })
  );
  render();
}

function render() {
  consumedTodayEl.textContent = `${state.consumed} ml`;
  goalTodayEl.textContent = `${state.goal} ml`;

  const progress = Math.min(100, Math.round((state.consumed / state.goal) * 100) || 0);
  progressFillEl.style.width = `${progress}%`;
  progressTextEl.textContent = `${progress} % denneho ciela`;

  if (state.recommendation) {
    recommendedGoalEl.textContent = `${state.recommendation.goal} ml`;
    recommendedScheduleEl.textContent = `${state.recommendation.reminders}x denne`;
    elementProfileEl.textContent = state.recommendation.elementsLabel;
    recommendationNotesEl.innerHTML = state.recommendation.notes
      .map((note) => `<p>${note}</p>`)
      .join("");
  } else {
    recommendationNotesEl.innerHTML =
      "<p>Vypln dotaznik a appka ti pripravi odporucany pitny plan aj orientacny elementovy profil.</p>";
  }

  renderSchedule();
  nextReminderEl.textContent = getNextReminderLabel();
}

function renderSchedule() {
  if (!state.schedule.length) {
    scheduleListEl.innerHTML = "<li>Zatial nie je vytvoreny ziaden harmonogram.</li>";
    return;
  }

  scheduleListEl.innerHTML = state.schedule
    .map((time, index) => {
      const dose = Math.round(state.goal / state.schedule.length);
      return `<li>${index + 1}. davka o <strong>${time}</strong> - orientacne ${dose} ml</li>`;
    })
    .join("");
}

function buildSchedule(startTime, endTime, remindersPerDay) {
  const startMinutes = timeToMinutes(startTime);
  const endMinutes = timeToMinutes(endTime);

  if (remindersPerDay <= 1 || endMinutes <= startMinutes) {
    return [startTime];
  }

  const interval = (endMinutes - startMinutes) / (remindersPerDay - 1);
  return Array.from({ length: remindersPerDay }, (_, index) =>
    minutesToTime(Math.round(startMinutes + interval * index))
  );
}

function generateRecommendation(payload) {
  const age = calculateAge(payload.birthDate);
  const bmi = payload.weight / Math.pow(payload.height / 100, 2);

  let goal = payload.weight * 32;

  if (payload.activity === "medium") goal += 300;
  if (payload.activity === "high") goal += 700;

  if (payload.climate === "hot") goal += 450;
  if (payload.climate === "cool") goal -= 150;

  if (payload.bodySignal === "dry") goal += 250;
  if (payload.bodySignal === "heated") goal += 200;
  if (payload.bodySignal === "heavy") goal -= 100;

  if (payload.dosha === "vata") goal += 200;
  if (payload.dosha === "pitta") goal += 250;
  if (payload.dosha === "kapha") goal -= 100;

  if (age >= 55) goal += 150;
  if (bmi >= 28) goal += 150;

  goal = clamp(Math.round(goal / 50) * 50, 1400, 4500);

  const reminders = clamp(Math.round(goal / 280), 5, 12);
  const elements = calculateElementProfile(payload);

  const notes = [
    `${payload.name || "Pouzivatel"} ma orientacny ciel ${goal} ml denne rozdeleny do ${reminders} davok.`,
    `Dosha profil ${payload.dosha} a telesne signaly typu ${payload.bodySignal} upravuju odporucanie najma smerom k pravidelnosti a teplote tekutin.`,
    `Ak mas problemy s oblickami, srdcom alebo ti lekar obmedzil tekutiny, tento wellness odhad si treba individualne upravit.`
  ];

  return {
    age,
    bmi: Number(bmi.toFixed(1)),
    goal,
    reminders,
    elements,
    elementsLabel: `Vzduch ${elements.air}% · Ohen ${elements.fire}% · Voda ${elements.water}% · Zem ${elements.earth}% · Priestor ${elements.space}%`,
    notes
  };
}

function calculateElementProfile(payload) {
  let air = 20;
  let fire = 20;
  let water = 20;
  let earth = 20;
  let space = 20;

  if (payload.dosha === "vata") {
    air += 18;
    space += 16;
    water -= 10;
    earth -= 8;
  }

  if (payload.dosha === "pitta") {
    fire += 22;
    water += 8;
    air -= 6;
  }

  if (payload.dosha === "kapha") {
    water += 16;
    earth += 18;
    fire -= 8;
    space -= 6;
  }

  if (payload.bodySignal === "dry") {
    air += 8;
    water -= 5;
  }

  if (payload.bodySignal === "heated") {
    fire += 8;
    water += 4;
  }

  if (payload.bodySignal === "heavy") {
    earth += 6;
    water += 5;
    air -= 4;
  }

  if (payload.climate === "hot") {
    fire += 6;
    water += 3;
  }

  if (payload.climate === "cool") {
    earth += 4;
    water -= 2;
  }

  const birthInfluence = deriveBirthInfluence(payload.birthDate);
  air += birthInfluence.air;
  fire += birthInfluence.fire;
  water += birthInfluence.water;
  earth += birthInfluence.earth;
  space += birthInfluence.space;

  const total = air + fire + water + earth + space;

  return {
    air: Math.round((air / total) * 100),
    fire: Math.round((fire / total) * 100),
    water: Math.round((water / total) * 100),
    earth: Math.round((earth / total) * 100),
    space: Math.round((space / total) * 100)
  };
}

function deriveBirthInfluence(birthDate) {
  if (!birthDate) {
    return { air: 0, fire: 0, water: 0, earth: 0, space: 0 };
  }

  const date = new Date(birthDate);
  const month = date.getMonth() + 1;
  const day = date.getDate();
  const seed = (month * 31 + day) % 5;

  const patterns = [
    { air: 3, fire: 1, water: 0, earth: 1, space: 2 },
    { air: 1, fire: 3, water: 1, earth: 0, space: 2 },
    { air: 0, fire: 1, water: 3, earth: 2, space: 0 },
    { air: 1, fire: 0, water: 1, earth: 3, space: 1 },
    { air: 2, fire: 1, water: 1, earth: 0, space: 3 }
  ];

  return patterns[seed];
}

function calculateAge(birthDate) {
  if (!birthDate) return 30;
  const today = new Date();
  const dob = new Date(birthDate);
  let age = today.getFullYear() - dob.getFullYear();
  const monthDiff = today.getMonth() - dob.getMonth();

  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < dob.getDate())) {
    age -= 1;
  }

  return age;
}

function triggerReminder(timeLabel) {
  if ("Notification" in window && Notification.permission === "granted") {
    new Notification("Cas na vodu", {
      body: `Daj si pohar vody. Planovana pripomienka na ${timeLabel}.`,
      icon: "icon.svg"
    });
  } else {
    alert(`Cas na vodu. Planovana pripomienka na ${timeLabel}.`);
  }
}

function getNextReminderLabel() {
  const now = new Date();
  const next = state.schedule.find((timeLabel) => createTodayTime(timeLabel) > now);
  return next || "Dnes uz ziadna";
}

function createTodayTime(timeLabel) {
  const [hours, minutes] = timeLabel.split(":").map(Number);
  const date = new Date();
  date.setHours(hours, minutes, 0, 0);
  return date;
}

function clearReminderTimers() {
  if (!Array.isArray(state.reminderTimerIds)) {
    state.reminderTimerIds = [];
    return;
  }

  state.reminderTimerIds.forEach((id) => window.clearTimeout(id));
  state.reminderTimerIds = [];
}

function timeToMinutes(time) {
  const [hours, minutes] = time.split(":").map(Number);
  return hours * 60 + minutes;
}

function minutesToTime(minutes) {
  const normalized = ((minutes % 1440) + 1440) % 1440;
  const hours = String(Math.floor(normalized / 60)).padStart(2, "0");
  const mins = String(normalized % 60).padStart(2, "0");
  return `${hours}:${mins}`;
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}
