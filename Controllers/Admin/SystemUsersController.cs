using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SitioSubicIMS.Web.Controllers.Admin
{
    [Authorize(Roles = "Administrator")]
    public class SystemUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public SystemUsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users
                .Where(u => u.UserName != "admin@sitiosubicims.local")
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
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

            var allRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.AllRoles = allRoles;

            return View(userViewModels);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
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
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string id)
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
            TempData["Message"] = $"{fullName}'s account has been {(user.IsActive ? "deactivated" : "reactivated")}.";
            return RedirectToAction("Index");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(newRole)) return NotFound();
            if (IsProtectedUser(user))
            {
                TempData["Error"] = "You cannot perform this action on your own or protected admin account.";
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
            return RedirectToAction("Index");
        }
        private async Task<string> GetFullName(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return string.Empty;

            var uvm = new UserViewModel
            {
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName
            };

            return uvm.FullName;
        }
        private bool IsProtectedUser(ApplicationUser user)
        {
            return user.UserName == "admin@sitiosubicims.local" ||
                   user.Id == _userManager.GetUserId(User);
        }

    }
}
