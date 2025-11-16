using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class Weighbridge
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Weighbridge name is required")]
        [StringLength(100)]
        [Display(Name = "Weighbridge Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Quarry")]
        public int? QuarryId { get; set; }

        [StringLength(255)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [Required(ErrorMessage = "Connection type is required")]
        [StringLength(20)]
        [Display(Name = "Connection Type")]
        public string ConnectionType { get; set; } = string.Empty; // Serial, TCP, USB

        [StringLength(500)]
        [Display(Name = "Connection String")]
        public string? ConnectionString { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Capacity (kg)")]
        [Range(0.01, 999999.99, ErrorMessage = "Capacity must be between 0.01 and 999,999.99")]
        public decimal? Capacity { get; set; }

        [StringLength(20)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated Date")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Quarry? Quarry { get; set; }
        public virtual ICollection<WeighmentTransaction> WeighmentTransactions { get; set; } = new List<WeighmentTransaction>();

        // Helper properties
        public bool IsActive()
        {
            return Status == "Active";
        }

        public bool IsConnected()
        {
            return Status == "Active" && !string.IsNullOrEmpty(ConnectionString);
        }

        public string GetConnectionTypeDisplay()
        {
            return ConnectionType switch
            {
                "Serial" => "Serial Port",
                "TCP" => "Network (TCP/IP)",
                "USB" => "USB Connection",
                _ => ConnectionType
            };
        }

        public static readonly string[] ConnectionTypes =
        {
            "Serial",
            "TCP",
            "USB"
        };
    }
}