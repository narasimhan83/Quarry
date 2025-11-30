using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    public class FiscalYear
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Year Code")]
        public string YearCode { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Is Current Year")]
        public bool IsCurrent { get; set; }

        [Display(Name = "Is Closed")]
        public bool IsClosed { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<AccountFiscalYearBalance> AccountBalances { get; set; } = new List<AccountFiscalYearBalance>();
    }
}