using GCTL.Core.Data;
using GCTL.Core.ViewModels.Common;
using GCTL.Core.ViewModels.HrmPaySlipReports;
using GCTL.Data.Models;
using GCTL.Service.HrmPaySlipReports;
using GCTL.UI.Core.ViewModels.HrmPaySlipReports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;

namespace GCTL.UI.Core.Controllers
{
    public class HrmPaySlipReportController : BaseController
    {
        private readonly IHrmPaySlipReportService reportService;
        private readonly IRepository<HrmPayMonth> monthRepo;

        public HrmPaySlipReportController(IHrmPaySlipReportService reportService, IRepository<HrmPayMonth> monthRepo)
        {
            this.reportService = reportService;
            this.monthRepo = monthRepo;
        }

        public async Task<IActionResult> Index()
        {
            //var hasPermission = await reportService.PagePermissionAsync(LoginInfo.AccessCode);
            //if (!hasPermission)
            //    return RedirectToAction("Login", "Accounts");

            var currentMonth = DateTime.Now.ToString("MMMM");
            var months = await monthRepo.All().AsNoTracking().Select(e => new CommonSelectModel
            {
                Code = e.MonthName,
                Name = e.MonthName
            }).ToListAsync();

            ViewBag.MonthList = new SelectList(months, "Code", "Name", currentMonth);

            HrmPaySlipReportPageVM model = new HrmPaySlipReportPageVM()
            {
                PageUrl = Url.Action(nameof(Index)),
                AccessCode = LoginInfo.AccessCode
            };
            return View(model);
        }

        [HttpPost("HrmPaySlipReport/PreviewReport")]
        public async Task<IActionResult> PreviewReport([FromBody] PaySlipReportRequest request)
        {
            request.FilterData.EmployeeId = LoginInfo.EmployeeId;
            request.FilterData.AccessCode = LoginInfo.AccessCode;

            var data = await reportService.GetPaySlipDataAsync(request.FilterData);

            if (data == null || !data.Any())
                return NotFound("No pay slip data found for the given filter.");

            var pdfBytes = reportService.GeneratePaySlipPdf(data);
            return File(pdfBytes, "application/pdf");
        }

        [HttpPost("HrmPaySlipReport/ExportReport")]
        public async Task<IActionResult> ExportReport([FromBody] PaySlipReportRequest request)
        {
            request.FilterData.EmployeeId = LoginInfo.EmployeeId;
            request.FilterData.AccessCode = LoginInfo.AccessCode;

            var data = await reportService.GetPaySlipDataAsync(request.FilterData);

            if (data == null || !data.Any())
                return NotFound("No pay slip data found for the given filter.");

            byte[] fileBytes;
            string fileName;
            string contentType;

            switch (request.ExportFormat?.ToLowerInvariant())
            {
                case "pdf":
                case "downloadpdf":
                    fileBytes = reportService.GeneratePaySlipPdf(data);
                    fileName = $"PaySlipReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    break;

                case "excel":
                case "downloadexcel":
                    fileBytes = reportService.GeneratePaySlipExcel(data);
                    fileName = $"PaySlipReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    break;

                case "download":
                    fileBytes = reportService.GeneratePaySlipPdf(data);
                    fileName = $"PaySlipReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    Response.Headers.Add("Content-Disposition", $"inline; filename=\"{fileName}\"");
                    return File(fileBytes, contentType);

                default:
                    fileBytes = reportService.GeneratePaySlipPdf(data);
                    fileName = $"PaySlipReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                    contentType = "application/pdf";
                    break;
            }

            return File(fileBytes, contentType, fileName);
        }

    }
}
