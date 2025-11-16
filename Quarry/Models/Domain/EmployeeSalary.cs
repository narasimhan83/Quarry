using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuarryManagementSystem.Utils;

namespace QuarryManagementSystem.Models.Domain
{
    public class EmployeeSalary
    {
        public int Id { get; set; }

        [Display(Name = "Payroll Run")]
        public int PayrollRunId { get; set; }

        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Basic Salary")]
        public decimal BasicSalary { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Housing Allowance")]
        public decimal HousingAllowance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Transport Allowance")]
        public decimal TransportAllowance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Other Allowances")]
        public decimal OtherAllowances { get; set; }

        [NotMapped]
        [Display(Name = "Gross Pay")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal GrossPay => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowances;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "PAYE Tax")]
        public decimal PAYE { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Pension Employee")]
        public decimal PensionEmployee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Pension Employer")]
        public decimal PensionEmployer { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "NHIS")]
        public decimal NHIS { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "NHF")]
        public decimal NHF { get; set; } // National Housing Fund

        [NotMapped]
        [Display(Name = "Total Deductions")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal TotalDeductions => PAYE + PensionEmployee + NHIS + NHF;

        [NotMapped]
        [Display(Name = "Net Pay")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal NetPay => GrossPay - TotalDeductions;

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Failed

        [StringLength(100)]
        [Display(Name = "Payment Reference")]
        public string? PaymentReference { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual PayrollRun PayrollRun { get; set; } = null!;
        public virtual Employee Employee { get; set; } = null!;

        // Helper methods
        public bool IsPaid()
        {
            return PaymentStatus == "Paid";
        }

        public bool IsPending()
        {
            return PaymentStatus == "Pending";
        }

        public bool IsFailed()
        {
            return PaymentStatus == "Failed";
        }

        public string GetPaymentStatusDisplay()
        {
            return PaymentStatus switch
            {
                "Pending" => "Pending",
                "Paid" => "Paid",
                "Failed" => "Failed",
                _ => "Unknown"
            };
        }

        // Nigerian payroll calculations
        public void CalculateDeductions()
        {
            // Calculate pension (8% of basic + housing)
            var pensionableSalary = BasicSalary + HousingAllowance;
            PensionEmployee = pensionableSalary * 0.08m;
            PensionEmployer = pensionableSalary * 0.10m;

            // Calculate NHIS (5% of basic - employer contribution)
            NHIS = BasicSalary * 0.05m;

            // Calculate NHF (2.5% of basic)
            NHF = BasicSalary * 0.025m;

            // Calculate PAYE based on annual gross
            var annualGross = GrossPay * 12;
            PAYE = PayrollRun.CalculatePAYE(annualGross) / 12; // Monthly PAYE
        }

        public decimal GetTotalEmployerContributions()
        {
            return PensionEmployer + NHIS; // Employer pays 10% pension + 5% NHIS
        }

        public string GetPayslipNumber()
        {
            return $"PSL/{Employee.EmployeeCode}/{PaymentDate:yyyy/MM}";
        }

        public string GetAmountInWords()
        {
            return NumberToWordsConverter.ConvertAmountToWords(NetPay);
        }

        public Dictionary<string, decimal> GetSalaryBreakdown()
        {
            return new Dictionary<string, decimal>
            {
                { "Basic Salary", BasicSalary },
                { "Housing Allowance", HousingAllowance },
                { "Transport Allowance", TransportAllowance },
                { "Other Allowances", OtherAllowances },
                { "Gross Pay", GrossPay },
                { "PAYE Tax", -PAYE },
                { "Pension (Employee)", -PensionEmployee },
                { "NHIS", -NHIS },
                { "NHF", -NHF },
                { "Total Deductions", -TotalDeductions },
                { "Net Pay", NetPay }
            };
        }

        // Validation methods
        public bool HasValidSalary()
        {
            return BasicSalary > 0 && GrossPay > 0 && NetPay > 0;
        }

        public bool HasReasonableDeductions()
        {
            // Total deductions should not exceed 50% of gross pay
            return TotalDeductions <= GrossPay * 0.5m;
        }

        public bool IsCompliantWithNigerianLaw()
        {
            // Check minimum wage compliance (₦30,000 monthly as of 2023)
            var minimumWage = 30000m;
            return NetPay >= minimumWage;
        }
    }
}