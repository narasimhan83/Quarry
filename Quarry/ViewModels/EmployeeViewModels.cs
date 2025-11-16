using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.ViewModels
{
    public class EmployeeListViewModel
    {
        public List<Employee> Employees { get; set; } = new();
        
        [Display(Name = "Search Term")]
        public string? SearchTerm { get; set; }
        
        [Display(Name = "Department")]
        public string? SelectedDepartment { get; set; }
        
        [Display(Name = "Status")]
        public string? SelectedStatus { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        
        // Dropdown data
        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        
        // Helper properties
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        
        public string? ErrorMessage { get; set; }
        
        // Summary statistics
        [Display(Name = "Total Employees")]
        public int TotalEmployees => Employees.Count;
        
        [Display(Name = "Active Employees")]
        public int ActiveEmployees => Employees.Count(e => e.Status == "Active");
        
        [Display(Name = "Average Salary")]
        [DataType(DataType.Currency)]
        public decimal AverageSalary => Employees.Any() ? Employees.Average(e => e.GrossSalary) : 0;
    }

    public class EmployeeCreateViewModel
    {
        [Required(ErrorMessage = "Employee code is required")]
        [StringLength(20)]
        [Display(Name = "Employee Code")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(200)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression(@"^(?:\+234|0)[7-9]\d{9}$", ErrorMessage = "Invalid Nigerian phone number format. Use +234XXXXXXXXXX or 0XXXXXXXXXX")]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [StringLength(50)]
        [Display(Name = "Department")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Designation is required")]
        [StringLength(50)]
        [Display(Name = "Designation")]
        public string Designation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of joining is required")]
        [Display(Name = "Date of Joining")]
        [DataType(DataType.Date)]
        public DateTime DateOfJoining { get; set; }

        [Required(ErrorMessage = "Basic salary is required")]
        [Display(Name = "Basic Salary")]
        [Range(30000, 999999999.99, ErrorMessage = "Basic salary must be at least ₦30,000 (minimum wage)")]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Housing Allowance")]
        public decimal? HousingAllowance { get; set; }

        [Display(Name = "Transport Allowance")]
        public decimal? TransportAllowance { get; set; }

        [Display(Name = "Other Allowances")]
        public decimal OtherAllowances { get; set; } = 0;

        [StringLength(20)]
        [Display(Name = "Pension PIN")]
        public string? PensionPIN { get; set; }

        [StringLength(100)]
        [Display(Name = "Bank Name")]
        public string? BankName { get; set; }

        [StringLength(20)]
        [Display(Name = "Bank Account Number")]
        public string? BankAccountNumber { get; set; }

        [StringLength(20)]
        [Display(Name = "NHIS Number")]
        public string? NHISNumber { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Quarry")]
        public int? QuarryId { get; set; }

        // Dropdown data
        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> Designations { get; set; } = new();
        public List<SelectListItem> Banks { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
        public List<SelectListItem> Quarries { get; set; } = new();

        // Calculated properties
        [NotMapped]
        [Display(Name = "Gross Salary")]
        [DataType(DataType.Currency)]
        public decimal GrossSalary => BasicSalary + (HousingAllowance ?? 0) + (TransportAllowance ?? 0) + OtherAllowances;

        [NotMapped]
        [Display(Name = "Monthly Pension")]
        [DataType(DataType.Currency)]
        public decimal MonthlyPension => (BasicSalary + (HousingAllowance ?? 0)) * 0.08m;

        [NotMapped]
        [Display(Name = "Monthly NHIS")]
        [DataType(DataType.Currency)]
        public decimal MonthlyNHIS => BasicSalary * 0.05m;

        [NotMapped]
        [Display(Name = "VAT Rate")]
        public decimal VatRate { get; set; } = 7.5m;
    }

    public class EmployeeEditViewModel : EmployeeCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Created Date")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Updated Date")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedAt { get; set; }
    }

    public class EmployeeDetailsViewModel
    {
        public Employee Employee { get; set; } = new();

        public List<EmployeeSalary> RecentSalaries { get; set; } = new();

        [Display(Name = "Total Payroll Runs")]
        public int TotalPayrollRuns { get; set; }

        [Display(Name = "Last Payment Date")]
        [DataType(DataType.Date)]
        public DateTime? LastPaymentDate { get; set; }

        [Display(Name = "Age")]
        public int? Age { get; set; }

        [Display(Name = "Years of Service")]
        public int? YearsOfService { get; set; }

        [Display(Name = "Monthly Pension Contribution")]
        [DataType(DataType.Currency)]
        public decimal MonthlyPensionContribution { get; set; }

        [Display(Name = "Monthly NHIS Contribution")]
        [DataType(DataType.Currency)]
        public decimal MonthlyNHISContribution { get; set; }
    }

    public class EmployeePayrollDetailsViewModel
    {
        public Employee Employee { get; set; } = new();

        [Display(Name = "Monthly Gross Salary")]
        [DataType(DataType.Currency)]
        public decimal MonthlyGross { get; set; }

        [Display(Name = "Monthly Pension")]
        [DataType(DataType.Currency)]
        public decimal MonthlyPension { get; set; }

        [Display(Name = "Monthly NHIS")]
        [DataType(DataType.Currency)]
        public decimal MonthlyNHIS { get; set; }

        [Display(Name = "Annual Gross Salary")]
        [DataType(DataType.Currency)]
        public decimal AnnualGross { get; set; }

        public TaxCalculationViewModel TaxCalculation { get; set; } = new();

        public List<EmployeeSalary> RecentPayslips { get; set; } = new();
    }

    public class TaxCalculationViewModel
    {
        [Display(Name = "Annual Gross")]
        [DataType(DataType.Currency)]
        public decimal AnnualGross { get; set; }

        [Display(Name = "Annual Tax (PAYE)")]
        [DataType(DataType.Currency)]
        public decimal AnnualTax { get; set; }

        [Display(Name = "Monthly Tax")]
        [DataType(DataType.Currency)]
        public decimal MonthlyTax { get; set; }

        [Display(Name = "Effective Tax Rate")]
        [DisplayFormat(DataFormatString = "{0:P2}")]
        public double EffectiveTaxRate { get; set; }
    }

    public class PayslipViewModel
    {
        public Employee Employee { get; set; } = new();

        public EmployeeSalary EmployeeSalary { get; set; } = new();

        public CompanyDetailsViewModel CompanyDetails { get; set; } = new();

        [Display(Name = "Amount in Words")]
        public string AmountInWords { get; set; } = string.Empty;

        [Display(Name = "Payslip Number")]
        public string PayslipNumber => EmployeeSalary.GetPayslipNumber();

        [Display(Name = "Is Sample")]
        public bool IsSample { get; set; }

        [Display(Name = "Print Date")]
        [DataType(DataType.DateTime)]
        public DateTime PrintDate { get; set; } = DateTime.Now;

        [Display(Name = "Salary Breakdown")]
        public Dictionary<string, decimal> SalaryBreakdown => EmployeeSalary.GetSalaryBreakdown();
    }

    public class EmployeeFilterViewModel
    {
        [Display(Name = "Department")]
        public string? Department { get; set; }

        [Display(Name = "Status")]
        public string? Status { get; set; }

        [Display(Name = "Salary Range From")]
        [DataType(DataType.Currency)]
        public decimal? SalaryRangeFrom { get; set; }

        [Display(Name = "Salary Range To")]
        [DataType(DataType.Currency)]
        public decimal? SalaryRangeTo { get; set; }

        [Display(Name = "Date of Joining From")]
        [DataType(DataType.Date)]
        public DateTime? DateOfJoiningFrom { get; set; }

        [Display(Name = "Date of Joining To")]
        [DataType(DataType.Date)]
        public DateTime? DateOfJoiningTo { get; set; }

        // Dropdown data
        public List<SelectListItem> Departments { get; set; } = new();
        public List<SelectListItem> Statuses { get; set; } = new();
    }
}