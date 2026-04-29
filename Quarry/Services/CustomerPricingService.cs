using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Services
{
    /// <summary>
    /// Single source of truth for "what price/VAT should apply for this customer
    /// on this material?". Used by Weighment, Quotation, and Invoice flows so
    /// that pricing logic isn't scattered across controllers.
    /// </summary>
    public interface ICustomerPricingService
    {
        /// <summary>
        /// Returns the effective unit price for (customerId, materialId) as of the
        /// given date. Falls back to the material's catalog price if no
        /// per-customer price exists.
        /// </summary>
        Task<decimal> GetUnitPriceAsync(int customerId, int materialId, DateTime? asOf = null);

        /// <summary>
        /// Returns full pricing context — the price, the VAT rate that should
        /// apply, whether the source was customer-specific or catalog, and the
        /// customer's VAT type. Use when you need to make decisions based on
        /// which source the price came from (e.g. show a "custom price" badge).
        /// </summary>
        Task<CustomerPriceResult> GetPricingAsync(int customerId, int materialId, DateTime? asOf = null);

        /// <summary>
        /// Persists a new price row for (customerId, materialId). Marks any prior
        /// row for the same pair as IsCurrent = false and adds the new row as
        /// IsCurrent = true. This creates history automatically.
        /// </summary>
        Task AddOrUpdatePriceAsync(int customerId, int materialId, decimal unitPrice, decimal? vatRate, DateTime effectiveFrom, string? createdBy, string? notes = null);
    }

    public class CustomerPriceResult
    {
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public bool IsCustomerSpecific { get; set; }
        public string VatType { get; set; } = "Exclusive"; // "Inclusive" | "Exclusive"
    }

    public class CustomerPricingService : ICustomerPricingService
    {
        private readonly ApplicationDbContext _context;

        public CustomerPricingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetUnitPriceAsync(int customerId, int materialId, DateTime? asOf = null)
        {
            var result = await GetPricingAsync(customerId, materialId, asOf);
            return result.UnitPrice;
        }

        public async Task<CustomerPriceResult> GetPricingAsync(int customerId, int materialId, DateTime? asOf = null)
        {
            var effectiveDate = asOf ?? DateTime.Now;

            // Customer VAT type (Inclusive / Exclusive). Default to Exclusive for legacy rows.
            var vatTypeName = await _context.Customers
                .Where(c => c.Id == customerId)
                .Select(c => c.VatType != null ? c.VatType.Name : null)
                .FirstOrDefaultAsync() ?? "Exclusive";

            // Latest effective per-customer price as of the given date. Using
            // IsCurrent first as a fast path, then falling back to EffectiveFrom
            // in case the flag is stale or backdated rows exist.
            var customerPrice = await _context.CustomerMaterialPrices
                .Where(p => p.CustomerId == customerId
                         && p.MaterialId == materialId
                         && p.EffectiveFrom <= effectiveDate)
                .OrderByDescending(p => p.EffectiveFrom)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (customerPrice != null)
            {
                // Resolve VAT rate: per-customer override, else the material's own rate.
                var vatRate = customerPrice.VatRate;
                if (!vatRate.HasValue)
                {
                    vatRate = await _context.Materials
                        .Where(m => m.Id == materialId)
                        .Select(m => (decimal?)m.VatRate)
                        .FirstOrDefaultAsync() ?? 7.5m;
                }

                return new CustomerPriceResult
                {
                    UnitPrice = customerPrice.UnitPrice,
                    VatRate = vatRate.Value,
                    IsCustomerSpecific = true,
                    VatType = vatTypeName
                };
            }

            // No customer-specific price → fall back to the material catalog.
            var material = await _context.Materials
                .Where(m => m.Id == materialId)
                .Select(m => new { m.UnitPrice, m.VatRate })
                .FirstOrDefaultAsync();

            return new CustomerPriceResult
            {
                UnitPrice = material?.UnitPrice ?? 0m,
                VatRate = material?.VatRate ?? 7.5m,
                IsCustomerSpecific = false,
                VatType = vatTypeName
            };
        }

        public async Task AddOrUpdatePriceAsync(int customerId, int materialId, decimal unitPrice, decimal? vatRate, DateTime effectiveFrom, string? createdBy, string? notes = null)
        {
            // Unmark any existing "current" rows for this pair — history is preserved
            // but the flag should only point at one row.
            var existingCurrent = await _context.CustomerMaterialPrices
                .Where(p => p.CustomerId == customerId && p.MaterialId == materialId && p.IsCurrent)
                .ToListAsync();

            foreach (var prior in existingCurrent)
            {
                prior.IsCurrent = false;
            }

            _context.CustomerMaterialPrices.Add(new CustomerMaterialPrice
            {
                CustomerId = customerId,
                MaterialId = materialId,
                UnitPrice = unitPrice,
                VatRate = vatRate,
                EffectiveFrom = effectiveFrom,
                IsCurrent = true,
                Notes = notes,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });
        }
    }
}
