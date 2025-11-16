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
                .IsUnique();

            modelBuilder.Entity<WeighmentTransaction>()
                .HasIndex(w => w.TransactionNumber)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
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



            // Configure default values
            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.TransactionDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.Status)
                .HasDefaultValue("InProgress");

            modelBuilder.Entity<WeighmentTransaction>()
                .Property(w => w.WeightUnit)
                .HasDefaultValue("kg");

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
                .IsRequired()
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

            // Seed initial data
            modelBuilder.Entity<ChartOfAccounts>().HasData(
                new ChartOfAccounts { Id = 1, AccountCode = "1001", AccountName = "Cash & Bank Balances", AccountType = "Asset", SubType = "Current" },
                new ChartOfAccounts { Id = 2, AccountCode = "1101", AccountName = "Accounts Receivable", AccountType = "Asset", SubType = "Current" },
                new ChartOfAccounts { Id = 3, AccountCode = "1201", AccountName = "Raw Material Stock", AccountType = "Asset", SubType = "Current" },
                new ChartOfAccounts { Id = 4, AccountCode = "1501", AccountName = "Plant & Machinery (Quarry Equipment)", AccountType = "Asset", SubType = "Fixed" },
                new ChartOfAccounts { Id = 5, AccountCode = "2001", AccountName = "Accounts Payable", AccountType = "Liability", SubType = "Current" },
                new ChartOfAccounts { Id = 6, AccountCode = "2101", AccountName = "VAT Output Tax", AccountType = "Liability", SubType = "Current" },
                new ChartOfAccounts { Id = 7, AccountCode = "2102", AccountName = "VAT Input Tax", AccountType = "Asset", SubType = "Current" },
                new ChartOfAccounts { Id = 8, AccountCode = "4001", AccountName = "Sale of Aggregates", AccountType = "Revenue", SubType = "Sales" },
                new ChartOfAccounts { Id = 9, AccountCode = "5001", AccountName = "Cost of Materials Sold", AccountType = "Expense", SubType = "COGS" }
            );

            modelBuilder.Entity<Material>().HasData(
                new Material { Id = 1, Name = "Granite Aggregate 20mm", Type = "Aggregate", UnitPrice = 45000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 2, Name = "Granite Aggregate 10mm", Type = "Aggregate", UnitPrice = 48000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 3, Name = "Sharp Sand", Type = "Sand", UnitPrice = 35000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 4, Name = "Plaster Sand", Type = "Sand", UnitPrice = 38000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 5, Name = "Quarry Dust", Type = "Dust", UnitPrice = 25000.00m, VatRate = 7.5m, Unit = "Ton" },
                new Material { Id = 6, Name = "Laterite", Type = "Laterite", UnitPrice = 15000.00m, VatRate = 7.5m, Unit = "Ton" }
            );
        }
    }
}