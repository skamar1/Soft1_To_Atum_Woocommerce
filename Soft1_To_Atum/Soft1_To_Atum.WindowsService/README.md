# Soft1 To ATUM Windows Service

Αυτό το Windows Service εκτελεί αυτόματο συγχρονισμό μεταξύ SoftOne Go, WooCommerce και ATUM Multi-Inventory.

## Εγκατάσταση

### Προαπαιτούμενα
- .NET 9 Runtime
- Windows Operating System
- Administrator rights

### Βήματα Εγκατάστασης

1. **Export Settings από Blazor UI**
   - Ανοίξτε το Blazor application
   - Πηγαίνετε στο Settings page
   - Πατήστε "Export for Windows Service"
   - Αποθηκεύστε το αρχείο `appsettings.Soft1ToAtum.YYYYMMDD-HHMMSS.json`

2. **Deploy Service Files**
   ```powershell
   # Δημιουργία φακέλου για το service
   mkdir C:\Services\Soft1ToAtumSync

   # Αντιγραφή των published files στον φάκελο
   dotnet publish -c Release -o C:\Services\Soft1ToAtumSync

   # Αντιγραφή του configuration file
   copy appsettings.Soft1ToAtum.*.json C:\Services\Soft1ToAtumSync\appsettings.Soft1ToAtum.json
   ```

3. **Εγκατάσταση ως Windows Service**
   ```powershell
   # Ανοίξτε PowerShell ως Administrator
   sc.exe create Soft1ToAtumSyncService binPath= "C:\Services\Soft1ToAtumSync\Soft1_To_Atum.WindowsService.exe" start= auto

   # Εναλλακτικά, χρήση New-Service cmdlet
   New-Service -Name "Soft1ToAtumSyncService" `
               -BinaryPathName "C:\Services\Soft1ToAtumSync\Soft1_To_Atum.WindowsService.exe" `
               -DisplayName "Soft1 To ATUM Sync Service" `
               -Description "Automatic synchronization service for SoftOne Go, WooCommerce and ATUM Multi-Inventory" `
               -StartupType Automatic
   ```

4. **Εκκίνηση Service**
   ```powershell
   Start-Service Soft1ToAtumSyncService
   ```

## Configuration

Το service διαβάζει τις ρυθμίσεις από το `appsettings.Soft1ToAtum.json` file:

```json
{
  "softOne": {
    "baseUrl": "https://your-softone-url",
    "token": "your-token",
    "appId": "your-app-id",
    "s1Code": "your-company-code"
  },
  "wooCommerce": {
    "consumerKey": "ck_...",
    "consumerSecret": "cs_..."
  },
  "atum": {
    "locationId": 123,
    "locationName": "store1_location"
  },
  "syncSettings": {
    "intervalMinutes": 10,
    "enableAutoSync": true,
    "batchSize": 50
  }
}
```

## Λειτουργία

Το service εκτελεί τα ακόλουθα βήματα κάθε X λεπτά (configurableμεapo το intervalMinutes):

1. **Διάβασμα από SoftOne**: Ανακτά προϊόντα από το SoftOne Go API
2. **Αποθήκευση στη βάση**: Ενημερώνει την τοπική SQLite database
3. **WooCommerce Matching**: Ταιριάζει προϊόντα με SKU ή δημιουργεί νέα
4. **ATUM Creation**: Δημιουργεί inventory records στο ATUM
5. **ATUM Update**: Ενημερώνει τις ποσότητες

## Διαχείριση

### Έλεγχος κατάστασης
```powershell
Get-Service Soft1ToAtumSyncService
```

### Διακοπή
```powershell
Stop-Service Soft1ToAtumSyncService
```

### Επανεκκίνηση
```powershell
Restart-Service Soft1ToAtumSyncService
```

### Απεγκατάσταση
```powershell
Stop-Service Soft1ToAtumSyncService
sc.exe delete Soft1ToAtumSyncService
```

## Logs

Τα logs του service καταγράφονται στο:
- **Windows Event Log**: Application log, Source: "Soft1ToAtumSyncService"
- **Console** (αν τρέχει σε development mode)

Για να δείτε τα logs:
```powershell
Get-EventLog -LogName Application -Source "Soft1ToAtumSyncService" -Newest 50
```

## Troubleshooting

### Service δεν ξεκινάει
- Ελέγξτε ότι το configuration file υπάρχει
- Ελέγξτε τα Windows Event Logs για errors
- Βεβαιωθείτε ότι το .NET 9 Runtime είναι εγκατεστημένο

### Sync errors
- Ελέγξτε τις ρυθμίσεις στο configuration file
- Βεβαιωθείτε ότι τα API credentials είναι σωστά
- Ελέγξτε network connectivity προς τα external APIs

## Development

Για development testing:
```powershell
cd Soft1_To_Atum.WindowsService
dotnet run
```

Το service θα τρέξει σε console mode και θα εμφανίζει logs στην κονσόλα.
