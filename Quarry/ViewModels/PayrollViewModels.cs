using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    public class PayrollListViewModel
    {
        public List<PayrollRun> PayrollRuns { get; set; } = new();
        
        [Display(Name = "Search Term")]
        public string? SearchTerm { get; set; }
        
        [Display(Name = "Status")]
        public string? SelectedStatus { get; set; }
        
        [Display(Name = "Year")]
        public int? SelectedYear { get; set; }
        
        [Display(Name = "Month")]
        public int? SelectedMonth { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        
        // Dropdown data
        public List<SelectListItem> Statuses { get; set; } = new();
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
        
        // Helper properties
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        
        public string? ErrorMessage { get; set; }
        
        // Summary statistics
        [Display(Name = "Total Payroll Runs")]
        public int TotalPayrollRuns => PayrollRuns.Count;
        
        [Display(Name = "Total Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal TotalGrossPay => PayrollRuns.Sum(pr => pr.GrossPay);
        
        [Display(Name = "Total Net Pay")]
        [DataType(DataType.Currency)]
        public decimal TotalNetPay => PayrollRuns.Sum(pr => pr.NetPay);
        
        [Display(Name = "Average Employees")]
        public double AverageEmployees => PayrollRuns.Any() ? PayrollRuns.Average(pr => pr.TotalEmployees) : 0;
    }

    public class PayrollCreateViewModel
    {
        [Required(ErrorMessage = "Payment month is required")]
        [Display(Name = "Payment Month")]
        [DataType(DataType.Date)]
        public DateTime PaymentMonth { get; set; }

        [Required(ErrorMessage = "Payment date is required")]
        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Draft";

        // Dropdown data
        public List<SelectListItem> Statuses { get; set; } = new();

        // Helper properties
        [Display(Name = "Expected Employees")]
        public int ExpectedEmployees { get; set; }

        [Display(Name = "Estimated Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal EstimatedGrossPay { get; set; }

        [Display(Name = "Estimated Net Pay")]
        [DataType(DataType.Currency)]
        public decimal EstimatedNetPay { get; set; }
    }

    public class PayrollEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Run Number")]
        public string RunNumber { get; set; } = string.Empty;

        [Display(Name = "Payment Month")]
        [DataType(DataType.Date)]
        public DateTime PaymentMonth { get; set; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal GrossPay { get; set; }

        [Display(Name = "Total PAYE")]
        [DataType(DataType.Currency)]
        public decimal TotalPAYE { get; set; }

        [Display(Name = "Total Pension")]
        [DataType(DataType.Currency)]
        public decimal TotalPension { get; set; }

        [Display(Name = "Total NHIS")]
        [DataType(DataType.Currency)]
        public decimal TotalNHIS { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Processed By")]
        public string? ProcessedBy { get; set; }

        [Display(Name = "Processed At")]
        [DataType(DataType.DateTime)]
        public DateTime? ProcessedAt { get; set; }

        [Display(Name = "Created Date")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; }
    }

    public class PayrollDetailsViewModel
    {
        public PayrollRun PayrollRun { get; set; } = new();

        [Display(Name = "Total Employer Contributions")]
        [DataType(DataType.Currency)]
        public decimal TotalEmployerContributions { get; set; }

        [Display(Name = "Bank Payment File Ready")]
        public bool BankPaymentFileReady { get; set; }

        [Display(Name = "Can Process")]
        public bool CanProcess { get; set; }

        [Display(Name = "Can Pay")]
        public bool CanPay { get; set; }

        [Display(Name = "Can Edit")]
        public bool CanEdit { get; set; }

        [Display(Name = "Compliance Status")]
        public string ComplianceStatus => GetComplianceStatus();

        private string GetComplianceStatus()
        {
            if (PayrollRun.Status == "Draft")
                return "Pending Processing";
            if (PayrollRun.Status == "Processed")
                return "Ready for Payment";
            if (PayrollRun.Status == "Paid")
                return "Payment Completed";
            return "Unknown Status";
        }
    }

    public class EmployeeSalaryViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Employee")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "Employee Code")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Display(Name = "Basic Salary")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Housing Allowance")]
        [DataType(DataType.Currency)]
        public decimal HousingAllowance { get; set; }

        [Display(Name = "Transport Allowance")]
        [DataType(DataType.Currency)]
        public decimal TransportAllowance { get; set; }

        [Display(Name = "Other Allowances")]
        [DataType(DataType.Currency)]
        public decimal OtherAllowances { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal GrossPay { get; set; }

        [Display(Name = "PAYE Tax")]
        [DataType(DataType.Currency)]
        public decimal PAYE { get; set; }

        [Display(Name = "Pension Employee")]
        [DataType(DataType.Currency)]
        public decimal PensionEmployee { get; set; }

        [Display(Name = "Pension Employer")]
        [DataType(DataType.Currency)]
        public decimal PensionEmployer { get; set; }

        [Display(Name = "NHIS")]
        [DataType(DataType.Currency)]
        public decimal NHIS { get; set; }

        [Display(Name = "NHF")]
        [DataType(DataType.Currency)]
        public decimal NHF { get; set; }

        [Display(Name = "Total Deductions")]
        [DataType(DataType.Currency)]
        public decimal TotalDeductions { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }

        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = string.Empty;

        [Display(Name = "Payment Reference")]
        public string? PaymentReference { get; set; }

        [Display(Name = "Bank Details")]
        public string BankDetails => GetBankDetails();

        private string GetBankDetails()
        {
            // This would come from the actual employee data
            return "Bank details not available";
        }
    }

    public class PayrollSummaryViewModel
    {
        [Display(Name = "Total Payroll Runs")]
        public int TotalPayrollRuns { get; set; }

        [Display(Name = "Total Employees Processed")]
        public int TotalEmployeesProcessed { get; set; }

        [Display(Name = "Total Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal TotalGrossPay { get; set; }

        [Display(Name = "Total Net Pay")]
        [DataType(DataType.Currency)]
        public decimal TotalNetPay { get; set; }

        [Display(Name = "Total PAYE Remitted")]
        [DataType(DataType.Currency)]
        public decimal TotalPAYERemitted { get; set; }

        [Display(Name = "Total Pension Remitted")]
        [DataType(DataType.Currency)]
        public decimal TotalPensionRemitted { get; set; }

        [Display(Name = "Total NHIS Remitted")]
        [DataType(DataType.Currency)]
        public decimal TotalNHISRemitted { get; set; }

        [Display(Name = "Average Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal AverageGrossPay => TotalEmployeesProcessed > 0 ? TotalGrossPay / TotalEmployeesProcessed : 0;

        [Display(Name = "Average Net Pay")]
        [DataType(DataType.Currency)]
        public decimal AverageNetPay => TotalEmployeesProcessed > 0 ? TotalNetPay / TotalEmployeesProcessed : 0;

        public List<MonthlyPayrollSummaryViewModel> MonthlySummaries { get; set; } = new();
    }

    public class MonthlyPayrollSummaryViewModel
    {
        [Display(Name = "Month")]
        public string Month { get; set; } = string.Empty;

        [Display(Name = "Year")]
        public int Year { get; set; }

        [Display(Name = "Employees")]
        public int EmployeeCount { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal GrossPay { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "PAYE")]
        [DataType(DataType.Currency)]
        public decimal PAYE { get; set; }

        [Display(Name = "Pension")]
        [DataType(DataType.Currency)]
        public decimal Pension { get; set; }

        [Display(Name = "NHIS")]
        [DataType(DataType.Currency)]
        public decimal NHIS { get; set; }
    }

    public class PayrollFilterViewModel
    {
        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Year")]
        public int? Year { get; set; }

        [Display(Name = "Month")]
        public int? Month { get; set; }

        [Display(Name = "Amount Range From")]
        [DataType(DataType.Currency)]
        public decimal? AmountRangeFrom { get; set; }

        [Display(Name = "Amount Range To")]
        [DataType(DataType.Currency)]
        public decimal? AmountRangeTo { get; set; }

        // Dropdown data
        public List<SelectListItem> Statuses { get; set; } = new();
        public List<SelectListItem> Years { get; set; } = new();
        public List<SelectListItem> Months { get; set; } = new();
    }

    public class ComplianceReportViewModel
    {
        [Display(Name = "PAYE Remittance")]
        [DataType(DataType.Currency)]
        public decimal PAYERemittance { get; set; }

        [Display(Name = "Pension Remittance")]
        [DataType(DataType.Currency)]
        public decimal PensionRemittance { get; set; }

        [Display(Name = "NHIS Remittance")]
        [DataType(DataType.Currency)]
        public decimal NHISRemittance { get; set; }

        [Display(Name = "Total Remittance")]
        [DataType(DataType.Currency)]
        public decimal TotalRemittance => PAYERemittance + PensionRemittance + NHISRemittance;

        [Display(Name = "Employee Count")]
        public int EmployeeCount { get; set; }

        [Display(Name = "Compliance Status")]
        public string ComplianceStatus { get; set; } = string.Empty;

        [Display(Name = "Remittance Deadline")]
        [DataType(DataType.Date)]
        public DateTime RemittanceDeadline { get; set; }

        [Display(Name = "Days Until Deadline")]
        public int DaysUntilDeadline => (RemittanceDeadline - DateTime.Now).Days;
    }
}