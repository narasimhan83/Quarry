using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuarryManagementSystem.Models;
using QuarryManagementSystem.ViewModels;

namespace QuarryManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // GET: Account/Login
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            
            // Debug: Check raw form data
            var rawUsername = Request.Form["Username"].ToString();
            var rawPassword = Request.Form["Password"].ToString();
            _logger.LogInformation($"=== RAW FORM DATA DEBUG ===");
            _logger.LogInformation($"Raw Username from Form: '{rawUsername}'");
            _logger.LogInformation($"Raw Password from Form: '{(string.IsNullOrEmpty(rawPassword) ? "empty" : "*****")}'");
            _logger.LogInformation($"=== MODEL DATA DEBUG ===");
            _logger.LogInformation($"Username from model: '{model.Username}'");
            _logger.LogInformation($"Password from model: '{(string.IsNullOrEmpty(model.Password) ? "empty" : "*****")}'");
            _logger.LogInformation($"RememberMe: {model.RememberMe}");
            _logger.LogInformation($"=== END DEBUG ===");
            
            // Log model state for debugging
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid. Validation errors:");
                foreach (var error in ModelState)
                {
                    foreach (var errorMsg in error.Value.Errors)
                    {
                        _logger.LogWarning($"Field: {error.Key}, Error: {errorMsg.ErrorMessage}");
                    }
                }
                return View(model);
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"Attempting login for user: {model.Username}");
                    
                    // This doesn't count login failures towards account lockout
                    // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                    var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);
                    
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User logged in successfully.");
                        return RedirectToLocal(returnUrl);
                    }
                    if (result.RequiresTwoFactor)
                    {
                        return RedirectToAction(nameof(LoginWith2fa), new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                    }
                    if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User account locked out.");
                        return RedirectToAction(nameof(Lockout));
                    }
                    else
                    {
                        _logger.LogWarning($"Login failed for user: {model.Username}");
                        ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your username and password.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error during login for user: {model.Username}");
                    ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
                    return View(model);
                }
            }

            // If we got this far, something failed, redisplay form
            _logger.LogWarning("Login failed due to model validation errors.");
            return View(model);
        }

        // GET: Account/Register
        [HttpGet]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            
            // Log model state for debugging
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Registration model state is invalid. Validation errors:");
                foreach (var error in ModelState)
                {
                    foreach (var errorMsg in error.Value.Errors)
                    {
                        _logger.LogWarning($"Field: {error.Key}, Error: {errorMsg.ErrorMessage}");
                    }
                }
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"Attempting to register new user: {model.Username}");
                    
                    var user = new ApplicationUser
                    {
                        UserName = model.Username,
                        Email = model.Email,
                        FullName = model.FullName,
                        CreatedAt = DateTime.Now
                    };
                    
                    var result = await _userManager.CreateAsync(user, model.Password);
                    
                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"User {model.Username} created successfully.");

                        // Add default role
                        await _userManager.AddToRoleAsync(user, "Operator");

                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation($"User {model.Username} signed in successfully.");
                        
                        return RedirectToLocal(returnUrl);
                    }
                    
                    AddErrors(result);
                    _logger.LogWarning($"User creation failed for {model.Username}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error during registration for user: {model.Username}");
                    ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction(nameof(Login));
        }

        // GET: Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: Account/Lockout
        [HttpGet]
        public IActionResult Lockout()
        {
            return View();
        }

        // GET: Account/LoginWith2fa
        [HttpGet]
        public async Task<IActionResult> LoginWith2fa(bool rememberMe, string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();

            if (user == null)
            {
                throw new ApplicationException($"Unable to load two-factor authentication user.");
            }

            var model = new LoginWith2faViewModel { RememberMe = rememberMe };
            ViewData["ReturnUrl"] = returnUrl;

            return View(model);
        }

        // POST: Account/LoginWith2fa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model, bool rememberMe, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var authenticatorCode = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, model.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID {UserId} logged in with 2fa.", user.Id);
                return RedirectToLocal(returnUrl);
            }
            else if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID {UserId} account locked out.", user.Id);
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                _logger.LogWarning("Invalid authenticator code entered for user with ID {UserId}.", user.Id);
                ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
                return View();
            }
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(DashboardController.Index), "Dashboard");
            }
        }

        #endregion
    }
}