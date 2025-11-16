using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class WeighmentTransaction
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Transaction Number")]
        public string TransactionNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // Vehicle Information
        [Required(ErrorMessage = "Vehicle registration is required")]
        [StringLength(20)]
        [RegularExpression(@"^[A-Z]{3}-\d{3}-[A-Z]{3}$|^[A-Z]{2}\d{2}[A-Z]{2}\d{3}$", ErrorMessage = "Invalid Nigerian vehicle registration format")]
        [Display(Name = "Vehicle Registration")]
        public string VehicleRegNumber { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Driver Name")]
        public string? DriverName { get; set; }

        [StringLength(20)]
        [RegularExpression(@"^(?:\+234|0)[\s-]?[7-9]\d{2}[\s-]?\d{3}[\s-]?\d{4}$", ErrorMessage = "Invalid Nigerian phone number")]
        [Display(Name = "Driver Phone")]
        public string? DriverPhone { get; set; }

        // Foreign Keys
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Weighbridge")]
        public int? WeighbridgeId { get; set; }

        [Display(Name = "Order")]
        public int? OrderId { get; set; }

        [Display(Name = "Material")]
        public int? MaterialId { get; set; }

        // Material and Pricing
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Price Per Unit")]
        [Range(0.01, 999999.99, ErrorMessage = "Price per unit must be between 0.01 and 999,999.99")]
        public decimal? PricePerUnit { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "VAT Rate (%)")]
        [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
        public decimal VatRate { get; set; } = 7.5m;

        // Weight Information
        [Required(ErrorMessage = "Gross weight is required")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Gross Weight (kg)")]
        [Range(0.01, 999999.99, ErrorMessage = "Gross weight must be between 0.01 and 999,999.99")]
        public decimal GrossWeight { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Tare Weight (kg)")]
        [Range(0, 999999.99, ErrorMessage = "Tare weight must be between 0 and 999,999.99")]
        public decimal? TareWeight { get; set; }

        [Display(Name = "Net Weight (kg)")]
        public decimal NetWeight { get; set; }

        [Required]
        [StringLength(10)]
        [Display(Name = "Weight Unit")]
        public string WeightUnit { get; set; } = "kg";

        // Financial Calculations
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Subtotal")]
        public decimal? SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "VAT Amount")]
        public decimal? VatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount")]
        public decimal? TotalAmount { get; set; }

        // Timing
        [Display(Name = "Entry Time")]
        public DateTime? EntryTime { get; set; }

        [Display(Name = "Exit Time")]
        public DateTime? ExitTime { get; set; }

        // Additional Information
        [StringLength(20)]
        [Display(Name = "Transaction Type")]
        public string TransactionType { get; set; } = "Sales";

        [StringLength(20)]
        public string Status { get; set; } = "InProgress";

        [StringLength(50)]
        [Display(Name = "Challan Number")]
        public string? ChallanNumber { get; set; }

        [Display(Name = "Is Invoiced")]
        public bool IsInvoiced { get; set; } = false;

        // Audit Trail
        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        [Display(Name = "Modified By")]
        public string? ModifiedBy { get; set; }

        [Display(Name = "Modified Date")]
        public DateTime? ModifiedAt { get; set; }

        [Display(Name = "Invoice ID")]
        public int? InvoiceId { get; set; }

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual Weighbridge? Weighbridge { get; set; }
        public virtual Material? Material { get; set; }
        public virtual ApplicationUser? OperatorUser { get; set; }
        public virtual Invoice? Invoice { get; set; }

        // Helper methods
        public void CalculateFinancials()
        {
            // Calculate NetWeight first
            NetWeight = GrossWeight - (TareWeight ?? 0);
            
            if (NetWeight > 0 && PricePerUnit.HasValue)
            {
                decimal quantityInTons = WeightUnit == "kg" ? NetWeight / 1000 : NetWeight;
                SubTotal = quantityInTons * PricePerUnit.Value;
                VatAmount = SubTotal * (VatRate / 100);
                TotalAmount = SubTotal + VatAmount;
            }
        }

        public bool IsCompleted()
        {
            return Status == "Completed";
        }

        public bool IsInProgress()
        {
            return Status == "InProgress";
        }

        public TimeSpan? GetDuration()
        {
            if (EntryTime.HasValue && ExitTime.HasValue)
            {
                return ExitTime.Value - EntryTime.Value;
            }
            return null;
        }

        public string GetTransactionStatus()
        {
            if (IsInvoiced)
                return "Invoiced";
            if (IsCompleted())
                return "Completed";
            if (IsInProgress())
                return "In Progress";
            return Status;
        }
    }
}