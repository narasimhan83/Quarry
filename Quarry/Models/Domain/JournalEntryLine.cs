using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class JournalEntryLine
    {
        public int Id { get; set; }

        [Display(Name = "Journal Entry")]
        public int JournalEntryId { get; set; }

        [Display(Name = "Account")]
        public int AccountId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Debit Amount")]
        public decimal DebitAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Credit Amount")]
        public decimal CreditAmount { get; set; } = 0;

        [StringLength(500)]
        [Display(Name = "Line Description")]
        public string? LineDescription { get; set; }

        // Navigation properties
        public virtual JournalEntry JournalEntry { get; set; } = null!;
        public virtual ChartOfAccounts Account { get; set; } = null!;

        // Helper methods
        public bool IsValidLine()
        {
            // Must have either debit or credit, but not both
            if (DebitAmount > 0 && CreditAmount > 0)
                return false;
            if (DebitAmount == 0 && CreditAmount == 0)
                return false;
            if (DebitAmount < 0 || CreditAmount < 0)
                return false;
            return true;
        }

        public decimal GetAmount()
        {
            return Math.Max(DebitAmount, CreditAmount);
        }

        public bool IsDebitLine()
        {
            return DebitAmount > 0;
        }

        public bool IsCreditLine()
        {
            return CreditAmount > 0;
        }

        public string GetEntryType()
        {
            return IsDebitLine() ? "Debit" : "Credit";
        }

        public string GetFormattedAmount()
        {
            var amount = GetAmount();
            return $"₦{amount:N2}";
        }

        public string GetAccountImpact()
        {
            if (IsDebitLine())
            {
                return Account?.AccountType switch
                {
                    "Asset" => "Increase",
                    "Expense" => "Increase",
                    "Liability" => "Decrease",
                    "Equity" => "Decrease",
                    "Revenue" => "Decrease",
                    _ => "Unknown"
                };
            }
            else
            {
                return Account?.AccountType switch
                {
                    "Asset" => "Decrease",
                    "Expense" => "Decrease",
                    "Liability" => "Increase",
                    "Equity" => "Increase",
                    "Revenue" => "Increase",
                    _ => "Unknown"
                };
            }
        }

        // Validation for Nigerian accounting standards
        public bool IsCompliantWithNigerianStandards()
        {
            // Check if amount is reasonable (not too large)
            if (GetAmount() > 1000000000) // 1 billion Naira
                return false;

            // Check if description is provided for large amounts
            if (GetAmount() > 1000000 && string.IsNullOrWhiteSpace(LineDescription))
                return false;

            return true;
        }

        public string GetLineSummary()
        {
            var accountName = Account?.AccountName ?? "Unknown Account";
            var amount = GetFormattedAmount();
            var entryType = GetEntryType();
            
            return $"{accountName}: {entryType} {amount}";
        }
    }
}