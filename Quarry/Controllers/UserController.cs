using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace QuarryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserController> _logger;

        public UserController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // GET: /User
        public async Task<IActionResult> Index(string? search, string? role, string? status)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.UserName!.Contains(search) ||
                    u.Email!.Contains(search) ||
                    u.FullName.Contains(search) ||
                    (u.PhoneNumber ?? "").Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                // status: Active / Inactive
                if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(u => u.IsActive);
                else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(u => !u.IsActive);
            }

            var users = await query
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.UserName)
                .ToListAsync();

            var result = new UserIndexViewModel
            {
                Search = search,
                Role = role,
                Status = status
            };

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role))
                    continue;

                result.Users.Add(new UserListItemViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    PhoneNumber = u.PhoneNumber ?? "",
                    IsActive = u.IsActive,
                    Roles = roles.ToList(),
                    LastLogin = u.LastLogin
                });
            }

            // Load all roles for filters
            result.AvailableRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            return View(result);
        }

        // POST: /User/ToggleActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive([FromForm][Required] string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null) return NotFound();

                user.IsActive = !user.IsActive;

                // Optionally lock/unlock sign-in while inactive
                if (!user.IsActive)
                {
                    user.LockoutEnabled = true;
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                }
                else
                {
                    user.LockoutEnd = null;
                }

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                }
                else
                {
                    TempData["Success"] = $"User {(user.FullName ?? user.UserName)} is now {(user.IsActive ? "Active" : "Inactive")}.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user active status");
                TempData["Error"] = "An error occurred while updating user status.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /User/AddToRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToRole([FromForm][Required] string id, [FromForm][Required] string role)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null) return NotFound();

                if (!await _roleManager.RoleExistsAsync(role))
                {
                    TempData["Error"] = $"Role '{role}' does not exist.";
                    return RedirectToAction(nameof(Index));
                }

                if (!(await _userManager.IsInRoleAsync(user, role)))
                {
                    var result = await _userManager.AddToRoleAsync(user, role);
                    if (!result.Succeeded)
                    {
                        TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                    }
                    else
                    {
                        TempData["Success"] = $"Added '{user.FullName ?? user.UserName}' to role '{role}'.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user to role");
                TempData["Error"] = "An error occurred while assigning the role.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /User/RemoveFromRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromRole([FromForm][Required] string id, [FromForm][Required] string role)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null) return NotFound();

                if (await _userManager.IsInRoleAsync(user, role))
                {
                    var result = await _userManager.RemoveFromRoleAsync(user, role);
                    if (!result.Succeeded)
                    {
                        TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                    }
                    else
                    {
                        TempData["Success"] = $"Removed '{user.FullName ?? user.UserName}' from role '{role}'.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from role");
                TempData["Error"] = "An error occurred while removing the role.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ViewModels for User Management screen
        public class UserIndexViewModel
        {
            public List<UserListItemViewModel> Users { get; set; } = new();
            public string? Search { get; set; }
            public string? Role { get; set; }
            public string? Status { get; set; }
            public List<string> AvailableRoles { get; set; } = new();
        }

        public class UserListItemViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public List<string> Roles { get; set; } = new();
            public DateTime? LastLogin { get; set; }
        }
    }
}