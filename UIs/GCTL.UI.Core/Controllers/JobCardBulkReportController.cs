// ═══════════════════════════════════════════════════════════════════
// File: GCTL.UI.Core/Controllers/JobCardBulkReportController.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.Data;
using GCTL.Core.ViewModels.JobCardBulkReport;
using GCTL.Data.Models;
using GCTL.Service.JobCardBulkReport;
using GCTL.UI.Core.ViewModels.JobCardBulkReport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GCTL.UI.Core.Controllers
{
    public class JobCardBulkReportController : BaseController
    {
        private readonly IJobCardBulkReportService _service;
        private readonly IRepository<HrmPayMonth> _monthRepo;
        private readonly IWebHostEnvironment _env;

        public JobCardBulkReportController(
            IJobCardBulkReportService service,
            IRepository<HrmPayMonth> monthRepo,
            IWebHostEnvironment env)
        {
            _service = service;
            _monthRepo = monthRepo;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var hasPermission = await _service.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission)
                return RedirectToAction("Login", "Accounts");

            ViewBag.AccessCode = LoginInfo.AccessCode;

            var monthList = _monthRepo.All()
                .OrderBy(m => m.MonthNum)
                .Select(m => new { id = (int)m.MonthNum, name = m.MonthName })
                .ToList();

            if (!monthList.Any())
            {
                monthList = Enumerable.Range(1, 12).Select(i => new
                {
                    id = i,
                    name = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i)
                }).ToList();
            }

            ViewBag.MonthList = new SelectList(monthList, "id", "name");

            var model = new JobCardBulkReportViewModel
            {
                PageUrl = Url.Action(nameof(Index))
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GetSummary([FromBody] JobCardBulkReportFilterDto filter)
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
        public async Task<IActionResult> PreviewPdf([FromBody] JobCardBulkReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.GeneratePdfAsync(filter, logoPath);

                return File(bytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadPdf([FromBody] JobCardBulkReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.GeneratePdfAsync(filter, logoPath);
                var fileName = $"JobCardBulkReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadExcel([FromBody] JobCardBulkReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.ExportExcelAsync(filter, logoPath);
                var fileName = $"JobCardBulkReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

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
        public async Task<IActionResult> DownloadCsv([FromBody] JobCardBulkReportFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var bytes = await _service.ExportCsvAsync(filter);
                var fileName = $"JobCardBulkReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

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
