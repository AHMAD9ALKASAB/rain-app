using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Rain.Infrastructure.Files;

namespace Rain.Web.Services
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly IWebHostEnvironment _env;
        public LocalFileStorage(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveProductImageAsync(int productId, IFormFile file, CancellationToken ct = default)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "products", productId.ToString());
            Directory.CreateDirectory(uploadsRoot);
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{System.Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }
            var relative = $"/uploads/products/{productId}/{fileName}";
            return relative.Replace("\\", "/");
        }

        public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return Task.FromResult(false);
            var path = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), path);
            if (File.Exists(full))
            {
                File.Delete(full);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
