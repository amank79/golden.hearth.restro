RESTAURANT POS - INSTALL, UPDATE, BACKUP AND RESTORE
====================================================

INSTALL (first time) or UPDATE (new version)
  1. Extract the zip to any folder (for example the Desktop).
  2. Double-click Install.cmd and click "Yes" when Windows asks for permission.
  3. When it says "installed and running", the billing screen opens.
     From then on, use the "Restaurant POS" icon on the desktop.
  4. First time only: double-click "Load menu.cmd" to add the dishes from menu.csv
     (open menu.csv in Excel to check or change prices first). Running it again skips dishes
     that are already on the menu. After that, change dishes in the app (Menu).
  Updating keeps all bills, menu and settings. A backup is saved before every update.

  Extra backup copy on a pen drive (recommended): open a Command Prompt in this folder and run
     Install.cmd -BackupFolder "E:\RestaurantPOS-Backup"
  (use the pen drive's letter). The pen drive can stay plugged in; if it is missing, that copy
  is skipped and the normal backup still happens.

OWNER PIN
  Set it in Restaurant POS > Settings > Owner PIN (4 to 6 digits). After that, changing the menu,
  giving a discount, cancelling a bill and changing settings ask for the PIN. Billing, payments,
  printing and the "available today" switch never need it. Once entered, the PIN stays unlocked
  for 5 minutes after the last change; tap "Owner unlocked - Lock" at the top to lock straight away.
  Forgot the PIN? Start menu > "Restaurant POS - reset owner PIN" (or "Reset PIN.cmd" here) and
  click "Yes". Only the PIN is removed; bills and menu are kept.

WHERE THINGS ARE
  App:       C:\Program Files\RestaurantPos
  Data:      C:\ProgramData\RestaurantPos\data\pos.db
  Backups:   C:\ProgramData\RestaurantPos\backups   (Start menu: "Restaurant POS backups")
  Settings:  C:\ProgramData\RestaurantPos\settings.json
  Logs:      C:\ProgramData\RestaurantPos\logs  and  backups\backup-log.txt

BACKUPS (automatic)
  - A backup is made once a day (soon after the laptop starts if it was off), and when the
    laptop shuts down if anything changed.
  - Each backup is checked after it is made. The last 30 days are kept (always at least 7).

ONLINE BACKUP WITH GOOGLE DRIVE (do this once, so the bills are safe even if the laptop is lost)
  1. Use the restaurant's own Gmail account, and turn on 2-Step Verification for it
     (myaccount.google.com > Security). The backups contain all bills.
  2. Open a Command Prompt in this folder and run
        Install.cmd -BackupFolder "C:\GoldenHearthBackup"
     (it keeps the data; it only adds this folder for an extra copy of every backup).
  3. Open Restaurant POS > Settings > "Back up now". The folder C:\GoldenHearthBackup now
     has a pos-....db file, and Settings lists it under "Extra copy".
  4. Install "Google Drive for desktop" (google.com/drive/download) and sign in with that Gmail.
  5. Google Drive icon (bottom right) > gear > Preferences > My Computer (or "Folders from your
     computer") > Add folder > choose C:\GoldenHearthBackup > "Sync with Google Drive" > Done > Save.
  6. Check it worked: on drive.google.com open "Computers" > this laptop > GoldenHearthBackup.
     The newest file should have today's date.
  Every new backup is uploaded by itself whenever there is internet. No internet for a few days
  is fine: the backups wait on the laptop and upload later.
  Once a week, check step 6. If the newest file there is old, open the Google Drive app and
  sign in again.
  The pen drive and Google Drive copies can both be used: run step 2 once for each folder.

LOGS (for fixing problems)
  - The app writes one log file per day: C:\ProgramData\RestaurantPos\logs\app-yyyyMMdd.log
    (errors, warnings, start/stop, backups, and one line per action with its time). Kept 60 days.
    Start menu: "Restaurant POS logs" opens the folder.
  - To send logs to the developer: Start menu > "Restaurant POS - collect logs for support"
    (or Collect logs.cmd here). It makes RestaurantPOS-logs-<date>.zip on the desktop.
    It contains NO bills or customer data. Send it on WhatsApp or email.
  - More detail for a while: in settings.json add  "Serilog": { "MinimumLevel": "Debug" }
    (remove it again afterwards; the app picks it up after a restart).

RESTORE (only if the data is lost or damaged)
  Double-click Restore.cmd, pick a backup from the list and type YES.
  The current data is kept as data\pos.before-restore-<time>.db, so nothing is thrown away.
  To restore from a pen drive:
     Restore.cmd -BackupFile "E:\RestaurantPOS-Backup\pos-2026-10-11_223015.db"
  From Google Drive (for example on a new laptop: run Install.cmd first):
     download the newest pos-....db from drive.google.com > Computers, then run
     Restore.cmd -BackupFile "%USERPROFILE%\Downloads\pos-2026-10-11_223015.db"

UNINSTALL
  Double-click Uninstall.cmd. The data and backups are KEPT.
  To delete everything as well:  Uninstall.cmd -RemoveData

IF THE BILLING SCREEN DOES NOT OPEN
  1. Restart the laptop (the app starts by itself with Windows).
  2. Still not working: run Install.cmd again (it keeps the data).
  3. Check the logs folder and Event Viewer > Windows Logs > Application.
