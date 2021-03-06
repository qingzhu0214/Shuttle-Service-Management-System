﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin.Security;
using ShuttleServiceManagementSystem.Models;
using ShuttleServiceManagementSystem.Utilities;
using SSMSDataModel.DAL;

namespace ShuttleServiceManagementSystem.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private SDSU_SchoolEntities db = new SDSU_SchoolEntities();

        public AccountController()
            : this(new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext())))
        {
        }

        public AccountController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        public UserManager<ApplicationUser> UserManager { get; private set; }

        public SSMS_Helper ssms = new SSMS_Helper();

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            List<string> mylist = ssms.GetDestinationList();
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindAsync(model.UserName, model.Password);
                if (user != null)
                {
                    await SignInAsync(user, model.RememberMe);

                    // Create record log for this login
                    ssms.CreateSystemLog(user.Id);

                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser() { UserName = model.UserName };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await SignInAsync(user, isPersistent: false);

                    // Assign the new user to the default role of "Customer"
                    ssms.InsertNewUserRole(user.Id, (int)Roles.CUSTOMER);

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ResetPassword
        public ActionResult ResetPassword(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            ViewBag.HasLocalPassword = HasPassword();
            ViewBag.ReturnUrl = Url.Action("Manage");
            return View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ManageUserViewModel model)
        {
            bool hasPassword = HasPassword();
            ViewBag.HasLocalPassword = hasPassword;
            ViewBag.ReturnUrl = Url.Action("Manage");
            if (hasPassword)
            {
                if (ModelState.IsValid)
                {
                    IdentityResult result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("ResetPassword", new { Message = ManageMessageId.ChangePasswordSuccess });
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }
            else
            {
                // User does not have a password so remove any validation errors caused by a missing OldPassword field
                ModelState state = ModelState["OldPassword"];
                if (state != null)
                {
                    state.Errors.Clear();
                }

                if (ModelState.IsValid)
                {
                    IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ManageProfileInfo
        public ActionResult ManageProfileInfo()
        {
            // Variable Declarations
            USER_INFO userInfo = new USER_INFO();
            ProfileInfoViewModel model = new ProfileInfoViewModel();

            // Create a selectlist of destinations to be passed to the view
            ViewBag.CELL_CARRIER_NAME = new SelectList(db.CELL_CARRIERS, "CARRIER_ID", "CARRIER_NAME");

            // Check if the user already has entered their profile information before
            if (ssms.CheckIfUserInfoExists(User.Identity.GetUserId()))
            {
                ViewBag.PageInstruction = "Make your desired profile changes by editing the fields below and clicking the \"Submit\" button below.";

                // Get the users current profile info
                userInfo = ssms.GetUserProfileInfo(User.Identity.GetUserId());

                // Populate the view model
                model.FirstName = userInfo.FIRST_NAME;
                model.LastName = userInfo.LAST_NAME;
                model.StreetAddress = userInfo.STREET_ADDRESS;
                model.City = userInfo.CITY;
                model.State = userInfo.STATE;
                model.ZipCode = userInfo.ZIP_CODE;
                model.EmailAddress = userInfo.EMAIL_ADDRESS;
                model.CellNumber = userInfo.CELL_NUMBER;
                model.CellCarrierID = userInfo.CELL_CARRIER_ID;
                model.ReceiveText = userInfo.RECEIVE_TEXT;
                model.ReceiveEmail = userInfo.RECEIVE_EMAIL;

                return View(model);
            }
            else
            {
                ViewBag.PageInstruction = "Fill in the following fields and click the \"Submit\" button below to add your personal information to your account.";

                return View();
            }
        }

        //
        // POST: /Account/ManageProfileInfo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManageProfileInfo(ProfileInfoViewModel model)
        {
            // Variable Declarations
            string userID = "";

            // Check if the state of the model is OK
            if (ModelState.IsValid)
            {
                // Get the user id
                userID = User.Identity.GetUserId();

                // Check if this is an Edit or an Add operation
                if (ssms.CheckIfUserInfoExists(userID))
                {
                    // This is an edit operation

                    // Update the user's profile info
                    ssms.UpdateExistingUserInfo(userID, model.FirstName, model.LastName, model.StreetAddress, model.City,
                                           model.State, model.ZipCode, model.EmailAddress, model.CellNumber.Replace("-", ""), model.CellCarrierID.ToString(),
                                           Convert.ToBoolean(model.ReceiveText), Convert.ToBoolean(model.ReceiveEmail));
                }
                else
                {
                    // This is an add operation

                    // Insert the user profile info into the USER_INFO table
                    ssms.InsertNewUserInfo(userID, model.FirstName, model.LastName, model.StreetAddress, model.City,
                                           model.State, model.ZipCode, model.EmailAddress, model.CellNumber.Replace("-", ""), model.CellCarrierID.ToString(),
                                           Convert.ToBoolean(model.ReceiveText), Convert.ToBoolean(model.ReceiveEmail));
                }

                return RedirectToAction("Index", "Home");
            }
            else
            {
                ModelState.AddModelError("", "Opps!  Something went wrong!  Please check all your field inputs and try again.");
            }

            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut();
            return RedirectToAction("Login", "Account");
        }

        [ChildActionOnly]
        public ActionResult RemoveAccountList()
        {
            var linkedAccounts = UserManager.GetLogins(User.Identity.GetUserId());
            ViewBag.ShowRemoveButton = HasPassword() || linkedAccounts.Count > 1;
            return (ActionResult)PartialView("_RemoveAccountPartial", linkedAccounts);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && UserManager != null)
            {
                UserManager.Dispose();
                UserManager = null;
            }
            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private async Task SignInAsync(ApplicationUser user, bool isPersistent)
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
            var identity = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, identity);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            Error
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        private class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri) : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties() { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}