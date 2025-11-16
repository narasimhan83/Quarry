using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;
using QuarryManagementSystem.Utilities;
using System.Linq.Expressions;
using System.Security.Claims;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class PayrollController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PayrollController> _logger;

        public PayrollController(ApplicationDbContext context, ILogger<PayrollController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Payroll
        public async Task<IActionResult> Index(string searchTerm, string status, int? year, int? month, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.PayrollRuns
                    .Include(pr => pr.EmployeeSalaries)
                    .ThenInclude(es => es.Employee)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(pr => 
                        pr.RunNumber.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(pr => pr.Status == status);
                }

                if (year.HasValue)
                {
                    query = query.Where(pr => pr.PaymentMonth.Year == year.Value);
                }

                if (month.HasValue)
                {
                    query = query.Where(pr => pr.PaymentMonth.Month == month.Value);
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var payrollRuns = await query
                    .OrderByDescending(pr => pr.PaymentMonth)
                    .ThenByDescending(pr => pr.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new PayrollListViewModel
                {
                    PayrollRuns = payrollRuns,
                    SearchTerm = searchTerm,
                    SelectedStatus = status,
                    SelectedYear = year,
                    SelectedMonth = month,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    Statuses = GetPayrollStatuses(),
                    Years = GetYears(),
                    Months = GetMonths()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payroll list");
                return View(new PayrollListViewModel
                {
                    ErrorMessage = "An error occurred while loading payroll data. Please try again."
                });
            }
        }

        // GET: Payroll/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var currentMonth = DateTime.Today.AddMonths(-1); // Default to last month
                var viewModel = new PayrollCreateViewModel
                {
                    PaymentMonth = new DateTime(currentMonth.Year, currentMonth.Month, 1),
                    PaymentDate = DateTime.Now,
                    Status = "Draft"
                };

                await PopulateDropdowns(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create payroll form");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Payroll/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PayrollCreateViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check if payroll already exists for this month
                    var existingPayroll = await _context.PayrollRuns
                        .FirstOrDefaultAsync(pr => pr.PaymentMonth.Year == model.PaymentMonth.Year && 
                                                  pr.PaymentMonth.Month == model.PaymentMonth.Month);

                    if (existingPayroll != null)
                    {
                        ModelState.AddModelError("", "A payroll already exists for this month.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Generate payroll run number
                    var runNumber = await GeneratePayrollRunNumber(model.PaymentMonth);

                    // Get active employees
                    var employees = await _context.Employees
                        .Where(e => e.Status == "Active")
                        .ToListAsync();

                    if (!employees.Any())
                    {
                        ModelState.AddModelError("", "No active employees found for payroll processing.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Create payroll run
                    var payrollRun = new PayrollRun
                    {
                        RunNumber = runNumber,
                        PaymentMonth = model.PaymentMonth,
                        TotalEmployees = employees.Count,
                        Status = "Draft",
                        ProcessedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                        ProcessedAt = DateTime.Now,
                        CreatedAt = DateTime.Now
                    };

                    // Calculate salaries for each employee
                    decimal totalGross = 0;
                    decimal totalPAYE = 0;
                    decimal totalPension = 0;
                    decimal totalNHIS = 0;
                    decimal totalNet = 0;

                    foreach (var employee in employees)
                    {
                        var employeeSalary = CalculateEmployeeSalary(employee, payrollRun);
                        payrollRun.EmployeeSalaries.Add(employeeSalary);
                        
                        totalGross += employeeSalary.GrossPay;
                        totalPAYE += employeeSalary.PAYE;
                        totalPension += employeeSalary.PensionEmployee;
                        totalNHIS += employeeSalary.NHIS;
                        totalNet += employeeSalary.NetPay;
                    }

                    payrollRun.GrossPay = totalGross;
                    payrollRun.TotalPAYE = totalPAYE;
                    payrollRun.TotalPension = totalPension;
                    payrollRun.TotalNHIS = totalNHIS;
                    payrollRun.NetPay = totalNet;

                    _context.Add(payrollRun);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Payroll {runNumber} created successfully with {employees.Count} employees.";
                    _logger.LogInformation("Payroll {RunNumber} created by user {UserName} with {EmployeeCount} employees", 
                        runNumber, User.Identity?.Name, employees.Count);
                    
                    return RedirectToAction(nameof(Details), new { id = payrollRun.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payroll");
                ModelState.AddModelError("", "An error occurred while creating the payroll. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Payroll/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payrollRun = await _context.PayrollRuns
                .Include(pr => pr.EmployeeSalaries)
                .ThenInclude(es => es.Employee)
                .Include(pr => pr.ProcessedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (payrollRun == null)
            {
                return NotFound();
            }

            var viewModel = new PayrollDetailsViewModel
            {
                PayrollRun = payrollRun,
                TotalEmployerContributions = payrollRun.EmployeeSalaries.Sum(es => es.GetTotalEmployerContributions()),
                BankPaymentFileReady = payrollRun.Status == "Processed",
                CanProcess = payrollRun.IsDraft() && payrollRun.TotalEmployees > 0,
                CanPay = payrollRun.IsProcessed(),
                CanEdit = payrollRun.IsDraft()
            };

            return View(viewModel);
        }

        // POST: Payroll/Process/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int id)
        {
            try
            {
                var payrollRun = await _context.PayrollRuns
                    .Include(pr => pr.EmployeeSalaries)
                    .ThenInclude(es => es.Employee)
                    .FirstOrDefaultAsync(pr => pr.Id == id);

                if (payrollRun == null)
                {
                    return NotFound();
                }

                if (!payrollRun.CanProcess())
                {
                    TempData["Error"] = "This payroll cannot be processed.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Create journal entries for payroll
                await CreatePayrollJournalEntries(payrollRun);

                // Update payroll status
                payrollRun.Status = "Processed";
                payrollRun.ProcessedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
                payrollRun.ProcessedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Payroll {payrollRun.RunNumber} processed successfully.";
                _logger.LogInformation("Payroll {RunNumber} processed by user {UserName}", 
                    payrollRun.RunNumber, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payroll {PayrollId}", id);
                TempData["Error"] = "An error occurred while processing the payroll.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Payroll/GenerateBankFile/5
        public async Task<IActionResult> GenerateBankFile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payrollRun = await _context.PayrollRuns
                .Include(pr => pr.EmployeeSalaries)
                .ThenInclude(es => es.Employee)
                .FirstOrDefaultAsync(pr => pr.Id == id && pr.Status == "Processed");

            if (payrollRun == null)
            {
                return NotFound();
            }

            var bankFile = GenerateBankPaymentFile(payrollRun);
            var fileName = $"Payroll_BankFile_{payrollRun.RunNumber}_{DateTime.Now:yyyyMMdd}.csv";

            return File(System.Text.Encoding.UTF8.GetBytes(bankFile), "text/csv", fileName);
        }

        // GET: Payroll/Payslip/5
        public async Task<IActionResult> Payslip(int employeeSalaryId)
        {
            var employeeSalary = await _context.EmployeeSalaries
                .Include(es => es.Employee)
                .Include(es => es.PayrollRun)
                .FirstOrDefaultAsync(es => es.Id == employeeSalaryId);

            if (employeeSalary == null)
            {
                return NotFound();
            }

            var viewModel = new PayslipViewModel
            {
                Employee = employeeSalary.Employee,
                EmployeeSalary = employeeSalary,
                CompanyDetails = GetCompanyDetails(),
                AmountInWords = NumberToWordsConverter.ConvertSalaryAmount(employeeSalary.NetPay),
                PrintDate = DateTime.Now,
                IsSample = false
            };

            return View(viewModel);
        }

        // GET: Payroll/EmployeeSalaries/5
        public async Task<IActionResult> EmployeeSalaries(int payrollRunId)
        {
            var payrollRun = await _context.PayrollRuns
                .Include(pr => pr.EmployeeSalaries)
                .ThenInclude(es => es.Employee)
                .FirstOrDefaultAsync(pr => pr.Id == payrollRunId);

            if (payrollRun == null)
            {
                return NotFound();
            }

            return View(payrollRun);
        }

        // AJAX: Get payroll summary
        [HttpGet]
        public async Task<JsonResult> GetPayrollSummary(int payrollRunId)
        {
            try
            {
                var payrollRun = await _context.PayrollRuns
                    .Include(pr => pr.EmployeeSalaries)
                    .FirstOrDefaultAsync(pr => pr.Id == payrollRunId);

                if (payrollRun == null)
                {
                    return Json(new { success = false, message = "Payroll not found" });
                }

                var summary = new
                {
                    totalEmployees = payrollRun.TotalEmployees,
                    grossPay = payrollRun.GrossPay,
                    totalPAYE = payrollRun.TotalPAYE,
                    totalPension = payrollRun.TotalPension,
                    totalNHIS = payrollRun.TotalNHIS,
                    netPay = payrollRun.NetPay,
                    employerContributions = payrollRun.EmployeeSalaries.Sum(es => es.GetTotalEmployerContributions())
                };

                return Json(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payroll summary for payroll {PayrollRunId}", payrollRunId);
                return Json(new { success = false, message = "Error retrieving payroll summary" });
            }
        }

        // AJAX: Get payroll compliance report
        [HttpGet]
        public async Task<JsonResult> GetComplianceReport(int payrollRunId)
        {
            try
            {
                var payrollRun = await _context.PayrollRuns
                    .Include(pr => pr.EmployeeSalaries)
                    .ThenInclude(es => es.Employee)
                    .FirstOrDefaultAsync(pr => pr.Id == payrollRunId);

                if (payrollRun == null)
                {
                    return Json(new { success = false, message = "Payroll not found" });
                }

                var report = new
                {
                    payeRemittance = payrollRun.TotalPAYE,
                    pensionRemittance = payrollRun.TotalPension,
                    nhifRemittance = payrollRun.TotalNHIS,
                    totalRemittance = payrollRun.TotalPAYE + payrollRun.TotalPension + payrollRun.TotalNHIS,
                    employeeCount = payrollRun.TotalEmployees,
                    complianceStatus = "Ready for remittance"
                };

                return Json(new { success = true, data = report });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance report for payroll {PayrollRunId}", payrollRunId);
                return Json(new { success = false, message = "Error retrieving compliance report" });
            }
        }

        // Helper methods
        private async Task<string> GeneratePayrollRunNumber(DateTime paymentMonth)
        {
            var prefix = $"PR/{paymentMonth:yyyyMM}/";
            
            var lastPayroll = await _context.PayrollRuns
                .Where(pr => pr.RunNumber.StartsWith(prefix))
                .OrderByDescending(pr => pr.RunNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastPayroll != null)
            {
                var lastNumberStr = lastPayroll.RunNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D3}";
        }

        private EmployeeSalary CalculateEmployeeSalary(Employee employee, PayrollRun payrollRun)
        {
            var basicSalary = employee.BasicSalary ?? 0;
            var housingAllowance = employee.HousingAllowance ?? 0;
            var transportAllowance = employee.TransportAllowance ?? 0;
            var otherAllowances = employee.OtherAllowances ?? 0;

            var grossPay = basicSalary + housingAllowance + transportAllowance + otherAllowances;

            // Calculate deductions
            var pensionableSalary = basicSalary + housingAllowance;
            var pensionEmployee = pensionableSalary * 0.08m; // 8% employee contribution
            var pensionEmployer = pensionableSalary * 0.10m; // 10% employer contribution

            var nhif = basicSalary * 0.05m; // 5% NHIS
            var nhf = basicSalary * 0.025m; // 2.5% NHF

            // Calculate PAYE
            var annualGross = grossPay * 12;
            var annualTax = PayrollRun.CalculatePAYE(annualGross);
            var monthlyTax = annualTax / 12;

            var totalDeductions = monthlyTax + pensionEmployee + nhif + nhf;
            var netPay = grossPay - totalDeductions;

            // Important: set the navigation to the new payrollRun instead of FK=0 to avoid FK constraint errors on save
            return new EmployeeSalary
            {
                PayrollRun = payrollRun,
                Employee = employee,
                EmployeeId = employee.Id,
                BasicSalary = basicSalary,
                HousingAllowance = housingAllowance,
                TransportAllowance = transportAllowance,
                OtherAllowances = otherAllowances,
                // GrossPay is computed (NotMapped)
                PAYE = monthlyTax,
                PensionEmployee = pensionEmployee,
                PensionEmployer = pensionEmployer,
                NHIS = nhif,
                NHF = nhf,
                // NetPay is computed (NotMapped)
                PaymentDate = payrollRun.PaymentMonth,
                PaymentStatus = "Pending"
            };
        }

        private async Task CreatePayrollJournalEntries(PayrollRun payrollRun)
        {
            try
            {
                // Salary expense entry
                var salaryEntry = new JournalEntry
                {
                    EntryNumber = JournalEntry.GenerateEntryNumber("SAL"),
                    EntryDate = payrollRun.PaymentMonth,
                    Reference = $"Salary expense for {payrollRun.RunNumber}",
                    Description = $"Monthly salary expense for {payrollRun.PaymentMonth:MMMM yyyy}",
                    PostedBy = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier),
                    IsAutoGenerated = true,
                    CreatedAt = DateTime.Now
                };

                // Compute NHF total so the journal is balanced (NHF was previously excluded from credits)
                var nhfTotal = payrollRun.EmployeeSalaries.Sum(es => es.NHF);

                // Debit salary expense
                salaryEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetSalaryExpenseAccountId(),
                    DebitAmount = payrollRun.GrossPay,
                    CreditAmount = 0,
                    LineDescription = $"Salary expense for {payrollRun.RunNumber}"
                });

                // Credit various liability accounts
                salaryEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetSalariesPayableAccountId(),
                    DebitAmount = 0,
                    CreditAmount = payrollRun.NetPay,
                    LineDescription = $"Net salaries payable for {payrollRun.RunNumber}"
                });

                salaryEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetPAYEPayableAccountId(),
                    DebitAmount = 0,
                    CreditAmount = payrollRun.TotalPAYE,
                    LineDescription = $"PAYE tax payable for {payrollRun.RunNumber}"
                });

                salaryEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetPensionPayableAccountId(),
                    DebitAmount = 0,
                    CreditAmount = payrollRun.TotalPension,
                    LineDescription = $"Pension contributions payable for {payrollRun.RunNumber}"
                });

                salaryEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetNHISPayableAccountId(),
                    DebitAmount = 0,
                    CreditAmount = payrollRun.TotalNHIS,
                    LineDescription = $"NHIS contributions payable for {payrollRun.RunNumber}"
                });

                // Add NHF payable to fully balance the entry: Gross = Net + PAYE + Pension + NHIS + NHF
                if (nhfTotal > 0)
                {
                    salaryEntry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId = await GetNHFPayableAccountId(),
                        DebitAmount = 0,
                        CreditAmount = nhfTotal,
                        LineDescription = $"NHF contributions payable for {payrollRun.RunNumber}"
                    });
                }

                // Recalculate totals to persist balanced debit/credit
                salaryEntry.RecalculateTotals();

                _context.JournalEntries.Add(salaryEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payroll journal entries for payroll {PayrollRunId}", payrollRun.Id);
            }
        }

        private string GenerateBankPaymentFile(PayrollRun payrollRun)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("EmployeeCode,EmployeeName,BankName,BankAccountNumber,NetPay,PaymentReference");

            foreach (var salary in payrollRun.EmployeeSalaries)
            {
                if (salary.Employee.HasCompleteBankDetails())
                {
                    csv.AppendLine($"{salary.Employee.EmployeeCode}," +
                                 $"\"{salary.Employee.FullName}\"," +
                                 $"\"{salary.Employee.BankName}\"," +
                                 $"{salary.Employee.BankAccountNumber}," +
                                 $"{salary.NetPay}," +
                                 $"{salary.PaymentReference}");
                }
            }

            return csv.ToString();
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

        // Account ID helpers (these would normally come from a configuration or service)
        private async Task<int> GetSalaryExpenseAccountId() => 1;
        private async Task<int> GetSalariesPayableAccountId() => 2;
        private async Task<int> GetPAYEPayableAccountId() => 3;
        private async Task<int> GetPensionPayableAccountId() => 4;
        private async Task<int> GetNHISPayableAccountId() => 5;
        private async Task<int> GetNHFPayableAccountId() => 5; // Use Accounts Payable for NHF as well

        private List<SelectListItem> GetPayrollStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- All Statuses --" },
                new SelectListItem { Value = "Draft", Text = "Draft" },
                new SelectListItem { Value = "Processed", Text = "Processed" },
                new SelectListItem { Value = "Paid", Text = "Paid" }
            };
        }

        private List<SelectListItem> GetYears()
        {
            var currentYear = DateTime.Now.Year;
            var years = new List<SelectListItem>();
            
            for (int year = currentYear - 5; year <= currentYear + 1; year++)
            {
                years.Add(new SelectListItem 
                { 
                    Value = year.ToString(), 
                    Text = year.ToString() 
                });
            }
            
            return years;
        }

        private List<SelectListItem> GetMonths()
        {
            var months = new List<SelectListItem>();
            
            for (int month = 1; month <= 12; month++)
            {
                months.Add(new SelectListItem 
                { 
                    Value = month.ToString(), 
                    Text = new DateTime(2000, month, 1).ToString("MMMM") 
                });
            }
            
            return months;
        }

        private async Task PopulateDropdowns(PayrollCreateViewModel model)
        {
            model.Statuses = GetPayrollStatuses();
        }
    }
}