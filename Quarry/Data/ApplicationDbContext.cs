using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Models;
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

        // Accounting Fiscal Years
        public DbSet<FiscalYear> FiscalYears { get; set; }
        public DbSet<AccountFiscalYearBalance> AccountFiscalYearBalances { get; set; }

        // Quotations
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<PayrollRun> PayrollRuns { get; set; }
        public DbSet<EmployeeSalary> EmployeeSalaries { get; set; }
        public DbSet<StockYard> StockYards { get; set; }
        public DbSet<CustomerPrepayment> CustomerPrepayments { get; set; }
        public DbSet<PrepaymentLineItem> PrepaymentLineItems { get; set; }
        public DbSet<PrepaymentApplication> PrepaymentApplications { get; set; }

        // Customer classification + per-customer pricing
        public DbSet<CustomerType> CustomerTypes { get; set; }
        public DbSet<VatType> VatTypes { get; set; }
        public DbSet<CustomerMaterialPrice> CustomerMaterialPrices { get; set; }

        // Payment methods lookup (Cash, Bank Transfer, Online Payment, Cheque, ...)
        public DbSet<PaymentMethod> PaymentMethods { get; set; }

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

            // Configure WeighmentTransaction relationships
            modelBuilder.Entity<WeighmentTransaction>()
                .HasOne(w => w.Customer)
                .WithMany(c => c.WeighmentTransactions)
                .HasForeignKey(w => w.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WeighmentTransaction>()
                .HasOne(w => w.Material)
                .WithMany(m => m.WeighmentTransactions)
                .HasForeignKey(w => w.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WeighmentTransaction>()
                .HasOne(w => w.Weighbridge)
                .WithMany(wb => wb.WeighmentTransactions)
                .HasForeignKey(w => w.WeighbridgeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Weighment → optional Prepayment allocation hint. Nullable because
            // most weighments aren't tied to a prepayment. Restrict on delete so
            // a prepayment with weighments pointing at it can't be deleted out
            // from under them.
            modelBuilder.Entity<WeighmentTransaction>()
                .HasOne(w => w.SelectedPrepayment)
                .WithMany()
                .HasForeignKey(w => w.SelectedPrepaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WeighmentTransaction>()
                .HasOne(w => w.SelectedPrepaymentLineItem)
                .WithMany()
                .HasForeignKey(w => w.SelectedPrepaymentLineItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Invoice relationships
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.WeighmentTransaction)
                .WithOne(w => w.Invoice)
                .HasForeignKey<Invoice>(i => i.WeighmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Quotation relationships
            modelBuilder.Entity<Quotation>()
                .HasOne(q => q.Customer)
                .WithMany(c => c.Quotations)
                .HasForeignKey(q => q.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuotationItem>()
                .HasOne(qi => qi.Quotation)
                .WithMany(q => q.Items)
                .HasForeignKey(qi => qi.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuotationItem>()
                .HasOne(qi => qi.Material)
                .WithMany()
                .HasForeignKey(qi => qi.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
 
            // Configure Prepayment line-item relationships
            modelBuilder.Entity<PrepaymentLineItem>()
                .HasOne(pli => pli.CustomerPrepayment)
                .WithMany(p => p.LineItems)
                .HasForeignKey(pli => pli.CustomerPrepaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PrepaymentLineItem>()
                .HasOne(pli => pli.Material)
                .WithMany()
                .HasForeignKey(pli => pli.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            // When an application references a specific line item, cascade is
            // undesirable because it would delete history. Restrict is fine
            // because line items are cascade-deleted with the parent prepayment
            // only when there are no applications (enforced at controller level).
            modelBuilder.Entity<PrepaymentApplication>()
                .HasOne(pa => pa.PrepaymentLineItem)
                .WithMany()
                .HasForeignKey(pa => pa.PrepaymentLineItemId)
                .OnDelete(DeleteBehavior.Restrict);
 
            // Prepayment → PaymentMethod lookup. Nullable FK so legacy rows
            // (which only carried the denormalized string) keep loading. Restrict
            // on delete so we never orphan a prepayment if an admin removes a
            // method — IsActive = false is the correct way to retire one.
            modelBuilder.Entity<CustomerPrepayment>()
                .HasOne(p => p.PaymentMethodRef)
                .WithMany(pm => pm.Prepayments)
                .HasForeignKey(p => p.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer classification FKs
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.CustomerType)
                .WithMany(ct => ct.Customers)
                .HasForeignKey(c => c.CustomerTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Customer>()
                .HasOne(c => c.VatType)
                .WithMany(vt => vt.Customers)
                .HasForeignKey(c => c.VatTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Per-customer material pricing history
            modelBuilder.Entity<CustomerMaterialPrice>()
                .HasOne(cmp => cmp.Customer)
                .WithMany(c => c.MaterialPrices)
                .HasForeignKey(cmp => cmp.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerMaterialPrice>()
                .HasOne(cmp => cmp.Material)
                .WithMany()
                .HasForeignKey(cmp => cmp.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index supports the hot-path lookup: "current price for this customer+material".
            // Non-unique because history keeps many rows per pair; the IsCurrent flag
            // is managed at service level (only one row per pair has IsCurrent = true).
            modelBuilder.Entity<CustomerMaterialPrice>()
                .HasIndex(cmp => new { cmp.CustomerId, cmp.MaterialId, cmp.EffectiveFrom });
 
            // Configure Employee relationships
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Quarry)
                .WithMany(q => q.Employees)
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Payroll relationships
            modelBuilder.Entity<PayrollRun>()
                .HasOne(pr => pr.ProcessedByUser)
                .WithMany(u => u.PayrollRuns)
                .HasForeignKey(pr => pr.ProcessedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeeSalary>()
                .HasOne(es => es.PayrollRun)
                .WithMany(pr => pr.EmployeeSalaries)
                .HasForeignKey(es => es.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeeSalary>()
                .HasOne(es => es.Employee)
                .WithMany(e => e.EmployeeSalaries)
                .HasForeignKey(es => es.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Journal Entry relationships
            modelBuilder.Entity<JournalEntry>()
                .HasOne(je => je.PostedByUser)
                .WithMany(u => u.JournalEntries)
                .HasForeignKey(je => je.PostedBy)
                .OnDelete(DeleteBehavior.Restrict);
 
            modelBuilder.Entity<JournalEntryLine>()
                .HasOne(jel => jel.JournalEntry)
                .WithMany(je => je.JournalEntryLines)
                .HasForeignKey(jel => jel.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);
 
            modelBuilder.Entity<JournalEntryLine>()
                .HasOne(jel => jel.Account)
                .WithMany(ca => ca.JournalEntryLines)
                .HasForeignKey(jel => jel.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Fiscal Year / AccountFiscalYearBalance relationships
            modelBuilder.Entity<AccountFiscalYearBalance>()
                .HasOne(b => b.Account)
                .WithMany()
                .HasForeignKey(b => b.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AccountFiscalYearBalance>()
                .HasOne(b => b.FiscalYear)
                .WithMany(fy => fy.AccountBalances)
                .HasForeignKey(b => b.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure StockYard relationships
            modelBuilder.Entity<StockYard>()
                .HasOne(sy => sy.Quarry)
                .WithMany(q => q.StockYards)
                .HasForeignKey(sy => sy.QuarryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockYard>()
                .HasOne(sy => sy.Material)
                .WithMany(m => m.StockYards)
                .HasForeignKey(sy => sy.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add unique constraints
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Phone)
                .IsUnique()
                .HasFilter("[Phone] IS NOT NULL");
 
            modelBuilder.Entity<WeighmentTransaction>()
                .HasIndex(w => w.TransactionNumber)
                .IsUnique();
 modelBuilder.Entity<Invoice>()
     .HasIndex(i => i.InvoiceNumber)
     .IsUnique();
 
 modelBuilder.Entity<Quotation>()
     .HasIndex(q => q.QuotationNumber)
     .IsUnique();
 
 
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.EmployeeCode)
                .IsUnique();
 
            modelBuilder.Entity<ChartOfAccounts>()
                .HasIndex(c => c.AccountCode)
                .IsUnique();
 
            modelBuilder.Entity<PayrollRun>()
                .HasIndex(pr => pr.RunNumber)
                .IsUnique();
 
            modelBuilder.Entity<JournalEntry>()
                .HasIndex(je => je.EntryNumber)
                .IsUnique();

            modelBuilder.Entity<FiscalYear>()
                .HasIndex(f => f.YearCode)
                .IsUnique();

            modelBuilder.Entity<AccountFiscalYearBalance>()
                .HasIndex(b => new { b.AccountId, b.FiscalYearId })
                .IsUnique();



            // Configure default values
            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.TransactionDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.Status)
                .HasDefaultValue("InProgress");

            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.WeightUnit)
                .HasDefaultValue("Ton");

            modelBuilder.Entity<Material>()
                .Property(m => m.VatRate)
                .HasDefaultValue(7.5m);

            modelBuilder.Entity<Material>()
                .Property(m => m.Unit)
                .HasDefaultValue("Ton");

            // Configure string lengths and requirements
            modelBuilder.Entity<Customer>()
                .Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Customer>()
                .Property(c => c.Phone)
                .HasMaxLength(20);

            modelBuilder.Entity<Material>()
                .Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.TransactionNumber)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.VehicleRegNumber)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<Employee>()
                .Property(e => e.EmployeeCode)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<Employee>()
                .Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(200);

            modelBuilder.Entity<PayrollRun>()
                .Property(pr => pr.RunNumber)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<JournalEntry>()
                .Property(je => je.EntryNumber)
                .IsRequired()
                .HasMaxLength(50);

            // Seed initial data for Chart of Accounts
            // NOTE: Id values 1-10 are existing and used by migrations; do not change them.
            modelBuilder.Entity<ChartOfAccounts>().HasData(
                // ASSETS
                new ChartOfAccounts { Id = 1,  AccountCode = "1001", AccountName = "Cash & Bank Balances",             AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { Id = 2,  AccountCode = "1101", AccountName = "Accounts Receivable",               AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { Id = 3,  AccountCode = "1201", AccountName = "Raw Material Stock",                AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { Id = 4,  AccountCode = "1501", AccountName = "Plant & Machinery (Quarry Equipment)", AccountType = "Asset", SubType = "Fixed" },

                // LIABILITIES
                new ChartOfAccounts { Id = 5,  AccountCode = "2001", AccountName = "Accounts Payable",                  AccountType = "Liability", SubType = "Current" },
                new ChartOfAccounts { Id = 6,  AccountCode = "2101", AccountName = "VAT Output Tax",                    AccountType = "Liability", SubType = "Current" },
                new ChartOfAccounts { Id = 7,  AccountCode = "2102", AccountName = "VAT Input Tax",                     AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { Id = 10, AccountCode = "2103", AccountName = "Customer Prepayments",             AccountType = "Liability", SubType = "Current" },

                // REVENUE
                new ChartOfAccounts { Id = 8,  AccountCode = "4001", AccountName = "Sale of Aggregates",                AccountType = "Revenue",   SubType = "Sales" },
                new ChartOfAccounts { Id = 11, AccountCode = "4002", AccountName = "Transport & Delivery Income",   AccountType = "Revenue",   SubType = "Service" },
                new ChartOfAccounts { Id = 12, AccountCode = "4003", AccountName = "Other Operating Income",            AccountType = "Revenue",   SubType = "Other" },

                // CONTRA-REVENUE
                // 4010 is a contra-revenue account: its balance reduces total
                // revenue when reported, so it's classified as Revenue with
                // SubType = "Contra" to distinguish it in the P&L. The
                // CreateInvoiceJournalEntryAsync helper Dr's this account with
                // the rebate amount on every invoice that has a rebate.
                new ChartOfAccounts { Id = 23, AccountCode = "4010", AccountName = "Sales Rebates & Discounts",         AccountType = "Revenue",   SubType = "Contra" },

                // DIRECT COSTS / COGS
                new ChartOfAccounts { Id = 9,  AccountCode = "5001", AccountName = "Cost of Materials Sold",            AccountType = "Expense",   SubType = "COGS" },
                new ChartOfAccounts { Id = 13, AccountCode = "5002", AccountName = "Production & Quarrying Costs", AccountType = "Expense",   SubType = "COGS" },
                new ChartOfAccounts { Id = 14, AccountCode = "5003", AccountName = "Transport & Loading Costs",    AccountType = "Expense",   SubType = "COGS" },

                // OPERATING EXPENSES
                new ChartOfAccounts { Id = 15, AccountCode = "6001", AccountName = "Salaries & Wages",              AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { Id = 16, AccountCode = "6002", AccountName = "Fuel & Lubricants",            AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { Id = 17, AccountCode = "6003", AccountName = "Repairs & Maintenance",        AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { Id = 18, AccountCode = "6004", AccountName = "Office & Admin Expenses",      AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { Id = 19, AccountCode = "6005", AccountName = "Utilities",                        AccountType = "Expense",   SubType = "Operating" },

                // EQUITY
                new ChartOfAccounts { Id = 20, AccountCode = "3001", AccountName = "Owner's Equity / Share Capital", AccountType = "Equity", SubType = "Capital" },
                new ChartOfAccounts { Id = 21, AccountCode = "3101", AccountName = "Retained Earnings",                AccountType = "Equity",    SubType = "Retained" },
                new ChartOfAccounts { Id = 22, AccountCode = "3102", AccountName = "Opening Balance Equity",           AccountType = "Equity",    SubType = "Capital" }
            );

            modelBuilder.Entity<Material>().HasData(
                new Material { Id = 1, Name = "Granite Aggregate 20mm", Type = "Aggregate", UnitPrice = 45000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 2, Name = "Granite Aggregate 10mm", Type = "Aggregate", UnitPrice = 48000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 3, Name = "Sharp Sand", Type = "Sand", UnitPrice = 35000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 4, Name = "Plaster Sand", Type = "Sand", UnitPrice = 38000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 5, Name = "Quarry Dust", Type = "Dust", UnitPrice = 25000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 6, Name = "Laterite", Type = "Laterite", UnitPrice = 15000.00m, VatRate = 7.5m, Unit = "Ton" }
            );

            // Seed classification lookup tables
            modelBuilder.Entity<CustomerType>().HasData(
                new CustomerType { Id = 1, Name = "Dealer",   IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
                new CustomerType { Id = 2, Name = "Supplier", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
            );

            modelBuilder.Entity<VatType>().HasData(
                new VatType { Id = 1, Name = "Inclusive", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
                new VatType { Id = 2, Name = "Exclusive", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
            );

            // Seed payment methods. DisplayOrder follows real-world frequency so
            // Cash comes first and Cheque last; admins can rearrange later by
            // editing the PaymentMethods rows directly. The Ids here must match
            // what the SQL migration inserts so the two seeding paths agree.
            modelBuilder.Entity<PaymentMethod>().HasData(
                new PaymentMethod { Id = 1, Name = "Cash",           DisplayOrder = 1, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
                new PaymentMethod { Id = 2, Name = "Bank Transfer",  DisplayOrder = 2, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
                new PaymentMethod { Id = 3, Name = "Online Payment", DisplayOrder = 3, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
                new PaymentMethod { Id = 4, Name = "Cheque",         DisplayOrder = 4, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
            );
        }
    }
}