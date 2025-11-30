using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuarryManagementSystem.ViewModels
{
    public class OpeningBalanceAccountRow
    {
        [Required]
        public int AccountId { get; set; }

        [Display(Name = "Account Code")]
        public string AccountCode { get; set; } = string.Empty;

        [Display(Name = "Account Name")]
        public string AccountName { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public string AccountType { get; set; } = string.Empty;

        [Display(Name = "Sub Type")]
        public string? SubType { get; set; }

        [Display(Name = "Opening Balance")]
        [DataType(DataType.Currency)]
        public decimal OpeningBalance { get; set; }
    }

    public class OpeningBalanceFiscalYearViewModel
    {
        [Required]
        [Display(Name = "Fiscal Year")]
        public int FiscalYearId { get; set; }

        public string FiscalYearName { get; set; } = string.Empty;

        public List<SelectListItem> FiscalYears { get; set; } = new List<SelectListItem>();

        public List<OpeningBalanceAccountRow> Accounts { get; set; } = new List<OpeningBalanceAccountRow>();
    }
}