using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QuarryManagementSystem.Models.Domain
{
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Company name is required")]
        [StringLength(100)]
        [Display(Name = "Company Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "RC Number")]
        public string? RCNumber { get; set; }

        [StringLength(255)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [StringLength(100)]
        [Display(Name = "Local Government Area")]
        public string? LGA { get; set; }

        [StringLength(50)]
        public string? State { get; set; }

        [StringLength(50)]
        [Display(Name = "Mining License Number")]
        public string? MiningLicenseNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Contact Person")]
        public string? ContactPerson { get; set; }

        [RegularExpression(@"^(?:\+234|0)[7-9]\d{9}$", ErrorMessage = "Invalid Nigerian phone number format. Use +234XXXXXXXXXX or 0XXXXXXXXXX")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(20)]
        [Display(Name = "Tax Identification Number")]
        public string? TIN { get; set; }

        [StringLength(20)]
        [Display(Name = "Bank Verification Number")]
        public string? BVN { get; set; }

        [StringLength(500)]
        [Display(Name = "Billing Address")]
        public string? BillingAddress { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Credit Limit")]
        [Range(0, 999999999.99, ErrorMessage = "Credit limit must be between 0 and 999,999,999.99")]
        public decimal CreditLimit { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Outstanding Balance")]
        public decimal OutstandingBalance { get; set; } = 0;

        [Display(Name = "Available Credit")]
        public decimal AvailableCredit { get; set; }

        [StringLength(20)]
        public string? Status { get; set; } = "Active";

        // ---------- New: classification ----------
        [Display(Name = "Customer Type")]
        public int? CustomerTypeId { get; set; }

        [Display(Name = "VAT Type")]
        public int? VatTypeId { get; set; }

        // ---------- New: rebate ----------
        [Display(Name = "Has Rebate")]
        public bool HasRebate { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rebate Amount")]
        [Range(0, 999999999.99)]
        public decimal? RebateAmount { get; set; }

        // ---------- New: transport ----------
        [Display(Name = "Transport Required")]
        public bool TransportRequired { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Transport Amount")]
        [Range(0, 999999999.99)]
        public decimal? TransportAmount { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CustomerType? CustomerType { get; set; }
        public virtual VatType? VatType { get; set; }
        public virtual ICollection<CustomerMaterialPrice> MaterialPrices { get; set; } = new List<CustomerMaterialPrice>();
        public virtual ICollection<WeighmentTransaction> WeighmentTransactions { get; set; } = new List<WeighmentTransaction>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

        // Helper methods
        public bool HasExceededCreditLimit(decimal additionalAmount)
        {
            return (OutstandingBalance + additionalAmount) > CreditLimit;
        }

        public bool IsActiveCustomer()
        {
            return Status == "Active";
        }

        public string GetFullAddress()
        {
            var addressParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Location))
                addressParts.Add(Location);
            if (!string.IsNullOrWhiteSpace(LGA))
                addressParts.Add(LGA);
            if (!string.IsNullOrWhiteSpace(State))
                addressParts.Add(State);

            return string.Join(", ", addressParts);
        }

        public void UpdateAvailableCredit()
        {
            AvailableCredit = CreditLimit - OutstandingBalance;
        }
    }
}
