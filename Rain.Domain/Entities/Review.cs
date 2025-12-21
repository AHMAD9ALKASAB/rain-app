using System;

namespace Rain.Domain.Entities
{
    public class Review
    {
        public int Id { get; set; }
        public int? SupplierOfferId { get; set; }
        public SupplierOffer? SupplierOffer { get; set; }
        public string? SupplierId { get; set; } // optional direct supplier target
        public int? OrderId { get; set; }
        public Order? Order { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public int Rating { get; set; } // 1-5
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
