﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ShuttleServiceManagementSystem.Controllers
{
    [Authorize(Roles = "Manager, Administrator")]
    public class ManagersController : Controller
    {
        //
        // GET: /Managers/
        public ActionResult Home()
        {
            return View();
        }
	}
}