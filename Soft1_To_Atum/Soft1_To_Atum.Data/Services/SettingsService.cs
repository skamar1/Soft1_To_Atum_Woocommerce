using Microsoft.EntityFrameworkCore;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Data.Services;

public class SettingsService
{
    private readonly SyncDbContext _context;

    public SettingsService(SyncDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings> GetAppSettingsAsync()
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings == null)
        {
            // Create default settings if they don't exist
            settings = new AppSettings { Id = 1 };
            _context.AppSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    public async Task UpdateAppSettingsAsync(AppSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _context.AppSettings.Update(settings);
        await _context.SaveChangesAsync();
    }
}