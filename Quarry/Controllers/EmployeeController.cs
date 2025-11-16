using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.Utils;
using QuarryManagementSystem.ViewModels;
using System.Linq.Expressions;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(ApplicationDbContext context, ILogger<EmployeeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Employee
        public async Task<IActionResult> Index(string searchTerm, string department, string status, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.Employees
                    .Include(e => e.Quarry)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(e => 
                        e.FullName.Contains(searchTerm) || 
                        e.EmployeeCode.Contains(searchTerm) ||
                        e.Phone.Contains(searchTerm) ||
                        e.Email.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(department))
                {
                    query = query.Where(e => e.Department == department);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(e => e.Status == status);
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var employees = await query
                    .OrderBy(e => e.FullName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new EmployeeListViewModel
                {
                    Employees = employees,
                    SearchTerm = searchTerm,
                    SelectedDepartment = department,
                    SelectedStatus = status,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    Departments = GetDepartments(),
                    Statuses = GetEmployeeStatuses()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee list");
                return View(new EmployeeListViewModel
                {
                    ErrorMessage = "An error occurred while loading employees. Please try again."
                });
            }
        }

        // GET: Employee/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.Quarry)
                .Include(e => e.EmployeeSalaries)
                .ThenInclude(es => es.PayrollRun)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var viewModel = new EmployeeDetailsViewModel
            {
                Employee = employee,
                RecentSalaries = employee.EmployeeSalaries
                    .OrderByDescending(es => es.PaymentDate)
                    .Take(12)
                    .ToList(),
                TotalPayrollRuns = employee.EmployeeSalaries.Count,
                LastPaymentDate = employee.EmployeeSalaries.Any() ? 
                    employee.EmployeeSalaries.Max(es => es.PaymentDate) : (DateTime?)null,
                Age = employee.Age,
                YearsOfService = employee.YearsOfService,
                MonthlyPensionContribution = employee.CalculateMonthlyPensionContribution(),
                MonthlyNHISContribution = employee.CalculateMonthlyNHISContribution()
            };

            return View(viewModel);
        }

        // GET: Employee/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new EmployeeCreateViewModel
            {
                DateOfJoining = DateTime.Now,
                Status = "Active",
                BasicSalary = 30000, // Minimum wage
                HousingAllowance = 0,
                TransportAllowance = 0,
                OtherAllowances = 0,
                VatRate = 7.5m
            };

            await PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: Employee/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeCreateViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if employee code already exists
                    if (await _context.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode))
                    {
                        ModelState.AddModelError("EmployeeCode", "An employee with this code already exists.");
                    }
                    else
                    {
                        var employee = new Employee
                        {
                            EmployeeCode = model.EmployeeCode,
                            FullName = model.FullName,
                            Phone = model.Phone,
                            Email = model.Email,
                            DateOfBirth = model.DateOfBirth,
                            Department = model.Department,
                            Designation = model.Designation,
                            DateOfJoining = model.DateOfJoining,
                            BasicSalary = model.BasicSalary,
                            HousingAllowance = model.HousingAllowance,
                            TransportAllowance = model.TransportAllowance,
                            OtherAllowances = model.OtherAllowances,
                            PensionPIN = model.PensionPIN,
                            BankName = model.BankName,
                            BankAccountNumber = model.BankAccountNumber,
                            NHISNumber = model.NHISNumber,
                            Status = model.Status,
                            QuarryId = model.QuarryId,
                            CreatedAt = DateTime.Now
                        };

                        _context.Add(employee);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = $"Employee {employee.FullName} created successfully.";
                        _logger.LogInformation("Employee {EmployeeCode} - {FullName} created by user {UserName}", 
                            employee.EmployeeCode, employee.FullName, User.Identity?.Name);
                        
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee");
                ModelState.AddModelError("", "An error occurred while creating the employee. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Employee/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            var model = new EmployeeEditViewModel
            {
                Id = employee.Id,
                EmployeeCode = employee.EmployeeCode,
                FullName = employee.FullName,
                Phone = employee.Phone,
                Email = employee.Email,
                DateOfBirth = employee.DateOfBirth ?? DateTime.Now,
                Department = employee.Department ?? "",
                Designation = employee.Designation ?? "",
                DateOfJoining = employee.DateOfJoining ?? DateTime.Now,
                BasicSalary = employee.BasicSalary ?? 0,
                HousingAllowance = employee.HousingAllowance ?? 0,
                TransportAllowance = employee.TransportAllowance ?? 0,
                OtherAllowances = employee.OtherAllowances ?? 0,
                PensionPIN = employee.PensionPIN,
                BankName = employee.BankName,
                BankAccountNumber = employee.BankAccountNumber,
                NHISNumber = employee.NHISNumber,
                Status = employee.Status,
                QuarryId = employee.QuarryId,
                CreatedAt = employee.CreatedAt
            };

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Employee/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check if employee code already exists for another employee
                    if (await _context.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode && e.Id != model.Id))
                    {
                        ModelState.AddModelError("EmployeeCode", "Another employee with this code already exists.");
                    }
                    else
                    {
                        try
                        {
                            var employee = await _context.Employees.FindAsync(id);
                            if (employee == null)
                            {
                                return NotFound();
                            }

                            // Update employee details
                            employee.EmployeeCode = model.EmployeeCode;
                            employee.FullName = model.FullName;
                            employee.Phone = model.Phone;
                            employee.Email = model.Email;
                            employee.DateOfBirth = model.DateOfBirth;
                            employee.Department = model.Department;
                            employee.Designation = model.Designation;
                            employee.DateOfJoining = model.DateOfJoining;
                            employee.BasicSalary = model.BasicSalary;
                            employee.HousingAllowance = model.HousingAllowance;
                            employee.TransportAllowance = model.TransportAllowance;
                            employee.OtherAllowances = model.OtherAllowances;
                            employee.PensionPIN = model.PensionPIN;
                            employee.BankName = model.BankName;
                            employee.BankAccountNumber = model.BankAccountNumber;
                            employee.NHISNumber = model.NHISNumber;
                            employee.Status = model.Status;
                            employee.QuarryId = model.QuarryId;
                            employee.UpdatedAt = DateTime.Now;

                            await _context.SaveChangesAsync();

                            TempData["Success"] = $"Employee {employee.FullName} updated successfully.";
                            _logger.LogInformation("Employee {EmployeeCode} - {FullName} updated by user {UserName}", 
                                employee.EmployeeCode, employee.FullName, User.Identity?.Name);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            if (!EmployeeExists(model.Id))
                            {
                                return NotFound();
                            }
                            else
                            {
                                throw;
                            }
                        }
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee");
                ModelState.AddModelError("", "An error occurred while updating the employee. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Employee/PayrollDetails/5
        public async Task<IActionResult> PayrollDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.EmployeeSalaries)
                .ThenInclude(es => es.PayrollRun)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var viewModel = new EmployeePayrollDetailsViewModel
            {
                Employee = employee,
                MonthlyGross = employee.GrossSalary,
                MonthlyPension = employee.CalculateMonthlyPensionContribution(),
                MonthlyNHIS = employee.CalculateMonthlyNHISContribution(),
                AnnualGross = employee.GrossSalary * 12,
                TaxCalculation = CalculateAnnualTax(employee.GrossSalary * 12),
                RecentPayslips = employee.EmployeeSalaries
                    .OrderByDescending(es => es.PaymentDate)
                    .Take(6)
                    .ToList()
            };

            return View(viewModel);
        }

        // GET: Employee/GeneratePayslip/5
        public async Task<IActionResult> GeneratePayslip(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            // Get latest payroll run or create a sample one
            var latestSalary = await _context.EmployeeSalaries
                .Include(es => es.PayrollRun)
                .Where(es => es.EmployeeId == id)
                .OrderByDescending(es => es.PaymentDate)
                .FirstOrDefaultAsync();

            var viewModel = new PayslipViewModel
            {
                Employee = employee,
                EmployeeSalary = latestSalary ?? CreateSamplePayslip(employee),
                CompanyDetails = GetCompanyDetails(),
                AmountInWords = latestSalary != null ?
                    NumberToWordsConverter.ConvertAmountToWords(latestSalary.NetPay) :
                    "Sample Payslip",
                IsSample = latestSalary == null
            };

            return View(viewModel);
        }

        // AJAX: Get employee salary breakdown
        [HttpGet]
        public async Task<JsonResult> GetSalaryBreakdown(int employeeId)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(employeeId);
                if (employee == null)
                {
                    return Json(new { success = false, message = "Employee not found" });
                }

                var breakdown = new
                {
                    basicSalary = employee.BasicSalary,
                    housingAllowance = employee.HousingAllowance,
                    transportAllowance = employee.TransportAllowance,
                    otherAllowances = employee.OtherAllowances,
                    grossSalary = employee.GrossSalary,
                    monthlyPension = employee.CalculateMonthlyPensionContribution(),
                    monthlyNHIS = employee.CalculateMonthlyNHISContribution(),
                    annualGross = employee.GrossSalary * 12
                };

                return Json(new { success = true, data = breakdown });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting salary breakdown for employee {EmployeeId}", employeeId);
                return Json(new { success = false, message = "Error retrieving salary breakdown" });
            }
        }

        // Helper methods
        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }

        private List<SelectListItem> GetDepartments()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Department --" },
                new SelectListItem { Value = "Operations", Text = "Operations" },
                new SelectListItem { Value = "Admin", Text = "Admin" },
                new SelectListItem { Value = "Finance", Text = "Finance" },
                new SelectListItem { Value = "Maintenance", Text = "Maintenance" },
                new SelectListItem { Value = "Security", Text = "Security" },
                new SelectListItem { Value = "Logistics", Text = "Logistics" }
            };
        }

        private List<SelectListItem> GetEmployeeStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Status --" },
                new SelectListItem { Value = "Active", Text = "Active" },
                new SelectListItem { Value = "Inactive", Text = "Inactive" },
                new SelectListItem { Value = "Retired", Text = "Retired" },
                new SelectListItem { Value = "Terminated", Text = "Terminated" }
            };
        }

        private List<SelectListItem> GetDesignations()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Designation --" },
                new SelectListItem { Value = "Weighbridge Operator", Text = "Weighbridge Operator" },
                new SelectListItem { Value = "Manager", Text = "Manager" },
                new SelectListItem { Value = "Accountant", Text = "Accountant" },
                new SelectListItem { Value = "Supervisor", Text = "Supervisor" },
                new SelectListItem { Value = "Driver", Text = "Driver" },
                new SelectListItem { Value = "Mechanic", Text = "Mechanic" },
                new SelectListItem { Value = "Security Officer", Text = "Security Officer" },
                new SelectListItem { Value = "Admin Officer", Text = "Admin Officer" },
                new SelectListItem { Value = "Finance Officer", Text = "Finance Officer" },
                new SelectListItem { Value = "Store Keeper", Text = "Store Keeper" }
            };
        }

        private List<SelectListItem> GetNigerianBanks()
        {
            return Employee.NigerianBanks.Select(bank => new SelectListItem
            {
                Value = bank,
                Text = bank
            }).ToList();
        }

        private async Task PopulateDropdowns(EmployeeCreateViewModel model)
        {
            model.Departments = GetDepartments();
            model.Designations = GetDesignations();
            model.Banks = GetNigerianBanks();
            model.Statuses = GetEmployeeStatuses();
            
            model.Quarries = await _context.Quarries
                .Where(q => q.Status == "Active")
                .Select(q => new SelectListItem
                {
                    Value = q.Id.ToString(),
                    Text = q.Name
                })
                .ToListAsync();
        }

        private async Task PopulateDropdowns(EmployeeEditViewModel model)
        {
            model.Departments = GetDepartments();
            model.Designations = GetDesignations();
            model.Banks = GetNigerianBanks();
            model.Statuses = GetEmployeeStatuses();
            
            model.Quarries = await _context.Quarries
                .Where(q => q.Status == "Active")
                .Select(q => new SelectListItem
                {
                    Value = q.Id.ToString(),
                    Text = q.Name
                })
                .ToListAsync();
        }

        private TaxCalculationViewModel CalculateAnnualTax(decimal annualGross)
        {
            // Nigerian PAYE tax calculation
            decimal tax = 0;
            decimal remaining = annualGross;

            var taxBrackets = new[] { 300000m, 300000m, 500000m, 500000m, 1600000m, 3200000m };
            var taxRates = new[] { 0.07m, 0.11m, 0.15m, 0.19m, 0.21m, 0.24m };

            for (int i = 0; i < taxBrackets.Length; i++)
            {
                if (remaining <= 0) break;

                decimal taxableAmount = Math.Min(remaining, taxBrackets[i]);
                tax += taxableAmount * taxRates[i];
                remaining -= taxableAmount;
            }

            return new TaxCalculationViewModel
            {
                AnnualGross = annualGross,
                AnnualTax = tax,
                MonthlyTax = tax / 12,
                EffectiveTaxRate = annualGross > 0 ? (double)(tax / annualGross) * 100 : 0
            };
        }

        private EmployeeSalary CreateSamplePayslip(Employee employee)
        {
            var grossSalary = employee.GrossSalary;
            var basicSalary = employee.BasicSalary ?? 0;
            var housingAllowance = employee.HousingAllowance ?? 0;

            // Calculate deductions
            var pensionEmployee = (basicSalary + housingAllowance) * 0.08m; // 8% pension
            var nhif = basicSalary * 0.05m; // 5% NHIS
            var nhf = basicSalary * 0.025m; // 2.5% NHF
            var annualTax = CalculateAnnualTax(grossSalary * 12);
            var monthlyTax = annualTax.MonthlyTax;

            return new EmployeeSalary
            {
                Employee = employee,
                BasicSalary = basicSalary,
                HousingAllowance = housingAllowance,
                TransportAllowance = employee.TransportAllowance ?? 0,
                OtherAllowances = employee.OtherAllowances ?? 0,
                // GrossPay = grossSalary, // This is a read-only property
                PAYE = monthlyTax,
                PensionEmployee = pensionEmployee,
                PensionEmployer = (basicSalary + housingAllowance) * 0.10m, // 10% employer
                NHIS = nhif,
                NHF = nhf,
                // NetPay = grossSalary - (monthlyTax + pensionEmployee + nhif + nhf), // This is a read-only property
                PaymentDate = DateTime.Now,
                PaymentStatus = "Sample"
            };
        }

        private CompanyDetailsViewModel GetCompanyDetails()
        {
            return new CompanyDetailsViewModel
            {
                CompanyName = "Nigerian Quarry Management System",
                Address = "123 Quarry Road, Industrial Estate",
                City = "Lagos",
                State = "Lagos",
                Phone = "+234-1-2345678",
                Email = "hr@quarry.ng",
                Website = "www.quarry.ng",
                TaxNumber = "12345678-0001",
                BankDetails = "Access Bank, Account: 1234567890"
            };
        }
    }
}