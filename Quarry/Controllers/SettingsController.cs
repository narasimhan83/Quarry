using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuarryManagementSystem.ViewModels;
using System.Text.Json;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ILogger<SettingsController> _logger;
        private readonly string _settingsFilePath;

        public SettingsController(ILogger<SettingsController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            // Persist to a simple JSON file under wwwroot for demo purposes
            _settingsFilePath = Path.Combine(env.WebRootPath, "system-settings.json");
        }

        // GET: /Settings
        [HttpGet]
        public IActionResult Index()
        {
            var model = LoadSettings() ?? new SystemSettingsViewModel
            {
                IsPersistenceAvailable = true // we will persist to JSON file
            };

            return View(model);
        }

        // POST: /Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(SystemSettingsViewModel model)
        {
            try
            {
                model.IsPersistenceAvailable = true;

                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please fix validation errors and try again.";
                    return View(model);
                }

                SaveSettings(model);
                TempData["Success"] = "Settings saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                TempData["Error"] = "An error occurred while saving settings.";
                return View(model);
            }
        }

        private SystemSettingsViewModel? LoadSettings()
        {
            try
            {
                if (System.IO.File.Exists(_settingsFilePath))
                {
                    var json = System.IO.File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<SystemSettingsViewModel>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (loaded != null)
                    {
                        loaded.IsPersistenceAvailable = true;
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load settings file {Path}", _settingsFilePath);
            }
            return null;
        }

        private void SaveSettings(SystemSettingsViewModel model)
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);
                throw;
            }
        }
    }
}