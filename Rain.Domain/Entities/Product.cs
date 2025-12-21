using System.Collections.Generic;

namespace Rain.Domain.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SellerName { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public int? BrandId { get; set; }
        public Brand? Brand { get; set; }
        public string? Description { get; set; }
        public List<ProductImage> Images { get; set; } = new();
        public List<SupplierOffer> SupplierOffers { get; set; } = new();
    }
}
