using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class ChartOfAccounts
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Account code is required")]
        [StringLength(20)]
        [Display(Name = "Account Code")]
        public string AccountCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account name is required")]
        [StringLength(200)]
        [Display(Name = "Account Name")]
        public string AccountName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Account type is required")]
        [StringLength(50)]
        [Display(Name = "Account Type")]
        public string AccountType { get; set; } = string.Empty; // Asset, Liability, Equity, Revenue, Expense

        [StringLength(50)]
        [Display(Name = "Sub Type")]
        public string? SubType { get; set; } // Current, Fixed, etc.

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Opening Balance")]
        public decimal OpeningBalance { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Current Balance")]
        public decimal CurrentBalance { get; set; } = 0;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

        // Helper properties
        public bool IsAssetAccount()
        {
            return AccountType == "Asset";
        }

        public bool IsLiabilityAccount()
        {
            return AccountType == "Liability";
        }

        public bool IsEquityAccount()
        {
            return AccountType == "Equity";
        }

        public bool IsRevenueAccount()
        {
            return AccountType == "Revenue";
        }

        public bool IsExpenseAccount()
        {
            return AccountType == "Expense";
        }

        public string GetAccountCategory()
        {
            return AccountType switch
            {
                "Asset" => IsCurrentAsset() ? "Current Asset" : "Fixed Asset",
                "Liability" => IsCurrentLiability() ? "Current Liability" : "Long-term Liability",
                _ => AccountType
            };
        }

        public bool IsCurrentAsset()
        {
            return AccountType == "Asset" && SubType == "Current";
        }

        public bool IsCurrentLiability()
        {
            return AccountType == "Liability" && SubType == "Current";
        }

        public bool IsFixedAsset()
        {
            return AccountType == "Asset" && SubType == "Fixed";
        }

        public decimal GetAccountBalance()
        {
            return CurrentBalance;
        }

        public void UpdateBalance(decimal amount, bool isDebit)
        {
            if (isDebit)
            {
                if (IsAssetAccount() || IsExpenseAccount())
                    CurrentBalance += amount;
                else
                    CurrentBalance -= amount;
            }
            else
            {
                if (IsAssetAccount() || IsExpenseAccount())
                    CurrentBalance -= amount;
                else
                    CurrentBalance += amount;
            }
        }

        // Common Nigerian Chart of Accounts
        public static readonly string[] AccountTypes =
        {
            "Asset",
            "Liability", 
            "Equity",
            "Revenue",
            "Expense"
        };

        public static readonly string[] SubTypes =
        {
            "Current",
            "Fixed", 
            "Owner",
            "Sales",
            "COGS",
            "Operating",
            "Tax"
        };

        public static readonly Dictionary<string, string[]> AccountTypeSubTypes = new()
        {
            { "Asset", new[] { "Current", "Fixed" } },
            { "Liability", new[] { "Current", "Long-term" } },
            { "Equity", new[] { "Owner" } },
            { "Revenue", new[] { "Sales", "Other" } },
            { "Expense", new[] { "COGS", "Operating", "Tax" } }
        };
    }
}