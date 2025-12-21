namespace Rain.Domain.Entities
{
    public class SupplierOffer
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public string SupplierId { get; set; } = string.Empty; // UserId of supplier
        public decimal Price { get; set; }
        public string Currency { get; set; } = "KWD";
        public int StockQty { get; set; }
        public int MinOrderQty { get; set; }
        public bool IsActive { get; set; } = true;
        public double AverageRating { get; set; }
        public int RatingsCount { get; set; }
    }
}
