
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

        public HRMATDShiftsController(
            IHrmAtdShiftService hrmAtdShiftService, 
            IRepository<HrmDefShiftType> shiftService, 
            ICommonService commonService, 
            IRepository<HrmDefOvertimeRule> overtimeRuleService,
            IRepository<CoreCompany> companyRepo,
            IRepository<CoreBranch> branchRepo,
            IRepository<HrmDefDepartment> deptRepo,
            IRepository<HrmEmployee> empRepo)
        {
            this.hrmAtdShiftService = hrmAtdShiftService;
            this.commonService = commonService;
            this.shiftService = shiftService;
            this.overtimeRuleService = overtimeRuleService;
            this.companyRepo = companyRepo;
            this.branchRepo = branchRepo;
            this.deptRepo = deptRepo;
            this.empRepo = empRepo;
        }



        #region GetByallId
        public async Task<IActionResult> Index(string? id)
        {
            var hasPermission = await hrmAtdShiftService.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission)
            {
                return RedirectToAction("Login", "Accounts");
            }
            //Get all
            HrmAtdShiftPageViewModel model = new HrmAtdShiftPageViewModel();
            var list = await hrmAtdShiftService.GetAllAsync();
            model.HrmAttendenceShiftList = list ?? new List<HrmAtdShiftSetupViewModel>();
            //Get By Id
            if (!string.IsNullOrEmpty(id))
            {

                model.Setup = await hrmAtdShiftService.GetByIdAsync(id);
            }

            //
            //var shiftTypes = new List<SelectListItem>
            //{
            //     new SelectListItem { Value = "Day shift", Text = "Day shift" },
            //     new SelectListItem { Value = "Night shift", Text = "Night shift" }
            //};

            //// Assign the shift types to ViewBag
            //ViewBag.ShiftTypeDD = new SelectList(shiftTypes, "Value", "Text");

            var companies = companyRepo.All().Select(x => new { id = x.CompanyCode, name = x.CompanyName }).ToList();
            var branches = branchRepo.All().Select(x => new { id = x.BranchCode, name = x.BranchName }).ToList();
            var depts = deptRepo.All().Select(x => new { id = x.DepartmentCode, name = x.DepartmentName }).ToList();
            var emps = empRepo.All().Take(500).Select(x => new { id = x.EmployeeId, name = x.EmployeeId + " - " + ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim() }).ToList();
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


        public async Task<IActionResult> Setup(HrmAtdShiftSetupViewModel modelVM)
        {
            try
            {

                if (await hrmAtdShiftService.IsExistAsync(modelVM.ShiftName, modelVM.ShiftCode))
                {
                    return Json(new { isSuccess = false, message = $"Already Exists!", isDuplicate = true });
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
                        await hrmAtdShiftService.SaveAsync(modelVM);
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
                        await hrmAtdShiftService.UpdateAsync(modelVM);
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

                Console.WriteLine($"Error deleting  type: {ex.Message}");

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

                return Json(new { isSuccess = true, message = $"Already  Exists!" });

            }

            return Json(new { isSuccess = false });
        }
        #endregion

        #region TabeleLodaing

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
                
                var result = list.Select(s => new {
                    autoId = s.AutoId,
                    id = s.ShiftCode,
                    name = s.ShiftName,
                    type = !string.IsNullOrEmpty(s.ShiftTypeName) ? s.ShiftTypeName : (s.ShiftTypeId == "02" || s.ShiftTypeId == "2" ? "Night" : "Day"),
                    typeId = s.ShiftTypeId,
                    desc = s.Description ?? s.ShiftShortName ?? "",
                    start = s.ShiftStartTime != DateTime.MinValue ? s.ShiftStartTime.ToString("hh:mm:ss tt") : "09:00:00 AM",
                    end = s.ShiftEndTime != DateTime.MinValue ? s.ShiftEndTime.ToString("hh:mm:ss tt") : "06:00:00 PM",
                    late = s.LateTime != DateTime.MinValue ? s.LateTime.ToString("hh:mm:ss tt") : "09:15:00 AM",
                    absent = s.AbsentTime != DateTime.MinValue ? s.AbsentTime.ToString("hh:mm:ss tt") : "11:00:00 AM",
                    bf = s.LunchOutTime != DateTime.MinValue ? s.LunchOutTime.ToString("hh:mm:ss tt") : "01:00:00 PM",
                    bt = s.LunchInTime != DateTime.MinValue ? s.LunchInTime.ToString("hh:mm:ss tt") : "02:00:00 PM",
                    breakHour = s.LunchBreakHour,
                    early = !string.IsNullOrEmpty(s.EarlyLeaveTime) ? s.EarlyLeaveTime : "05:30:00 PM",
                    cross = s.IsCrossMidnight,
                    company = !string.IsNullOrEmpty(s.CompanyIds) ? s.CompanyIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>(),
                    branch = !string.IsNullOrEmpty(s.BranchIds) ? s.BranchIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>(),
                    dept = !string.IsNullOrEmpty(s.DepartmentIds) ? s.DepartmentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>(),
                    emp = !string.IsNullOrEmpty(s.EmployeeIds) ? s.EmployeeIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>(),
                    otRule = !string.IsNullOrEmpty(s.OvertimeRuleName) ? s.OvertimeRuleName : (!string.IsNullOrEmpty(s.OvertimeRuleID) ? s.OvertimeRuleID : "Not Applicable"),
                    otRuleId = s.OvertimeRuleID,
                    otMax = 4,
                    eff = s.Wef != DateTime.MinValue ? s.Wef.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd"),
                    status = !string.IsNullOrEmpty(s.IsActiveStatus) ? s.IsActiveStatus : (s.IsActive ? "Active" : "Inactive"),
                    rem = s.Remarks ?? ""
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

                // Add title

                worksheet.Cell(1, 1).Value = "Shift Information";
                worksheet.Range(1, 1, 1, 9).Merge(); // Merge cells across the header columns
                worksheet.Row(1).Style.Font.Bold = true;
                worksheet.Row(1).Style.Font.FontSize = 14;
                worksheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Row(1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Row(1).Height = 20; // Adjust row height for visibility
                worksheet.Row(1).Style.Font.FontColor = XLColor.Black; // Ensure font is visible


                //
                // Leave an empty row
                worksheet.Range(2, 1, 2, 9).Merge();

                int dataStartRow = 3;

                // Add headers
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



                // Add data
                var designations = await hrmAtdShiftService.GetAllAsync();
                int row = dataStartRow + 1;
                foreach (var designation in designations)
                {
                    // worksheet.Cell(row, 1).Value = designation.DepartmentCode;
                    //worksheet.Cell(row, 1).Value = designation.DepartmentCode.PadLeft(2, '0');
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
                    worksheet.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; //center

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                // Save to a stream
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


