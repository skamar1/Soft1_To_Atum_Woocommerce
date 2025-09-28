using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Data.Services;

public interface ISoftOneGoClient
{
    Task<List<SoftOneProduct>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<SoftOneProduct?> GetProductByIdAsync(string productId, CancellationToken cancellationToken = default);
}