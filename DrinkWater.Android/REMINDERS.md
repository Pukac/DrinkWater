# Nastavenie upozorneni

V casti Upozornenia vyber rezim a stlac Ulozit nastavenie upozorneni.

- Pripominat kazdy pohar: nastav zaciatok, koniec a pocet pripomienok (1 az 16). Zapis vody odlozi dalsiu pripomienku. Existujuca frekvencia a minimalny interval 45 minut zostavaju zachovane.
- Raz denne vecer: nastav cas HH:mm, predvolene 20:00. Upozornenie ukaze dnesne mnozstvo a zvysok do ciela; najviac raz za miestny kalendarny den.
- Vypnute: zrusi naplanovane pripomienky aj zobrazene upozornenie.

Pri splnenom cieli sa dalsie upozornenia v ten den nezobrazuju. Android moze dorucenie odlozit. Vecerne upozornenie oneskorene do dalsieho dna sa nezobrazi.

Starsie ulozene nastavenia zostavaju zachovane: zapnute pripomienky pouzivaju priebezny rezim, vypnute zostavaju vypnute. JSON zaloha obsahuje aj vecerny cas; po obnove zostavaju pripomienky vypnute, ako doteraz.

Kontrola logiky vecerneho planu:

    dotnet build verification/ReminderChecks/ReminderChecks.csproj
    dotnet verification/ReminderChecks/bin/Debug/net10.0/ReminderChecks.dll

Na telefone treba overit prepnutie vsetkych troch rezimov, ulozenie a opatovne otvorenie aplikacie, dorucenie na pozadi, odlozenie po zapise vody, potlacenie pri splnenom cieli, zmenu casu, restart telefonu a odmietnute povolenie upozorneni.
