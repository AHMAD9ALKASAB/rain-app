namespace Rain.Domain.Entities
{
    public class ComparisonItem
    {
        public int Id { get; set; }
        public int ComparisonListId { get; set; }
        public ComparisonList? ComparisonList { get; set; }
        public int SupplierOfferId { get; set; }
        public SupplierOffer? SupplierOffer { get; set; }
    }
}
