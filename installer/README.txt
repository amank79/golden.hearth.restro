RESTAURANT POS - INSTALL, UPDATE, BACKUP AND RESTORE
====================================================

INSTALL (first time) or UPDATE (new version)
  1. Extract the zip to any folder (for example the Desktop).
  2. Double-click Install.cmd and click "Yes" when Windows asks for permission.
  3. When it says "installed and running", the billing screen opens.
     From then on, use the "Restaurant POS" icon on the desktop.
  Updating keeps all bills, menu and settings. A backup is saved before every update.

  Extra backup copy on a pen drive (recommended): open a Command Prompt in this folder and run
     Install.cmd -BackupFolder "E:\RestaurantPOS-Backup"
  (use the pen drive's letter). The pen drive can stay plugged in; if it is missing, that copy
  is skipped and the normal backup still happens.

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
  - Google Drive: in Google Drive for desktop, choose Preferences > My Computer > Add folder,
    and add C:\ProgramData\RestaurantPos\backups. The backups are then also copied online.

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

UNINSTALL
  Double-click Uninstall.cmd. The data and backups are KEPT.
  To delete everything as well:  Uninstall.cmd -RemoveData

IF THE BILLING SCREEN DOES NOT OPEN
  1. Restart the laptop (the app starts by itself with Windows).
  2. Still not working: run Install.cmd again (it keeps the data).
  3. Check the logs folder and Event Viewer > Windows Logs > Application.
