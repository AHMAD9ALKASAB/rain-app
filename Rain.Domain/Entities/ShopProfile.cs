namespace Rain.Domain.Entities
{
    public class ShopProfile
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public string? TaxNumber { get; set; }
        public string? Address { get; set; }
    }
}
