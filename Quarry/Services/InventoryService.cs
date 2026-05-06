using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Data;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Services
{
    /// <summary>
    /// Single entry point for any code path that changes inventory state.
    /// Wraps the dual update of (a) writing a StockMovement audit row and
    /// (b) updating the running MaterialCostState into one method per
    /// movement type, so controllers don't have to re-derive the math.
    /// <para/>
    /// Does NOT call SaveChangesAsync. The caller controls the transaction so
    /// the inventory mutation can be batched with the source-document save
    /// (e.g. a ProductionRun and all its outputs in one transaction).
    /// <para/>
    /// Does NOT post journal entries. That comes in Phase 4 — a separate
    /// IInventoryAccountingService will subscribe to the same operations and
    /// add the GL postings within the same transaction.
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// Reads the current weighted-average cost state for a (quarry, finished
        /// material) pair. Returns null when no state row exists yet (i.e.
        /// nothing has ever been produced or received for that combination).
        /// </summary>
        Task<MaterialCostState?> GetMaterialStateAsync(int quarryId, int materialId);

        /// <summary>
        /// Same as <see cref="GetMaterialStateAsync"/> but for raw-material inputs.
        /// </summary>
        Task<MaterialCostState?> GetRawMaterialStateAsync(int quarryId, int rawMaterialId);

        /// <summary>
        /// Records receipt of raw material. Writes a +qty StockMovement and
        /// updates the per-quarry-per-raw-material WAC.
        /// </summary>
        Task RecordRawReceiptAsync(RawMaterialReceipt receipt, string? createdBy);

        /// <summary>
        /// Posts a draft production run. Validates mass balance, draws the
        /// input cost from the current raw-material WAC, allocates that cost
        /// across outputs by weight share, writes one StockMovement per
        /// input/output, and updates MaterialCostState rows accordingly.
        /// The caller is responsible for setting Status="Posted" and
        /// PostedAt after this returns successfully.
        /// </summary>
        /// <returns>
        /// The total cost drawn from raw inventory (== run.InputTotalCost
        /// after this method runs).
        /// </returns>
        Task<decimal> PostProductionRunAsync(ProductionRun run, string? createdBy);

        /// <summary>
        /// Reverses a previously-posted production run. Writes negative
        /// movements for each output and a positive movement for the input,
        /// each at the original costs frozen on the run. Used by the Cancel
        /// flow on Posted runs.
        /// </summary>
        Task ReverseProductionRunAsync(ProductionRun run, string? createdBy);

        /// <summary>
        /// Records a sale: the dispatched quantity drops out of finished-goods
        /// inventory at the prevailing weighted-average cost. Writes a -qty
        /// Sale movement linked to the weighment, and decrements the
        /// MaterialCostState for (quarry, material). Refuses with a clear
        /// error if there's not enough stock on hand or no state row exists.
        /// <para/>
        /// Caller must ensure the weighment has been saved (so it has an Id
        /// for the FK) before calling. Quantity is in the same unit as the
        /// MaterialCostState — controllers are responsible for converting kg
        /// to tons before calling.
        /// </summary>
        /// <returns>The unit cost (WAC) used for the sale, for caller logging.</returns>
        Task<decimal> RecordSaleAsync(int quarryId, int materialId, decimal quantityInStockUnit, int weighmentId, string? transactionNumber, string? createdBy);

        /// <summary>
        /// Reverses a previously-recorded sale. Used when a Completed weighment
        /// is moved back to InProgress or Cancelled. Writes a +qty movement
        /// at the same unit cost the sale used (looked up from the prior
        /// movement) and restores the MaterialCostState quantity. The total
        /// cost is restored at that frozen unit cost, which keeps the running
        /// average stable in the common case.
        /// </summary>
        Task ReverseSaleAsync(int weighmentId, string? createdBy);
    }

    /// <summary>
    /// Implementation. Pure database I/O — all math is local.
    /// </summary>
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(ApplicationDbContext context, ILogger<InventoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MaterialCostState?> GetMaterialStateAsync(int quarryId, int materialId)
        {
            return await _context.MaterialCostStates
                .FirstOrDefaultAsync(s => s.QuarryId == quarryId && s.MaterialId == materialId);
        }

        public async Task<MaterialCostState?> GetRawMaterialStateAsync(int quarryId, int rawMaterialId)
        {
            return await _context.MaterialCostStates
                .FirstOrDefaultAsync(s => s.QuarryId == quarryId && s.RawMaterialId == rawMaterialId);
        }

        public async Task RecordRawReceiptAsync(RawMaterialReceipt receipt, string? createdBy)
        {
            if (receipt.Quantity <= 0)
                throw new InvalidOperationException("Receipt quantity must be positive.");

            // 1) Audit row
            var movement = new StockMovement
            {
                MovementDate = receipt.ReceiptDate,
                QuarryId = receipt.QuarryId,
                RawMaterialId = receipt.RawMaterialId,
                MaterialId = null,
                MovementType = "RawReceipt",
                Quantity = receipt.Quantity,
                UnitCost = receipt.UnitCost,
                RawMaterialReceiptId = receipt.Id,
                Notes = receipt.Notes,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            };
            _context.StockMovements.Add(movement);

            // 2) Running WAC update for (quarry, raw material)
            var state = await GetRawMaterialStateAsync(receipt.QuarryId, receipt.RawMaterialId);
            if (state == null)
            {
                state = new MaterialCostState
                {
                    QuarryId = receipt.QuarryId,
                    RawMaterialId = receipt.RawMaterialId,
                    MaterialId = null,
                    QuantityOnHand = 0m,
                    TotalCostOnHand = 0m
                };
                _context.MaterialCostStates.Add(state);
            }

            state.QuantityOnHand = Math.Round(state.QuantityOnHand + receipt.Quantity, 3);
            state.TotalCostOnHand = Math.Round(state.TotalCostOnHand + (receipt.Quantity * receipt.UnitCost), 2);
            state.LastUpdated = DateTime.Now;
        }

        public async Task<decimal> PostProductionRunAsync(ProductionRun run, string? createdBy)
        {
            if (string.Equals(run.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run is already posted.");
            if (run.InputQuantity <= 0)
                throw new InvalidOperationException("Input quantity must be positive.");
            if (run.Outputs == null || run.Outputs.Count == 0)
                throw new InvalidOperationException("A production run needs at least one output line before it can be posted.");

            var totalOutput = run.Outputs.Sum(o => o.Quantity);
            if (totalOutput <= 0)
                throw new InvalidOperationException("Total output quantity must be positive.");

            // Mass balance: outputs + waste must not exceed input. We allow
            // (input - outputs - waste) > 0 to be treated as additional waste
            // implicitly, so we tolerate a positive delta but reject negative.
            var massBalanceDelta = run.InputQuantity - totalOutput - run.WasteQuantity;
            if (massBalanceDelta < -0.0001m)
            {
                throw new InvalidOperationException(
                    $"Mass balance failed: outputs ({totalOutput}) + waste ({run.WasteQuantity}) " +
                    $"exceed input ({run.InputQuantity}) by {-massBalanceDelta:N3}.");
            }
            // Roll any unaccounted-for delta into waste for posterity.
            if (massBalanceDelta > 0.0001m)
            {
                run.WasteQuantity = Math.Round(run.WasteQuantity + massBalanceDelta, 3);
            }

            // 1) Draw input cost from raw inventory at current WAC.
            var rawState = await GetRawMaterialStateAsync(run.QuarryId, run.RawMaterialId);
            if (rawState == null || rawState.QuantityOnHand < run.InputQuantity - 0.0001m)
            {
                var available = rawState?.QuantityOnHand ?? 0m;
                throw new InvalidOperationException(
                    $"Insufficient raw material on hand at this quarry: " +
                    $"need {run.InputQuantity:N3}, have {available:N3}. " +
                    "Record a Raw Material Receipt before posting this run.");
            }
            var rawAvgCost = rawState.AverageUnitCost;
            var inputTotalCost = Math.Round(run.InputQuantity * rawAvgCost, 2);
            run.InputTotalCost = inputTotalCost;

            // 2) ProductionInput audit row + raw state decrement.
            //    On a draw at the prevailing average, the average is unchanged.
            _context.StockMovements.Add(new StockMovement
            {
                MovementDate = run.RunDate,
                QuarryId = run.QuarryId,
                RawMaterialId = run.RawMaterialId,
                MaterialId = null,
                MovementType = "ProductionInput",
                Quantity = -run.InputQuantity,
                UnitCost = rawAvgCost,
                ProductionRunId = run.Id,
                Notes = $"Consumed by run {run.RunNumber}",
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });

            rawState.QuantityOnHand = Math.Round(rawState.QuantityOnHand - run.InputQuantity, 3);
            rawState.TotalCostOnHand = Math.Round(rawState.TotalCostOnHand - inputTotalCost, 2);
            // Floor at zero in case of rounding drift on the last unit.
            if (rawState.QuantityOnHand < 0) rawState.QuantityOnHand = 0;
            if (rawState.TotalCostOnHand < 0) rawState.TotalCostOnHand = 0;
            rawState.LastUpdated = DateTime.Now;

            // 3) Allocate input cost to outputs by weight share. Waste does not
            //    pick up any cost here — Phase 4 will post it to Production
            //    Variance (5002) instead. Last output absorbs any rounding
            //    remainder so allocations sum exactly to inputTotalCost.
            decimal allocated = 0m;
            var outputs = run.Outputs.OrderBy(o => o.Id).ToList();
            for (int i = 0; i < outputs.Count; i++)
            {
                var o = outputs[i];
                decimal share;
                if (i == outputs.Count - 1)
                {
                    share = inputTotalCost - allocated;
                }
                else
                {
                    share = Math.Round(inputTotalCost * (o.Quantity / totalOutput), 2);
                    allocated += share;
                }
                o.AllocatedCost = share;

                // ProductionOutput audit row
                _context.StockMovements.Add(new StockMovement
                {
                    MovementDate = run.RunDate,
                    QuarryId = run.QuarryId,
                    MaterialId = o.MaterialId,
                    RawMaterialId = null,
                    MovementType = "ProductionOutput",
                    Quantity = o.Quantity,
                    UnitCost = o.Quantity > 0 ? Math.Round(o.AllocatedCost / o.Quantity, 4) : 0m,
                    ProductionRunId = run.Id,
                    ProductionRunOutputId = o.Id,
                    Notes = $"Produced by run {run.RunNumber}",
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.Now
                });

                // Finished-goods state for (quarry, material): WAC update.
                var fgState = await GetMaterialStateAsync(run.QuarryId, o.MaterialId);
                if (fgState == null)
                {
                    fgState = new MaterialCostState
                    {
                        QuarryId = run.QuarryId,
                        MaterialId = o.MaterialId,
                        RawMaterialId = null,
                        QuantityOnHand = 0m,
                        TotalCostOnHand = 0m
                    };
                    _context.MaterialCostStates.Add(fgState);
                }
                fgState.QuantityOnHand = Math.Round(fgState.QuantityOnHand + o.Quantity, 3);
                fgState.TotalCostOnHand = Math.Round(fgState.TotalCostOnHand + o.AllocatedCost, 2);
                fgState.LastUpdated = DateTime.Now;
            }

            return inputTotalCost;
        }

        public async Task ReverseProductionRunAsync(ProductionRun run, string? createdBy)
        {
            if (!string.Equals(run.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only Posted runs can be reversed.");

            // 1) Re-add input quantity at the original input avg cost.
            var inputAvgCost = run.InputQuantity > 0
                ? Math.Round(run.InputTotalCost / run.InputQuantity, 4)
                : 0m;

            _context.StockMovements.Add(new StockMovement
            {
                MovementDate = DateTime.Now,
                QuarryId = run.QuarryId,
                RawMaterialId = run.RawMaterialId,
                MaterialId = null,
                MovementType = "ProductionInput",
                Quantity = run.InputQuantity, // positive: returning to raw inventory
                UnitCost = inputAvgCost,
                ProductionRunId = run.Id,
                Notes = $"Reversal of run {run.RunNumber}",
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });

            var rawState = await GetRawMaterialStateAsync(run.QuarryId, run.RawMaterialId);
            if (rawState == null)
            {
                rawState = new MaterialCostState
                {
                    QuarryId = run.QuarryId,
                    RawMaterialId = run.RawMaterialId,
                    MaterialId = null,
                    QuantityOnHand = 0m,
                    TotalCostOnHand = 0m
                };
                _context.MaterialCostStates.Add(rawState);
            }
            rawState.QuantityOnHand = Math.Round(rawState.QuantityOnHand + run.InputQuantity, 3);
            rawState.TotalCostOnHand = Math.Round(rawState.TotalCostOnHand + run.InputTotalCost, 2);
            rawState.LastUpdated = DateTime.Now;

            // 2) Subtract each output back from finished-goods state.
            foreach (var o in run.Outputs)
            {
                _context.StockMovements.Add(new StockMovement
                {
                    MovementDate = DateTime.Now,
                    QuarryId = run.QuarryId,
                    MaterialId = o.MaterialId,
                    RawMaterialId = null,
                    MovementType = "ProductionOutput",
                    Quantity = -o.Quantity,
                    UnitCost = o.Quantity > 0 ? Math.Round(o.AllocatedCost / o.Quantity, 4) : 0m,
                    ProductionRunId = run.Id,
                    ProductionRunOutputId = o.Id,
                    Notes = $"Reversal of run {run.RunNumber}",
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.Now
                });

                var fgState = await GetMaterialStateAsync(run.QuarryId, o.MaterialId);
                if (fgState != null)
                {
                    fgState.QuantityOnHand = Math.Round(fgState.QuantityOnHand - o.Quantity, 3);
                    fgState.TotalCostOnHand = Math.Round(fgState.TotalCostOnHand - o.AllocatedCost, 2);
                    if (fgState.QuantityOnHand < 0) fgState.QuantityOnHand = 0;
                    if (fgState.TotalCostOnHand < 0) fgState.TotalCostOnHand = 0;
                    fgState.LastUpdated = DateTime.Now;
                }
            }
        }

        public async Task<decimal> RecordSaleAsync(int quarryId, int materialId, decimal quantityInStockUnit, int weighmentId, string? transactionNumber, string? createdBy)
        {
            if (quantityInStockUnit <= 0)
                throw new InvalidOperationException("Sale quantity must be positive.");

            // Read the current cost state for this (quarry, material). If no
            // row exists or there's not enough on hand, refuse — same rule as
            // production: you can't sell what you haven't produced.
            var state = await GetMaterialStateAsync(quarryId, materialId);
            if (state == null || state.QuantityOnHand < quantityInStockUnit - 0.0001m)
            {
                var available = state?.QuantityOnHand ?? 0m;
                throw new InvalidOperationException(
                    $"Insufficient finished-goods inventory at this quarry: " +
                    $"need {quantityInStockUnit:N3}, have {available:N3}. " +
                    "Post a Production Run for this material first, or pick a different quarry.");
            }

            var unitCost = state.AverageUnitCost;
            var totalCost = Math.Round(quantityInStockUnit * unitCost, 2);

            // Sale audit row: negative qty by convention.
            _context.StockMovements.Add(new StockMovement
            {
                MovementDate = DateTime.Now,
                QuarryId = quarryId,
                MaterialId = materialId,
                RawMaterialId = null,
                MovementType = "Sale",
                Quantity = -quantityInStockUnit,
                UnitCost = unitCost,
                WeighmentTransactionId = weighmentId,
                Notes = string.IsNullOrEmpty(transactionNumber)
                    ? $"Sale: weighment {weighmentId}"
                    : $"Sale: weighment {transactionNumber}",
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });

            // Decrement state. WAC is unchanged on a sale (we're drawing at the
            // current average) so just subtract qty and qty*avg from the totals.
            state.QuantityOnHand = Math.Round(state.QuantityOnHand - quantityInStockUnit, 3);
            state.TotalCostOnHand = Math.Round(state.TotalCostOnHand - totalCost, 2);
            if (state.QuantityOnHand < 0) state.QuantityOnHand = 0;
            if (state.TotalCostOnHand < 0) state.TotalCostOnHand = 0;
            state.LastUpdated = DateTime.Now;

            return unitCost;
        }

        public async Task ReverseSaleAsync(int weighmentId, string? createdBy)
        {
            // Find the prior Sale movement(s) for this weighment. There should
            // be exactly one in normal flow but we handle the rare case of
            // multiple (e.g. if Phase 4+ ever splits a single dispatch across
            // sub-movements) by reversing them all.
            var sales = await _context.StockMovements
                .Where(m => m.WeighmentTransactionId == weighmentId && m.MovementType == "Sale")
                .ToListAsync();

            if (sales.Count == 0)
            {
                // Nothing to reverse — the weighment was never marked Completed,
                // or the sale movement was already reversed. Treat as no-op
                // rather than an error so the caller can call this idempotently.
                return;
            }

            foreach (var sale in sales)
            {
                if (!sale.MaterialId.HasValue) continue;

                // Original sale was a -qty row. Reversal is +qty at the same
                // frozen unit cost. We don't go re-read the current WAC —
                // restoring at the original cost keeps the books consistent
                // with what was recorded at sale time.
                var revQty = -sale.Quantity; // sale.Quantity was negative; -negative = positive
                var unitCost = sale.UnitCost;
                var revCost = Math.Round(revQty * unitCost, 2);

                _context.StockMovements.Add(new StockMovement
                {
                    MovementDate = DateTime.Now,
                    QuarryId = sale.QuarryId,
                    MaterialId = sale.MaterialId,
                    RawMaterialId = null,
                    MovementType = "Sale",
                    Quantity = revQty,
                    UnitCost = unitCost,
                    WeighmentTransactionId = weighmentId,
                    Notes = $"Reversal of sale on weighment {weighmentId}",
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.Now
                });

                var state = await GetMaterialStateAsync(sale.QuarryId, sale.MaterialId.Value);
                if (state == null)
                {
                    state = new MaterialCostState
                    {
                        QuarryId = sale.QuarryId,
                        MaterialId = sale.MaterialId,
                        RawMaterialId = null,
                        QuantityOnHand = 0m,
                        TotalCostOnHand = 0m
                    };
                    _context.MaterialCostStates.Add(state);
                }
                state.QuantityOnHand = Math.Round(state.QuantityOnHand + revQty, 3);
                state.TotalCostOnHand = Math.Round(state.TotalCostOnHand + revCost, 2);
                state.LastUpdated = DateTime.Now;
            }
        }
    }
}
