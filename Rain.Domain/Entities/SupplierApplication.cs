using System;
using Rain.Domain.Enums;

namespace Rain.Domain.Entities
{
    public class SupplierApplication
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty; // صاحب الطلب

        // بيانات العرض والهوية
        public string DisplayName { get; set; } = string.Empty; // الاسم المعروض في الموقع
        public string FullName { get; set; } = string.Empty;    // الاسم الثلاثي
        public string CompanyOrShopName { get; set; } = string.Empty; // اسم الشركة/المحل
        public string PhoneWithCountry { get; set; } = string.Empty;  // الهاتف مع النداء
        public string Email { get; set; } = string.Empty;

        public string CompanyType { get; set; } = string.Empty; // نوع الشركة
        public string ProductScope { get; set; } = string.Empty; // ما هي المنتجات التي سيعرضها
        public string ResidenceLocation { get; set; } = string.Empty; // مكان الإقامة
        public string ExactLocation { get; set; } = string.Empty; // موقع الشركة/المحل بالتحديد

        // الخطة
        public SupplierPlanType PlanType { get; set; } = SupplierPlanType.Commission;

        // حالة الطلب
        public SupplierApplicationStatus Status { get; set; } = SupplierApplicationStatus.Pending;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewerUserId { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
