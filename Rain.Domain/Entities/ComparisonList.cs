using System.Collections.Generic;

namespace Rain.Domain.Entities
{
    public class ComparisonList
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? SessionKey { get; set; }
        public List<ComparisonItem> Items { get; set; } = new();
    }
}
