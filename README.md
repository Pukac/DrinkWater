# DrinkWater Ritual

Personal hydration tracker with a native Android app written in .NET for Android
(`net10.0-android`) and the original browser/PWA prototype.

## Android app

- Daily water goal, quick logging, history and statistics.
- Hydration reminders throughout the day, once in the evening, or off.
- Independent custom reminders with a title, note, multiple times and selected weekdays.
- Local storage, CSV hydration export and JSON backup/restore.

Notifications use approximate alarms and can be delayed by Android power saving.
Recommendations are informational; the app does not prescribe medication or doses.

See [Android documentation](DrinkWater.Android/README.md) and
[hydration reminder settings](DrinkWater.Android/REMINDERS.md).

## Build and verification

Install the .NET 10 SDK and Android workload, then run:

```powershell
dotnet workload install android
dotnet build DrinkWater.Android/DrinkWater.Android.csproj
dotnet run --project verification/ReminderChecks/ReminderChecks.csproj
```

The debug APK is generated in `DrinkWater.Android/bin/Debug/net10.0-android/`.
Build output, local SDK/package caches and signing keys are excluded from Git.
UI, notification delivery, reboot recovery and backup file-picker behavior require
validation on an Android device.

## Original PWA prototype

The root `index.html`, `app.js`, `styles.css`, `manifest.json`, `sw.js` and
`icon.svg` contain the original browser prototype. Serve the directory through
localhost or HTTPS. Browser notifications depend on an active session;
the native Android app provides background reminder scheduling.
