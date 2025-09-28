# SoftOne Go to WooCommerce ATUM Synchronization

Εφαρμογή PHP για αυτόματο συγχρονισμό προϊόντων και αποθεμάτων μεταξύ SoftOne Go ERP και WooCommerce ATUM Multi Inventory.

## Περιγραφή

Η εφαρμογή διαβάζει προϊόντα από διάφορα καταστήματα μέσω του SoftOne Go API και τα συγχρονίζει με το WooCommerce ATUM Multi Inventory plugin, διατηρώντας ενημερωμένα τα αποθέματα σε πραγματικό χρόνο.

## Χαρακτηριστικά

- ✅ **Multi-Store Support**: Υποστήριξη πολλαπλών καταστημάτων με ξεχωριστές ρυθμίσεις
- ✅ **Αυτόματος Συγχρονισμός**: Εκτέλεση κάθε 15 λεπτά μέσω cron job
- ✅ **Manual Sync**: Web interface για χειροκίνητη εκτέλεση
- ✅ **Product Matching**: Έξυπνη αντιστοίχιση προϊόντων βάσει SKU/Barcode
- ✅ **Batch Operations**: Αποδοτικές batch κλήσεις για καλύτερη απόδοση
- ✅ **Email Notifications**: Ειδοποιήσεις για νέα προϊόντα
- ✅ **Comprehensive Logging**: Detailed logs για debugging και monitoring
- ✅ **Error Handling**: Robust error handling με retry logic
- ✅ **Security**: Ασφαλής διαχείριση credentials και API keys

## Απαιτήσεις Συστήματος

- PHP 7.4 ή νεότερη έκδοση
- MySQL/MariaDB
- cURL extension
- Access σε SoftOne Go API
- WooCommerce με ATUM Multi Inventory plugin
- Write permissions για logs directory

## Εγκατάσταση

### 1. Clone το Repository
```bash
git clone <repository-url>
cd Soft1_To_Atum_Woocommerce
```

### 2. Ρυθμίσεις Directory Permissions
```bash
chmod 755 .
chmod 777 logs/
chmod 644 config/*.php
```

### 3. Database Setup
```bash
mysql -u username -p database_name < database/sync_logs.sql
```

### 4. Configuration
Αντιγράψτε και επεξεργαστείτε τα configuration files:

```bash
cp config/app.example.php config/app.php
cp config/stores.example.php config/stores.php
cp config/database.example.php config/database.php
```

### 5. Environment Variables
Δημιουργήστε `.env` αρχείο:
```env
# Database Configuration
DB_HOST=localhost
DB_NAME=your_database
DB_USER=your_username
DB_PASS=your_password

# Email Configuration
MAIL_HOST=smtp.example.com
MAIL_PORT=587
MAIL_USERNAME=your_email@example.com
MAIL_PASSWORD=your_password
MAIL_ENCRYPTION=tls
MAIL_FROM=noreply@example.com
MAIL_FROM_NAME="SoftOne ATUM Sync"
```

### 6. Cron Job Setup
```bash
php install_cron.php
```

## Δομή Αρχείων

```
/
├── config/
│   ├── app.php                 # Βασικές ρυθμίσεις εφαρμογής
│   ├── stores.php              # Ρυθμίσεις καταστημάτων
│   └── database.php            # Database configuration
├── src/
│   ├── SoftOneGoClient.php     # SoftOne Go API client
│   ├── WooCommerceClient.php   # WooCommerce API client
│   ├── ProductSynchronizer.php # Core sync logic
│   ├── EmailNotifier.php       # Email notifications
│   └── Logger.php              # Logging system
├── database/
│   └── sync_logs.sql           # Database schema
├── logs/                       # Log files
├── sync.php                    # Main synchronization script
├── manual_sync.php             # Web interface για manual sync
└── install_cron.php           # Cron installation script
```

## Configuration

### Email Configuration

Η εφαρμογή υποστηρίζει πλήρη email notification system. Οι ρυθμίσεις email βρίσκονται στο `config/app.php`:

```php
// Email Settings στο config/app.php
'email_enabled' => true,
'email_on_error' => true,
'email_on_new_products' => true,
'email_daily_summary' => true,
'email_recipients' => [
    'admin@example.com',
    'manager@example.com'
],

// SMTP Configuration
'smtp' => [
    'host' => $_ENV['MAIL_HOST'] ?? 'localhost',
    'port' => $_ENV['MAIL_PORT'] ?? 587,
    'username' => $_ENV['MAIL_USERNAME'] ?? '',
    'password' => $_ENV['MAIL_PASSWORD'] ?? '',
    'encryption' => $_ENV['MAIL_ENCRYPTION'] ?? 'tls', // tls, ssl, or null
    'from_email' => $_ENV['MAIL_FROM'] ?? 'noreply@example.com',
    'from_name' => $_ENV['MAIL_FROM_NAME'] ?? 'SoftOne ATUM Sync',
    'timeout' => 30,
    'auth' => true
],
```

