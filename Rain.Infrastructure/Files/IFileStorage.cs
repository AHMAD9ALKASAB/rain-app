using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Rain.Infrastructure.Files
{
    public interface IFileStorage
    {
        Task<string> SaveProductImageAsync(int productId, IFormFile file, CancellationToken ct = default);
        Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default);
    }
}
