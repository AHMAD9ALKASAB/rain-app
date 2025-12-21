namespace Rain.Domain.Entities
{
    public class Address
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Country { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
        public bool IsDefault { get; set; }
    }
}
