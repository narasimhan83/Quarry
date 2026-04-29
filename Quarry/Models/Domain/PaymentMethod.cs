using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// Lookup table for payment methods used across prepayments and invoice
    /// payments — Cash, Bank Transfer, Online Payment, Cheque, etc. Seeded
    /// once and referenced wherever a payment is recorded.
    /// <para/>
    /// DisplayOrder controls the order methods appear in dropdowns so the most
    /// commonly used one can float to the top regardless of insertion order.
    /// IsActive lets an admin hide a method (e.g. Cheque) without deleting
    /// history — existing records keep their FK intact, new records can't pick it.
    /// </summary>
    public class PaymentMethod
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sort key for dropdown rendering. Lower = earlier. Ties are broken by Id.
        /// </summary>
        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; } = 0;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation — prepayments referencing this method.
        public virtual ICollection<CustomerPrepayment> Prepayments { get; set; } = new List<CustomerPrepayment>();
    }
}
