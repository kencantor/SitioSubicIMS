using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using SitioSubicIMS.Web.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SitioSubicIMS.Web.Controllers.Admin
{
    [Authorize(Roles = "Administrator")]
    public class SystemUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditLogger _auditLogger;

        public SystemUsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IAuditLogger auditLogger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditLogger = auditLogger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var users = _userManager.Users
                    .Where(u => u.UserName != "admin@sitiosubicims.local")
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .AsNoTracking()
                    .ToList();

                var userViewModels = new List<UserViewModel>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userViewModels.Add(new UserViewModel
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        MiddleName = user.MiddleName,
                        LastName = user.LastName,
                        Username = user.UserName,
                        Email = user.Email,
                        IsActive = user.IsActive,
                        IsLocked = user.IsLocked,
                        Role = roles.FirstOrDefault() ?? "None"
                    });
                }

                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View(userViewModels);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while loading the users list.";
                await LogWithCatch("System User", "Failed to load users.", ex);
                return View(new List<UserViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null) return NotFound();

                if (IsProtectedUser(user))
                {
                    TempData["Error"] = "You cannot perform this action on your own or protected admin account.";
                    return RedirectToAction("Index");
                }

                user.IsLocked = !user.IsLocked;
                user.LastModifiedDate = DateTime.Now;
                user.ModifiedBy = User.Identity.Name;

                await _userManager.UpdateAsync(user);

                var fullName = await GetFullName(id);
                TempData["Message"] = $"{fullName}'s account has been {(user.IsLocked ? "locked" : "unlocked")}.";

                await _auditLogger.LogAsync("System User", TempData["Message"].ToString(), User.Identity.Name ?? "N/A");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while locking/unlocking the account.";
                await LogWithCatch("System User", "Failed to toggle lock.", ex);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null) return NotFound();

                if (IsProtectedUser(user))
                {
                    TempData["Error"] = "You cannot perform this action on your own or protected admin account.";
                    return RedirectToAction("Index");
                }

                user.IsActive = !user.IsActive;
                user.LastModifiedDate = DateTime.Now;
                user.ModifiedBy = User.Identity.Name;

                await _userManager.UpdateAsync(user);

                var fullName = await GetFullName(id);
                TempData["Message"] = $"{fullName}'s account has been {(user.IsActive ? "reactivated" : "deactivated")}.";

                await _auditLogger.LogAsync("System User", TempData["Message"].ToString(), User.Identity.Name ?? "N/A");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while activating/deactivating the account.";
                await LogWithCatch("System User", "Failed to toggle active status.", ex);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string newRole)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || string.IsNullOrWhiteSpace(newRole)) return NotFound();

                if (IsProtectedUser(user))
                {
                    TempData["Error"] = "You cannot perform this action on your own or protected admin account.";
                    await _auditLogger.LogAsync("System User", $"Unable to modify role for {user.Email}.", User.Identity.Name ?? "N/A");
                    return RedirectToAction("Index");
                }

                var existingRoles = await _userManager.GetRolesAsync(user);
                if (existingRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, existingRoles);
                }

                if (!await _roleManager.RoleExistsAsync(newRole))
                {
                    await _roleManager.CreateAsync(new IdentityRole(newRole));
                }

                await _userManager.AddToRoleAsync(user, newRole);

                user.LastModifiedDate = DateTime.Now;
                user.ModifiedBy = User.Identity.Name;
                await _userManager.UpdateAsync(user);

                var fullName = await GetFullName(userId);
                TempData["Message"] = $"Role '{newRole}' assigned to {fullName}.";
                await _auditLogger.LogAsync("System User", TempData["Message"].ToString(), User.Identity.Name ?? "N/A");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while assigning role.";
                await LogWithCatch("System User", "Failed to assign role.", ex);
            }

            return RedirectToAction("Index");
        }

        private async Task<string> GetFullName(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return string.Empty;

            return new UserViewModel
            {
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName
            }.FullName;
        }

        private bool IsProtectedUser(ApplicationUser user)
        {
            return user.UserName == "admin@sitiosubicims.local" ||
                   user.Id == _userManager.GetUserId(User);
        }

        private async Task LogWithCatch(string module, string message, Exception ex)
        {
            try
            {
                await _auditLogger.LogAsync(module, $"{message} Exception: {ex.Message}", User.Identity.Name ?? "N/A");
            }
            catch
            {
                // Optional: handle logging failure silently or add fallback
            }
        }
    }
}
