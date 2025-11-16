using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Models.Domain
{
    public class Quarry
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Quarry name is required")]
        [StringLength(100)]
        [Display(Name = "Quarry Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "RC Number")]
        public string? RCNumber { get; set; }

        [StringLength(255)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [StringLength(100)]
        [Display(Name = "Local Government Area")]
        public string? LGA { get; set; }

        [StringLength(50)]
        [Display(Name = "State")]
        public string? State { get; set; }

        [StringLength(50)]
        [Display(Name = "Mining License Number")]
        public string? MiningLicenseNumber { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public virtual ICollection<Weighbridge> Weighbridges { get; set; } = new List<Weighbridge>();
        public virtual ICollection<StockYard> StockYards { get; set; } = new List<StockYard>();

        public bool IsActive()
        {
            return Status == "Active";
        }

        public string GetFullLocation()
        {
            return $"{Location}, {LGA}, {State}";
        }
    }
}