using Microsoft.EntityFrameworkCore;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Data.Services;

public class StoreSettingsService
{
    private readonly SyncDbContext _context;

    public StoreSettingsService(SyncDbContext context)
    {
        _context = context;
    }

    public async Task<List<StoreSettings>> GetAllStoresAsync()
    {
        return await _context.StoreSettings
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<List<StoreSettings>> GetEnabledStoresAsync()
    {
        return await _context.StoreSettings
            .Where(s => s.StoreEnabled)
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<StoreSettings?> GetStoreByIdAsync(int id)
    {
        return await _context.StoreSettings
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<StoreSettings> GetDefaultStoreAsync()
    {
        // Get the first enabled store, or create default if none exists
        var store = await _context.StoreSettings
            .Where(s => s.StoreEnabled)
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync();

        if (store == null)
        {
            // Create default store
            store = new StoreSettings
            {
                StoreName = "Κατάστημα Κέντρο",
                StoreEnabled = true,
                SoftOneGoBaseUrl = "https://go.s1cloud.net/s1services",
                SoftOneGoAppId = "703",
                SoftOneGoFilters = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999",
                AtumLocationId = 870,
                AtumLocationName = "store1_location",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.StoreSettings.Add(store);
            await _context.SaveChangesAsync();
        }

        return store;
    }

    public async Task<StoreSettings> CreateStoreAsync(StoreSettings storeSettings)
    {
        storeSettings.CreatedAt = DateTime.UtcNow;
        storeSettings.UpdatedAt = DateTime.UtcNow;

        _context.StoreSettings.Add(storeSettings);
        await _context.SaveChangesAsync();

        return storeSettings;
    }

    public async Task<StoreSettings?> UpdateStoreAsync(int id, StoreSettings updatedStore)
    {
        var existingStore = await _context.StoreSettings.FindAsync(id);
        if (existingStore == null)
        {
            return null;
        }

        existingStore.StoreName = updatedStore.StoreName;
        existingStore.StoreEnabled = updatedStore.StoreEnabled;
        existingStore.SoftOneGoBaseUrl = updatedStore.SoftOneGoBaseUrl;
        existingStore.SoftOneGoAppId = updatedStore.SoftOneGoAppId;
        existingStore.SoftOneGoToken = updatedStore.SoftOneGoToken;
        existingStore.SoftOneGoS1Code = updatedStore.SoftOneGoS1Code;
        existingStore.SoftOneGoFilters = updatedStore.SoftOneGoFilters;
        existingStore.AtumLocationId = updatedStore.AtumLocationId;
        existingStore.AtumLocationName = updatedStore.AtumLocationName;
        existingStore.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return existingStore;
    }

    public async Task<bool> DeleteStoreAsync(int id)
    {
        var store = await _context.StoreSettings.FindAsync(id);
        if (store == null)
        {
            return false;
        }

        // Check if there are products associated with this store
        var hasProducts = await _context.Products.AnyAsync(p => p.StoreSettingsId == id);
        if (hasProducts)
        {
            // Don't delete store with products - just disable it
            store.StoreEnabled = false;
            await _context.SaveChangesAsync();
            return true;
        }

        _context.StoreSettings.Remove(store);
        await _context.SaveChangesAsync();

        return true;
    }
}
