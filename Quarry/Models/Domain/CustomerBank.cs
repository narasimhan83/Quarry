using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// A bank account registered against a specific customer. Used for payment
    /// reconciliation, remittance details on invoices, and KYC. Customers may
    /// have multiple accounts (e.g. one per bank); only AccountNumber and
    /// BankName are required, the rest are optional.
    /// </summary>
    public class CustomerBank
    {
        public int CustomerBankId { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Account number is required")]
        [StringLength(50)]
        [Display(Name = "Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bank name is required")]
        [StringLength(150)]
        [Display(Name = "Bank Name")]
        public string BankName { get; set; } = string.Empty;

        [StringLength(255)]
        [Display(Name = "Bank Address")]
        public string? BankAddress { get; set; }

        [StringLength(150)]
        [Display(Name = "Bank Branch")]
        public string? BankBranch { get; set; }

        [StringLength(20)]
        [Display(Name = "SWIFT Code")]
        public string? BankSwiftCode { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual Customer? Customer { get; set; }
    }
}
