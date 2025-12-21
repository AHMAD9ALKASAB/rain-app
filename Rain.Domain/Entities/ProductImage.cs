namespace Rain.Domain.Entities
{
    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public string Url { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
