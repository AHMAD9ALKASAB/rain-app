namespace Rain.Domain.Entities
{
    public class SupplierProfile
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CRN { get; set; }
        public string? Address { get; set; }
    }
}
