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
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(ApplicationDbContext context, ILogger<InvoiceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Returns the current user's AspNetUsers.Id (a GUID string), or null
        /// if unauthenticated. Required anywhere we're assigning to a FK column
        /// that points at AspNetUsers — e.g. JournalEntry.PostedBy. Do NOT use
        /// User.Identity?.Name for these; that's the username/email, not the
        /// primary key, and it will blow up the FK constraint.
        /// </summary>
        private string? GetCurrentUserId() =>
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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
                            SelectedPaymentTerms = "30 days",
                            PaymentMode = "Credit"
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
                                .Where(w =>
                                    model.SelectedWeighmentIds.Contains(w.Id) &&
                                    w.Status == "Completed" &&
                                    !w.IsInvoiced &&
                                    // Do not allow weighments already linked to a non-cancelled invoice
                                    (w.InvoiceId == null ||
                                     !_context.Invoices.Any(i => i.Id == w.InvoiceId && i.Status != "Cancelled")))
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
        
                            // Load customer with classification info for rebate/transport/VAT type
                            var customer = await _context.Customers
                                .Include(c => c.VatType)
                                .FirstOrDefaultAsync(c => c.Id == model.CustomerId.Value);
                            if (customer == null)
                            {
                                ModelState.AddModelError("", "Customer not found.");
                                await PopulateDropdowns(model);
                                return View(model);
                            }

                            // Aggregate the already-calculated weighment values. Each
                            // weighment's SubTotal / VatAmount / RebateAmount / TotalAmount
                            // was produced by ApplyVatTreatmentAsync on save and is the
                            // authoritative, operator-visible number — we must bill what
                            // the driver/customer saw at the weighbridge, not recompute
                            // from customer settings that may have drifted since then.
                            decimal subTotal    = weighments.Sum(w => w.SubTotal     ?? 0m);
                            decimal vatAmount   = weighments.Sum(w => w.VatAmount    ?? 0m);
                            decimal rebateAmount = weighments.Sum(w => w.RebateAmount ?? 0m);
                            decimal weighmentsTotal = weighments.Sum(w => w.TotalAmount  ?? 0m);

                            // Transport is still customer-level (flat per invoice).
                            decimal transportAmount = customer.TransportRequired ? (customer.TransportAmount ?? 0m) : 0m;

                            // VatTypeSnapshot reflects the customer's treatment at invoice
                            // time — used by CreateInvoiceJournalEntryAsync to post the
                            // right Dr/Cr lines. Individual weighments already split VAT
                            // correctly; this snapshot is for the ledger posting.
                            string vatType = customer.VatType?.Name ?? "Exclusive";

                            // Final invoice total = weighments total + transport (flat).
                            decimal totalAmount = Math.Round(weighmentsTotal + transportAmount, 2);
        
                            // Generate invoice number
                            var invoiceNumber = await GenerateInvoiceNumber();
        
                            // Determine prepayment usage (wallet)
                            decimal prepaymentApplied = 0;
                            if (model.CustomerId.HasValue && string.Equals(model.PaymentMode, "Prepayment", StringComparison.OrdinalIgnoreCase))
                            {
                                var availablePrepayment = await GetAvailablePrepaymentAsync(model.CustomerId.Value);
                                if (availablePrepayment <= 0)
                                {
                                    ModelState.AddModelError("PaymentMode", "No available prepayment for this customer. Please select Credit or create a prepayment first.");
                                    await PopulateDropdowns(model);
                                    model.SubTotal = subTotal;
                                    model.VatAmount = vatAmount;
                                    model.TotalAmount = totalAmount;
                                    model.AvailablePrepayment = 0;
                                    return View(model);
                                }
        
                                prepaymentApplied = Math.Min(availablePrepayment, totalAmount);
                                model.AvailablePrepayment = availablePrepayment;
                            }
        
                            // Determine invoice status based on prepayment
                            string status = "Unpaid";
                            if (prepaymentApplied >= totalAmount)
                            {
                                status = "Paid";
                            }
                            else if (prepaymentApplied > 0)
                            {
                                status = "Partial";
                            }
        
                            // Create invoice
                            var invoice = new Invoice
                            {
                                InvoiceNumber = invoiceNumber,
                                CustomerId = model.CustomerId.Value,
                                InvoiceDate = model.InvoiceDate,
                                DueDate = model.DueDate,
                                SubTotal = subTotal,
                                RebateAmount = rebateAmount,
                                TransportAmount = transportAmount,
                                VatTypeSnapshot = vatType,
                                VatAmount = vatAmount,
                                TotalAmount = totalAmount,
                                PaidAmount = prepaymentApplied,
                                PrepaymentApplied = prepaymentApplied,
                                IsFullyPrepaid = prepaymentApplied >= totalAmount,
                                Status = status,
                                PaymentTerms = model.SelectedPaymentTerms,
                                LGAReceiptNumber = model.LGAReceiptNumber,
                                Notes = model.Notes,
                                CreatedBy = User.Identity?.Name,
                                CreatedAt = DateTime.Now
                            };
        
                            _context.Add(invoice);
                            await _context.SaveChangesAsync();

                            // Post the invoice to the general ledger. Creates a balanced
                            // Dr/Cr entry that splits sales, transport, VAT, and rebate
                            // into their respective accounts so the Trial Balance and P&L
                            // reflect the invoice immediately — not only when paid.
                            var invoiceJournalPosted = await CreateInvoiceJournalEntryAsync(invoice);

                            // Link weighments to this invoice BEFORE prepayment application
                            // so the drain logic can read SelectedPrepaymentId / LineItemId
                            // off the weighment and prefer it over strict FIFO.
                            foreach (var weighment in weighments)
                            {
                                weighment.IsInvoiced = true;
                                weighment.InvoiceId = invoice.Id;
                                weighment.ModifiedBy = User.Identity?.Name;
                                weighment.ModifiedAt = DateTime.Now;
                            }
                            await _context.SaveChangesAsync();

                            // Apply prepayment wallet after invoice and weighment links exist
                            bool prepaymentApplyOk = true;
                            if (prepaymentApplied > 0 && string.Equals(model.PaymentMode, "Prepayment", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    await ApplyPrepaymentToInvoiceAsync(invoice, prepaymentApplied);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error applying prepayment to invoice {InvoiceNumber}", invoice.InvoiceNumber);
                                    prepaymentApplyOk = false;
                                }
                            }

                            // Craft the success message so it's honest about what worked
                            // and what didn't. In the common case everything succeeds and
                            // the user sees the normal "created successfully" toast. When
                            // the ledger post or prepayment drain failed, we call it out
                            // so the operator knows to reconcile rather than assuming all
                            // is well.
                            if (invoiceJournalPosted && prepaymentApplyOk)
                            {
                                TempData["Success"] = $"Invoice {invoiceNumber} created successfully.";
                            }
                            else
                            {
                                var parts = new List<string>();
                                if (!invoiceJournalPosted) parts.Add("ledger journal entry");
                                if (!prepaymentApplyOk)    parts.Add("prepayment application");
                                var issueList = string.Join(" and ", parts);
                                TempData["Error"] =
                                    $"Invoice {invoiceNumber} was created, but the {issueList} could not " +
                                    $"be posted. Please run the reconciliation SQL or contact an administrator. " +
                                    $"See server logs for details.";
                            }
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
                        .Include(i => i.PrepaymentApplications)
                            .ThenInclude(pa => pa.CustomerPrepayment)
                        .FirstOrDefaultAsync(m => m.Id == id);
        
                    if (invoice == null)
                    {
                        return NotFound();
                    }
        
                    // Load any auto-generated payment journal entries linked to this invoice
                    var paymentEntries = await _context.JournalEntries
                        .Include(je => je.JournalEntryLines)
                            .ThenInclude(jel => jel.Account)
                        .Where(je =>
                            je.IsAutoGenerated &&
                            je.EntryNumber.StartsWith("PAY") &&
                            je.Description != null &&
                            je.Description.Contains(invoice.InvoiceNumber))
                        .OrderBy(je => je.EntryDate)
                        .ThenBy(je => je.Id)
                        .ToListAsync();
        
                    var viewModel = new InvoiceDetailsViewModel
                    {
                        Invoice = invoice,
                        AmountInWords = NumberToWordsConverter.ConvertInvoiceAmount(invoice.TotalAmount),
                        CanEditPayment = invoice.Status != "Paid",
                        CanCancel = invoice.Status == "Unpaid" && invoice.PaidAmount == 0,
                        PaymentJournalEntries = paymentEntries,
                        PrepaymentApplications = invoice.PrepaymentApplications.ToList()
                    };
        
                    return View(viewModel);
                }
        
                // GET: Invoice/PaymentReceipt/5
                public async Task<IActionResult> PaymentReceipt(int? id, int? journalEntryId)
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
        
                    var query = _context.JournalEntries
                        .Include(je => je.JournalEntryLines)
                            .ThenInclude(jel => jel.Account)
                        .Where(je =>
                            je.IsAutoGenerated &&
                            je.EntryNumber.StartsWith("PAY"));
        
                    if (journalEntryId.HasValue)
                    {
                        query = query.Where(je => je.Id == journalEntryId.Value);
                    }
                    else
                    {
                        query = query.Where(je =>
                            je.Description != null &&
                            je.Description.Contains(invoice.InvoiceNumber));
                    }
        
                    var paymentEntry = await query
                        .OrderByDescending(je => je.EntryDate)
                        .ThenByDescending(je => je.Id)
                        .FirstOrDefaultAsync();
        
                    if (paymentEntry == null)
                    {
                        TempData["Error"] = "No payment record found for this invoice.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
        
                    var receiptViewModel = new PaymentReceiptViewModel
                    {
                        Invoice = invoice,
                        PaymentEntry = paymentEntry,
                        CompanyDetails = GetCompanyDetails()
                    };
        
                    return View(receiptViewModel);
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

            // Populate payment-method dropdown so the view can render the <select>
            // sourced from the PaymentMethods lookup (Cash, Bank Transfer, etc.).
            await PopulatePaymentMethodsAsync(model);

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

                    // Resolve the payment-method name from the selected FK so
                    // the journal entry description / receipt show a readable
                    // label rather than an Id. Falls back to "Unknown" only if
                    // the client somehow posts a stale Id — validation should
                    // have caught the blank case via [Required] above.
                    var methodName = await _context.PaymentMethods
                        .Where(pm => pm.Id == (model.PaymentMethodId ?? 0))
                        .Select(pm => pm.Name)
                        .FirstOrDefaultAsync() ?? "Unknown";
                    model.PaymentMethodName = methodName;

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
                    await CreatePaymentJournalEntry(invoice, model.PaymentAmount, methodName);

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

                // Reverse the INV ledger posting so AR, Sales, VAT, Rebate, and Transport
                // all drop back by the invoice amount. Safe to call even if no entry exists.
                await ReverseInvoiceJournalEntryAsync(invoice);

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
                            .Where(w =>
                                w.CustomerId == customerId &&
                                w.Status == "Completed" &&
                                !w.IsInvoiced &&
                                // Also exclude weighments already tied to a non-cancelled invoice
                                (w.InvoiceId == null ||
                                 !_context.Invoices.Any(i => i.Id == w.InvoiceId && i.Status != "Cancelled")))
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
                //
                // Design note: totals are SUMMED from the per-weighment stored values
                // (SubTotal / VatAmount / RebateAmount / TotalAmount), NOT recomputed
                // from customer settings. Rationale:
                //   • Each weighment already applied the customer's per-ton rebate at
                //     save time (₦X/ton × NetTons). Re-applying customer.RebateAmount
                //     as a flat per-invoice discount would either double-count or
                //     wrong-count depending on net weight.
                //   • If the customer's rebate rate changed between weighment and
                //     invoice, the weighment's stored rebate is what the operator
                //     saw and agreed with the driver — that's the authoritative
                //     number to bill.
                //   • Transport is still customer-level (flat per invoice), because
                //     it's a per-delivery charge, not a per-ton one.
                [HttpPost]
                public async Task<JsonResult> CalculateInvoiceTotals([FromBody] InvoiceTotalsRequest request)
                {
                    try
                    {
                        var weighmentIds = request?.WeighmentIds ?? Array.Empty<int>();
                        var customerId = request?.CustomerId ?? 0;

                        var weighments = await _context.WeighmentTransactions
                            .Where(w =>
                                weighmentIds.Contains(w.Id) &&
                                w.Status == "Completed" &&
                                !w.IsInvoiced &&
                                (w.InvoiceId == null ||
                                 !_context.Invoices.Any(i => i.Id == w.InvoiceId && i.Status != "Cancelled")))
                            .ToListAsync();

                        // Sum the already-calculated weighment-level values. Each
                        // weighment's Subtotal / VAT / Rebate / Total was produced by
                        // ApplyVatTreatmentAsync on save, honoring customer VAT type
                        // and per-ton rebate — so we just add them up here.
                        decimal subTotal    = weighments.Sum(w => w.SubTotal     ?? 0m);
                        decimal vatAmount   = weighments.Sum(w => w.VatAmount    ?? 0m);
                        decimal rebateAmount = weighments.Sum(w => w.RebateAmount ?? 0m);
                        decimal weighmentsTotal = weighments.Sum(w => w.TotalAmount  ?? 0m);

                        // Transport is customer-level (flat per invoice). Load it from
                        // the customer profile, independently of the weighments.
                        decimal transportAmount = 0m;
                        string vatType = "Exclusive";
                        if (customerId > 0)
                        {
                            var customer = await _context.Customers
                                .Include(c => c.VatType)
                                .FirstOrDefaultAsync(c => c.Id == customerId);
                            if (customer != null)
                            {
                                transportAmount = customer.TransportRequired ? (customer.TransportAmount ?? 0m) : 0m;
                                vatType         = customer.VatType?.Name ?? "Exclusive";
                            }
                        }

                        // Final invoice total = weighments total (with their own
                        // Subtotal + VAT − Rebate already applied) + Transport.
                        // Transport is added post-hoc and does NOT pick up VAT here
                        // because it's typically a pass-through cost; if that needs
                        // to change it should be modeled alongside the weighment
                        // flow, not re-invented on the invoice side.
                        decimal totalAmount = Math.Round(weighmentsTotal + transportAmount, 2);

                        return Json(new
                        {
                            success = true,
                            subTotal,
                            rebateAmount,
                            transportAmount,
                            vatType,
                            vatAmount,
                            totalAmount,
                            weighmentCount = weighments.Count
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error calculating invoice totals");
                        return Json(new { success = false, message = "Error calculating totals" });
                    }
                }

                // Request payload for CalculateInvoiceTotals. Keeping this nested
                // in the controller file for locality.
                public class InvoiceTotalsRequest
                {
                    public int[] WeighmentIds { get; set; } = Array.Empty<int>();
                    public int CustomerId { get; set; }
                }
        
                // AJAX: Check customer credit (including prepayment wallet)
                //
                // Returns a consistent, operator-friendly credit snapshot. A few rules
                // bake into this endpoint that aren't obvious from the field names:
                //
                //   • currentOutstanding is floored at 0. A negative raw balance means
                //     legacy corruption from the old weighment NetWeight bug or a customer
                //     who overpaid — neither of which should display as "customer owes us
                //     −₦X". Zero is the correct operator-visible answer; any surplus is
                //     recoverable via the prepayment wallet, not the AR column.
                //
                //   • projectedOutstanding accounts for the prepayment wallet draining the
                //     new invoice first. If the wallet fully covers the invoice, projected
                //     stays at the current effective outstanding (no new AR created). If
                //     it partially covers, only the shortfall adds to outstanding.
                //
                // The raw customer.OutstandingBalance is also returned as rawOutstanding
                // for diagnostic purposes only; the UI should render currentOutstanding.
                [HttpGet]
                public async Task<JsonResult> CheckCustomerCredit(int customerId, decimal estimatedAmount)
                {
                    try
                    {
                        var customer = await _context.Customers.FindAsync(customerId);
                        if (customer == null)
                        {
                            return Json(new { success = false, message = "Customer not found" });
                        }

                        // Floor the raw column at 0 for display. See rationale above.
                        var rawOutstanding = customer.OutstandingBalance;
                        var currentOutstanding = rawOutstanding < 0 ? 0m : rawOutstanding;

                        // Include prepayment wallet in effective exposure
                        var prepaymentBalance = await GetAvailablePrepaymentAsync(customerId);

                        var effectiveOutstanding = currentOutstanding - prepaymentBalance;
                        if (effectiveOutstanding < 0)
                        {
                            effectiveOutstanding = 0;
                        }

                        // Projected: figure out how much of the new invoice the wallet
                        // covers, then only the leftover gets added to outstanding.
                        //   walletAfterCurrent = prepayment left after paying down current
                        //                         outstanding (if any)
                        //   shortfall = portion of the new invoice the wallet can't cover
                        var walletUsedForCurrent = Math.Min(currentOutstanding, prepaymentBalance);
                        var walletAfterCurrent  = prepaymentBalance - walletUsedForCurrent;
                        var shortfall           = Math.Max(0m, estimatedAmount - walletAfterCurrent);
                        var projectedOutstanding = effectiveOutstanding + shortfall;

                        var exceedsLimit = projectedOutstanding > customer.CreditLimit;
                        var availableCredit = customer.AvailableCredit;

                        return Json(new
                        {
                            success = true,
                            exceedsLimit,
                            availableCredit,
                            currentOutstanding,
                            rawOutstanding,
                            creditLimit = customer.CreditLimit,
                            prepaymentBalance,
                            effectiveOutstanding,
                            projectedOutstanding
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking customer credit for customer {CustomerId}", customerId);
                        return Json(new { success = false, message = "Error checking customer credit" });
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

        /// <summary>
        /// Posts the invoice to the general ledger with a single balanced journal
        /// entry. Splits the invoice amount across:
        ///   - 1101 Accounts Receivable        (Dr, total invoice)
        ///   - 4010 Sales Rebates & Discounts  (Dr, contra-revenue for rebate)
        ///   - 4001 Sale of Aggregates         (Cr, gross material sales)
        ///   - 4002 Transport & Delivery       (Cr, transport income)
        ///   - 2101 VAT Output Tax             (Cr, VAT payable)
        /// <para/>
        /// Returns true if the entry was posted (or the invoice had 0 total and
        /// needed no entry), false if a validation failed or the insert threw.
        /// Callers should surface false to the user so silent half-saved invoices
        /// don't pile up — that exact failure mode (missing account 4010) is
        /// what made this method start returning a status instead of swallowing.
        /// </summary>
        private async Task<bool> CreateInvoiceJournalEntryAsync(Invoice invoice)
        {
            try
            {
                // Zero-amount invoices don't need a ledger entry.
                if (invoice.TotalAmount <= 0) return true;

                var isInclusive = string.Equals(invoice.VatTypeSnapshot, "Inclusive", StringComparison.OrdinalIgnoreCase);

                decimal salesCredit;
                decimal transportCredit;
                decimal rebateDebit = invoice.RebateAmount;
                decimal vatCredit = invoice.VatAmount;
                decimal arDebit = invoice.TotalAmount;

                if (isInclusive)
                {
                    // Inclusive: subTotal and transportAmount are gross (include VAT).
                    // Back out the VAT share from each so revenue lines are net.
                    // VAT is apportioned between sales and transport by their gross share.
                    var grossBase = invoice.SubTotal - invoice.RebateAmount + invoice.TransportAmount;
                    if (grossBase <= 0)
                    {
                        _logger.LogWarning("Invoice {InvoiceNumber} has zero/negative gross base; skipping ledger post.", invoice.InvoiceNumber);
                        return false;
                    }

                    // VAT portion of the subtotal (after rebate) vs transport. Since the
                    // stored vatAmount was computed on (subTotal − rebate + transport),
                    // apportion it by those components' share.
                    var subNetOfRebate = invoice.SubTotal - invoice.RebateAmount;
                    var vatShareOfSub = grossBase > 0
                        ? Math.Round(invoice.VatAmount * (subNetOfRebate / grossBase), 2)
                        : 0m;
                    var vatShareOfTransport = invoice.VatAmount - vatShareOfSub;

                    // Net revenue = gross − VAT share embedded in that gross.
                    // Sales net credit backs out vatShareOfSub from (subTotal − rebate portion of sales).
                    // Rebate debit also needs to be net-of-VAT so the entry balances.
                    salesCredit     = Math.Round(invoice.SubTotal      - (vatShareOfSub + (invoice.RebateAmount > 0 ? Math.Round(invoice.VatAmount * (invoice.RebateAmount / grossBase), 2) : 0m)), 2);
                    transportCredit = Math.Round(invoice.TransportAmount - vatShareOfTransport, 2);
                    rebateDebit     = invoice.RebateAmount > 0
                        ? Math.Round(invoice.RebateAmount - Math.Round(invoice.VatAmount * (invoice.RebateAmount / grossBase), 2), 2)
                        : 0m;
                }
                else
                {
                    // Exclusive: subTotal and transportAmount are already net of VAT,
                    // so they post directly. VAT sits on top as its own credit.
                    salesCredit     = invoice.SubTotal;
                    transportCredit = invoice.TransportAmount;
                    // rebateDebit already set to invoice.RebateAmount above
                }

                // Balance check before posting: Sum(Dr) must equal Sum(Cr).
                var totalDebit  = arDebit + rebateDebit;
                var totalCredit = salesCredit + transportCredit + vatCredit;
                if (Math.Abs(totalDebit - totalCredit) > 0.02m)
                {
                    _logger.LogError(
                        "Invoice {InvoiceNumber} ledger posting would not balance: Dr={Debit} Cr={Credit}. Skipping.",
                        invoice.InvoiceNumber, totalDebit, totalCredit);
                    return false;
                }
                // Absorb rounding residual (<= 0.02) into the sales credit so the entry is exact.
                if (totalDebit != totalCredit)
                {
                    salesCredit += (totalDebit - totalCredit);
                }

                var accountsReceivableId = await GetAccountsReceivableId();
                var salesAccountId       = await GetAccountIdByCodeAsync("4001");
                var transportAccountId   = await GetAccountIdByCodeAsync("4002");
                var vatOutputAccountId   = await GetAccountIdByCodeAsync("2101");
                var rebateAccountId      = await GetAccountIdByCodeAsync("4010");

                // Pre-flight: ensure every account we're about to touch actually
                // exists. GetAccountIdByCodeAsync returns 0 for missing codes, which
                // would blow up the FK on insert. Checking here gives a readable
                // log line and lets us return false cleanly instead of throwing.
                if (accountsReceivableId == 0 || salesAccountId == 0
                    || (rebateDebit     > 0 && rebateAccountId    == 0)
                    || (transportCredit > 0 && transportAccountId == 0)
                    || (vatCredit       > 0 && vatOutputAccountId == 0))
                {
                    _logger.LogError(
                        "Invoice {InvoiceNumber}: one or more required ledger accounts missing " +
                        "(1101={AR}, 4001={Sales}, 4002={Transport}, 2101={VAT}, 4010={Rebate}). " +
                        "Run SQL/Migration_SeedSalesRebatesAccount.sql and verify the Chart of Accounts.",
                        invoice.InvoiceNumber,
                        accountsReceivableId, salesAccountId, transportAccountId, vatOutputAccountId, rebateAccountId);
                    return false;
                }

                var entry = new JournalEntry
                {
                    EntryNumber    = JournalEntry.GenerateEntryNumber("INV"),
                    EntryDate      = invoice.InvoiceDate,
                    Reference      = $"Invoice {invoice.InvoiceNumber}",
                    Description    = $"Sales invoice {invoice.InvoiceNumber} for customer {invoice.CustomerId}",
                    // PostedBy is an FK to AspNetUsers.Id — use the NameIdentifier
                    // claim, NOT Identity.Name. Wrong column would fail the FK and
                    // cause the whole entry (and every line) to vanish silently.
                    PostedBy       = GetCurrentUserId(),
                    IsAutoGenerated = true,
                    CreatedAt      = DateTime.Now
                };

                // Dr Accounts Receivable
                entry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId       = accountsReceivableId,
                    DebitAmount     = arDebit,
                    CreditAmount    = 0,
                    LineDescription = $"Receivable raised for invoice {invoice.InvoiceNumber}"
                });

                // Dr Sales Rebates (contra-revenue) — only when rebate > 0
                if (rebateDebit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId       = rebateAccountId,
                        DebitAmount     = rebateDebit,
                        CreditAmount    = 0,
                        LineDescription = $"Customer rebate on invoice {invoice.InvoiceNumber}"
                    });
                }

                // Cr Sale of Aggregates
                if (salesCredit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId       = salesAccountId,
                        DebitAmount     = 0,
                        CreditAmount    = salesCredit,
                        LineDescription = $"Sales revenue from invoice {invoice.InvoiceNumber}"
                    });
                }

                // Cr Transport & Delivery Income — only when transport > 0
                if (transportCredit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId       = transportAccountId,
                        DebitAmount     = 0,
                        CreditAmount    = transportCredit,
                        LineDescription = $"Transport charge on invoice {invoice.InvoiceNumber}"
                    });
                }

                // Cr VAT Output Tax — only when VAT > 0
                if (vatCredit > 0)
                {
                    entry.JournalEntryLines.Add(new JournalEntryLine
                    {
                        AccountId       = vatOutputAccountId,
                        DebitAmount     = 0,
                        CreditAmount    = vatCredit,
                        LineDescription = $"VAT output on invoice {invoice.InvoiceNumber}"
                    });
                }

                entry.RecalculateTotals();
                _context.JournalEntries.Add(entry);
                await _context.SaveChangesAsync();

                // Refresh running balances on every account we touched.
                await RecalculateAccountBalanceAsync(accountsReceivableId);
                await RecalculateAccountBalanceAsync(salesAccountId);
                await RecalculateAccountBalanceAsync(transportAccountId);
                await RecalculateAccountBalanceAsync(vatOutputAccountId);
                await RecalculateAccountBalanceAsync(rebateAccountId);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting invoice {InvoiceNumber} to the ledger", invoice.InvoiceNumber);
                return false;
            }
        }

        /// <summary>
        /// Reverses the INV journal entry for a cancelled invoice. Removes the
        /// entry entirely rather than posting a negating entry — matches the
        /// pattern used by Prepayment delete. Also recomputes affected account
        /// balances so the Trial Balance stays in sync.
        /// </summary>
        private async Task ReverseInvoiceJournalEntryAsync(Invoice invoice)
        {
            try
            {
                var prior = await _context.JournalEntries
                    .Include(je => je.JournalEntryLines)
                    .Where(je =>
                        je.EntryNumber.StartsWith("INV") &&
                        je.Reference != null &&
                        je.Reference.Contains(invoice.InvoiceNumber))
                    .ToListAsync();

                if (!prior.Any()) return;

                // Capture affected account IDs before deletion so we can recompute them after.
                var affectedAccountIds = prior
                    .SelectMany(je => je.JournalEntryLines.Select(l => l.AccountId))
                    .Distinct()
                    .ToList();

                foreach (var je in prior)
                {
                    _context.JournalEntryLines.RemoveRange(je.JournalEntryLines);
                }
                _context.JournalEntries.RemoveRange(prior);
                await _context.SaveChangesAsync();

                foreach (var accId in affectedAccountIds)
                {
                    await RecalculateAccountBalanceAsync(accId);
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reversing invoice journal entry for {InvoiceNumber}", invoice.InvoiceNumber);
            }
        }

        /// <summary>
        /// Recomputes CurrentBalance for a ledger account by summing all its
        /// journal entry lines. Mirrors the helper in PrepaymentController so
        /// both flows stay in sync.
        /// </summary>
        private async Task RecalculateAccountBalanceAsync(int accountId)
        {
            var account = await _context.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == accountId);
            if (account == null) return;

            var totals = await _context.JournalEntryLines
                .Where(l => l.AccountId == accountId)
                .GroupBy(l => l.AccountId)
                .Select(g => new { Debit = g.Sum(l => l.DebitAmount), Credit = g.Sum(l => l.CreditAmount) })
                .FirstOrDefaultAsync();

            decimal totalDebit  = totals?.Debit  ?? 0;
            decimal totalCredit = totals?.Credit ?? 0;
            decimal netMovement = (account.IsAssetAccount() || account.IsExpenseAccount())
                ? totalDebit - totalCredit
                : totalCredit - totalDebit;
            account.CurrentBalance = account.OpeningBalance + netMovement;
        }

        private async Task<int> GetAccountIdByCodeAsync(string code)
        {
            var acc = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.AccountCode == code);
            return acc?.Id ?? 0;
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
                    // PostedBy → AspNetUsers.Id; see GetCurrentUserId() helper.
                    PostedBy = GetCurrentUserId(),
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

                journalEntry.RecalculateTotals();

                _context.JournalEntries.Add(journalEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry for payment");
            }
        }

        private async Task CreatePrepaymentApplicationJournalEntry(Invoice invoice, decimal prepaymentAmount)
        {
            try
            {
                if (prepaymentAmount <= 0)
                    return;

                var entryNumber = JournalEntry.GenerateEntryNumber("ADVAPPLY");

                var journalEntry = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    EntryDate = DateTime.Now,
                    Reference = $"Prepayment applied to Invoice {invoice.InvoiceNumber}",
                    Description = $"Customer prepayment of {prepaymentAmount:C} applied to invoice {invoice.InvoiceNumber}",
                    // PostedBy → AspNetUsers.Id; see GetCurrentUserId() helper.
                    PostedBy = GetCurrentUserId(),
                    IsAutoGenerated = true,
                    CreatedAt = DateTime.Now
                };

                // Debit Customer Prepayments (liability down).
                //
                // Important: PrepaymentController credits a PER-CUSTOMER sub-account
                // (2103-000123 for customer 123), not the generic 2103 header.
                // We must debit the SAME sub-account here so the liability clears
                // correctly — otherwise the Trial Balance ends up with a phantom
                // negative on the customer account and a phantom positive on the
                // generic 2103. GetCustomerPrepaymentAccountIdAsync resolves the
                // right one (with a fallback to the generic 2103 for legacy rows).
                var customerPrepaymentAccountId = await GetCustomerPrepaymentAccountIdForCustomerAsync(invoice.CustomerId);
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = customerPrepaymentAccountId,
                    DebitAmount = prepaymentAmount,
                    CreditAmount = 0,
                    LineDescription = $"Reduce customer prepayment for invoice {invoice.InvoiceNumber}"
                });

                // Credit Accounts Receivable (receivable down)
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    AccountId = await GetAccountsReceivableId(),
                    DebitAmount = 0,
                    CreditAmount = prepaymentAmount,
                    LineDescription = $"Reduce receivable for invoice {invoice.InvoiceNumber} (prepayment applied)"
                });

                journalEntry.RecalculateTotals();

                _context.JournalEntries.Add(journalEntry);
                await _context.SaveChangesAsync();

                // Refresh running balances on both sides of the entry so the Trial
                // Balance reflects the application immediately.
                await RecalculateAccountBalanceAsync(customerPrepaymentAccountId);
                await RecalculateAccountBalanceAsync(await GetAccountsReceivableId());
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry for prepayment application");
                throw; // Rethrow so the caller's try/catch surfaces the failure
                       // to the user instead of a silent partial-post.
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

        private async Task<int> GetCustomerPrepaymentAccountId()
        {
            var prepayAccount = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(ca => ca.AccountCode == "2103");
            return prepayAccount?.Id ?? 5; // fallback to Accounts Payable if not found
        }

        /// <summary>
        /// Returns the ledger account Id to use when draining a customer's
        /// prepayment wallet. Prefers the per-customer sub-account
        /// ("2103-{customerId:D6}") that PrepaymentController creates on prepayment
        /// save; falls back to the generic 2103 header for legacy prepayments that
        /// predate sub-accounts or for customers whose sub-account wasn't created.
        /// <para/>
        /// This must match what PrepaymentController credits when the prepayment
        /// is created, otherwise draining posts against the wrong account and the
        /// Trial Balance shows phantom positives/negatives on both accounts.
        /// </summary>
        private async Task<int> GetCustomerPrepaymentAccountIdForCustomerAsync(int customerId)
        {
            var customerSpecificCode = $"2103-{customerId:D6}";
            var customerSpecific = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(ca => ca.AccountCode == customerSpecificCode);
            if (customerSpecific != null)
            {
                return customerSpecific.Id;
            }
            return await GetCustomerPrepaymentAccountId();
        }

        private async Task<decimal> GetAvailablePrepaymentAsync(int customerId)
        {
            var prepayments = await _context.CustomerPrepayments
                .Where(p => p.CustomerId == customerId && p.Status == "Active")
                .ToListAsync();

            return prepayments.Sum(p => p.Amount - p.UsedAmount);
        }

        private async Task ApplyPrepaymentToInvoiceAsync(Invoice invoice, decimal amountToApply)
        {
            if (amountToApply <= 0)
                return;

            decimal remaining = amountToApply;

            // Step 1 — If the underlying weighment picked a specific prepayment
            // (and optionally a specific line), drain that one first. The later
            // FIFO step then only handles the shortfall. This gives the operator
            // a predictable allocation: the prepayment they chose at the scale
            // actually gets used on the resulting invoice.
            var linkedWeighment = await _context.WeighmentTransactions
                .FirstOrDefaultAsync(w => w.InvoiceId == invoice.Id && w.SelectedPrepaymentId != null);

            if (linkedWeighment?.SelectedPrepaymentId != null)
            {
                var preferred = await _context.CustomerPrepayments
                    .Include(p => p.LineItems)
                    .FirstOrDefaultAsync(p =>
                        p.Id == linkedWeighment.SelectedPrepaymentId.Value &&
                        p.CustomerId == invoice.CustomerId &&
                        p.Status == "Active");

                if (preferred != null)
                {
                    remaining = await DrainPrepaymentAsync(
                        preferred,
                        invoice,
                        remaining,
                        preferredLineItemId: linkedWeighment.SelectedPrepaymentLineItemId);
                }
            }

            // Step 2 — FIFO drain for any remaining amount. Skips the preferred
            // prepayment (already drained above, possibly to exhaustion).
            if (remaining > 0)
            {
                var prepayments = await _context.CustomerPrepayments
                    .Include(p => p.LineItems)
                    .Where(p => p.CustomerId == invoice.CustomerId && p.Status == "Active")
                    .OrderBy(p => p.PrepaymentDate)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                var preferredId = linkedWeighment?.SelectedPrepaymentId;
                foreach (var prepayment in prepayments)
                {
                    if (remaining <= 0) break;
                    if (preferredId.HasValue && prepayment.Id == preferredId.Value) continue;

                    remaining = await DrainPrepaymentAsync(prepayment, invoice, remaining, preferredLineItemId: null);
                }
            }

            // Adjust customer outstanding balance
            var customer = await _context.Customers.FindAsync(invoice.CustomerId);
            if (customer != null)
            {
                customer.OutstandingBalance -= amountToApply;
                if (customer.OutstandingBalance < 0)
                {
                    customer.OutstandingBalance = 0;
                }
                customer.UpdateAvailableCredit();
            }

            await CreatePrepaymentApplicationJournalEntry(invoice, amountToApply);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Drains a single prepayment against the invoice. Handles both the
        /// new-style (with line items) and legacy (header-only) cases, and
        /// supports starting from a specific line item when the weighment
        /// flagged one. Returns how much of <paramref name="remaining"/> is
        /// still unallocated after this prepayment was drained.
        /// </summary>
        private async Task<decimal> DrainPrepaymentAsync(
            CustomerPrepayment prepayment,
            Invoice invoice,
            decimal remaining,
            int? preferredLineItemId)
        {
            var availableOnPrepayment = prepayment.Amount - prepayment.UsedAmount;
            if (availableOnPrepayment <= 0 || remaining <= 0) return remaining;

            if (prepayment.LineItems != null && prepayment.LineItems.Any())
            {
                // New-style: line items. Drain preferred line first (if any and
                // it belongs to this prepayment), then the rest in Id order.
                var lines = prepayment.LineItems.OrderBy(li => li.Id).ToList();
                if (preferredLineItemId.HasValue)
                {
                    var preferredLine = lines.FirstOrDefault(li => li.Id == preferredLineItemId.Value);
                    if (preferredLine != null)
                    {
                        lines.Remove(preferredLine);
                        lines.Insert(0, preferredLine);
                    }
                }

                foreach (var line in lines)
                {
                    if (remaining <= 0) break;

                    var availableOnLine = line.LineTotal - line.UsedAmount;
                    if (availableOnLine <= 0) continue;

                    var applyToLine = Math.Min(availableOnLine, remaining);
                    if (applyToLine <= 0) continue;

                    _context.PrepaymentApplications.Add(new PrepaymentApplication
                    {
                        CustomerPrepaymentId = prepayment.Id,
                        PrepaymentLineItemId = line.Id,
                        InvoiceId = invoice.Id,
                        AppliedAmount = applyToLine,
                        AppliedDate = DateTime.Now,
                        Description = $"Prepayment applied to invoice {invoice.InvoiceNumber} (line {line.Id})"
                    });

                    line.UsedAmount += applyToLine;
                    // Effective per-unit value for this line is the line's gross
                    // billed amount divided by its quantity. Works for both the
                    // new model (UnitPrice is the raw customer price, LineTotal
                    // includes VAT+rebate) and legacy records (UnitPrice was
                    // already the effective/baked price). Avoids divide-by-zero
                    // and falls back to UnitPrice if Quantity is missing.
                    if (line.Quantity > 0)
                    {
                        var effectivePerUnit = line.LineTotal / line.Quantity;
                        if (effectivePerUnit > 0)
                        {
                            var qtyUsedForThisApplication = Math.Round(applyToLine / effectivePerUnit, 3);
                            line.UsedQuantity = Math.Min(line.Quantity, line.UsedQuantity + qtyUsedForThisApplication);
                        }
                    }
                    else if (line.UnitPrice > 0)
                    {
                        var qtyUsedForThisApplication = Math.Round(applyToLine / line.UnitPrice, 3);
                        line.UsedQuantity = Math.Min(line.Quantity, line.UsedQuantity + qtyUsedForThisApplication);
                    }

                    prepayment.UsedAmount += applyToLine;
                    remaining -= applyToLine;
                }
            }
            else
            {
                // Legacy header-only prepayment: drain the header directly.
                var applyToHeader = Math.Min(availableOnPrepayment, remaining);
                if (applyToHeader > 0)
                {
                    _context.PrepaymentApplications.Add(new PrepaymentApplication
                    {
                        CustomerPrepaymentId = prepayment.Id,
                        InvoiceId = invoice.Id,
                        AppliedAmount = applyToHeader,
                        AppliedDate = DateTime.Now,
                        Description = $"Prepayment applied to invoice {invoice.InvoiceNumber}"
                    });

                    prepayment.UsedAmount += applyToHeader;
                    remaining -= applyToHeader;
                }
            }

            if (prepayment.Amount - prepayment.UsedAmount <= 0)
            {
                prepayment.Status = "Exhausted";
            }

            return remaining;
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

            // Quantity in tons. The stored NetWeight is in whatever WeightUnit
            // the weighment was saved as — legacy kg rows need dividing by 1000,
            // but current "Ton" rows pass through. Net is also recomputed from
            // Gross−Tare in case the stored value is stale (same defense we use
            // on the Index view).
            var isKg = string.Equals(weighment.WeightUnit, "kg", StringComparison.OrdinalIgnoreCase);
            decimal toTons(decimal? v) => isKg ? (v ?? 0m) / 1000m : (v ?? 0m);
            var qtyTons = toTons(weighment.GrossWeight) - toTons(weighment.TareWeight);
            if (qtyTons < 0) qtyTons = 0;

            return new List<InvoiceItemViewModel>
            {
                new InvoiceItemViewModel
                {
                    Description = $"{weighment.Material?.Name} - Vehicle {weighment.VehicleRegNumber}",
                    Quantity = qtyTons,
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

                // Repopulate dropdown so a failed POST + re-render keeps the
                // <select> working. Missing this would leave the list empty
                // on validation errors.
                await PopulatePaymentMethodsAsync(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating payment view model for invoice {InvoiceId}", model.InvoiceId);
            }
        }

        /// <summary>
        /// Populates the payment-method dropdown on <see cref="InvoicePaymentViewModel"/>
        /// from the active entries in the PaymentMethods lookup. Shared by the
        /// GET handler (RecordPayment) and <see cref="PopulatePaymentViewModelAsync"/>
        /// so both code paths render the same ordered list.
        /// </summary>
        private async Task PopulatePaymentMethodsAsync(InvoicePaymentViewModel model)
        {
            model.PaymentMethods = await _context.PaymentMethods
                .Where(pm => pm.IsActive)
                .OrderBy(pm => pm.DisplayOrder)
                .ThenBy(pm => pm.Id)
                .Select(pm => new SelectListItem
                {
                    Value = pm.Id.ToString(),
                    Text = pm.Name
                })
                .ToListAsync();
        }
    }
}