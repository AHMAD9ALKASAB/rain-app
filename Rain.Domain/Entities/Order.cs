using System;
using System.Collections.Generic;
using Rain.Domain.Enums;

namespace Rain.Domain.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public string BuyerUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public decimal Total { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public int? ShippingAddressId { get; set; }
        public Address? ShippingAddress { get; set; }
    }
}
