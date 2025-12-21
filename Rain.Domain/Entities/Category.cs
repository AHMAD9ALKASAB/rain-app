using System.Collections.Generic;

namespace Rain.Domain.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public Category? Parent { get; set; }
        public List<Category> Children { get; set; } = new();
        public List<Product> Products { get; set; } = new();
    }
}
