using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class PayrollRun
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Run Number")]
        public string RunNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Payment Month")]
        [DataType(DataType.Date)]
        public DateTime PaymentMonth { get; set; }

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Gross Pay")]
        public decimal GrossPay { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total PAYE")]
        public decimal TotalPAYE { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Pension")]
        public decimal TotalPension { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total NHIS")]
        public decimal TotalNHIS { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Net Pay")]
        public decimal NetPay { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft"; // Draft, Processed, Paid

        [StringLength(450)]
        [Display(Name = "Processed By")]
        public string? ProcessedBy { get; set; }

        [Display(Name = "Processed At")]
        public DateTime? ProcessedAt { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<EmployeeSalary> EmployeeSalaries { get; set; } = new List<EmployeeSalary>();
        public virtual ApplicationUser? ProcessedByUser { get; set; }

        // Helper methods
        public bool IsDraft()
        {
            return Status == "Draft";
        }

        public bool IsProcessed()
        {
            return Status == "Processed";
        }

        public bool IsPaid()
        {
            return Status == "Paid";
        }

        public bool CanEdit()
        {
            return Status == "Draft";
        }

        public bool CanProcess()
        {
            return Status == "Draft" && TotalEmployees > 0;
        }

        public bool CanPay()
        {
            return Status == "Processed";
        }

        public string GetStatusDisplay()
        {
            return Status switch
            {
                "Draft" => "Draft",
                "Processed" => "Processed",
                "Paid" => "Paid",
                _ => "Unknown"
            };
        }

        public decimal GetTotalDeductions()
        {
            return TotalPAYE + TotalPension + TotalNHIS;
        }

        public decimal GetTotalEmployerContributions()
        {
            // Employer pays 10% pension + 5% NHIS
            return (TotalPension * 1.25m) + (TotalNHIS * 2); // Approximate employer contributions
        }

        // Nigerian payroll constants
        public static readonly decimal[] TaxBrackets = 
        {
            300000,   // First ₦300,000 at 7%
            300000,   // Next ₦300,000 at 11%
            500000,   // Next ₦500,000 at 15%
            500000,   // Next ₦500,000 at 19%
            1600000,  // Next ₦1,600,000 at 21%
            3200000   // Above ₦3,200,000 at 24%
        };

        public static readonly decimal[] TaxRates = 
        {
            0.07m,    // 7%
            0.11m,    // 11%
            0.15m,    // 15%
            0.19m,    // 19%
            0.21m,    // 21%
            0.24m     // 24%
        };

        public static decimal CalculatePAYE(decimal annualGross)
        {
            decimal tax = 0;
            decimal remaining = annualGross;

            for (int i = 0; i < TaxBrackets.Length; i++)
            {
                if (remaining <= 0) break;

                decimal taxableAmount = Math.Min(remaining, TaxBrackets[i]);
                tax += taxableAmount * TaxRates[i];
                remaining -= taxableAmount;
            }

            return tax;
        }
    }
}