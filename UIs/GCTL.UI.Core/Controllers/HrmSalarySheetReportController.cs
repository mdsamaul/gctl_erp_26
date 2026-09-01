using System;
using System.Linq;
using System.Threading.Tasks;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.Common;
using GCTL.Core.ViewModels.HrmSalarySheetReports;
using GCTL.Data.Models;
using GCTL.Service.HrmSalarySheetReports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GCTL.UI.Core.Controllers
{
    public class HrmSalarySheetReportController : BaseController
    {
        private readonly IHrmSalarySheetReportService reportService;
        private readonly IRepository<HrmPayMonth> monthRepo;

        public HrmSalarySheetReportController(
            IHrmSalarySheetReportService reportService,
            IRepository<HrmPayMonth> monthRepo)
        {
            this.reportService = reportService;
            this.monthRepo = monthRepo;
        }

        public async Task<IActionResult> Index()
        {
            var hasPermission = await reportService.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission)
                return RedirectToAction("Login", "Accounts");

            var currentMonth = DateTime.Now.ToString("MMMM");
            var months = await monthRepo.All().AsNoTracking().Select(e => new CommonSelectModel
            {
                Code = e.MonthName,
                Name = e.MonthName
            }).ToListAsync();

            ViewBag.MonthList = new SelectList(months, "Code", "Name", currentMonth);

            HrmSalarySheetReportPageVM model = new HrmSalarySheetReportPageVM()
            {
                PageUrl = Url.Action(nameof(Index)),
                AccessCode = LoginInfo.AccessCode
            };
            return View(model);
        }

        [HttpPost("HrmSalarySheetReport/PreviewReport")]
        public async Task<IActionResult> PreviewReport([FromBody] SalarySheetReportRequest request)
        {
            request.FilterData.AccessCode = LoginInfo.AccessCode;
            request.FilterData.EmployeeId = LoginInfo.EmployeeId;

            var data = await reportService.GetSalarySheetDataAsync(request.FilterData);

            if (data == null || !data.Any())
                return NotFound("No salary sheet data found for the given filter.");

            var pdfBytes = reportService.GenerateSalarySheetPdf(data);
            return File(pdfBytes, "application/pdf");
        }

        [HttpPost("HrmSalarySheetReport/ExportReport")]
        public async Task<IActionResult> ExportReport([FromBody] SalarySheetReportRequest request)
        {
            request.FilterData.EmployeeId = LoginInfo.EmployeeId;
            request.FilterData.AccessCode = LoginInfo.AccessCode;

            var data = await reportService.GetSalarySheetDataAsync(request.FilterData);

            if (data == null || !data.Any())
                return NotFound("No salary sheet data found for the given filter.");

            byte[] fileBytes;
            string fileName;
            string contentType;

            switch (request.ExportFormat?.ToLowerInvariant())
            {
                case "pdf":
                case "downloadpdf":
                    fileBytes = reportService.GenerateSalarySheetPdf(data);
                    fileName = $"SalarySheetReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    break;

                case "excel":
                case "downloadexcel":
                    fileBytes = reportService.GenerateSalarySheetExcel(data);
                    fileName = $"SalarySheetReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                    contentType = "application/vnd.ms-excel";
                    break;

                case "download":
                    fileBytes = reportService.GenerateSalarySheetPdf(data);
                    fileName = $"SalarySheetReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    Response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                    return File(fileBytes, contentType);

                default:
                    fileBytes = reportService.GenerateSalarySheetPdf(data);
                    fileName = $"SalarySheetReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    break;
            }

            return File(fileBytes, contentType, fileName);
        }
    }
}
