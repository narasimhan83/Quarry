using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// VAT treatment for a customer — Inclusive means their unit prices already
    /// contain VAT (back it out to report); Exclusive means prices are net and
    /// VAT is added on top. Seeded once and referenced by Customer.VatTypeId.
    /// </summary>
    public class VatType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "VAT Type")]
        public string Name { get; set; } = string.Empty; // "Inclusive" or "Exclusive"

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
    }
}
