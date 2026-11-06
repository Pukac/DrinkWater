# DrinkWater Ritual Android

Natívna Android appka v `.NET for Android` (`net10.0-android`).

## Co vie

- denny ciel vody a priebezny progres
- rychle pridavanie 150 / 250 / 330 / 500 ml
- manualny pitny rezim
- odporucany rezim podla vahy, vysky, veku, treningu, potenia, klimy, kofeinu a alkoholu
- transparentny rozpis faktorov, z ktorych sa ciel sklada
- bezpecnostny opatrny rezim pri obmedzeni tekutin, oblickach a srdci
- oddelenie `science` vrstvy od wellness vrstvy `vata/pitta/kapha`
- historia pitia po dnoch
- 7 a 30 dnove statistiky: priemer, aktivne dni, splneny ciel, konzistentnost
- lokalne ukladanie nastaveni
- planovanie Android upozorneni cez `AlarmManager`

## Build

Projekt sa builduje prikazom:

```powershell
dotnet build DrinkWater.Android.csproj -f net10.0-android
```

## APK

Po builde je debug APK tu:

- `C:\Users\jan.janda\Documents\DrinkWater\DrinkWater.Android\bin\Debug\net10.0-android\com.companyname.DrinkWater.Android-Signed.apk`

## Poznamka

## Rychly zapis z upozornenia

Rozbal upozornenie a stlac "Vypite 250 ml". Akcia zapise objem aj aktualny cas,
zavrie upozornenie a preplanuje dalsiu kontrolu. Tuknutie na telo otvori appku.
Jedno upozornenie moze zapisat iba jednu davku. Starsie nahradene akcie sa ignoruju.

Overenie na telefone:

1. Povol upozornenia a zapni pripomienky v dennom casovom rozsahu.
2. Po prijati upozornenia pouzi "Vypite 250 ml" bez otvorenia appky.
3. Otvor appku a skontroluj narast o 250 ml aj historiu.
4. Pri dosiahnuti ciela skontroluj, ze dalsia kontrola je az na dalsi den.

Alarmy su priblizne; usporny rezim Androidu ich moze oneskorit.

## Moje pripomienky

Na hlavnej obrazovke otvor "Moje pripomienky". Kazda ma nazov, volitelnu
poznamku, 1 az 16 roznych casov HH:mm oddelenych ciarkou a vybrane dni tyzdna.
Mozes ju upravit, vypnut alebo odstranit. Zoznam ma limit 100 poloziek.
Funguje nezavisle od vody, denneho ciela a vypnutia pitnych pripomienok.
Upozornenie otvori ich zoznam; nezapisuje vodu ani potvrdenie uzitia.

Aj tieto alarmy su priblizne. Po restarte, aktualizacii a zmene casu sa
obnovuju. Oneskorena pripomienka z predosleho dna sa uz nezobrazi. Cas,
ktory pri jarnom posune hodin neexistuje, sa presunie na prvu platnu minutu.
Pri jesennom opakovani hodiny sa pouzije standardny cas (jedno upozornenie).

JSON zaloha verzie 2 obsahuje aj vlastne pripomienky a ich poznamky.
Obnova nahradi oba zoznamy a vsetky pripomienky vypne. Zaloha verzie 1
sa da nacitat, ale kedze vlastne pripomienky neobsahuje, ich zoznam vymaze.
CSV nadalej obsahuje iba pitnu historiu.

Overenie na telefone pred beznym pouzivanim:

1. Nastav rannu a vecernu pripomienku, vybrane dni a dve pripomienky v rovnaky cas.
2. Vypni pitne pripomienky a over, ze vlastne nadalej prichadzaju.
3. Skus upravu, vypnutie a odstranenie pred dorucenim aj po nom.
4. Over zamknuty telefon, usporny rezim, restart a zmenu casoveho pasma.
5. Zakaz a znovu povol upozornenia, otvor appku a over obnovenie planu.
6. Exportuj/obnov novu aj staru JSON zalohu a over nahradenie a vypnutie pripomienok.

Logicke kontroly: `dotnet run --project ../verification/ReminderChecks/ReminderChecks.csproj`.

Ak budeme chciet dalsi krok, viem doplnit:

- lepsi dizajn pre Material 3
- historiu pitia po dnoch
- auto-obnovenie upozorneni po restarte telefonu
- export nastaveni a statistik
