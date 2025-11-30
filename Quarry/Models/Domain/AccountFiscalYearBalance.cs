using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class AccountFiscalYearBalance
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Account")]
        public int AccountId { get; set; }

        [Required]
        [Display(Name = "Fiscal Year")]
        public int FiscalYearId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Opening Balance")]
        public decimal OpeningBalance { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Closing Balance")]
        public decimal? ClosingBalance { get; set; }

        [Display(Name = "Is Locked")]
        public bool IsLocked { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ChartOfAccounts? Account { get; set; }
        public virtual FiscalYear? FiscalYear { get; set; }
    }
}