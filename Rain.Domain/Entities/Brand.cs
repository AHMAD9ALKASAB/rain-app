using System.Collections.Generic;

namespace Rain.Domain.Entities
{
    public class Brand
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new();
    }
}
