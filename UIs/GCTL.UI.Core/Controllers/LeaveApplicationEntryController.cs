using AspNetCore.Reporting;
using DocumentFormat.OpenXml.InkML;
using GCTL.Core;
using GCTL.Core.Data;
using GCTL.Core.DataTables;
using GCTL.Core.Helpers;
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.Accounts;
using GCTL.Core.ViewModels.DeleteHistories;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;
using GCTL.Service;
using GCTL.Service.Branches;
using GCTL.Service.Companies;
using GCTL.Service.DeleteHistories;
using GCTL.Service.Employees;
using GCTL.Service.FileHandle;
using GCTL.Service.HrmEmployeeOfficialInfoServe;
using GCTL.Service.HrmEmployees2;
using GCTL.Service.HrmLeaveApplicationEntrys;
using GCTL.Service.LeaveAppDay;
using GCTL.Service.LeaveTypes;
using GCTL.UI.Core.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Org.BouncyCastle.Ocsp;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GCTL.UI.Core.Controllers
{
    public class LeaveApplicationEntryController : BaseController
    {

        #region Ctor

        private readonly GCTL_ERP_DB_DatapathContext _context;
        private readonly ICompanyService _companyService;
        private readonly IEmployeeService _employeeService;
        private readonly IHrmEmployee2Service _hrmEmployeeService2;
        private readonly IHrmEmployeeOfficialInfoService _hrmEmployeeOfficialService;
        private readonly ILeaveTypeService _leaveTypeService;
        private readonly IHrmLeaveApplicationEntry _leaveApplicatioinEntry;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IFileHandle _fileHandle;
        private readonly ILeaveApplicationDay _leaveAppDay;
        private readonly ICoreBranch _coreBranch;
        private readonly IRepository<CoreCompany> _companyRepository;
        //private readonly UserManager<CoreUserInfo> _manager;

        private readonly IEmployeeService hrmEmployee;

        public LeaveApplicationEntryController(GCTL_ERP_DB_DatapathContext context, ICompanyService companyService,
            IEmployeeService employeeService, IHrmEmployee2Service hrmEmployeeService2,
            IHrmEmployeeOfficialInfoService hrmEmployeeOfficialService, ILeaveTypeService leaveTypeService,
            IHrmLeaveApplicationEntry leaveApplicatioinEntry, IWebHostEnvironment webHostEnvironment,
            IFileHandle fileHandle, ILeaveApplicationDay leaveAppDay, ICoreBranch coreBranch, IEmployeeService hrmEmployee, IRepository<CoreCompany> companyRepository)
        {
            _context = context;
            _companyService = companyService;
            _employeeService = employeeService;
            _hrmEmployeeService2 = hrmEmployeeService2;
            _hrmEmployeeOfficialService = hrmEmployeeOfficialService;
            _leaveTypeService = leaveTypeService;
            _leaveApplicatioinEntry = leaveApplicatioinEntry;
            _webHostEnvironment = webHostEnvironment;
            _fileHandle = fileHandle;
            _leaveAppDay = leaveAppDay;
            _coreBranch = coreBranch;
            this.hrmEmployee = hrmEmployee;
            _companyRepository = companyRepository;
        }

        #endregion

        bool isBypass = true;
        string leaveEntryCreate = "Leave Application Entry";
        string leaveEntryLeaveApply = "Leave Application Entry";
        string leaveEntryReport = "Leave Application Entry";
        string newSkin = "Leave Application Entry";
        string indexSkin = "Leave Application Entry";


        #region NewApproval Screen


        public IActionResult NewApproval(string leaveId = null, string empId = null, int ApvStage = 0)
        {
            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));

            var hasPermission = _leaveApplicatioinEntry.PagePermissionUniversal(LoginInfo.AccessCode, newSkin).Result;
            if (!hasPermission && !isBypass)
            {
                return PartialView("_NoAccessView", "You have no access.");
            }

            if (userInfo.EmployeeId == empId)
            {
                var isValidRequest = _leaveApplicatioinEntry.ValidRequest(leaveId, ApvStage).Result;
                if (isValidRequest)
                {
                    ViewBag.PreSelectedLeaveId = leaveId;
                    return View();
                }
            }
            if (leaveId != null)
            {
                ViewBag.PreSelectedLeaveId = "NotValid";

            }
            return View();
        }

        public async Task<IActionResult> SubmitLeaveApproval([FromBody] LeaveApprovalViewModel model)
        {


            try
            {
                if (model.ApprovalStatus == "--Select Approval Status--")
                {
                    return Json(new { success = false, message = "Please fill the form." });
                }

                model.ToAudit(LoginInfo);

                var hasPermission = await _leaveApplicatioinEntry.UpdatePermissionUniversal(LoginInfo.AccessCode, newSkin);
                if (!hasPermission)
                {
                    return Json(new { success = false, message = "You have no access." });
                }

                var result = await _leaveApplicatioinEntry.SaveLeaveApproval(model);
                if (result)
                {
                    // var a = await 

                    var mailRes = await _leaveApplicatioinEntry.NotifyMailOnApprove(model);



                    return Json(new { success = true, data = result });
                }
                return Json(new { success = false, Message = "rejected" });

            }
            catch (Exception ex)
            {
                // Log the exception (if a logging mechanism is in place)
                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> SubmitSelectedLeaves([FromBody] LeaveRequest leaveIds)
        {


            if (leaveIds == null)
            {
                //return BadRequest("No leave IDs provided.");
                //return  Ok(new { message = "Please Select Again" });
                return Json(new { success = false, message = "Please Select Again" });

            }
            leaveIds.ToAudit(LoginInfo);

            var hasPermission = await _leaveApplicatioinEntry.UpdatePermissionUniversal(LoginInfo.AccessCode, newSkin);
            if (!hasPermission)
            {
                return Json(new { success = false, message = "You have no access." });
            }

            // Process the submitted leave IDs
            var result = await _leaveApplicatioinEntry.SaveBulkApproval(leaveIds);

            var notifyBulk = await _leaveApplicatioinEntry.NotifyBulk(leaveIds, "Approved");

            return Json(new { success = true, message = "Leave applications processed successfully." });

            //return Ok(new { message = "Leave applications processed successfully." });
        }


        [HttpPost]
        public async Task<IActionResult> SubmitRejectedLeaves([FromBody] LeaveRequest leaveIds)
        {


            if (leaveIds == null)
            {
                //return BadRequest("No leave IDs provided.");
                //return  Ok(new { message = "Please Select Again" });
                return Json(new { success = false, message = "Please Select Again" });

            }
            leaveIds.ToAudit(LoginInfo);


            var hasPermission = await _leaveApplicatioinEntry.UpdatePermissionUniversal(LoginInfo.AccessCode, newSkin);
            if (!hasPermission)
            {
                return Json(new { success = false, message = "You have no access." });
            }


            // Process the submitted leave IDs
            var result = await _leaveApplicatioinEntry.SaveBulkReject(leaveIds);

            var mailNotify = await _leaveApplicatioinEntry.NotifyBulk(leaveIds, "Rejected");

            return Json(new { success = true, message = "Leave applications processed successfully." });
        }

        public IActionResult HrApproval()
        {

            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));


            return View();
        }

        public async Task<IActionResult> GetLeavePendingForHR()
        {
            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));
            var empId = userInfo.EmployeeId;

            //TODO: 27-01-025 Here match with Hr Id
            //var id = "08060104001";
            try
            {
                var application = await _leaveApplicatioinEntry.GetPendingLeaveForHR();
                // var application = await _leaveApplicatioinEntry.GetByIdAsync(id);
                return Json(new { success = true, data = application });
            }
            catch (Exception)
            {

                throw;
            }

        }


        [HttpGet]
        public async Task<IActionResult> GetApprovedLeaveByEmpID(string empId)
        {
            try
            {
                var leaveApplications = await _leaveApplicatioinEntry.GetApprovedLeaveByEmpID(empId, "Approved");
                return Ok(leaveApplications); // Return data as JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", details = ex.Message });
            }
        }

        #endregion

        #region leave apply page e Leave apply Button

        [HttpPost]
        public async Task<IActionResult> SubmitIndividualLeaveApplication(LeaveApplicationViewModel? model)
        {
            //var request = HttpContext.Request; 
            //var response = HttpContext.Response;

            //// Build the base URL
            //var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";


            // return Json(new { isSuccess = false, data = model });





            var validChk = await _leaveApplicatioinEntry.LeaveValidation(model);

            if (!validChk.Success)
            {
                return Json(new { isSuccess = false, message = validChk.Message });
            }

            if (model.CompanyCode == null || model.EmployeeId == null || model.ImmediateSupervisor == null || model.HeadOfDepartment == null || model.LeaveFormat == null || model.LeaveType == null)
            {
                return Json(new { isSuccess = false, message = "Fill the form " });
            }


            model.ToAudit(LoginInfo, model.LeaveAppEntryCode > 0);
            if (model.LeaveAppEntryCode == null)
            {

                var hasSavePermission = await _leaveApplicatioinEntry.SavePermissionUniversal(LoginInfo.AccessCode, leaveEntryLeaveApply);
                if (hasSavePermission || isBypass)
                {
                    if (model.SelectedDates != null)
                    {
                        var chk = _leaveApplicatioinEntry.CheckDuplicate(model);
                        if (chk)
                        {
                            return Json(new { isSuccess = false, message = "Already Exists" });

                        }
                    }

                    if (model.LeaveFile != null)
                    {
                        var a = FileHandle(model);
                        model.LeaveFileLink = a.Result;
                    }

                    var data =  await _leaveApplicatioinEntry.SaveAsync(model, false);
                    if (data)
                    {
                        // Assume LeaveFormat is bound from the form (selectedItem)
                            string? rawFormat = model.LeaveFormat;

                            string normalizedFormat = rawFormat switch
                            {
                                "halfDayLeave" => "Half Day Leave",
                                "fullDayLeave" => "Full Leave",
                                "shortLeave" => "Short Leave",
                                _ => string.Empty
                            };

                            // Save back into the property if you want the normalized text
                            model.LeaveFormat = normalizedFormat;

                        

                        var mail =  await _leaveApplicatioinEntry.NotifyMailOnApply(model);

                        if (mail)
                        {
                            return Json(new { isSuccess = true, message = "Saved Successfully ", redirectUrl = Url.Action("Create", "LeaveApplicationEntry"), lastCode = model.EntryId });
                        }
                        else
                        {
                            return Json(new { isSuccess = true, message = "Saved Successfully but not notify", redirectUrl = Url.Action("Create", "LeaveApplicationEntry"), lastCode = model.EntryId });
                        }

                        
                    }
                    else
                    {
                        return Json(new { isSuccess = false, message = "Saved failed", redirectUrl = Url.Action("Create", "LeaveApplicationEntry"), lastCode = model.EntryId });
                    }

                }
                else
                {
                    //TODO: validation
                    // var check = await _leaveApplicatioinEntry.GetLeaveEntriesByEmpCodeAsync(model.EmployeeId);




                    // await _leaveApplicatioinEntry.UpdateAsync(model);
                    //TempData["Data"] = "This is the data I want to share.";
                    //return RedirectToAction("Index", "HrmEmployee2");
                    return Json(new { isSuccess = false, message = "You have no access to save", noSavePermission = true });


                }
            }
            else
            {

                var hasUpdatePermission = await _leaveApplicatioinEntry.UpdatePermissionUniversal(LoginInfo.AccessCode, leaveEntryLeaveApply);
                if (hasUpdatePermission)
                {
                    if (model.LeaveFile != null)
                    {
                        var a = FileHandle(model);
                        model.LeaveFileLink = a.Result;
                    }
                    await _leaveApplicatioinEntry.UpdateAsync(model);
                    return Json(new { isSuccess = true, message = "Updated Successfully", redirectUrl = Url.Action("Create", "LeaveApplicationEntry"), lastCode = model.EntryId });
                }
                else
                {
                    return Json(new { isSuccess = false, message = "You have no access to update", noUpdatePermission = true });
                }
            }
        }

        #endregion

        public async Task<IActionResult> LeaveApply()
        {

            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));
            var EmpId = userInfo.EmployeeId;


            var hasPermission = _leaveApplicatioinEntry.PagePermissionUniversal(LoginInfo.AccessCode, leaveEntryReport).Result;
            if (!hasPermission && !isBypass)
            {
                return PartialView("_NoAccessView", "You have no access.");
            }

            //TODO: LoginId
            //var temp = "16012510004";
            var employees = await _hrmEmployeeOfficialService.GetEmployeesByEmpId(EmpId);


            var entryId = await _leaveApplicatioinEntry.GenerateLeaveApplicationEntryID();
            ViewBag.entryId = entryId;


            return View(employees);
        }

        [HttpGet]
        public IActionResult GetLeaveTypes()
        {
            var leaveTypes = _context.HrmAtdLeaveType
                .Select(l => new { l.LeaveTypeCode, l.Name })
                .ToList();

            return Json(leaveTypes);
        }



        #region Approval






        // In LeaveApplicationEntryController.cs
        public IActionResult Approval(string leaveId = null, string empId = null, int ApvStage = 0)
        {
            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));

            if (userInfo.EmployeeId == empId)
            {
                var isValidRequest = _leaveApplicatioinEntry.ValidRequest(leaveId, ApvStage).Result;
                if (isValidRequest)
                {
                    ViewBag.PreSelectedLeaveId = leaveId;
                    return View(new BaseViewModel());
                }
            }
            if (leaveId != null)
            {
                ViewBag.PreSelectedLeaveId = "NotValid";

            }
            return View(new BaseViewModel());
        }







        public async Task<IActionResult> GetLeavePendingByIsID()
        {
            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));
            var empId = userInfo.EmployeeId;

            //TODO: LoginId
            //var id = "08060104001";
            try
            {
                var application = await _leaveApplicatioinEntry.GetPendingLeaveByHodIs(empId);
                // var application = await _leaveApplicatioinEntry.GetByIdAsync(id);
                return Json(new { success = true, data = application });
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<IActionResult> GetLeaveTypeByLeaveTypeIdEmpId(string LeaveTypeId, string EmpId)
        {
            try
            {
                // Get the list of employees based on the company code

                var employees = await _leaveApplicatioinEntry.LeaveBalanceByLeaveIdEmpId(LeaveTypeId, EmpId);

                // var employees = _context.HrmEmployeeOfficialInfo.Where(e => e.CompanyCode == compCode).ToList();

                // Return the result as JSON
                return Json(new { success = true, data = employees });
            }
            catch (Exception ex)
            {
                // Log the exception (if a logging mechanism is in place)

                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }




        [HttpPost]
        public async Task<IActionResult> GetApprovedLeaveApplications(DataTableParams? parameters)
        {
            try
            {
                var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));
                var empId = userInfo.EmployeeId;
                //var id = "08060104001";
                var id = empId;


                var allData = await _leaveApplicatioinEntry.GetApprovedAPAAsync(id, parameters);


                // Apply sorting
                if (parameters.Order != null && parameters.Order.Any())
                {
                    var orderColumn = parameters.Columns[parameters.Order[0].Column].Data;
                    var orderDir = parameters.Order[0].Dir;

                    switch (orderColumn)
                    {
                        case "lDate":
                            allData = orderDir == "desc" ?
                                allData.OrderByDescending(x => x.Ldate).ToList() :
                                allData.OrderBy(x => x.Ldate).ToList();
                            break;
                        case "employeeID":
                            allData = orderDir == "desc" ?
                                allData.OrderByDescending(x => x.EmployeeID).ToList() :
                                allData.OrderBy(x => x.EmployeeID).ToList();
                            break;

                        case "leaveAppEntryId":
                            allData = orderDir == "desc" ?
                                allData.OrderByDescending(x => x.LeaveAppEntryId).ToList() :
                                allData.OrderBy(x => x.LeaveAppEntryId).ToList();
                            break;
                            // Add other columns as needed
                    }
                }


                // Total records before filtering
                int totalRecords = allData.Select(e => e.CountTotal).FirstOrDefault();

                // Total records after filtering
                int filteredRecords = allData.Count();

                return Ok(new
                {
                    draw = parameters.Draw,
                    recordsTotal = filteredRecords,
                    recordsFiltered = totalRecords,
                    data = allData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", details = ex.Message });
            }
        }






        #endregion

        #region Report
        public IActionResult Report()
        {
            try
            {

                var hasPermission = _leaveApplicatioinEntry.PagePermissionUniversal(LoginInfo.AccessCode, leaveEntryReport).Result;
                if (!hasPermission && !isBypass)
                {
                    return PartialView("_NoAccessView", "You have no access.");
                }

                //var entryId = _leaveApplicatioinEntry.GenerateLeaveApplicationEntryID();
                //ViewBag.entryId = entryId;


                //var company = _companyService.GetCompanies();

                //var company = _companyRepository.GetAll();

                //var company = _context.CoreCompany.ToList();

                //ViewBag.Company = company;

                //var leaveType = _leaveTypeService.GetLeaveTypesAsync();
                //var leaveType = _context.HrmAtdLeaveType.ToList();
                //ViewBag.leaveType = leaveType;
            }
            catch (Exception ex)
            {

                throw;
            }



            return View();
        }


        [HttpPost]
        //Not Using
        public IActionResult PreviewReport(LeaveReportViewModel model)
        {
            try
            {
                var data = _leaveApplicatioinEntry.GetReport(model);


                // Group by DepartmentName
                var groupedResult = data.Result
                                    .GroupBy(app => app.DepartmentName)
                                    .ToDictionary(
                                        g => g.Key, // DepartmentName as the key
                                        g => g.ToList() // List of leave applications as the value
                                    );


                return Json(new
                {
                    success = true,
                    message = "Preview is here",
                    data = groupedResult
                });
            }
            catch (Exception ex)
            {

                throw;
            }

        }


        // Add this new action method to your controller
        public IActionResult PreviewLeaveReport(LeaveReportViewModel? model)
        {
            if (model.Company == null)
            {
                return Json(new
                {
                    success = true,
                    message = "please fill company Name",

                });
            }

            var companyName = _context.CoreCompany
                .Where(e => e.CompanyCode == model.Company)
                .Select(app => app.CompanyName)
                .FirstOrDefault();

            var Sdate = model.DateFrom.ToString("dd-MM-yyyy");
            var Edate = model.DateTo.ToString("dd-MM-yyyy");

            if (model.DateFrom == DateTime.MinValue)
                Sdate = null;
            if (model.DateTo == DateTime.MinValue)
                Edate = null;

            var data = _leaveApplicatioinEntry.GetReport(model);
            var groupedResult = data.Result
                .GroupBy(app => app.DepartmentName)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            var list = new List<LeaveReportGridVM>();
            foreach (var kvp in groupedResult)
            {
                var leaveReport = new LeaveReportGridVM
                {
                    Department = kvp.Key,
                    Data = kvp.Value
                };
                list.Add(leaveReport);
            }

            var pdfBytes = _leaveApplicatioinEntry.GenerateLeavePdfReport(list, companyName, Sdate, Edate);
            var base64String = Convert.ToBase64String(pdfBytes);

            return Json(new { pdfData = base64String });
        }



        public IActionResult PreviewLeaveReportArray(LeaveReportArrayViewModel? model)
        {

            try
            {

                var hasPermission = _leaveApplicatioinEntry.PrintPermissionUniversal(LoginInfo.AccessCode, leaveEntryReport).Result;
                if (!hasPermission && !isBypass)
                {
                    return PartialView("_NoAccessView", "You have no access. See Priview");
                }

                if (model == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "please fill company Name",

                    });
                }

                if (model.Company == null || model == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "please fill company Name",

                    });
                }
                model.DateTo = DateTime.ParseExact(model.DateToStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                model.DateFrom = DateTime.ParseExact(model.DateFromStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

                var data = _leaveApplicatioinEntry.GetReportArray(model);

                var res = data.Result;

                return Json(new
                {
                    success = true,
                    message = "Data Get",
                    data = data

                });
            }
            catch (Exception)
            {

                throw;
            }


        }



        [HttpPost]
        public IActionResult DownloadReportN(LeaveReportArrayViewModel? model)
        {
            try
            {
                var hasPermission = _leaveApplicatioinEntry.PrintPermissionUniversal(LoginInfo.AccessCode, leaveEntryReport).Result;
                if (!hasPermission && !isBypass)
                {
                    return PartialView("_NoAccessView", "You have no access. See Download");
                }


                if (model?.Company == null || model?.ReportFormat == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Please fill in Company Name and select a Report Format."
                    });
                }

                model.DateTo = DateTime.ParseExact(model.DateToStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                model.DateFrom = DateTime.ParseExact(model.DateFromStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

                string Sdate = model.DateFrom != DateTime.MinValue ? model.DateFrom.ToString("dd-MM-yyyy") : null;
                string Edate = model.DateTo != DateTime.MinValue ? model.DateTo.ToString("dd-MM-yyyy") : null;

                var data = _leaveApplicatioinEntry.GetReportArray(model);

                var res = data.Result;

                // Generate PDF Report
                if (model.ReportFormat == "pdf")
                {
                    var pdfBytes = _leaveApplicatioinEntry.GenerateLeavePdfReportArray(data, Sdate, Edate);
                    string fileName = $"LeaveReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                    return File(pdfBytes, "application/pdf", fileName);
                }

                // Generate Excel Report
                if (model.ReportFormat == "excel")
                {
                    var fileContents = _leaveApplicatioinEntry.GenerateLeaveExcelReportArray(data, Sdate, Edate);
                    string fileName = $"LeaveReport_{DateTime.Now:yyyyMMdd}.xlsx";

                    return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }

                return Ok(new
                {
                    success = false,
                    message = "Invalid report format. Only PDF and Excel are supported."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while generating the report.",
                    error = ex.Message
                });
            }
        }








        #endregion

        #region Create
        public IActionResult Index()
        {
            var userInfo = HttpContext.Session.Get<UserInfoViewModel>(nameof(ApplicationConstants.LoginSessionKey));
            var hasPermission = _leaveApplicatioinEntry.PagePermissionUniversal(LoginInfo.AccessCode, indexSkin).Result;
            if (!hasPermission && !isBypass)
            {
                return PartialView("_NoAccessView", "You have no access.");
            }            
            return View();
        }


        [HttpGet]
        public IActionResult GetBranches(string companyCode)
        {
            try
            {
                var result = _leaveApplicatioinEntry.GetBranchesByCompCode(companyCode);
                return Json(new { result });
            }
            catch (Exception)
            {

                throw;
            }

        }





        #region Multi DD

        [HttpGet]
        public async Task<IActionResult> GetBranchesMultiComp([FromQuery] string[] companyCode, bool isAll)
        {
            try
            {
                var branches = await _leaveApplicatioinEntry.GetBranchesByMultiCompCode(companyCode.ToList(), isAll);
                return Json(new { result = branches });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetDeptMultiCompBranch([FromQuery] string[] companyCode, string[] branchCode, bool isAll)
        {
            try
            {
                var Dept = await _leaveApplicatioinEntry.GetDeptByMultiCompBranch(companyCode.ToList(), branchCode.ToList(), isAll);
                return Json(new { result = Dept });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmpMultiCompBranchDept([FromQuery] string[] companyCode, string[] branchCode, string[] departmentCode, bool isAll)
        {
            try
            {
                var Emp = await _leaveApplicatioinEntry.GetEmpMultiCompBranchDept(companyCode.ToList(), branchCode.ToList(), departmentCode.ToList(), isAll);
                return Json(new { result = Emp });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetDesigMultiCompBranchDept([FromQuery] string[] companyCode, string[] branchCode, string[] departmentCode, bool isAll)
        {
            try
            {
                var Emp = await _leaveApplicatioinEntry.GetDesigMultiCompBranchDept(companyCode.ToList(), branchCode.ToList(), departmentCode.ToList(), isAll);
                return Json(new { result = Emp });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        #endregion






        public async Task<IActionResult> DeleteLeaveApplication(string id)
        {
            string PageTittle = "Leave Application Entry";
            try
            {
                var application = await _leaveApplicatioinEntry.GetLeaveEntriesAsync(id);
                // var application = await _leaveApplicatioinEntry.GetByIdAsync(id);

                if (application != null)
                {

                    if (application.Days != null)
                    {

                        await _leaveAppDay.DeleteAsync(application.LeaveAppEntryId);

                    }

                    var hasPermission = await _leaveApplicatioinEntry.DeletePermissionUniversal(LoginInfo.AccessCode, PageTittle);
                    if (!hasPermission)
                    {
                        return Json(new { success = false, message = "You have no access." });
                    }

                    DeleteHistoryViewModel dm = new DeleteHistoryViewModel();
                    dm.ToAudit(LoginInfo);

                    var result = await _leaveApplicatioinEntry.DeleteLeaveApplication(id, dm);

                    return Json(new { success = true, data = " Deleted" });

                }
                return Json(new { success = false, data = "Not Deleted" });
            }
            catch (Exception)
            {

                throw;
            }

        }


        public async Task<IActionResult> DownloadLeaveApplication(string url)
        {

            try
            {

                var request = HttpContext.Request;
                //   var response = HttpContext.Response;

                // Build the base URL
                var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}/";




                var reqUrl = baseUrl + url;
                return Json(new { success = true, downloadUrl = reqUrl });


            }
            catch (Exception)
            {

                throw;
            }

        }



        [HttpPost]
        public async Task<IActionResult> UpdateLeaveApplication([FromForm] LeaveApplicationViewModel model)
        {
            var result = await _leaveApplicatioinEntry.GetAllAsync();
            return Json(new { success = true });
        }


        #region AllGet


        [HttpGet]
        public async Task<IActionResult> GetCompanies()
        {
            try
            {
                var result = await _companyRepository.GetAllAsync();
                return Json(new { result });
            }
            catch (Exception)
            {

                throw;
            }

        }

        [HttpGet]
        public IActionResult GetDeptByCompCode(string compCode)
        {
            try
            {
                var result = _leaveApplicatioinEntry.GetDepartmentByCompCode(compCode);
                return Json(new { result });
            }
            catch (Exception)
            {

                throw;
            }
        }

        [HttpGet]
        public IActionResult GetEmpByCompDeptBranch(string compCode, string dept, string branchCode)
        {
            try
            {
                var result = _hrmEmployeeOfficialService.GetEmployeesByCompDept(compCode, dept, branchCode);
                return Json(new { result });
            }
            catch (Exception)
            {

                throw;
            }

        }


        public async Task<IActionResult> GetLeaveIDByEntryId(string id)
        {

            try
            {
                if (id == "NotValid")
                {
                    return Json(new { success = true, data = id });
                }

                var application = await _leaveApplicatioinEntry.GetLeaveEntriesByEntryId(id);
                // var application = await _leaveApplicatioinEntry.GetByIdAsync(id);
                return Json(new { success = true, data = application });
            }
            catch (Exception)
            {

                throw;
            }

        }
        public async Task<IActionResult> GetLeaveApplication1(string id)
        {

            try
            {
                var application = await _leaveApplicatioinEntry.GetLeaveEntriesAsync(id);
                // var application = await _leaveApplicatioinEntry.GetByIdAsync(id);
                return Json(new { success = true, data = application });
            }
            catch (Exception)
            {

                throw;
            }

        }




        [HttpGet]
        public IActionResult GenerateEntryId()
        {
            var entryId = _leaveApplicatioinEntry.GenerateLeaveApplicationEntryID();
            return Json(new { entryId });
        }



        public async Task<IActionResult> GetEmployeeByCode(string compCode)
        {
            try
            {
                //TODO: CHECK 
                // Get the list of employees based on the company code
                var employees = await _leaveApplicatioinEntry.GetByCompCodeAsync(compCode);


                // Return the result as JSON
                return Json(new { success = true, data = employees });
            }
            catch (Exception ex)
            {
                // Log the exception (if a logging mechanism is in place)

                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }


        public IActionResult GetEmployeeOfficialByEmpId(string EmpId)
        {
            try
            {
                // Get the list of employees based on the company code

                var employees = _hrmEmployeeOfficialService.GetEmployeesByEmpId(EmpId);

                // var employees = _context.HrmEmployeeOfficialInfo.Where(e => e.CompanyCode == compCode).ToList();

                // Return the result as JSON
                return Json(new { success = true, data = employees });
            }
            catch (Exception ex)
            {
                // Log the exception (if a logging mechanism is in place)

                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }


        public async Task<IActionResult> GetEmployeeLeaveBalance(string EmpId)
        {
            try
            {
                // Get the list of employees based on the company code

                var employees = await _leaveApplicatioinEntry.LeaveBalanceByEmpId(EmpId);

                // var employees = _context.HrmEmployeeOfficialInfo.Where(e => e.CompanyCode == compCode).ToList();

                // Return the result as JSON
                return Json(new { success = true, data = employees });
            }
            catch (Exception ex)
            {
                // Log the exception (if a logging mechanism is in place)

                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> GetEmployeeLeaveStatusList(string EmpId)
        {
            try
            {
                // Get the list of employees based on the company code

                var employees = await _leaveApplicatioinEntry.LeaveStatusByEmpId(EmpId, "");



                // Return the result as JSON
                return Json(new { success = true, data = employees });
            }
            catch (Exception ex)
            {


                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }
        public IActionResult GetEmployeeLeaveTaken(string EmpId)
        {
            try
            {
                // Get the list of employees based on the company code

                var employees = _leaveApplicatioinEntry.LeaveTakenByEmpId(EmpId);



                // Return the result as JSON
                return Json(new { success = true, data = employees });
            }
            catch (Exception ex)
            {


                // Return an error response as JSON
                return Json(new { success = false, message = ex.Message });
            }
        }


        #endregion


        [HttpGet]
        public async Task<IActionResult> GetEmployeeByCodePaged(string compCode, string search = "", int page = 1, int pageSize = 20)
        {
            try
            {
                var result = await _leaveApplicatioinEntry.GetByCompCodePagedAsync(compCode, search, page, pageSize);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Create

        public async Task<IActionResult> Create()
        {
            try
            {

                var hasPermission = await _leaveApplicatioinEntry.PagePermissionUniversal(LoginInfo.AccessCode, leaveEntryCreate);
                if (!hasPermission && !isBypass)
                {
                    return PartialView("_NoAccessView", "You have no access.");
                }

                var entryId = await _leaveApplicatioinEntry.GenerateLeaveApplicationEntryID();
                ViewBag.entryId = entryId;


                var company = await _leaveApplicatioinEntry.GetCompanieListAsync();

                ViewBag.Company = company;

                //var leaveType = _leaveTypeService.GetLeaveTypesAsync();
                var leaveType = _context.HrmAtdLeaveType.ToList();
                ViewBag.leaveType = leaveType;

                ViewBag.EmployeeDD = new SelectList(hrmEmployee.GetEmployeeDropSelections(), "Code", "Name");
            }
            catch (Exception ex)
            {
                return View(new LeaveApplicationViewModel());

                // throw;
            }



            return View(new LeaveApplicationViewModel());
        }


        [HttpPost]
        public async Task<IActionResult> SubmitLeaveApplication(LeaveApplicationViewModel? model)
        {




            var validChk = await _leaveApplicatioinEntry.LeaveValidation(model);

            if (!validChk.Success)
            {
                return Json(new { isSuccess = false, message = validChk.Message });
            }

            if (model.CompanyCode == null || model.EmployeeId == null || model.ImmediateSupervisor == null || model.HeadOfDepartment == null || model.LeaveFormat == null || model.LeaveType == null)
            {
                return Json(new { isSuccess = false, message = "Fill the form " });
            }


            model.ToAudit(LoginInfo, model.LeaveAppEntryCode > 0);
            if (model.LeaveAppEntryCode == null)
            {

                var hasSavePermission = await _leaveApplicatioinEntry.SavePermissionUniversal(LoginInfo.AccessCode, leaveEntryCreate);
                if (hasSavePermission || isBypass)
                {
                    if (model.SelectedDates != null)
                    {
                        var chk = _leaveApplicatioinEntry.CheckDuplicate(model);
                        if (chk)
                        {
                            return Json(new { isSuccess = false, message = "Already Exists" });

                        }
                    }



                    if (model.LeaveFile != null)
                    {
                        var a = FileHandle(model);
                        model.LeaveFileLink = a.Result;
                    }
                    else
                    {
                        model.LeaveFileLink = "";
                    }



                    await _leaveApplicatioinEntry.SaveAsync(model, true);



                    return Json(new { isSuccess = true, message = "Saved Successfully", redirectUrl = Url.Action("Create", "LeaveApplicationEntry"), lastCode = model.EntryId });

                }
                else
                {
                    //TODO: validation
                    // var check = await _leaveApplicatioinEntry.GetLeaveEntriesByEmpCodeAsync(model.EmployeeId);




                    // await _leaveApplicatioinEntry.UpdateAsync(model);
                    //TempData["Data"] = "This is the data I want to share.";
                    //return RedirectToAction("Index", "HrmEmployee2");
                    return Json(new { isSuccess = false, message = "You have no access to save", noSavePermission = true });


                }
            }
            else
            {

                var hasUpdatePermission = await _leaveApplicatioinEntry.UpdatePermissionUniversal(LoginInfo.AccessCode, leaveEntryCreate);
                if (hasUpdatePermission)
                {
                    if (model.LeaveFile != null)
                    {
                        var a = FileHandle(model);
                        model.LeaveFileLink = a.Result;
                    }
                    var data =  await _leaveApplicatioinEntry.UpdateAsync(model);
                    return Json(new { isSuccess = true, message = "Updated Successfully", redirectUrl = Url.Action("Create", "LeaveApplicationEntry"), lastCode = model.EntryId, data = data.Data });
                }
                else
                {
                    return Json(new { isSuccess = false, message = "You have no access to update", noUpdatePermission = true });
                }
            }



        }

        #endregion

        public async Task<string> FileHandle(LeaveApplicationViewModel? model)
        {


            if (model.LeaveFile != null)
            {
                try
                {
                    var fileName = model.EntryId + "_" + model.EmployeeId;
                    var root = _webHostEnvironment.WebRootPath;
                    string a = await _fileHandle.SaveFileAsync(model.LeaveFile, root, "leaveApplication", fileName);
                    return a;
                }
                catch (Exception ex)
                {

                    throw;
                }


            }
            return null;

        }



        [HttpGet]
        public async Task<IActionResult> Grid(int page = 1, int pageSize = 10, string searchValue = "", string sortColumn = "", string sortDir = "desc" , string empId = "")
        {
            try
            {
                var result = await _leaveApplicatioinEntry.GetPagedAsync(page, pageSize, searchValue, sortColumn, sortDir, empId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", details = ex.Message });
            }
        }





        #endregion

        #region Delete History

        

        #endregion

    }
}
