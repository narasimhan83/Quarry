using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// Classification of a customer — Dealer, Supplier, etc. Seeded once and
    /// referenced by Customer.CustomerTypeId.
    /// </summary>
    public class CustomerType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Customer Type")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
    }
}
