# Inventory & Production Roadmap

This document tracks the multi-phase rollout of the inventory + production
accounting system. Each phase delivers a working slice; all earlier phases
must be applied before the next is meaningful.

---

## Phase 1 — Schema (this PR)

**Status:** ✅ Delivered.

- Domain entities: `RawMaterial`, `RawMaterialReceipt`, `ProductionRun`,
  `ProductionRunOutput`, `StockMovement`, `MaterialCostState`.
- DbContext registration with FK rules and indexes.
- Manual SQL: `Data/Migrations/manual/add_inventory_phase1.sql`.
- Chart of Accounts seed: 1301 / 1302 / 1303 / 5001 / 5002.
- **No controllers, no UI, no posting.** The schema is a foundation only.

**Apply by:** running the SQL script (or `dotnet ef migrations add AddInventoryPhase1 -o Data/Migrations` followed by `dotnet ef database update`).

**Verify by:** confirming the six new tables exist and the five new ChartOfAccounts rows are present:

```sql
SELECT name FROM sys.tables
WHERE name IN ('RawMaterials','RawMaterialReceipts','ProductionRuns',
               'ProductionRunOutputs','StockMovements','MaterialCostStates');

SELECT AccountCode, AccountName FROM dbo.ChartOfAccounts
WHERE AccountCode IN ('1301','1302','1303','5001','5002');
```

---

## Phase 2 — Production & raw-receipt UI (operational only, no posting)

**Status:** Not started.

Adds CRUD screens so operators can record what's happening. Stock movements
are written but **no journal entries are posted yet**. This lets you start
collecting real production data immediately while we build out the accounting
side carefully.

- `RawMaterialController` — Create/Index/Edit for the raw material catalogue.
- `RawMaterialReceiptController` — record incoming raw rock; auto-generates a `StockMovement` with `MovementType = "RawReceipt"`.
- `ProductionController` — Create/Index/Edit/Post for runs.
  - Draft state allows free editing.
  - Posting validates mass balance (input ≥ outputs + waste), allocates costs across outputs by weight share, generates one `StockMovement` per output (`ProductionOutput`), one for the input (`ProductionInput`), updates `MaterialCostState` for both raw-in and finished-out.
- `StockController` — read-only stock-on-hand view by quarry / material; replaces the existing stub stock report.

**Out of scope for Phase 2:**
- Journal entries (Phase 4).
- Weighbridge decrement (Phase 3).
- Stock transfers between yards (Phase 5).

---

## Phase 3 — Weighbridge integration

**Status:** Not started.

Sales actually decrement inventory.

- When a `WeighmentTransaction` moves to `Status = "Completed"`:
  - Look up the customer's source quarry (or the weighment's quarry if it's not customer-bound).
  - Read the `MaterialCostState` for `(QuarryId, MaterialId)` to get the current weighted-average cost.
  - Insert a `StockMovement` with `MovementType = "Sale"`, negative quantity, `UnitCost = currentWAC`, link to the weighment.
  - Update `MaterialCostState`: subtract qty, subtract `qty × oldAvgCost` from total cost. Average stays the same.
  - Refuse the completion if `MaterialCostState.QuantityOnHand < weighmentQty` (configurable: refuse vs warn vs allow-negative).

**Risk:** this changes the existing weighment posting path. Needs careful regression testing — invoice posting, customer credit checks, prepayment draws must all still work.

---

## Phase 4 — Journal entry posting

**Status:** Not started.

Wire the inventory transactions into the accounting books.

- **Raw material receipt:** `Dr Raw Material Inventory (1301) / Cr Cash or A/P`.
- **Production run posted:** `Dr Finished Goods Inventory (1302) [for each output × allocated cost] / Cr Raw Material Inventory (1301) [InputTotalCost] / Dr Production Variance (5002) [waste cost share]`.
- **Sale completed:** in addition to the existing customer-side posting, add `Dr COGS (5001) / Cr Finished Goods Inventory (1302)` at the WAC.
- **Stock adjustments:** `Dr/Cr Inventory / Dr/Cr Variance`.

The cost-flow service is the heart of this phase — a single class responsible for reading current WAC, computing posting amounts, and applying state updates within the same DB transaction as the document save. Until this exists, Phases 2-3 run "operational only" with no GL impact.

---

## Phase 5 — Stock transfers between yards

**Status:** Not started.

`StockTransfer` entity + controller. Transfers post a paired `TransferOut` / `TransferIn` movement at the source yard's WAC. No journal impact (inventory account total unchanged) but the per-yard cost states update accordingly.

---

## Phase 6 — Reports

**Status:** Not started.

- Stock report: current on-hand qty + value per (yard, material). Replaces existing stub.
- Stock movement report: filterable log of every change.
- Production efficiency: input vs output yields per period.
- Cost-of-production trend: WAC over time per material.
- Inventory valuation report: as-of snapshot for tax / audit.

---

## Phase 7 — Year-end & adjustments

**Status:** Not started.

- Stocktake workflow: enter physical count, system computes variance, posts adjustment movements.
- Period close lock: prevent backdated movements once a month / fiscal year is closed.
- Re-cost workflow for retrospective price corrections (rare, but needed when a raw rock invoice arrives late).

---

## Open design questions for later phases

1. **What if a customer's quarry is unset and a sale comes in?** Phase 3 needs to handle walk-ins / no-customer weighments. Probably fall back to the weighbridge's quarry. Confirm at Phase 3 design time.
2. **Should `ProductionInput` create a negative `MaterialCostState.QuantityOnHand` if there's no raw material in stock?** Probably refuse with a hard error in Phase 2 — operator must record receipts first.
3. **Multi-quarry transfers + WAC.** When stock arrives at Quarry B from Quarry A, Quarry B's WAC needs to absorb it at A's WAC. Confirm this at Phase 5.
4. **Roll-forward of `Material.UnitPrice` (sales price) vs `MaterialCostState.AverageUnitCost`.** Two different concepts — UnitPrice is what we sell at; AverageUnitCost is what we paid to produce. Reports must be careful never to confuse them.
