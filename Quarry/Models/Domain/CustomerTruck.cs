using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    /// <summary>
    /// A truck registered against a specific customer. The customer screen
    /// captures one or more truck numbers; weighment / invoice flows can later
    /// look these up to validate that an arriving truck belongs to the named
    /// customer.
    /// </summary>
    public class CustomerTruck
    {
        public int CustomerTruckId { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Truck number is required")]
        [StringLength(100)]
        [Display(Name = "Truck Number")]
        public string CustomerTruckNumber { get; set; } = string.Empty;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual Customer? Customer { get; set; }
    }
}
