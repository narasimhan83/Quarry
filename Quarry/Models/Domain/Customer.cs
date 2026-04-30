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
        [Display(Name = "Customer Number")]
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
        public virtual ICollection<CustomerTruck> Trucks { get; set; } = new List<CustomerTruck>();
        public virtual ICollection<CustomerBank> BankAccounts { get; set; } = new List<CustomerBank>();
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

        /// <summary>
        /// Computed health-of-balance bucket used to drive UI color coding
        /// (Customer list, Weighbridge, Prepayment / Wallet screens).
        /// Thresholds:
        ///  - Critical: outstanding has reached or passed 90% of the credit
        ///    limit, OR limit is zero (no credit allowed and any balance is
        ///    by definition critical), OR available credit has gone negative.
        ///  - Warning : outstanding is between 50% (exclusive) and 90% of limit.
        ///  - Good    : everything else, including a brand-new customer with a
        ///    limit but no outstanding balance.
        /// Pure function of CreditLimit and OutstandingBalance — always agrees
        /// with whatever the latest invoice / payment has saved, no extra
        /// maintenance calls required.
        /// </summary>
        public BalanceStatus GetBalanceStatus()
        {
            // No credit allowed: anything owed is automatically Critical;
            // a zero balance with zero limit is still considered Good.
            if (CreditLimit <= 0m)
            {
                return OutstandingBalance > 0m ? BalanceStatus.Critical : BalanceStatus.Good;
            }

            // Available has gone negative — the customer is over their limit.
            if (OutstandingBalance > CreditLimit)
            {
                return BalanceStatus.Critical;
            }

            var ratio = OutstandingBalance / CreditLimit;
            if (ratio >= 0.90m) return BalanceStatus.Critical;
            if (ratio >  0.50m) return BalanceStatus.Warning;
            return BalanceStatus.Good;
        }

        /// <summary>
        /// CSS class hint matching the bucket from <see cref="GetBalanceStatus"/>.
        /// Views can do <c>class="@customer.GetBalanceStatusCssClass()"</c>.
        /// </summary>
        public string GetBalanceStatusCssClass()
        {
            return GetBalanceStatus() switch
            {
                BalanceStatus.Critical => "balance-critical",
                BalanceStatus.Warning  => "balance-warning",
                _                      => "balance-good"
            };
        }
    }

    /// <summary>Bucket used to color-code customer rows by balance health.</summary>
    public enum BalanceStatus
    {
        Good,
        Warning,
        Critical
    }
}