**Τύποι Email Notifications:**
- **New Products**: Ειδοποίηση όταν δημιουργούνται νέα προϊόντα
- **Error Notifications**: Ειδοποίηση για σφάλματα sync
- **Daily Summary**: Ημερήσια αναφορά δραστηριότητας

**Για Production SMTP:**
Για production χρήση με authenticated SMTP, προτείνεται η χρήση PHPMailer:
```bash
composer require phpmailer/phpmailer
```

### Stores Configuration (config/stores.php)

```php
return [
    'store1' => [
        'name' => 'Κατάστημα 1',
        'softone_go' => [
            'base_url' => 'https://go.s1cloud.net/s1services',
            'app_id' => 'YOUR_APP_ID',
            'token' => 'YOUR_TOKEN',
            's1code' => 'YOUR_S1CODE'
        ],
        'atum' => [
            'location_id' => 870,
            'location_name' => 'store1_location'
        ],
        'woocommerce' => [
            'url' => 'https://yourstore.com',
            'consumer_key' => 'ck_xxxxx',
            'consumer_secret' => 'cs_xxxxx'
        ]
    ]
    // Προσθέστε περισσότερα καταστήματα εδώ
];
```

## Χρήση

### Αυτόματος Συγχρονισμός
Η εφαρμογή τρέχει αυτόματα κάθε 15 λεπτά μέσω cron job.

### Manual Sync
Επισκεφθείτε το `manual_sync.php` στον browser για χειροκίνητη εκτέλεση:
```
https://yourdomain.com/path/to/manual_sync.php
```

### Command Line
```bash
php sync.php
```

## Sync Logic

### 1. Ανάκτηση Δεδομένων
- Διαβάζει προϊόντα από SoftOne Go για κάθε κατάστημα
- Ανακτά τρέχοντα inventory από ATUM

### 2. Product Matching
- Αντιστοιχίζει προϊόντα βάσει SKU ή Barcode
- Δημιουργεί mapping table για γρήγορη αναζήτηση

### 3. Sync Operations
- **Same Quantity**: Δεν κάνει τίποτα
- **New Products**: Δημιουργεί νέα προϊόντα στο WooCommerce και ATUM
- **Missing Products**: Προσθέτει στο ATUM inventory
- **Different Quantity**: Ενημερώνει το ATUM inventory

### 4. Notifications
- Στέλνει email για νέα προϊόντα
- Logs όλες τις ενέργειες για audit trail

## API Endpoints

### SoftOne Go API
```
POST https://go.s1cloud.net/s1services/list/item
```

### WooCommerce ATUM API
```
GET  /wp-json/wc/v3/atum/inventories?location={location_id}
POST /wp-json/wc/v3/atum/inventories/batch
GET  /wp-json/wc/v3/products?sku={sku}
POST /wp-json/wc/v3/products
```

## Monitoring & Logs

### Log Files
- `logs/sync_YYYY-MM-DD.log` - Daily sync logs
- `logs/error_YYYY-MM-DD.log` - Error logs
- `logs/email_YYYY-MM-DD.log` - Email notification logs

### Database Tables
- `sync_logs` - Sync execution history
- `product_mappings` - Product ID mappings
- `sync_statistics` - Performance metrics

## Error Handling

- **API Timeouts**: Automatic retry με exponential backoff
- **Network Issues**: Graceful degradation
- **Data Validation**: Comprehensive input validation
- **Memory Limits**: Batch processing για μεγάλα datasets

## Performance Optimizations

- **Batch API Calls**: Μέχρι 100 items per request
- **Memory Management**: Efficient memory usage για μεγάλα datasets
- **Database Indexing**: Optimized για γρήγορες αναζητήσεις
- **Caching**: Redis cache για frequent data lookups

## Security

- **Environment Variables**: Sensitive data σε .env αρχείο
- **API Rate Limiting**: Respect για API limits
- **Input Sanitization**: Protection από injection attacks
- **File Permissions**: Proper file και directory permissions

## Troubleshooting

### Συχνά Προβλήματα

1. **Connection Timeout**
   - Ελέγξτε network connectivity
   - Verify API endpoints
   - Check firewall settings

2. **Authentication Errors**
   - Verify API credentials
   - Check token expiration
   - Confirm API permissions

3. **Memory Issues**
   - Increase PHP memory limit
   - Use batch processing
   - Monitor log files για memory usage

4. **Cron Job Issues**
   - Check cron log: `tail -f /var/log/cron`
   - Verify PHP path στο cron command
   - Check file permissions

### Debug Mode
Enable debug mode στο `config/app.php`:
```php
'debug' => true,
'log_level' => 'DEBUG'
```

## Support & Maintenance

### Backup
- Regular backup του database
- Backup των configuration files
- Monitor log file sizes

### Updates
- Regular check για WooCommerce/ATUM updates
- Test sync functionality μετά από updates
- Monitor API changes

## License

Αυτή η εφαρμογή είναι ιδιόκτητη και προορίζεται για εσωτερική χρήση.

## Changelog

### v1.0.0 (2025-09-27)
- Initial release
- Basic sync functionality
- Multi-store support
- Email notifications
- Comprehensive logging

---

Για περισσότερες πληροφορίες ή support, επικοινωνήστε με το development team.