namespace Rain.Domain.Entities
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        public int SupplierOfferId { get; set; }
        public SupplierOffer? SupplierOffer { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
        // Commission settlement fields (for supplier revenue)
        public decimal CommissionRate { get; set; } // e.g., 0.02 for 2%
        public decimal CommissionAmount { get; set; }
        public decimal NetToSupplier { get; set; }
    }
}
