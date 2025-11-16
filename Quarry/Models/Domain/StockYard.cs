using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuarryManagementSystem.Models.Domain
{
    public class StockYard
    {
        public int Id { get; set; }

        [Display(Name = "Quarry")]
        public int QuarryId { get; set; }

        [Display(Name = "Material")]
        public int MaterialId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Current Stock")]
        public decimal CurrentStock { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Reserved Stock")]
        public decimal ReservedStock { get; set; } = 0;

        [NotMapped]
        [Display(Name = "Available Stock")]
        public decimal AvailableStock => CurrentStock - ReservedStock;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual Quarry Quarry { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;

        // Helper methods
        public bool HasAvailableStock(decimal quantity)
        {
            return AvailableStock >= quantity;
        }

        public bool HasSufficientStock(decimal quantity)
        {
            return CurrentStock >= quantity;
        }

        public void AddStock(decimal quantity)
        {
            CurrentStock += quantity;
            LastUpdated = DateTime.Now;
        }

        public void RemoveStock(decimal quantity)
        {
            if (HasSufficientStock(quantity))
            {
                CurrentStock -= quantity;
                LastUpdated = DateTime.Now;
            }
        }

        public void ReserveStock(decimal quantity)
        {
            if (HasAvailableStock(quantity))
            {
                ReservedStock += quantity;
                LastUpdated = DateTime.Now;
            }
        }

        public void ReleaseReservedStock(decimal quantity)
        {
            if (ReservedStock >= quantity)
            {
                ReservedStock -= quantity;
                LastUpdated = DateTime.Now;
            }
        }

        public string GetStockStatus()
        {
            if (CurrentStock <= 0)
                return "Out of Stock";
            if (AvailableStock < 10) // Minimum stock threshold
                return "Low Stock";
            return "In Stock";
        }

        public bool IsLowStock()
        {
            return AvailableStock < 10; // 10 tons minimum threshold
        }

        public bool IsOutOfStock()
        {
            return CurrentStock <= 0;
        }

        public decimal GetStockValue()
        {
            return AvailableStock * (Material?.UnitPrice ?? 0);
        }
    }
}