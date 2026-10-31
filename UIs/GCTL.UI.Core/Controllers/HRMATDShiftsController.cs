using ClosedXML.Excel;
using GCTL.Core.Data;
using GCTL.Core.Helpers;
using GCTL.Core.ViewModels.HrmAtdShifts;
using GCTL.Data.Models;
using GCTL.Service.Common;
using GCTL.Service.HrmAtdShifts;
using GCTL.UI.Core.ViewModels.HrmAtdShifts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GCTL.UI.Core.Controllers
{
    public class HRMATDShiftsController : BaseController
    {
        private readonly IHrmAtdShiftService hrmAtdShiftService;
        private readonly ICommonService commonService;
        private readonly IRepository<HrmDefShiftType> shiftService;
        private readonly IRepository<HrmDefOvertimeRule> overtimeRuleService;
        private readonly IRepository<CoreCompany> companyRepo;
        private readonly IRepository<CoreBranch> branchRepo;
        private readonly IRepository<HrmDefDepartment> deptRepo;
        private readonly IRepository<HrmEmployee> empRepo;
        private readonly IRepository<HrmEmployeeOfficialInfo> empOfficialRepo;

        public HRMATDShiftsController(
            IHrmAtdShiftService hrmAtdShiftService,
            IRepository<HrmDefShiftType> shiftService,
            ICommonService commonService,
            IRepository<HrmDefOvertimeRule> overtimeRuleService,
            IRepository<CoreCompany> companyRepo,
            IRepository<CoreBranch> branchRepo,
            IRepository<HrmDefDepartment> deptRepo,
            IRepository<HrmEmployee> empRepo,
            IRepository<HrmEmployeeOfficialInfo> empOfficialRepo)
        {
            this.hrmAtdShiftService = hrmAtdShiftService;
            this.commonService = commonService;
            this.shiftService = shiftService;
            this.overtimeRuleService = overtimeRuleService;
            this.companyRepo = companyRepo;
            this.branchRepo = branchRepo;
            this.deptRepo = deptRepo;
            this.empRepo = empRepo;
            this.empOfficialRepo = empOfficialRepo;
        }

        #region GetByallId
        public async Task<IActionResult> Index(string? id)
        {
            var hasPermission = await hrmAtdShiftService.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission)
            {
                return RedirectToAction("Login", "Accounts");
            }
            HrmAtdShiftPageViewModel model = new HrmAtdShiftPageViewModel();
            var list = await hrmAtdShiftService.GetAllAsync();
            model.HrmAttendenceShiftList = list ?? new List<HrmAtdShiftSetupViewModel>();
            if (!string.IsNullOrEmpty(id))
            {
                model.Setup = await hrmAtdShiftService.GetByIdAsync(id);
            }

            var officialList = empOfficialRepo.All()
                .Where(o => o.EmployeeStatus == "01" || o.EmployeeStatus == "Active")
                .Select(o => new
                {
                    empId = (o.EmployeeId ?? "").Trim(),
                    compId = (o.CompanyCode ?? "").Trim(),
                    brId = (o.BranchCode ?? "").Trim(),
                    deptId = (o.DepartmentCode ?? "").Trim(),
                    status = (o.EmployeeStatus ?? "").Trim()
                })
                .ToList();

            var officialByEmp = officialList
                .Where(o => !string.IsNullOrEmpty(o.empId))
                .GroupBy(o => o.empId)
                .ToDictionary(g => g.Key, g => g.First());

            var branchCompanies = officialList
                .Where(o => !string.IsNullOrEmpty(o.brId) && !string.IsNullOrEmpty(o.compId))
                .GroupBy(o => o.brId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.compId).Distinct().ToList());

            var deptCompanies = officialList
                .Where(o => !string.IsNullOrEmpty(o.deptId) && !string.IsNullOrEmpty(o.compId))
                .GroupBy(o => o.deptId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.compId).Distinct().ToList());

            var deptBranches = officialList
                .Where(o => !string.IsNullOrEmpty(o.deptId) && !string.IsNullOrEmpty(o.brId))
                .GroupBy(o => o.deptId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.brId).Distinct().ToList());

            var companies = companyRepo.All()
                .Select(x => new { id = (x.CompanyCode ?? "").Trim(), name = x.CompanyName ?? "" })
                .OrderBy(x => x.name)
                .ToList();

            var branches = branchRepo.All()
                .ToList()
                .Select(x =>
                {
                    var bid = (x.BranchCode ?? "").Trim();
                    var compSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(x.CompanyCode)) compSet.Add(x.CompanyCode.Trim());
                    if (branchCompanies.TryGetValue(bid, out var cList))
                    {
                        foreach (var c in cList) compSet.Add(c);
                    }
                    return new
                    {
                        id = bid,
                        name = x.BranchName ?? "",
                        companyCodes = compSet.ToList()
                    };
                })
                .OrderBy(x => x.name)
                .ToList();

            var depts = deptRepo.All()
                .ToList()
                .Select(x =>
                {
                    var did = (x.DepartmentCode ?? "").Trim();
                    var compSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(x.CompanyCode)) compSet.Add(x.CompanyCode.Trim());
                    if (deptCompanies.TryGetValue(did, out var cList))
                    {
                        foreach (var c in cList) compSet.Add(c);
                    }
                    var brSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (deptBranches.TryGetValue(did, out var bList))
                    {
                        foreach (var b in bList) brSet.Add(b);
                    }
                    return new
                    {
                        id = did,
                        name = x.DepartmentName ?? "",
                        companyCodes = compSet.ToList(),
                        branchCodes = brSet.ToList()
                    };
                })
                .OrderBy(x => x.name)
                .ToList();

            var emps = empRepo.All()
                .Select(x => new
                {
                    id = (x.EmployeeId ?? "").Trim(),
                    firstName = x.FirstName ?? "",
                    lastName = x.LastName ?? ""
                })
                .ToList()
                .Where(x => !string.IsNullOrEmpty(x.id) && officialByEmp.ContainsKey(x.id))
                .Select(x =>
                {
                    officialByEmp.TryGetValue(x.id, out var off);
                    return new
                    {
                        id = x.id,
                        name = (x.id + " - " + (x.firstName + " " + x.lastName).Trim()).Trim(),
                        companyCode = off?.compId ?? "",
                        branchCode = off?.brId ?? "",
                        deptCode = off?.deptId ?? "",
                        isActive = true
                    };
                })
                .OrderBy(x => x.id)
                .ToList();

            var shiftTypes = shiftService.All().Select(x => new { id = x.ShiftTypeId, name = x.ShiftTypeName }).ToList();
            var otRules = overtimeRuleService.All().Select(x => new { id = x.OvertimeRuleId, name = x.OvertimeRuleName }).ToList();

            ViewBag.CompanyList = companies;
            ViewBag.BranchList = branches;
            ViewBag.DepartmentList = depts;
            ViewBag.EmployeeList = emps;
            ViewBag.ShiftTypeList = shiftTypes;
            ViewBag.OvertimeRuleList = otRules;

            ViewBag.ShiftTypeDD = new SelectList(shiftTypes, "id", "name");
            ViewBag.OvertimeRuleDD = new SelectList(otRules, "id", "name");

            model.PageUrl = Url.Action(nameof(Index));
            return View(model);
        }
        #endregion

        #region Post Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Setup([FromBody] HrmAtdShiftSetupViewModel modelVM)
        {
            try
            {
                if (modelVM == null)
                {
                    return Json(new { isSuccess = false, message = "Invalid request data." });
                }

                if (await hrmAtdShiftService.IsExistAsync(modelVM.ShiftName, modelVM.ShiftCode))
                {
                    return Json(new { isSuccess = false, message = "Already Exists!", isDuplicate = true });
                }

                if (string.IsNullOrEmpty(modelVM.ShiftCode))
                {
                    modelVM.ShiftCode = await hrmAtdShiftService.GenerateNextCode();
                }

                modelVM.ToAudit(LoginInfo, modelVM.AutoId > 0);
                if (modelVM.AutoId == 0)
                {
                    var hasSavePermission = await hrmAtdShiftService.SavePermissionAsync(LoginInfo.AccessCode);
                    if (hasSavePermission)
                    {
                        var saved = await hrmAtdShiftService.SaveAsync(modelVM);
                        if (!saved)
                        {
                            return Json(new { isSuccess = false, message = "Failed to save shift." });
                        }
                        return Json(new { isSuccess = true, message = "Saved Successfully", lastCode = modelVM.ShiftCode });
                    }
                    else
                    {
                        return Json(new { isSuccess = false, message = "You have no access to save", noSavePermission = true });
                    }
                }
                else
                {
                    var hasUpdatePermission = await hrmAtdShiftService.UpdatePermissionAsync(LoginInfo.AccessCode);
                    if (hasUpdatePermission)
                    {
                        var updated = await hrmAtdShiftService.UpdateAsync(modelVM);
                        if (!updated)
                        {
                            return Json(new { isSuccess = false, message = "Failed to update shift." });
                        }
                        return Json(new { isSuccess = true, message = "Updated Successfully", lastCode = modelVM.ShiftCode });
                    }
                    else
                    {
                        return Json(new { isSuccess = false, message = "You have no access to update", noUpdatePermission = true });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDetail([FromBody] HrmAtdShiftDetailViewModel detailVM)
        {
            try
            {
                if (detailVM == null || string.IsNullOrEmpty(detailVM.ShiftCode))
                {
                    return Json(new { isSuccess = false, message = "Invalid detail data." });
                }

                bool hasPerm = detailVM.AutoId == 0
                    ? await hrmAtdShiftService.SavePermissionAsync(LoginInfo.AccessCode)
                    : await hrmAtdShiftService.UpdatePermissionAsync(LoginInfo.AccessCode);

                if (!hasPerm)
                {
                    return Json(new { isSuccess = false, message = "You have no permission for this action." });
                }

                detailVM.ToAudit(LoginInfo, detailVM.AutoId > 0);
                var ok = await hrmAtdShiftService.SaveDetailAsync(detailVM);
                return Json(new
                {
                    isSuccess = ok,
                    message = ok ? (detailVM.AutoId == 0 ? "Shift detail added successfully" : "Shift detail updated successfully") : "Failed to save shift detail"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { isSuccess = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDetail([FromBody] decimal detailAutoId)
        {
            try
            {
                var hasPermission = await hrmAtdShiftService.DeletePermissionAsync(LoginInfo.AccessCode);
                if (!hasPermission)
                {
                    return Json(new { isSuccess = false, message = "You have no access to delete." });
                }

                var ok = await hrmAtdShiftService.DeleteDetailAsync(detailAutoId);
                return Json(new { isSuccess = ok, message = ok ? "Shift detail deleted successfully" : "Detail record not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { isSuccess = false, message = ex.Message });
            }
        }
        #endregion

        #region Delete
        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] List<string> ids)
        {
            try
            {
                var hasPermission = await hrmAtdShiftService.DeletePermissionAsync(LoginInfo.AccessCode);
                if (hasPermission)
                {
                    foreach (var id in ids)
                    {
                        var result = hrmAtdShiftService.DeleteLeaveType(id);
                    }
                    return Json(new { isSuccess = true, message = "Data Deleted Successfully" });
                }
                else
                {
                    return Json(new { isSuccess = false, message = "You have no access" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting type: {ex.Message}");
                return StatusCode(500, new { isSuccess = false, message = ex.Message });
            }
        }
        #endregion

        #region NeaxtCode
        [HttpGet]
        public async Task<IActionResult> GenerateNextCode()
        {
            var nextCode = await hrmAtdShiftService.GenerateNextCode();
            return Json(nextCode);
        }
        #endregion

        #region CheckAvailability
        [HttpPost]
        public async Task<JsonResult> CheckAvailability(string name, string code)
        {
            if (await hrmAtdShiftService.IsExistAsync(name, code))
            {
                return Json(new { isSuccess = true, message = "Already Exists!" });
            }
            return Json(new { isSuccess = false });
        }
        #endregion

        #region TableLoading
        [HttpGet]
        public async Task<IActionResult> GetTableData(string searchText, string shiftType, string companyId, string branchId, string departmentId, string status)
        {
            try
            {
                var list = await hrmAtdShiftService.GetGridDataAsync(searchText, shiftType, companyId, branchId, departmentId, status);
                return PartialView("_Grid", list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetShifts(string searchText, string shiftType, string companyId, string branchId, string departmentId, string status)
        {
            try
            {
                var list = await hrmAtdShiftService.GetGridDataAsync(searchText, shiftType, companyId, branchId, departmentId, status);

                var result = list.Select(s =>
                {
                    var detailsList = (s.Details ?? new List<HrmAtdShiftDetailViewModel>()).Select(d => new
                    {
                        autoId = d.AutoId,
                        shiftCode = d.ShiftCode,
                        start = d.ShiftStartTime != DateTime.MinValue ? d.ShiftStartTime.ToString("hh:mm:ss tt") : "09:00:00 AM",
                        end = d.ShiftEndTime != DateTime.MinValue ? d.ShiftEndTime.ToString("hh:mm:ss tt") : "06:00:00 PM",
                        late = d.LateTime != DateTime.MinValue ? d.LateTime.ToString("hh:mm:ss tt") : "09:15:00 AM",
                        absent = d.AbsentTime != DateTime.MinValue ? d.AbsentTime.ToString("hh:mm:ss tt") : "11:00:00 AM",
                        bf = d.LunchOutTime != DateTime.MinValue ? d.LunchOutTime.ToString("hh:mm:ss tt") : "01:00:00 PM",
                        bt = d.LunchInTime != DateTime.MinValue ? d.LunchInTime.ToString("hh:mm:ss tt") : "02:00:00 PM",
                        breakHour = d.LunchBreakHour,
                        early = !string.IsNullOrEmpty(d.EarlyLeaveTime) ? d.EarlyLeaveTime : "05:30:00 PM",
                        cross = d.IsCrossMidnight,
                        company = d.CompanyIdsList ?? new List<string>(),
                        branch = d.BranchIdsList ?? new List<string>(),
                        dept = d.DepartmentIdsList ?? new List<string>(),
                        emp = d.EmployeeIdsList ?? new List<string>(),
                        otRule = !string.IsNullOrEmpty(d.OvertimeRuleName) ? d.OvertimeRuleName : (!string.IsNullOrEmpty(d.OvertimeRuleID) ? d.OvertimeRuleID : "Not Applicable"),
                        otRuleId = d.OvertimeRuleID ?? "",
                        otAfter = d.OTGraceTime > 0 ? d.OTGraceTime : 60,
                        otMax = d.MaxOTPerDay > 0 ? d.MaxOTPerDay : 4m,
                        otMin = d.MinOTMinutes > 0 ? d.MinOTMinutes : 50,
                        woOT = !string.IsNullOrEmpty(d.WeeklyOffOTRule) ? d.WeeklyOffOTRule : "Not Applicable",
                        hOT = !string.IsNullOrEmpty(d.HolidayOTRule) ? d.HolidayOTRule : "Not Applicable",
                        eff = d.Wef != DateTime.MinValue ? d.Wef.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd"),
                        status = !string.IsNullOrEmpty(d.IsActiveStatus) ? d.IsActiveStatus : (d.IsActive ? "Active" : "Inactive"),
                        rem = d.Remarks ?? ""
                    }).ToList();

                    var firstD = detailsList.FirstOrDefault();

                    return new
                    {
                        autoId = s.AutoId,
                        id = s.ShiftCode,
                        name = s.ShiftName,
                        type = !string.IsNullOrEmpty(s.ShiftTypeName) ? s.ShiftTypeName : (s.ShiftTypeId == "02" || s.ShiftTypeId == "2" ? "Night" : "Day"),
                        typeId = s.ShiftTypeId,
                        desc = s.Description ?? s.ShiftShortName ?? "",
                        start = firstD != null ? firstD.start : (s.ShiftStartTime != DateTime.MinValue ? s.ShiftStartTime.ToString("hh:mm:ss tt") : "09:00:00 AM"),
                        end = firstD != null ? firstD.end : (s.ShiftEndTime != DateTime.MinValue ? s.ShiftEndTime.ToString("hh:mm:ss tt") : "06:00:00 PM"),
                        late = firstD != null ? firstD.late : (s.LateTime != DateTime.MinValue ? s.LateTime.ToString("hh:mm:ss tt") : "09:15:00 AM"),
                        absent = firstD != null ? firstD.absent : (s.AbsentTime != DateTime.MinValue ? s.AbsentTime.ToString("hh:mm:ss tt") : "11:00:00 AM"),
                        bf = firstD != null ? firstD.bf : (s.LunchOutTime != DateTime.MinValue ? s.LunchOutTime.ToString("hh:mm:ss tt") : "01:00:00 PM"),
                        bt = firstD != null ? firstD.bt : (s.LunchInTime != DateTime.MinValue ? s.LunchInTime.ToString("hh:mm:ss tt") : "02:00:00 PM"),
                        breakHour = firstD != null ? firstD.breakHour : s.LunchBreakHour,
                        early = firstD != null ? firstD.early : (!string.IsNullOrEmpty(s.EarlyLeaveTime) ? s.EarlyLeaveTime : "05:30:00 PM"),
                        cross = firstD != null ? firstD.cross : s.IsCrossMidnight,
                        company = detailsList.SelectMany(x => x.company).Distinct().ToList(),
                        branch = detailsList.SelectMany(x => x.branch).Distinct().ToList(),
                        dept = detailsList.SelectMany(x => x.dept).Distinct().ToList(),
                        emp = detailsList.SelectMany(x => x.emp).Distinct().ToList(),
                        otRule = firstD != null ? firstD.otRule : (!string.IsNullOrEmpty(s.OvertimeRuleName) ? s.OvertimeRuleName : "Not Applicable"),
                        otRuleId = firstD != null ? firstD.otRuleId : s.OvertimeRuleID,
                        eff = firstD != null ? firstD.eff : (s.Wef != DateTime.MinValue ? s.Wef.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd")),
                        status = !string.IsNullOrEmpty(s.IsActiveStatus) ? s.IsActiveStatus : (s.IsActive ? "Active" : "Inactive"),
                        rem = s.Remarks ?? "",
                        details = detailsList
                    };
                });
                return Json(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardCount()
        {
            try
            {
                var count = await hrmAtdShiftService.GetDashboardCountAsync();
                return Json(count);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        #endregion

        #region Pdf
        public async Task<IActionResult> ExportToExcel()
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ShiftInformations");

                worksheet.Cell(1, 1).Value = "Shift Information";
                worksheet.Range(1, 1, 1, 9).Merge();
                worksheet.Row(1).Style.Font.Bold = true;
                worksheet.Row(1).Style.Font.FontSize = 14;
                worksheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Row(1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Row(1).Height = 20;
                worksheet.Row(1).Style.Font.FontColor = XLColor.Black;

                worksheet.Range(2, 1, 2, 9).Merge();

                int dataStartRow = 3;

                worksheet.Cell(dataStartRow, 1).Value = "Shift Id";
                worksheet.Cell(dataStartRow, 2).Value = "Shift Name";
                worksheet.Cell(dataStartRow, 3).Value = "Description";
                worksheet.Cell(dataStartRow, 4).Value = "In Time";
                worksheet.Cell(dataStartRow, 5).Value = "Out Time";
                worksheet.Cell(dataStartRow, 6).Value = "Late Time";
                worksheet.Cell(dataStartRow, 7).Value = "Absent Time";
                worksheet.Cell(dataStartRow, 8).Value = "Effective Date";
                worksheet.Cell(dataStartRow, 9).Value = "Shift Type";
                worksheet.Row(dataStartRow).Style.Font.Bold = true;
                worksheet.Row(dataStartRow).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Row(dataStartRow).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                var designations = await hrmAtdShiftService.GetAllAsync();
                int row = dataStartRow + 1;
                foreach (var designation in designations)
                {
                    worksheet.Cell(row, 1).Value = "'" + designation.ShiftCode.PadLeft(2, '0');
                    worksheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    worksheet.Cell(row, 2).Value = designation.ShiftName;
                    worksheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    worksheet.Cell(row, 3).Value = designation.Description;
                    worksheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    worksheet.Cell(row, 4).Value = designation.ShiftStartTime.ToString("hh:mm ss tt");
                    worksheet.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    worksheet.Cell(row, 5).Value = designation.ShiftEndTime.ToString("hh:mm ss tt");
                    worksheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    worksheet.Cell(row, 6).Value = designation.LateTime.ToString("hh:mm ss tt");
                    worksheet.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    worksheet.Cell(row, 7).Value = designation.AbsentTime.ToString("hh:mm ss tt");
                    worksheet.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    worksheet.Cell(row, 8).Value = designation.Wef.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    worksheet.Cell(row, 9).Value = designation.ShiftTypeId;
                    worksheet.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                "ShiftInformations.xlsx");
                }
            }
        }
        #endregion
    }
}
