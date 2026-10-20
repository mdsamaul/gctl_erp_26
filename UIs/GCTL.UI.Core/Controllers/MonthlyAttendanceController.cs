// ═══════════════════════════════════════════════════════════════════
// File: GCTL.UI.Core/Controllers/MonthlyAttendanceController.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.Data;
using GCTL.Core.ViewModels.MonthlyAttendance;
using GCTL.Data.Models;
using GCTL.Service.MonthlyAttendance;
using GCTL.UI.Core.ViewModels.MonthlyAttendance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GCTL.UI.Core.Controllers
{
    public class MonthlyAttendanceController : BaseController
    {
        private readonly IMonthlyAttendanceService _service;
        private readonly IRepository<HrmPayMonth> _monthRepo;
        private readonly IRepository<HrmEisDefEmploymentNature> _empNatureRepo;
        private readonly IWebHostEnvironment _env;

        public MonthlyAttendanceController(
            IMonthlyAttendanceService service,
            IRepository<HrmPayMonth> monthRepo,
            IRepository<HrmEisDefEmploymentNature> empNatureRepo,
            IWebHostEnvironment env)
        {
            _service = service;
            _monthRepo = monthRepo;
            _empNatureRepo = empNatureRepo;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var hasPermission = await _service.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission)
                return RedirectToAction("Login", "Accounts");

            ViewBag.AccessCode = LoginInfo.AccessCode;

            // Month list
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

            ViewBag.MonthList = new SelectList(monthList, "id", "name", DateTime.Now.Month);

            // Employment Nature list
            var natureList = _empNatureRepo.All()
                .Select(e => new { id = e.EmploymentNatureId, name = e.EmploymentNature })
                .ToList();

            ViewBag.EmploymentNatureList = new SelectList(natureList, "id", "name");

            var model = new MonthlyAttendanceViewModel
            {
                PageUrl = Url.Action(nameof(Index))
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GetSummary([FromBody] MonthlyAttendanceFilterDto filter)
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
        public async Task<IActionResult> PreviewPdf([FromBody] MonthlyAttendanceFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.GeneratePdfAsync(filter, logoPath);
                if (bytes == null || bytes.Length == 0)
                {
                    return Json(new { success = false, message = "No attendance data found for the selected criteria." });
                }

                return File(bytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadPdf([FromBody] MonthlyAttendanceFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.GeneratePdfAsync(filter, logoPath);
                if (bytes == null || bytes.Length == 0)
                {
                    return Json(new { success = false, message = "No attendance data found for the selected criteria." });
                }

                var fileName = $"MonthlyAttendanceDetailsReport.pdf";

                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownloadExcel([FromBody] MonthlyAttendanceFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var logoPath = Path.Combine(_env.WebRootPath, "images", "DP_logo.png");
                var bytes = await _service.ExportExcelAsync(filter, logoPath);
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"MonthlyAttendanceDetailsReport.xlsx";

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
        public async Task<IActionResult> DownloadCsv([FromBody] MonthlyAttendanceFilterDto filter)
        {
            try
            {
                filter.LoginEmployeeId = LoginInfo.EmployeeId;
                filter.AccessCodeId = LoginInfo.AccessCode;

                var bytes = await _service.ExportCsvAsync(filter);
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"MonthlyAttendanceDetailsReport.csv";

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
