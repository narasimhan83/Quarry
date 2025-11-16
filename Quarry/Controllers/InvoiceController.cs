using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;
using QuarryManagementSystem.ViewModels;
using QuarryManagementSystem.Utilities;
using System.Linq.Expressions;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager,Accountant")]
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(ApplicationDbContext context, ILogger<InvoiceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Invoice
        public async Task<IActionResult> Index(string searchTerm, string status, DateTime? dateFrom, DateTime? dateTo, int page = 1)
        {
            try
            {
                int pageSize = 20;
                var query = _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.WeighmentTransaction)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(i => 
                        i.InvoiceNumber.Contains(searchTerm) || 
                        i.Customer.Name.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(i => i.Status == status);
                }

                if (dateFrom.HasValue)
                {
                    query = query.Where(i => i.InvoiceDate >= dateFrom.Value);
                }

                if (dateTo.HasValue)
                {
                    query = query.Where(i => i.InvoiceDate <= dateTo.Value.AddDays(1));
                }

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Apply pagination
                var invoices = await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var viewModel = new InvoiceListViewModel
                {
                    Invoices = invoices,
                    SearchTerm = searchTerm,
                    SelectedStatus = status,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalCount = totalCount,
                    Statuses = GetInvoiceStatuses()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice list");
                return View(new InvoiceListViewModel
                {
                    ErrorMessage = "An error occurred while loading invoices. Please try again."
                });
            }
        }

        // GET: Invoice/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var viewModel = new InvoiceCreateViewModel
                {
                    InvoiceDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(30), // Default 30 days
                    VatRate = 7.5m, // Nigerian VAT rate
                    SelectedPaymentTerms = "30 days"
                };

                await PopulateDropdowns(viewModel);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create invoice form");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Invoice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InvoiceCreateViewModel model)
        {
            try
            {
                if (ModelState.IsValid && model.SelectedWeighmentIds != null && model.SelectedWeighmentIds.Any())
                {
                    // Validate customer selection
                    if (!model.CustomerId.HasValue)
                    {
                        ModelState.AddModelError("", "Please select a customer for the invoice.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Get selected weighments
                    var weighments = await _context.WeighmentTransactions
                        .Include(w => w.Material)
                        .Include(w => w.Customer)
                        .Where(w => model.SelectedWeighmentIds.Contains(w.Id) && 
                                   w.Status == "Completed" && 
                                   !w.IsInvoiced)
                        .ToListAsync();

                    if (!weighments.Any())
                    {
                        ModelState.AddModelError("", "No valid weighments selected for invoicing.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Validate all weighments belong to the same customer
                    var differentCustomer = weighments.FirstOrDefault(w => w.CustomerId != model.CustomerId);
                    if (differentCustomer != null)
                    {
                        ModelState.AddModelError("", "All selected weighments must belong to the same customer.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Calculate totals
                    decimal subTotal = weighments.Sum(w => w.SubTotal ?? 0);
                    decimal vatAmount = subTotal * (model.VatRate / 100);
                    decimal totalAmount = subTotal + vatAmount;

                    // Generate invoice number
                    var invoiceNumber = await GenerateInvoiceNumber();

                    // Create invoice
                    var invoice = new Invoice
                    {
                        InvoiceNumber = invoiceNumber,
                        CustomerId = model.CustomerId.Value,
                        InvoiceDate = model.InvoiceDate,
                        DueDate = model.DueDate,
                        SubTotal = subTotal,
                        VatAmount = vatAmount,
                        TotalAmount = totalAmount,
                        PaidAmount = 0,
                        Status = "Unpaid",
                        PaymentTerms = model.SelectedPaymentTerms,
                        LGAReceiptNumber = model.LGAReceiptNumber,
                        Notes = model.Notes,
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.Now
                    };

                    _context.Add(invoice);
                    await _context.SaveChangesAsync();

                    // Update weighments as invoiced
                    foreach (var weighment in weighments)
                    {
                        weighment.IsInvoiced = true;
                        weighment.ModifiedBy = User.Identity?.Name;
                        weighment.ModifiedAt = DateTime.Now;
                    }
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Invoice {invoiceNumber} created successfully.";
                    _logger.LogInformation("Invoice {InvoiceNumber} created by user {UserName} for customer {CustomerId}", 
                        invoiceNumber, User.Identity?.Name, model.CustomerId);
                    
                    return RedirectToAction(nameof(Index));
                }
                else if (model.SelectedWeighmentIds == null || !model.SelectedWeighmentIds.Any())
                {
                    ModelState.AddModelError("", "Please select at least one weighment to invoice.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice");
                ModelState.AddModelError("", "An error occurred while creating the invoice. Please try again.");
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.WeighmentTransaction)
                .ThenInclude(w => w.Material)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            var viewModel = new InvoiceDetailsViewModel
            {
                Invoice = invoice,
                AmountInWords = NumberToWordsConverter.ConvertInvoiceAmount(invoice.TotalAmount),
                CanEditPayment = invoice.Status != "Paid",
                CanCancel = invoice.Status == "Unpaid" && invoice.PaidAmount == 0
            };

            return View(viewModel);
        }

        // GET: Invoice/Print/5
        public async Task<IActionResult> Print(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.WeighmentTransaction)
                .ThenInclude(w => w.Material)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            var viewModel = new InvoicePrintViewModel
            {
                Invoice = invoice,
                AmountInWords = NumberToWordsConverter.ConvertInvoiceAmount(invoice.TotalAmount),
                CompanyDetails = GetCompanyDetails(),
                InvoiceItems = await GetInvoiceItems(invoice.Id)
            };

            return View(viewModel);
        }

        // GET: Invoice/RecordPayment/5
        public async Task<IActionResult> RecordPayment(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null)
            {
                return NotFound();
            }

            if (invoice.Status == "Paid")
            {
                TempData["Error"] = "This invoice has already been paid.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new InvoicePaymentViewModel
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerName = invoice.Customer?.Name ?? "Unknown",
                TotalAmount = invoice.TotalAmount,
                OutstandingAmount = invoice.OutstandingBalance,
                PaymentDate = DateTime.Now
            };

            return View(model);
        }

        // POST: Invoice/RecordPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(InvoicePaymentViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var invoice = await _context.Invoices
                        .Include(i => i.Customer)
                        .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);

                    if (invoice == null)
                    {
                        return NotFound();
                    }

                    if (invoice.Status == "Paid")
                    {
                        TempData["Error"] = "This invoice has already been paid.";
                        return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
                    }

                    if (model.PaymentAmount > invoice.OutstandingBalance)
                    {
                        ModelState.AddModelError("PaymentAmount", "Payment amount cannot exceed outstanding balance.");
                        await PopulatePaymentViewModelAsync(model);
                        return View(model);
                    }

                    // Update invoice
                    invoice.PaidAmount += model.PaymentAmount;
                    invoice.Status = invoice.PaidAmount >= invoice.TotalAmount ? "Paid" : "Partial";
                    invoice.UpdatedAt = DateTime.Now;

                    // Update customer outstanding balance
                    if (invoice.Customer != null)
                    {
                        invoice.Customer.OutstandingBalance -= model.PaymentAmount;
                    }

                    await _context.SaveChangesAsync();

                    // Create journal entry for payment
                    await CreatePaymentJournalEntry(invoice, model.PaymentAmount, model.PaymentMethod);

                    TempData["Success"] = $"Payment of {model.PaymentAmount:C} recorded successfully.";
                    _logger.LogInformation("Payment of {Amount} recorded for invoice {InvoiceNumber} by user {UserName}", 
                        model.PaymentAmount, invoice.InvoiceNumber, User.Identity?.Name);
                    
                    return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording payment for invoice {InvoiceId}", model.InvoiceId);
                ModelState.AddModelError("", "An error occurred while recording the payment. Please try again.");
            }

            await PopulatePaymentViewModelAsync(model);
            return View(model);
        }

        // GET: Invoice/Cancel/5
        public async Task<IActionResult> Cancel(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            if (invoice.Status == "Paid" || invoice.PaidAmount > 0)
            {
                TempData["Error"] = "Cannot cancel an invoice that has been paid or has payments.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(invoice);
        }

        // POST: Invoice/Cancel/5
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.WeighmentTransaction)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    return NotFound();
                }

                if (invoice.Status == "Paid" || invoice.PaidAmount > 0)
                {
                    TempData["Error"] = "Cannot cancel an invoice that has been paid or has payments.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Mark invoice as cancelled
                invoice.Status = "Cancelled";
                invoice.UpdatedAt = DateTime.Now;

                // Release weighments from invoice
                if (invoice.WeighmentTransaction != null)
                {
                    invoice.WeighmentTransaction.IsInvoiced = false;
                    invoice.WeighmentTransaction.ModifiedBy = User.Identity?.Name;
                    invoice.WeighmentTransaction.ModifiedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} cancelled successfully.";
                _logger.LogInformation("Invoice {InvoiceNumber} cancelled by user {UserName}", 
                    invoice.InvoiceNumber, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling invoice {InvoiceId}", id);
                TempData["Error"] = "An error occurred while cancelling the invoice.";
            }

            return RedirectToAction(nameof(Index));
        }

        // AJAX: Get unpaid weighments for customer
        [HttpGet]
        public async Task<JsonResult> GetUnpaidWeighments(int customerId)
        {
            try
            {
                var weighments = await _context.WeighmentTransactions
                    .Include(w => w.Material)
                    .Where(w => w.CustomerId == customerId && 
                               w.Status == "Completed" && 
                               !w.IsInvoiced)
                    .Select(w => new
                    {
                        id = w.Id,
                        transactionNumber = w.TransactionNumber,
                        transactionDate = w.TransactionDate.ToString("dd/MM/yyyy"),
                        vehicleRegNumber = w.VehicleRegNumber,
                        materialName = w.Material.Name,
                        netWeight = w.NetWeight,
                        totalAmount = w.TotalAmount ?? 0
                    })
                    .ToListAsync();

                return Json(new { success = true, data = weighments });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unpaid weighments for customer {CustomerId}", customerId);
                return Json(new { success = false, message = "Error retrieving weighments" });
            }
        }

        // AJAX: Calculate invoice totals
        [HttpPost]
        public async Task<JsonResult> CalculateInvoiceTotals([FromBody] int[] weighmentIds)
        {
            try
            {
                var weighments = await _context.WeighmentTransactions
                    .Where(w => weighmentIds.Contains(w.Id) && 
                               w.Status == "Completed" && 
                               !w.IsInvoiced)
                    .ToListAsync();

                decimal subTotal = weighments.Sum(w => w.SubTotal ?? 0);
                decimal vatAmount = subTotal * 0.075m; // 7.5% VAT
                decimal totalAmount = subTotal + vatAmount;

                return Json(new 
                { 
                    success = true, 
                    subTotal = subTotal,
                    vatAmount = vatAmount,
                    totalAmount = totalAmount,
                    weighmentCount = weighments.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating invoice totals");
                return Json(new { success = false, message = "Error calculating totals" });
            }
        }

        // Helper methods
        private async Task PopulateDropdowns(InvoiceCreateViewModel model)
        {
            model.Customers = await _context.Customers
                .Where(c => c.Status == "Active")
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Name} - {c.ContactPerson}"
                })
                .ToListAsync();

            model.PaymentTermsList = new List<SelectListItem>
            {
                new SelectListItem { Value = "15 days", Text = "15 days" },
                new SelectListItem { Value = "30 days", Text = "30 days" },
                new SelectListItem { Value = "45 days", Text = "45 days" },
                new SelectListItem { Value = "60 days", Text = "60 days" },
                new SelectListItem { Value = "90 days", Text = "90 days" }
            };
        }

        private List<SelectListItem> GetInvoiceStatuses()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- All Statuses --" },
                new SelectListItem { Value = "Unpaid", Text = "Unpaid" },
                new SelectListItem { Value = "Partial", Text = "Partial" },
                new SelectListItem { Value = "Paid", Text = "Paid" },
                new SelectListItem { Value = "Overdue", Text = "Overdue" },
                new SelectListItem { Value = "Cancelled", Text = "Cancelled" }
            };
        }

        private async Task<string> GenerateInvoiceNumber()
        {
            var today = DateTime.Today;
            var prefix = $"INV/NG/{today:yyyy}/";
            
            var lastInvoice = await _context.Invoices
                .Where(i => i.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(i => i.InvoiceNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastInvoice != null)
            {
                var lastNumberStr = lastInvoice.InvoiceNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        private async Task CreatePaymentJournalEntry(Invoice invoice, decimal paymentAmount, string paymentMethod)
        {
            try
            {
                var entryNumber = JournalEntry.GenerateEntryNumber("PAY");
                
                var journalEntry = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    EntryDate = DateTime.Now,
                    Reference = $"Payment for Invoice {invoice.InvoiceNumber}",
                    Description = $"Customer payment of {paymentAmount:C} for invoice {invoice.InvoiceNumber} via {paymentMethod}",
                    PostedBy = User.Identity?.Name,
                    IsAutoGenerated = true,
                    CreatedAt = DateTime.Now
                };

                // Debit Cash/Bank account
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetCashAccountId(), // Cash account
                    DebitAmount = paymentAmount,
                    CreditAmount = 0,
                    LineDescription = $"Payment received for invoice {invoice.InvoiceNumber}"
                });

                // Credit Accounts Receivable
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetAccountsReceivableId(), // Accounts Receivable
                    DebitAmount = 0,
                    CreditAmount = paymentAmount,
                    LineDescription = $"Reduce receivable for invoice {invoice.InvoiceNumber}"
                });

                _context.JournalEntries.Add(journalEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry for payment");
            }
        }

        private async Task<int> GetCashAccountId()
        {
            var cashAccount = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(ca => ca.AccountCode == "1001");
            return cashAccount?.Id ?? 1;
        }

        private async Task<int> GetAccountsReceivableId()
        {
            var arAccount = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(ca => ca.AccountCode == "1101");
            return arAccount?.Id ?? 2;
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
                Email = "info@quarry.ng",
                Website = "www.quarry.ng",
                TaxNumber = "12345678-0001",
                BankDetails = "Access Bank, Account: 1234567890"
            };
        }

        private async Task<List<InvoiceItemViewModel>> GetInvoiceItems(int invoiceId)
        {
            var weighment = await _context.WeighmentTransactions
                .Include(w => w.Material)
                .FirstOrDefaultAsync(w => w.InvoiceId == invoiceId);

            if (weighment == null)
                return new List<InvoiceItemViewModel>();

            return new List<InvoiceItemViewModel>
            {
                new InvoiceItemViewModel
                {
                    Description = $"{weighment.Material?.Name} - Vehicle {weighment.VehicleRegNumber}",
                    Quantity = weighment.NetWeight / 1000, // Convert to tons
                    Unit = "Tons",
                    UnitPrice = weighment.PricePerUnit ?? 0,
                    TotalAmount = weighment.SubTotal ?? 0,
                    VatAmount = weighment.VatAmount ?? 0
                }
            };
        }

        private async Task PopulatePaymentViewModelAsync(InvoicePaymentViewModel model)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);

                if (invoice != null)
                {
                    model.InvoiceNumber = invoice.InvoiceNumber;
                    model.CustomerName = invoice.Customer?.Name ?? "Unknown";
                    model.TotalAmount = invoice.TotalAmount;
                    model.OutstandingAmount = invoice.OutstandingBalance;

                    if (model.PaymentDate == default)
                    {
                        model.PaymentDate = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating payment view model for invoice {InvoiceId}", model.InvoiceId);
            }
        }
    }
}