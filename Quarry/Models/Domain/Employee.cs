using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee code is required")]
        [StringLength(20)]
        [Display(Name = "Employee Code")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(200)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression(@"^(\+234|0)[789][01]\d{9}$", ErrorMessage = "Invalid Nigerian phone number")]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(50)]
        [Display(Name = "Department")]
        public string? Department { get; set; } // Operations, Admin, Finance, Maintenance

        [StringLength(50)]
        [Display(Name = "Designation")]
        public string? Designation { get; set; } // Weighbridge Operator, Manager, Accountant, etc.

        [Display(Name = "Date of Joining")]
        [DataType(DataType.Date)]
        public DateTime? DateOfJoining { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Basic Salary")]
        [Range(0, 999999.99, ErrorMessage = "Basic salary must be between 0 and 999,999.99")]
        public decimal? BasicSalary { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Housing Allowance")]
        public decimal? HousingAllowance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Transport Allowance")]
        public decimal? TransportAllowance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Other Allowances")]
        public decimal? OtherAllowances { get; set; } = 0;

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
        public string? NHISNumber { get; set; } // Health Insurance

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Quarry")]
        public int? QuarryId { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Quarry? Quarry { get; set; }
        public virtual ICollection<EmployeeSalary> EmployeeSalaries { get; set; } = new List<EmployeeSalary>();

        // Helper properties
        [NotMapped]
        [Display(Name = "Gross Salary")]
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal GrossSalary => (BasicSalary ?? 0) + (HousingAllowance ?? 0) + (TransportAllowance ?? 0) + (OtherAllowances ?? 0);

        [NotMapped]
        [Display(Name = "Age")]
        public int? Age => DateOfBirth.HasValue ? DateTime.Now.Year - DateOfBirth.Value.Year : null;

        [NotMapped]
        [Display(Name = "Years of Service")]
        public int? YearsOfService => DateOfJoining.HasValue ? DateTime.Now.Year - DateOfJoining.Value.Year : null;

        // Common departments and designations
        public static readonly string[] Departments =
        {
            "Operations",
            "Admin",
            "Finance",
            "Maintenance",
            "Security",
            "Logistics"
        };

        public static readonly string[] Designations =
        {
            "Weighbridge Operator",
            "Manager",
            "Accountant",
            "Supervisor",
            "Driver",
            "Mechanic",
            "Security Officer",
            "Admin Officer",
            "Finance Officer",
            "Store Keeper"
        };

        public static readonly string[] NigerianBanks =
        {
            "Access Bank",
            "Citibank",
            "Diamond Bank",
            "Ecobank",
            "Fidelity Bank",
            "First Bank of Nigeria",
            "First City Monument Bank (FCMB)",
            "Globus Bank",
            "Guaranty Trust Bank (GTB)",
            "Heritage Bank",
            "Jaiz Bank",
            "Keystone Bank",
            "Polaris Bank",
            "Providus Bank",
            "Stanbic IBTC Bank",
            "Standard Chartered Bank",
            "Sterling Bank",
            "SunTrust Bank",
            "Union Bank of Nigeria",
            "United Bank for Africa (UBA)",
            "Unity Bank",
            "Wema Bank",
            "Zenith Bank"
        };

        public bool IsActive()
        {
            return Status == "Active";
        }

        public bool IsRetired()
        {
            return Status == "Retired";
        }

        public bool HasCompleteBankDetails()
        {
            return !string.IsNullOrEmpty(BankName) && !string.IsNullOrEmpty(BankAccountNumber);
        }

        public bool HasPensionDetails()
        {
            return !string.IsNullOrEmpty(PensionPIN);
        }

        public bool HasHealthInsurance()
        {
            return !string.IsNullOrEmpty(NHISNumber);
        }

        public string GetEmployeeStatus()
        {
            return Status switch
            {
                "Active" => "Active",
                "Inactive" => "Inactive",
                "Retired" => "Retired",
                "Terminated" => "Terminated",
                _ => "Unknown"
            };
        }

        public decimal CalculateMonthlyPensionContribution()
        {
            // Nigerian pension: 8% employee + 10% employer = 18% of basic + housing
            var pensionableSalary = (BasicSalary ?? 0) + (HousingAllowance ?? 0);
            return pensionableSalary * 0.08m; // Employee contribution
        }

        public decimal CalculateMonthlyNHISContribution()
        {
            // Nigerian NHIS: 5% of basic salary (employer contribution)
            return (BasicSalary ?? 0) * 0.05m;
        }
    }
}