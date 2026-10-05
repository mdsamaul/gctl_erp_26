using GCTL.Core.Data;
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;
using GCTL.Service.LeaveReport;
using GCTL.UI.Core.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace GCTL.Web.Controllers
{
    public class LeaveReportController : BaseController
    {
        private readonly ILeaveReportService _leaveReportService;
        private readonly IRepository<CoreCompany> _company;

        private const string leaveReportTitle = "Leave ApplicationEntry Info";
        private readonly bool developerMode = true;
        private string AccessCode => LoginInfo.AccessCode;

        public LeaveReportController(ILeaveReportService leaveReportService, IRepository<CoreCompany> company)
        {
            _leaveReportService = leaveReportService;
            _company = company;
        }

        // ─── REPORT PAGE ─────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            var hasPermission = await _leaveReportService.HasPagePermissionAsync(AccessCode, leaveReportTitle);
            if (!hasPermission && !developerMode)
                return PartialView("_NoAccessView", "You have no access.");

            var bsd = new BaseViewModel();

            return View(bsd);
        }

        // ─── PREVIEW ─────────────────────────────────────────────────────────────

        public async Task<IActionResult> PreviewLeaveReportArray(LeaveReportArrayViewModel? model)
        {
            try
            {
                var hasPermission = await _leaveReportService.HasPrintPermissionAsync(AccessCode, leaveReportTitle);
                if (!hasPermission && !developerMode)
                    return PartialView("_NoAccessView", "You have no access.");

                if (model?.Company == null)
                    return Json(new { success = false, message = "Please fill company name." });

                model.DateTo = DateTime.ParseExact(model.DateToStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                model.DateFrom = DateTime.ParseExact(model.DateFromStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

                //var data = await _leaveReportService.GetReportArrayAsync(model);


                var grouped = await _leaveReportService.GetLeaveReportArrayAsync(model);

                // Group into same structure JS expects
                var data = grouped
                    .GroupBy(x => "DataPath")
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            departmentData = g
                                .GroupBy(x => x.DepartmentName ?? "Unknown Department")
                                .ToDictionary(
                                    d => d.Key,
                                    d => new
                                    {
                                        departmentName = d.Key,
                                        leaveDetails = d.ToList()
                                    })
                        });

                

                return Json(new { success = true, message = "Data Get", data });
            }
            catch
            {
                throw;
            }
        }


        [HttpPost]
        public async Task<IActionResult> PreviewPdf(LeaveReportArrayViewModel? model)
        {
            try
            {
                if (model?.Company == null)
                    return Json(new { success = false, message = "Please select a company." });

                model.DateTo = DateTime.ParseExact(model.DateToStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                model.DateFrom = DateTime.ParseExact(model.DateFromStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

                string sdate = model.DateFrom != DateTime.MinValue ? model.DateFrom.ToString("dd-MM-yyyy") : null;
                string edate = model.DateTo != DateTime.MinValue ? model.DateTo.ToString("dd-MM-yyyy") : null;

                var flat = await _leaveReportService.GetLeaveReportArrayAsync(model);

                if (flat.Count() == 0)
                {
                    return Json(new { success = false, message ="No Data Found" });

                }

                // Group flat list → Dictionary structure GeneratePdfReportArray expects
                var grouped = flat
                    .GroupBy(x => "DataPath")  // hardcoded company name
                    .ToDictionary(
                        g => g.Key,
                        g => new CompanyLeaveDataVM
                        {
                            CompanyCode = g.Key,
                            DepartmentData = g
                                .GroupBy(x => x.DepartmentName ?? "Unknown Department")
                                .ToDictionary(
                                    d => d.Key,
                                    d => new DepartmentLeaveDataVM
                                    {
                                        DepartmentName = d.Key,
                                        LeaveDetails = d.Select(x => new LeaveDetailVM
                                        {
                                            EmployeeId = x.EmployeeID,
                                            EmployeeFirstName = x.EmployeeFirstName,
                                            DesignationName = x.DesignationName,
                                            LeaveAppEntryId = x.LeaveAppEntryId,
                                            ApplyLeaveFormat = x.ApplyLeaveFormat,
                                            HODApprovalStatus = x.HODApprovalStatus,
                                            LeaveTypeId = x.LeaveTypeId,
                                            StartDate = x.StartDate,
                                            EndDate = x.EndDate,
                                            NoOfDay = x.NoOfDay,
                                            FirstOrSecondHalf = x.FirstOrSecondHalf,
                                            Reason = x.Reason,
                                            IsApproved = x.IsApproved,
                                            ConfirmationRemarks = x.ConfirmationRemarks,
                                            HRApprovalStatus = x.HRApprovalStatus,
                                            HRApprovalRemarks = x.HRApprovalRemarks,
                                            ShortLeaveFrom = x.ShortLeaveFrom,
                                            ShortLeaveTo = x.ShortLeaveTo,
                                            DaysStr = x.DaysStr
                                        }).ToList()
                                    })
                        });

                var pdfBytes = _leaveReportService.GeneratePdfReportArray(grouped, sdate, edate);
                return Json(new { success = true, pdfData = Convert.ToBase64String(pdfBytes) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        // ─── EXPORT ──────────────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> DownloadReportN(LeaveReportArrayViewModel? model)
        {
            try
            {
                var hasPermission = await _leaveReportService.HasPrintPermissionAsync(AccessCode, leaveReportTitle);
                if (!hasPermission && !developerMode)
                    return PartialView("_NoAccessView", "You have no access.");

                if (model?.Company == null || model.ReportFormat == null)
                    return Ok(new { success = false, message = "Please fill in Company Name and select a Report Format." });

                model.DateTo = DateTime.ParseExact(model.DateToStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                model.DateFrom = DateTime.ParseExact(model.DateFromStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

                string sdate = model.DateFrom != DateTime.MinValue ? model.DateFrom.ToString("dd-MM-yyyy") : null;
                string edate = model.DateTo != DateTime.MinValue ? model.DateTo.ToString("dd-MM-yyyy") : null;

                //var data = await _leaveReportService.GetReportArrayAsync(model);
               

                var ggdata = await _leaveReportService.GetLeaveReportArrayAsync(model);

                if (ggdata.Count() == 0 || ggdata == null)
                {
                    return Ok(new { success = false, message = "No data found" });
                }

                // Convert List → Dictionary<companyName, CompanyLeaveDataVM>
                var data = ggdata
                    .GroupBy(x =>  "Datapath")
                    .ToDictionary(
                        g => g.Key,
                        g => new CompanyLeaveDataVM
                        {
                            CompanyCode = g.Key,
                            DepartmentData = g
                                .GroupBy(x => x.DepartmentName ?? "Unknown Department")
                                .ToDictionary(
                                    d => d.Key,
                                    d => new DepartmentLeaveDataVM
                                    {
                                        DepartmentName = d.Key,
                                        LeaveDetails = d.Select(x => new LeaveDetailVM
                                        {
                                            EmployeeId = x.EmployeeID,
                                            EmployeeFirstName = x.EmployeeFirstName,
                                            DesignationName = x.DesignationName,
                                            LeaveAppEntryId = x.LeaveAppEntryId,
                                            ApplyLeaveFormat = x.ApplyLeaveFormat,
                                            HODApprovalStatus = x.HODApprovalStatus,
                                            LeaveTypeId = x.LeaveTypeId,
                                            StartDate = x.StartDate,
                                            EndDate = x.EndDate,
                                            NoOfDay = x.NoOfDay,
                                            FirstOrSecondHalf = x.FirstOrSecondHalf,
                                            Reason = x.Reason,
                                            IsApproved = x.IsApproved,
                                            ConfirmationRemarks = x.ConfirmationRemarks,
                                            HRApprovalStatus = x.HRApprovalStatus,
                                            HRApprovalRemarks = x.HRApprovalRemarks,
                                            ShortLeaveFrom = x.ShortLeaveFrom,
                                            ShortLeaveTo = x.ShortLeaveTo,
                                            DaysStr = x.DaysStr
                                        }).ToList()
                                    })
                        });

               

                if (model.ReportFormat == "pdf")
                {
                    var bytes = _leaveReportService.GeneratePdfReportArray(data, sdate, edate);
                    return File(bytes, "application/pdf", $"LeaveReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                }

                if (model.ReportFormat == "excel")
                {
                    var bytes = _leaveReportService.GenerateExcelReportArray(data, sdate, edate);
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"LeaveReport_{DateTime.Now:yyyyMMdd}.xlsx");
                }

                return Ok(new { success = false, message = "Invalid report format." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message =  ex.Message, error = ex.Message });
            }
        }

        // ─── DROPDOWNS ───────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult GetCompanies()
        {
            var result = _company.All()
                .Select(c => new { c.CompanyCode, c.CompanyName })
                .ToList();

            if (result.Count() == 1)
            {
                var firstData = _company.All().Select(c => c.CompanyCode).FirstOrDefault();
                return Json(new { result , firstData });
            }

            return Json(new { result });
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesMultiComp(List<string> companyCode, bool isAll)
        {
            var result = await _leaveReportService.GetBranchesByMultiCompAsync(companyCode, isAll);
            return Json(new { result });
        }

        [HttpGet]
        public async Task<IActionResult> GetDeptMultiCompBranch(List<string> companyCode, List<string> branchCode, bool isAll)
        {
            var result = await _leaveReportService.GetDeptByMultiCompBranchAsync(companyCode, branchCode, isAll);
            return Json(new { result });
        }

        [HttpGet]
        public async Task<IActionResult> GetEmpMultiCompBranchDept(List<string> companyCode, List<string> branchCode, List<string> departmentCode, bool isAll)
        {
            var result = await _leaveReportService.GetEmpByMultiCompBranchDeptAsync(companyCode, branchCode, departmentCode, isAll);
            return Json(new { result });
        }
    }
}