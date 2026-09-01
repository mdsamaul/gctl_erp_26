using GCTL.Core.Data;
using GCTL.Core.ViewModels.Common;
using GCTL.Core.ViewModels.HrmSalaryCertificateReports;
using GCTL.Data.Models;
using GCTL.Service.HrmSalaryCertificateReports;
using GCTL.UI.Core.ViewModels.HrmSalaryCertificateReports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GCTL.UI.Core.Controllers
{
    public class HrmSalaryCertificateReportController : BaseController
    {
        private readonly IHrmSalaryCertificateReportService reportService;
        private readonly IRepository<HrmPayMonth> monthRepo;

        public HrmSalaryCertificateReportController(IHrmSalaryCertificateReportService reportService, IRepository<HrmPayMonth> monthRepo)
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

            HrmSalaryCertificatePageVM model = new HrmSalaryCertificatePageVM()
            {
                PageUrl = Url.Action(nameof(Index)),
                AccessCode = LoginInfo.AccessCode
            };
            return View(model);
        }
        [HttpPost("HrmSalaryCertificateReport/PreviewReport")]
        public async Task<IActionResult> PreviewReport([FromBody] SalaryCertificateReportRequest request)
        {
            request.FilterData.EmployeeId = LoginInfo.EmployeeId;
            request.FilterData.AccessCode = LoginInfo.AccessCode;

            var data = await reportService.GetPaySlipDataAsync(request.FilterData);

            if (data == null || !data.Any())
                return NotFound("No salary certificate data found for the given filter.");

            var pdfBytes = reportService.GenerateSalaryCertificatePdf(data);
            return File(pdfBytes, "application/pdf");
        }

        [HttpPost("HrmSalaryCertificateReport/ExportReport")]
        public async Task<IActionResult> ExportReport([FromBody] SalaryCertificateReportRequest request)
        {
            request.FilterData.EmployeeId = LoginInfo.EmployeeId;
            request.FilterData.AccessCode = LoginInfo.AccessCode;

            var data = await reportService.GetPaySlipDataAsync(request.FilterData);

            if (data == null || !data.Any())
                return NotFound("No salary certificate data found for the given filter.");

            byte[] fileBytes;
            string fileName;
            string contentType;

            switch (request.ExportFormat?.ToLowerInvariant())
            {
                case "pdf":
                case "downloadpdf":
                    fileBytes = reportService.GenerateSalaryCertificatePdf(data);
                    fileName = $"SalaryCertificateReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    break;

                case "excel":
                case "downloadexcel":
                    fileBytes = reportService.GenerateSalaryCertificateExcel(data);
                    fileName = $"SalaryCertificateReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    break;

                case "download":
                    fileBytes = reportService.GenerateSalaryCertificatePdf(data);
                    fileName = $"SalaryCertificateReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    Response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                    return File(fileBytes, contentType);

                default:
                    fileBytes = reportService.GenerateSalaryCertificatePdf(data);
                    fileName = $"SalaryCertificateReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    break;
            }

            return File(fileBytes, contentType, fileName);
        }
    }
}
