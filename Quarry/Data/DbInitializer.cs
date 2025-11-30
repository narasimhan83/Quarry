using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Models;
using QuarryManagementSystem.Models.Domain;

namespace QuarryManagementSystem.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            try
            {
                // Apply any pending migrations
                await context.Database.MigrateAsync();
    
                // Seed roles first (must exist before users)
                await SeedRoles(roleManager);
    
                // Seed admin user
                await SeedAdminUser(userManager);
    
                // Ensure core Chart of Accounts rows exist (safe even if some were deleted)
                await SeedChartOfAccountsAsync(context);

                // Seed other default data
                await SeedDefaultQuarry(context);
                await SeedDefaultWeighbridge(context);
                await SeedDefaultMaterials(context);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - this allows the app to start even if seeding fails
                Console.WriteLine($"Error during database initialization: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Admin", "Manager", "Accountant", "Operator", "Viewer" };

            foreach (var role in roles)
            {
                try
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        var result = await roleManager.CreateAsync(new IdentityRole(role));
                        if (!result.Succeeded)
                        {
                            Console.WriteLine($"Failed to create role {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating role {role}: {ex.Message}");
                }
            }
        }

        private static async Task SeedAdminUser(UserManager<ApplicationUser> userManager)
        {
            const string adminEmail = "admin@quarry.ng";
            const string adminUsername = "admin";

            try
            {
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminUsername,
                        Email = adminEmail,
                        FullName = "System Administrator",
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin@2024");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        Console.WriteLine("Admin user created successfully");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
                else
                {
                    Console.WriteLine("Admin user already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating admin user: {ex.Message}");
            }
        }

        private static async Task SeedDefaultQuarry(ApplicationDbContext context)
        {
            if (!context.Quarries.Any())
            {
                var defaultQuarry = new Quarry
                {
                    Name = "Main Quarry Site",
                    Location = "Industrial Area",
                    LGA = "Ikeja",
                    State = "Lagos",
                    Status = "Active",
                    CreatedAt = DateTime.Now
                };

                context.Quarries.Add(defaultQuarry);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedDefaultWeighbridge(ApplicationDbContext context)
        {
            if (!context.Weighbridges.Any())
            {
                var quarry = await context.Quarries.FirstOrDefaultAsync();
                if (quarry != null)
                {
                    var defaultWeighbridge = new Weighbridge
                    {
                        Name = "Main Weighbridge",
                        QuarryId = quarry.Id,
                        Location = "Main Entrance",
                        ConnectionType = "Serial",
                        ConnectionString = "COM1,9600,N,8,1",
                        Capacity = 50000, // 50 tonnes
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    };

                    context.Weighbridges.Add(defaultWeighbridge);
                    await context.SaveChangesAsync();
                }
            }
        }

        private static async Task SeedDefaultMaterials(ApplicationDbContext context)
        {
            if (!context.Materials.Any())
            {
                var defaultMaterials = new List<Material>
                {
                    new Material
                    {
                        Name = "Granite Aggregate 10mm",
                        Type = "Aggregate",
                        Unit = "Ton",
                        UnitPrice = 45000,
                        VatRate = 7.5m,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    },
                    new Material
                    {
                        Name = "Granite Aggregate 20mm",
                        Type = "Aggregate",
                        Unit = "Ton",
                        UnitPrice = 42000,
                        VatRate = 7.5m,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    },
                    new Material
                    {
                        Name = "Sharp Sand",
                        Type = "Sand",
                        Unit = "Ton",
                        UnitPrice = 25000,
                        VatRate = 7.5m,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    }
                };

                await context.Materials.AddRangeAsync(defaultMaterials);
                await context.SaveChangesAsync();
                Console.WriteLine("Default materials seeded successfully");
            }
        }

        /// <summary>
        /// Ensures that the core Chart of Accounts rows exist even if they were
        /// accidentally deleted. This runs at startup and only inserts missing
        /// accounts; it is safe to call repeatedly.
        /// </summary>
        private static async Task SeedChartOfAccountsAsync(ApplicationDbContext context)
        {
            var requiredAccounts = new List<ChartOfAccounts>
            {
                // Assets
                new ChartOfAccounts { AccountCode = "1001", AccountName = "Cash & Bank Balances",             AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { AccountCode = "1101", AccountName = "Accounts Receivable",               AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { AccountCode = "1201", AccountName = "Raw Material Stock",                AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { AccountCode = "1501", AccountName = "Plant & Machinery (Quarry Equipment)", AccountType = "Asset", SubType = "Fixed" },

                // Liabilities
                new ChartOfAccounts { AccountCode = "2001", AccountName = "Accounts Payable",                  AccountType = "Liability", SubType = "Current" },
                new ChartOfAccounts { AccountCode = "2101", AccountName = "VAT Output Tax",                    AccountType = "Liability", SubType = "Current" },
                new ChartOfAccounts { AccountCode = "2102", AccountName = "VAT Input Tax",                     AccountType = "Asset",     SubType = "Current" },
                new ChartOfAccounts { AccountCode = "2103", AccountName = "Customer Prepayments",             AccountType = "Liability", SubType = "Current" },

                // Revenue
                new ChartOfAccounts { AccountCode = "4001", AccountName = "Sale of Aggregates",                AccountType = "Revenue",   SubType = "Sales" },
                new ChartOfAccounts { AccountCode = "4002", AccountName = "Transport & Delivery Income",   AccountType = "Revenue",   SubType = "Service" },
                new ChartOfAccounts { AccountCode = "4003", AccountName = "Other Operating Income",            AccountType = "Revenue",   SubType = "Other" },

                // Direct costs / COGS
                new ChartOfAccounts { AccountCode = "5001", AccountName = "Cost of Materials Sold",            AccountType = "Expense",   SubType = "COGS" },
                new ChartOfAccounts { AccountCode = "5002", AccountName = "Production & Quarrying Costs", AccountType = "Expense",   SubType = "COGS" },
                new ChartOfAccounts { AccountCode = "5003", AccountName = "Transport & Loading Costs",    AccountType = "Expense",   SubType = "COGS" },

                // Operating expenses
                new ChartOfAccounts { AccountCode = "6001", AccountName = "Salaries & Wages",              AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { AccountCode = "6002", AccountName = "Fuel & Lubricants",            AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { AccountCode = "6003", AccountName = "Repairs & Maintenance",        AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { AccountCode = "6004", AccountName = "Office & Admin Expenses",      AccountType = "Expense",   SubType = "Operating" },
                new ChartOfAccounts { AccountCode = "6005", AccountName = "Utilities",                        AccountType = "Expense",   SubType = "Operating" },

                // Equity
                new ChartOfAccounts { AccountCode = "3001", AccountName = "Owner's Equity / Share Capital", AccountType = "Equity", SubType = "Capital" },
                new ChartOfAccounts { AccountCode = "3101", AccountName = "Retained Earnings",                AccountType = "Equity",    SubType = "Retained" }
            };

            var existingCodes = await context.ChartOfAccounts
                .Select(a => a.AccountCode)
                .ToListAsync();

            var toInsert = requiredAccounts
                .Where(a => !existingCodes.Contains(a.AccountCode))
                .ToList();

            if (toInsert.Any())
            {
                foreach (var acc in toInsert)
                {
                    acc.CreatedAt = DateTime.Now;
                    acc.IsActive = true;
                    acc.OpeningBalance = 0m;
                    acc.CurrentBalance = 0m;
                }

                await context.ChartOfAccounts.AddRangeAsync(toInsert);
                await context.SaveChangesAsync();
                Console.WriteLine($"Seeded {toInsert.Count} missing Chart of Accounts records.");
            }
        }

        public static async Task SeedSampleData(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            // Seed sample customers
            if (!context.Customers.Any())
            {
                var adminUser = await userManager.FindByEmailAsync("admin@quarry.ng");
                var sampleCustomers = new List<Customer>
                {
                    new Customer
                    {
                        Name = "ABC Construction Ltd",
                        RCNumber = "RC-123456",
                        Location = "Victoria Island",
                        LGA = "Eti-Osa",
                        State = "Lagos",
                        ContactPerson = "John Doe",
                        Phone = "08012345678",
                        Email = "john@abcconstruction.ng",
                        TIN = "12345678901",
                        BVN = "12345678901",
                        BillingAddress = "123 Construction Ave, Victoria Island, Lagos",
                        CreditLimit = 5000000,
                        OutstandingBalance = 0,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    },
                    new Customer
                    {
                        Name = "XYZ Builders Nigeria Ltd",
                        RCNumber = "RC-789012",
                        Location = "Ikeja",
                        LGA = "Ikeja",
                        State = "Lagos",
                        ContactPerson = "Jane Smith",
                        Phone = "08098765432",
                        Email = "jane@xyzbuilders.ng",
                        TIN = "98765432109",
                        BVN = "98765432109",
                        BillingAddress = "456 Builder St, Ikeja, Lagos",
                        CreditLimit = 3000000,
                        OutstandingBalance = 150000,
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    }
                };

                await context.Customers.AddRangeAsync(sampleCustomers);
                await context.SaveChangesAsync();
            }

            // Seed sample weighment transactions
            if (!context.WeighmentTransactions.Any())
            {
                var customer = await context.Customers.FirstOrDefaultAsync();
                var material = await context.Materials.FirstOrDefaultAsync();
                var weighbridge = await context.Weighbridges.FirstOrDefaultAsync();
                var adminUser = await userManager.FindByEmailAsync("admin@quarry.ng");

                if (customer != null && material != null && weighbridge != null && adminUser != null)
                {
                    var sampleTransactions = new List<WeighmentTransaction>
                    {
                        new WeighmentTransaction
                        {
                            TransactionNumber = "WB/NG/2024/001",
                            TransactionDate = DateTime.Now.AddDays(-2),
                            VehicleRegNumber = "ABC-123-XYZ",
                            DriverName = "Michael Johnson",
                            DriverPhone = "08055555555",
                            CustomerId = customer.Id,
                            WeighbridgeId = weighbridge.Id,
                            MaterialId = material.Id,
                            PricePerUnit = material.UnitPrice,
                            VatRate = material.VatRate,
                            GrossWeight = 28500, // 28.5 tonnes
                            TareWeight = 8500, // 8.5 tonnes
                            WeightUnit = "kg",
                            SubTotal = 900000, // 20 tonnes * ₦45,000
                            VatAmount = 67500, // 7.5% VAT
                            TotalAmount = 967500,
                            EntryTime = DateTime.Now.AddDays(-2).AddHours(-1),
                            ExitTime = DateTime.Now.AddDays(-2),
                            TransactionType = "Sales",
                            Status = "Completed",
                            ChallanNumber = "CHL/2024/001",
                            IsInvoiced = false,
                            CreatedBy = adminUser.FullName,
                            CreatedAt = DateTime.Now.AddDays(-2)
                        },
                        new WeighmentTransaction
                        {
                            TransactionNumber = "WB/NG/2024/002",
                            TransactionDate = DateTime.Now.AddDays(-1),
                            VehicleRegNumber = "DEF-456-UVW",
                            DriverName = "Sarah Williams",
                            DriverPhone = "08066666666",
                            CustomerId = customer.Id,
                            WeighbridgeId = weighbridge.Id,
                            MaterialId = material.Id,
                            PricePerUnit = material.UnitPrice,
                            VatRate = material.VatRate,
                            GrossWeight = 32000, // 32 tonnes
                            TareWeight = 12000, // 12 tonnes
                            WeightUnit = "kg",
                            SubTotal = 900000, // 20 tonnes * ₦45,000
                            VatAmount = 67500, // 7.5% VAT
                            TotalAmount = 967500,
                            EntryTime = DateTime.Now.AddDays(-1).AddHours(-1),
                            ExitTime = DateTime.Now.AddDays(-1),
                            TransactionType = "Sales",
                            Status = "Completed",
                            ChallanNumber = "CHL/2024/002",
                            IsInvoiced = false,
                            CreatedBy = adminUser.FullName,
                            CreatedAt = DateTime.Now.AddDays(-1)
                        }
                    };

                    await context.WeighmentTransactions.AddRangeAsync(sampleTransactions);
                    await context.SaveChangesAsync();
                }
            }

            // Seed sample invoices
            if (!context.Invoices.Any())
            {
                var customer = await context.Customers.FirstOrDefaultAsync();
                var transaction = await context.WeighmentTransactions.FirstOrDefaultAsync();
                var adminUser = await userManager.FindByEmailAsync("admin@quarry.ng");

                if (customer != null && transaction != null && adminUser != null)
                {
                    var sampleInvoice = new Invoice
                    {
                        InvoiceNumber = "INV/NG/2024/001",
                        CustomerId = customer.Id,
                        WeighmentId = transaction.Id,
                        InvoiceDate = DateTime.Now.AddDays(-1),
                        DueDate = DateTime.Now.AddDays(29), // 30 days payment terms
                        SubTotal = transaction.SubTotal ?? 0,
                        VatAmount = transaction.VatAmount ?? 0,
                        TotalAmount = transaction.TotalAmount ?? 0,
                        PaidAmount = 0,
                        Status = "Unpaid",
                        PaymentTerms = "30 days",
                        LGAReceiptNumber = "LGA/2024/001",
                        CreatedBy = adminUser.FullName,
                        CreatedAt = DateTime.Now.AddDays(-1)
                    };

                    await context.Invoices.AddAsync(sampleInvoice);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}