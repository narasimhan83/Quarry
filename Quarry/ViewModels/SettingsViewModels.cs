using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.ViewModels
{
    public class SystemSettingsViewModel
    {
        // Company
        [Display(Name = "Company Name")]
        [StringLength(200)]
        public string CompanyName { get; set; } = "Nigerian Quarry Management System";

        [Display(Name = "Address")]
        [StringLength(300)]
        public string? Address { get; set; } = "123 Quarry Road, Industrial Estate";

        [Display(Name = "City")]
        [StringLength(100)]
        public string? City { get; set; } = "Lagos";

        [Display(Name = "State")]
        [StringLength(100)]
        public string? State { get; set; } = "Lagos";

        [Display(Name = "Phone")]
        [StringLength(30)]
        public string? Phone { get; set; } = "+234-1-2345678";

        [EmailAddress]
        [Display(Name = "Support Email")]
        [StringLength(150)]
        public string? SupportEmail { get; set; } = "support@quarry.ng";

        [Display(Name = "Website")]
        [StringLength(150)]
        public string? Website { get; set; } = "www.quarry.ng";

        [Display(Name = "Tax Number")]
        [StringLength(50)]
        public string? TaxNumber { get; set; } = "12345678-0001";

        // Regional
        [Display(Name = "Currency Symbol")]
        [StringLength(10)]
        public string CurrencySymbol { get; set; } = "₦";

        [Display(Name = "Date Format")]
        [StringLength(20)]
        public string DateFormat { get; set; } = "yyyy-MM-dd";

        [Display(Name = "Time Zone")]
        [StringLength(100)]
        public string TimeZone { get; set; } = "Africa/Lagos";

        // Finance
        [Display(Name = "Default VAT Rate (%)")]
        [Range(0, 100)]
        public decimal DefaultVatRate { get; set; } = 7.5m;

        [Display(Name = "Enable Late Payment Penalty")]
        public bool EnableLatePaymentPenalty { get; set; } = true;

        [Display(Name = "Late Payment Penalty (% monthly)")]
        [Range(0, 100)]
        public decimal LatePaymentPenaltyRate { get; set; } = 2.0m;

        // UI/UX
        [Display(Name = "Theme")]
        [StringLength(30)]
        public string Theme { get; set; } = "default";

        [Display(Name = "Dashboard Auto-Refresh (seconds)")]
        [Range(0, 3600)]
        public int DashboardAutoRefreshSeconds { get; set; } = 30;

        [Display(Name = "Show Demo Badges")]
        public bool ShowDemoBadges { get; set; } = false;

        // Email (SMTP) - placeholders (non-persistent demo)
        [Display(Name = "SMTP Host")]
        [StringLength(200)]
        public string? SmtpHost { get; set; }

        [Display(Name = "SMTP Port")]
        [Range(1, 65535)]
        public int? SmtpPort { get; set; }

        [Display(Name = "SMTP Username")]
        [StringLength(200)]
        public string? SmtpUser { get; set; }

        [Display(Name = "SMTP Password")]
        [DataType(DataType.Password)]
        [StringLength(200)]
        public string? SmtpPassword { get; set; }

        [Display(Name = "Enable SSL")]
        public bool SmtpEnableSsl { get; set; } = true;

        // Flags informing persistence capability in the current build
        [Display(Name = "Persistence Enabled")]
        public bool IsPersistenceAvailable { get; set; } = false;

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; } = "This demo Settings page does not persist changes yet. Hook to DB/AppSettings in a later iteration.";
    }
}