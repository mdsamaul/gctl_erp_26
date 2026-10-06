// ═══════════════════════════════════════════════════════════════════
// File: GCTL.UI.Core/Controllers/JobCardReportController.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels.JobCardReport;
using GCTL.Service.JobCardReport;
using GCTL.UI.Core.ViewModels.JobCardReport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GCTL.UI.Core.Controllers
{
    public class JobCardReportController : BaseController
    {
        private readonly IJobCardReportService _service;
        private readonly IWebHostEnvironment _env;

        public JobCardReportController(
            IJobCardReportService service,
            IWebHostEnvironment env)
        {
            _service = service;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var hasPermission = await _service.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission)
                return RedirectToAction("Login", "Accounts");

            ViewBag.AccessCode = LoginInfo.AccessCode;
            var model = new JobCardReportViewModel
            {
                PageUrl = Url.Action(nameof(Index))
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GetSummary([FromBody] JobCardReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var data = await _service.GetReportDataAsync(filter);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadExcel([FromBody] JobCardReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.ExportExcelAsync(filter, logoPath);
                var fileName = $"JobCardReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadCsv([FromBody] JobCardReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var bytes = await _service.ExportCsvAsync(filter);
                var fileName = $"JobCardReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(bytes,
                    "text/csv; charset=utf-8",
                    fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
