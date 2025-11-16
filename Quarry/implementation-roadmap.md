# Nigerian Quarry Management System - Implementation Roadmap

## Phase 1: Foundation Setup (Week 1-2)

### 1.1 Project Setup
```bash
# Create ASP.NET Core MVC Project
dotnet new mvc -n QuarryManagementSystem -f net8.0
cd QuarryManagementSystem

# Add Required Packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
dotnet add package FluentValidation.AspNetCore
dotnet add package ClosedXML // For Excel reports
dotnet add package QuestPDF // For PDF generation
```

### 1.2 Database Connection Setup
**File**: `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=87.252.104.168;Database=QuarryManagementNG;User Id=sa;Password=*26malar19baby;TrustServerCertificate=true;",
    "BackupConnection": "Server=localhost;Database=QuarryManagementNG;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApplicationSettings": {
    "CompanyName": "Nigerian Quarry Management System",
    "VatRate": 7.5,
    "CurrencySymbol": "₦",
    "DateFormat": "dd/MM/yyyy",
    "TimeZone": "W. Central Africa Standard Time"
  }
}
```

### 1.3 Entity Framework Models
**File**: `Models/Domain/Customer.cs`
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Company name is required")]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(20)]
        [Display(Name = "RC Number")]
        public string RCNumber { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [StringLength(255)]
        public string Location { get; set; }

        [Required(ErrorMessage = "LGA is required")]
        [StringLength(100)]
        public string LGA { get; set; }

        [Required(ErrorMessage = "State is required")]
        [StringLength(50)]
        public string State { get; set; }

        [StringLength(50)]
        [Display(Name = "Mining License Number")]
        public string MiningLicenseNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        [Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"^(\+234|0)[789][01]\d{9}$", ErrorMessage = "Invalid Nigerian phone number")]
        [StringLength(20)]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(20)]
        [Display(Name = "Tax Identification Number")]
        public string TIN { get; set; }

        [StringLength(20)]
        [Display(Name = "Bank Verification Number")]
        public string BVN { get; set; }

        [StringLength(500)]
        [Display(Name = "Billing Address")]
        public string BillingAddress { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Credit Limit")]
        public decimal CreditLimit { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Outstanding Balance")]
        public decimal OutstandingBalance { get; set; } = 0;

        [StringLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<WeighmentTransaction> WeighmentTransactions { get; set; }
        public virtual ICollection<Invoice> Invoices { get; set; }
    }
}
```

### 1.4 Database Context
**File**: `Data/ApplicationDbContext.cs`
```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Domain Models
        public DbSet<ChartOfAccounts> ChartOfAccounts { get; set; }
        public DbSet<Quarry> Quarries { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Weighbridge> Weighbridges { get; set; }
        public DbSet<WeighmentTransaction> WeighmentTransactions { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<PayrollRun> PayrollRuns { get; set; }
        public DbSet<EmployeeSalary> EmployeeSalaries { get; set; }
        public DbSet<StockYard> StockYards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Nigerian-specific decimal precision
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // Configure relationships and constraints
            modelBuilder.Entity<WeighmentTransaction>()
                .HasOne(w => w.Customer)
                .WithMany(c => c.WeighmentTransactions)
                .HasForeignKey(w => w.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add unique constraints
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Phone)
                .IsUnique();

            modelBuilder.Entity<WeighmentTransaction>()
                .HasIndex(w => w.TransactionNumber)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();
        }
    }
}
```

## Phase 2: Core Functionality (Week 3-4)

### 2.1 Repository Pattern Setup
**File**: `Repositories/ICustomerRepository.cs`
```csharp
using System.Linq.Expressions;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Repositories
{
    public interface ICustomerRepository
    {
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<Customer> GetByIdAsync(int id);
        Task<Customer> GetByPhoneAsync(string phone);
        Task<IEnumerable<Customer>> SearchAsync(Expression<Func<Customer, bool>> predicate);
        Task<Customer> CreateAsync(Customer customer);
        Task<Customer> UpdateAsync(Customer customer);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<decimal> GetOutstandingBalanceAsync(int customerId);
        Task<bool> HasExceededCreditLimitAsync(int customerId, decimal additionalAmount);
    }
}
```

### 2.2 Customer Controller
**File**: `Controllers/CustomerController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QuarryManagementSystem.Models;
using QuarryManagementSystem.Repositories;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class CustomerController : Controller
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            ICustomerRepository customerRepository,
            IMapper mapper,
            ILogger<CustomerController> logger)
        {
            _customerRepository = customerRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string searchTerm, string state, string status)
        {
            var customers = await _customerRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                customers = customers.Where(c => 
                    c.Name.Contains(searchTerm) || 
                    c.ContactPerson.Contains(searchTerm) ||
                    c.Phone.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(state))
            {
                customers = customers.Where(c => c.State == state);
            }

            if (!string.IsNullOrEmpty(status))
            {
                customers = customers.Where(c => c.Status == status);
            }

            return View(customers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.States = GetNigerianStates();
            ViewBag.LGAs = GetNigerianLGAs();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var customer = _mapper.Map<Customer>(model);
                    customer.CreatedAt = DateTime.Now;
                    
                    await _customerRepository.CreateAsync(customer);
                    
                    TempData["Success"] = "Customer created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating customer");
                    ModelState.AddModelError("", "An error occurred while creating the customer.");
                }
            }

            ViewBag.States = GetNigerianStates();
            ViewBag.LGAs = GetNigerianLGAs();
            return View(model);
        }

        private List<SelectListItem> GetNigerianStates()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Lagos", Text = "Lagos" },
                new SelectListItem { Value = "Ogun", Text = "Ogun" },
                new SelectListItem { Value = "Oyo", Text = "Oyo" },
                new SelectListItem { Value = "Osun", Text = "Osun" },
                new SelectListItem { Value = "Ondo", Text = "Ondo" },
                new SelectListItem { Value = "Ekiti", Text = "Ekiti" },
                new SelectListItem { Value = "Kogi", Text = "Kogi" },
                new SelectListItem { Value = "Kwara", Text = "Kwara" },
                new SelectListItem { Value = "Niger", Text = "Niger" },
                new SelectListItem { Value = "FCT", Text = "Federal Capital Territory" }
                // Add all 36 states + FCT
            };
        }
    }
}
```

## Phase 3: Advanced Features (Week 5-6)

### 3.1 Weighment Transaction with Real-time Features
- SignalR integration for weighbridge
- Automatic calculations
- Credit limit validation
- Stock availability check

### 3.2 Invoice Generation
- PDF generation with Nigerian format
- Email sending to customers
- Payment tracking
- VAT reporting

### 3.3 Payroll Processing
- Nigerian tax calculation
- Payslip generation
- Bank payment file generation
- Compliance reporting

## Phase 4: Reporting & Analytics (Week 7-8)

### 4.1 Financial Reports
- Trial Balance
- Profit & Loss
- Balance Sheet
- Cash Flow Statement

### 4.2 Operational Reports
- Daily weighment summary
- Customer-wise sales
- Material-wise sales
- Stock reports

### 4.3 Tax Reports
- VAT input/output summary
- PAYE monthly returns
- Pension contribution summary
- NHF returns

## Deployment & Testing Strategy

### Testing Approach
1. **Unit Tests**: Business logic validation
2. **Integration Tests**: Database operations
3. **UI Tests**: Form validation and user workflows
4. **Performance Tests**: Report generation and bulk operations

### Deployment Checklist
- [ ] Database backup procedures
- [ ] User training materials
- [ ] Security audit
- [ ] Performance optimization
- [ ] Documentation completion
- [ ] Support procedures

## Success Metrics
1. **User Adoption**: 90% of quarry operations digitized
2. **Accuracy**: 99.9% data accuracy in weighments
3. **Efficiency**: 50% reduction in invoice processing time
4. **Compliance**: 100% tax compliance accuracy
5. **Uptime**: 99.5% system availability

## Risk Mitigation
1. **Data Backup**: Daily automated backups
2. **Offline Capability**: Critical forms work without internet
3. **User Training**: Comprehensive training program
4. **Support System**: 24/7 technical support
5. **Disaster Recovery**: Full system recovery within 4 hours