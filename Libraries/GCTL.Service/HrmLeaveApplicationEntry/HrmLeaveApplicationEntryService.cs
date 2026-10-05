#region using

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GCTL.Core.Data;
using GCTL.Core.DataTables;
using GCTL.Core.Helpers;
using GCTL.Core.ViewModels.Common;
using GCTL.Core.ViewModels.Companies;
using GCTL.Core.ViewModels.DeleteHistories;
using GCTL.Core.ViewModels.HrmEmployees2;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;
using GCTL.Service.DeleteHistories;
using GCTL.Service.EmailService;
using GCTL.Service.FileHandle;
using GCTL.Service.LeaveAppDay;
using iText.Commons.Actions.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static System.Net.Mime.MediaTypeNames;
using static iText.Signatures.LtvVerification;
using Word = DocumentFormat.OpenXml.Wordprocessing;

#endregion

namespace GCTL.Service.HrmLeaveApplicationEntrys
{
    public class HrmLeaveApplicationEntryService : AppService<HrmLeaveApplicationEntry>, IHrmLeaveApplicationEntry
    {

        #region Const


        private readonly IRepository<CoreAccessCode> accessCodeRepository;
        private readonly GCTL_ERP_DB_DatapathContext _context;
        private readonly IRepository<HrmLeaveApplicationEntry> _leaveRepository;
        private readonly IRepository<HrmLeaveApplicationDays> _leaveDaysRepository;
        private readonly ILeaveApplicationDay _leaveDaysService;
        private readonly IFileHandle _fileHandle;

        private readonly IRepository<HrmEmployee> empRepository;
        private readonly IRepository<HrmEmployeeOfficialInfo> empOfficalRepository;

        private readonly IRepository<CoreCompany> coreCompanyRepository;

        private readonly IEmailService _emailService;
        private readonly ILeaveEmailService _leaveEmailService;
        private readonly IDeleteHistoryService _deleteHistoryService;

        private readonly IRepository<CoreBranch> _branchRepository;
        private readonly IRepository<HrmDefDepartment> _departmentRepository;
        private readonly IRepository<HrmDefDesignation> _designationRepository;
        private readonly IRepository<HrmLeaveApplicationDays> _leaveDayRepository;


        private readonly IConfiguration _configuration;

        public HrmLeaveApplicationEntryService(
       IRepository<CoreAccessCode> accessCodeRepository,
       GCTL_ERP_DB_DatapathContext context,
       IRepository<HrmLeaveApplicationEntry> leaveRepository,
       IRepository<HrmLeaveApplicationDays> leaveDaysRepository,
       ILeaveApplicationDay leaveDaysService,
       IFileHandle fileHandle,
       IRepository<HrmEmployee> empRepository,

       IRepository<CoreCompany> coreCompanyRepository,
       IEmailService emailService,
       ILeaveEmailService leaveEmailService,
       IRepository<HrmEmployeeOfficialInfo> empOfficalRepository,
       IRepository<CoreBranch> branchRepository,
       IRepository<HrmDefDepartment> departmentRepository,
       IConfiguration configuration,
       IRepository<HrmDefDesignation> designationRepository,
       IRepository<HrmLeaveApplicationDays> leaveDayRepository,
       IDeleteHistoryService deleteHistoryService)
       : base(leaveRepository) // Pass the dependency to the base class constructor
        {
            this.accessCodeRepository = accessCodeRepository;
            _context = context;
            _leaveRepository = leaveRepository;
            _leaveDaysRepository = leaveDaysRepository;
            _leaveDaysService = leaveDaysService;
            _fileHandle = fileHandle;
            this.empRepository = empRepository;
            this.coreCompanyRepository = coreCompanyRepository;
            _emailService = emailService;
            _leaveEmailService = leaveEmailService;
            this.empOfficalRepository = empOfficalRepository;
            _branchRepository = branchRepository;
            _departmentRepository = departmentRepository;
            _configuration = configuration;
            _designationRepository = designationRepository;
            _leaveDayRepository = leaveDayRepository;
            _deleteHistoryService = deleteHistoryService;
        }


        #endregion

        #region EmpPaging

        public async Task<object> GetByCompCodePagedAsync(string compCode, string search, int page, int pageSize = 20)
        {
            try
            {
                var query = empRepository.All().AsNoTracking()
                    .Join(coreCompanyRepository.All().AsNoTracking(),
                          emp => emp.CompanyCode,
                          company => company.CompanyCode,
                          (emp, company) => new { emp, company })
                    .Where(x => x.emp.CompanyCode == compCode);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        x.emp.EmployeeId.Contains(search) ||
                        x.emp.FirstName.Contains(search) ||
                        x.emp.LastName.Contains(search));
                }

                var totalCount = await query.CountAsync();

                var data = await query
                    .OrderBy(x => x.emp.EmployeeId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new HrmEmployee2SetUpViewModel
                    {
                        AutoId = x.emp.AutoId,
                        EmployeeId = x.emp.EmployeeId,
                        FirstName = x.emp.FirstName,
                        LastName = x.emp.LastName,
                        CompanyCode = x.company.CompanyCode,
                        Company = x.company.CompanyName,
                    })
                    .ToListAsync();

                return new
                {
                    results = data,
                    totalCount = totalCount,
                    hasMore = (page * pageSize) < totalCount
                };
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region mail
        public async Task<bool> NotifyMailOnApply(LeaveApplicationViewModel model)
        {
            try
            {
                var emp = empRepository.All().Where(e => e.EmployeeId == model.EmployeeId).FirstOrDefault();
                var empOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == model.EmployeeId);
                var supOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == model.ImmediateSupervisor);

                model.EmployeeName = emp.FirstName + " " + emp.LastName;

                var EmpBody = _leaveEmailService.GenerateLeaveRequestEmail(model);

                var res = _emailService.SendEmailAsync(empOfc.Email, "Application for leave ", EmpBody);

                //var hrEmp = empRepository.All().Where(e => e.EmployeeId == model.ImmediateSupervisor).FirstOrDefault();

                var leaveBalDetails = await LeaveBalanceByLeaveIdEmpId(model.LeaveType, model.EmployeeId);


                var HrBody = await _leaveEmailService.GenerateNotifyHrEmail(model, leaveBalDetails, true, supOfc);

                var res1 = await _emailService.SendEmailAsync(supOfc.Email, $"Application for leave({model.EmployeeName})", HrBody);

                if (res1)
                {
                    return true;
                }
                else
                {
                    return false;
                }

                
            }
            catch (Exception)
            {

                return false;
            }
            
        }

        public Task<bool> ValidRequest(string leaveId, int apvStage)
        {
            var leave = _leaveRepository.All().FirstOrDefault(e => e.LeaveAppEntryId == leaveId);
            if (leave == null)
            {
                return Task.FromResult(false); // If leave is null, return false
            }
            if (leave.IsApproved == "Approved" && leave.HodapprovalStatus == "Approved" && leave.HrapprovalStatus == "Approved")
            {
                return Task.FromResult(false); // Return false if any approval is already done
            }
            if (leave.IsApproved == "Approved" && apvStage == 1)
            {
                return Task.FromResult(false); // Return false if leave is approved and stage is 1
            }
            else if (leave.HodapprovalStatus == "Approved" && apvStage == 2)
            {
                return Task.FromResult(false); // Return false if HOD approval is done and stage is 2
            }
            else if (leave.HrapprovalStatus == "Approved" && apvStage == 3)
            {
                return Task.FromResult(false); // Return false if HR approval is done and stage is 3
            }
            return Task.FromResult(true); // If none of the conditions are met, return true
        }

        public async Task<bool> NotifyMailOnApprove(LeaveApprovalViewModel model)
        {


            var leave = await GetLeaveEntriesByEntryId(model.LeaveAppEntryId);

            var emp = empRepository.All().Where(e => e.EmployeeId == leave.EmployeeId).FirstOrDefault();
            var empOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == leave.EmployeeId);
            var HodOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == empOfc.Hod);
            var HrId = GetHrIDasync().Result;
            var HrOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == HrId);
            var SupOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == empOfc.ReportingTo);


            var EmployeeName = emp.FirstName + " " + emp.LastName;

            if (leave == null) return await Task.FromResult(false);

            if (model.ApprovalStatus == "approved")
            {
                var leaveBalDetails = await LeaveBalanceByLeaveIdEmpId(leave.LeaveTypeId, leave.EmployeeId);

                if (leave.IsApproved == "Approved" && leave.HODApprovalStatus == "")
                {
                    //var HodOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == empOfc.Hod);
                    var HrBody = await _leaveEmailService.GenerateSecondNotifyHrEmail(leave, leaveBalDetails, true, HodOfc);

                    var res1 = _emailService.SendEmailAsync(HodOfc.Email, $"Review for leave({EmployeeName})", HrBody);
                }
                else if (leave.HODApprovalStatus == "Approved" && leave.HRApprovalStatus == "")
                {
                    //var HrId = GetHrIDasync().Result;
                    //var HrOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == HrId);
                    var HrBody = await _leaveEmailService.GenerateSecondNotifyHrEmail(leave, leaveBalDetails, false, HrOfc);

                    var res1 = _emailService.SendEmailAsync(HrOfc.Email, $"Review for leave({EmployeeName})", HrBody);
                }
                else if (leave.HRApprovalStatus == "Approved")
                {
                    var resEmp = _leaveEmailService.GenerateLeaveApprovalEmail(leave);
                    var res1 = _emailService.SendEmailAsync(empOfc.Email, $"Leave Application Approved emp1", resEmp);


                    var resSup = _leaveEmailService.GenerateLeaveApprovalOfcNotify(leave, SupOfc);
                    var res2 = _emailService.SendEmailAsync(SupOfc.Email, $"Leave Application Approved sup2 {emp.FirstName + " " + emp.LastName}({emp.EmployeeId})", resSup);


                    var resHod = _leaveEmailService.GenerateLeaveApprovalOfcNotify(leave, HodOfc);
                    var res3 = _emailService.SendEmailAsync(HodOfc.Email, $"Leave Application Approved hod3 {emp.FirstName + " " + emp.LastName}({emp.EmployeeId})", resHod);

                }
            }
            else if (model.ApprovalStatus == "rejected")
            {
                ////TODO: 27-01-25 RejectMail

                //var RejBody = await _leaveEmailService.GenerateLeaveRejectionEmail(leave , EmployeeName, model.ConfirmationRemark);
                //var RejMail = _emailService.SendEmailAsync("shefain3@gmail.com", $"Leave Request cancelled({EmployeeName})", RejBody);


                if (leave.IsApproved == "Rejected" && leave.HODApprovalStatus == "")
                {
                    //var HodOfc = empOfficalRepository.All().FirstOrDefault(e => e.EmployeeId == empOfc.Hod);
                    var empBody = _leaveEmailService.GenerateLeaveCancelEmail(leave, leave.BossEmpAutoId);

                    var res1 = _emailService.SendEmailAsync(empOfc.Email, $"Leave Application Cancel", empBody);
                }
                else if (leave.HODApprovalStatus == "Rejected" && leave.HRApprovalStatus == "")
                {
                    var empBody = _leaveEmailService.GenerateLeaveCancelEmail(leave, leave.HOD);

                    var res1 = _emailService.SendEmailAsync(empOfc.Email, $"Leave Application Cancel", empBody);

                    var supNotify = _leaveEmailService.GenerateLeaveCancelOfcNotify(leave, HodOfc);

                    var res2 = _emailService.SendEmailAsync(empOfc.Email, $"Leave Application Cancel{emp.FirstName + " " + emp.LastName}({emp.EmployeeId})", supNotify);
                }
                else if (leave.HRApprovalStatus == "Rejected")
                {
                    var empBody = _leaveEmailService.GenerateLeaveCancelEmail(leave, HrId);

                    var res1 = _emailService.SendEmailAsync(empOfc.Email, $"Leave Application Cancel", empBody);

                    var supNotify = _leaveEmailService.GenerateLeaveCancelOfcNotify(leave, HrOfc);

                    var res2 = _emailService.SendEmailAsync(SupOfc.Email, $"Leave Application Cancel{emp.FirstName + " " + emp.LastName}({emp.EmployeeId})", supNotify);

                    var hodNotify = _leaveEmailService.GenerateLeaveCancelOfcNotify(leave, HrOfc);

                    var res3 = _emailService.SendEmailAsync(HodOfc.Email, $"Leave Application Cancel{emp.FirstName + " " + emp.LastName}({emp.EmployeeId})", supNotify);
                }

            }
            return true;
        }

        public Task<bool> NotifyMailAsync(LeaveApplicationViewModel model)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region save bulk approval and reject
        public async Task<bool> SaveBulkReject(LeaveRequest leaveIds)
        {


            if (leaveIds == null)
            {
                return false;
            }
            foreach (var item in leaveIds.LeaveIds)
            {
                await _leaveRepository.BeginTransactionAsync();
                var entryResult = await _leaveRepository.FindByAsync(e => e.LeaveAppEntryId == item);
                var entry = await entryResult.FirstOrDefaultAsync();

                if (entry.IsApproved == "")
                {
                    entry.IsApproved = "Rejected";
                    entry.ConfirmationRemarks = leaveIds.Remarks;
                    if (entry.BossEmpAutoId == entry.Hod)
                    {
                        entry.HodapprovalStatus = "Rejected";
                        entry.HodapprovalRemarks = leaveIds.Remarks;

                    }
                }
                else if (entry.HodapprovalStatus == "")
                {
                    entry.HodapprovalStatus = "Rejected";
                    entry.HodapprovalRemarks = leaveIds.Remarks;

                }
                else if (entry.HrapprovalStatus == "")
                {
                    entry.HrapprovalStatus = "Rejected";
                    entry.HrapprovalRemarks = leaveIds.Remarks;

                }
                else
                {
                    await _leaveRepository.RollbackTransactionAsync();
                    return false;
                }

                entry.Ldate = leaveIds.Ldate;
                //entry.LeaveAppEntryCode = model.LeaveAppEntryCode;
                entry.Lip = leaveIds.Lip;
                entry.Lmac = leaveIds.Lmac;
                entry.Luser = leaveIds.Luser;

                entry.ModifyDate = leaveIds.ModifyDate;

                await _leaveRepository.UpdateAsync(entry);

                await _leaveRepository.CommitTransactionAsync();

            }
            return true;
        }

        public async Task<bool> SaveBulkApproval(LeaveRequest leaveIds)
        {


            if (leaveIds == null)
            {
                return false;
            }
            foreach (var item in leaveIds.LeaveIds)
            {
                await _leaveRepository.BeginTransactionAsync();
                var entryResult = await _leaveRepository.FindByAsync(e => e.LeaveAppEntryId == item);
                var entry = await entryResult.FirstOrDefaultAsync();

                if (entry.IsApproved == "")
                {
                    entry.IsApproved = "Approved";
                    entry.ConfirmationRemarks = leaveIds.Remarks;
                    if (entry.BossEmpAutoId == entry.Hod)
                    {
                        entry.HodapprovalStatus = "Approved";
                        entry.HodapprovalRemarks = leaveIds.Remarks;
                    }
                }
                else if (entry.HodapprovalStatus == "")
                {
                    entry.HodapprovalStatus = "Approved";
                    entry.HodapprovalRemarks = leaveIds.Remarks;
                }
                else if (entry.HrapprovalStatus == "")
                {
                    entry.HrapprovalStatus = "Approved";
                    entry.HrapprovalRemarks = leaveIds.Remarks;
                }
                else
                {
                    await _leaveRepository.RollbackTransactionAsync();
                    return false;
                }

                entry.Ldate = leaveIds.Ldate;
                //entry.LeaveAppEntryCode = model.LeaveAppEntryCode;
                entry.Lip = leaveIds.Lip;
                entry.Lmac = leaveIds.Lmac;
                entry.Luser = leaveIds.Luser;

                entry.ModifyDate = leaveIds.ModifyDate;

                await _leaveRepository.UpdateAsync(entry);

                await _leaveRepository.CommitTransactionAsync();

            }
            return true;
        }
        #endregion

        #region save single approval and reject
        public async Task<bool> SaveLeaveApproval(LeaveApprovalViewModel model)
        {
            await _leaveRepository.BeginTransactionAsync();
            var HrId = GetHrIDasync().Result;
            try
            {
                var entryResult = await _leaveRepository.FindByAsync(e => e.LeaveAppEntryId == model.LeaveAppEntryId);
                var entry = await entryResult.FirstOrDefaultAsync();
                if (entry == null)
                {
                    await _leaveRepository.RollbackTransactionAsync();
                    return false;
                }


                if (entry.IsApproved == "Rejected" || entry.HodapprovalStatus == "Rejected" || entry.HrapprovalStatus == "Rejected")
                {
                    await _leaveRepository.RollbackTransactionAsync();
                    return false;
                }

                if (model.ApprovalStatus == "approved")
                {
                    if (entry.IsApproved == "")
                    {
                        entry.IsApproved = "Approved";
                        entry.ConfirmationRemarks = model.ConfirmationRemark;
                        if (entry.BossEmpAutoId == entry.Hod)
                        {
                            entry.HodapprovalStatus = "Approved";
                            entry.HodapprovalRemarks = model.ConfirmationRemark;
                        }
                    }
                    else if (entry.HodapprovalStatus == "")
                    {
                        entry.HodapprovalStatus = "Approved";
                        entry.HodapprovalRemarks = model.ConfirmationRemark;
                        if (entry.Hod == HrId)
                        {
                            entry.HrapprovalStatus = "Approved";
                            entry.HrapprovalRemarks = model.ConfirmationRemark;
                        }
                    }
                    else if (entry.HrapprovalStatus == "")
                    {
                        entry.HrapprovalStatus = "Approved";
                        entry.HrapprovalRemarks = model.ConfirmationRemark;
                    }
                    else
                    {
                        await _leaveRepository.RollbackTransactionAsync();
                        return false;
                    }
                }

                if (model.ApprovalStatus == "rejected")
                {
                    if (entry.IsApproved == "")
                    {
                        entry.IsApproved = "Rejected";
                        entry.ConfirmationRemarks = model.ConfirmationRemark;
                        if (entry.BossEmpAutoId == entry.Hod)
                        {
                            entry.HodapprovalStatus = "Rejected";
                            entry.HodapprovalRemarks = model.ConfirmationRemark;
                        }
                    }
                    else if (entry.HodapprovalStatus == "")
                    {
                        entry.HodapprovalStatus = "Rejected";
                        entry.HodapprovalRemarks = model.ConfirmationRemark;
                    }
                    else if (entry.HrapprovalStatus == "")
                    {
                        entry.HrapprovalStatus = "Rejected";
                        entry.HrapprovalRemarks = model.ConfirmationRemark;
                    }
                    else
                    {
                        await _leaveRepository.RollbackTransactionAsync();
                        return false;
                    }
                }






                entry.Ldate = model.Ldate;
                //entry.LeaveAppEntryCode = model.LeaveAppEntryCode;
                entry.Lip = model.Lip;
                entry.Lmac = model.Lmac;
                entry.Luser = model.Luser;

                entry.ModifyDate = model.ModifyDate;



                //entry.LeaveApplyProcess = "_done";

                await _leaveRepository.UpdateAsync(entry);

                await _leaveRepository.CommitTransactionAsync();
                return true;


            }
            catch (Exception)
            {

                throw;
            }

        }
        #endregion

        #region Get Pending Leave
        public async Task<List<LeavePendingViewModel>> GetPendingLeaveForHR()
        {
            try
            {
                //TODO: 27-01-25 Here hr Pending add
                // Fetch main data from the database
                var leaveEntries = await (
                    from l in _context.HrmLeaveApplicationEntry
                    where l.IsApproved == "Approved" && l.HodapprovalStatus == "Approved" && l.HrapprovalStatus == ""
                    join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId
                    join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode
                    join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId
                    join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode
                    select new
                    {
                        l,
                        e,
                        type,
                        DesignationName = desig.DesignationName
                    }
                )
                //.Concat(
                //    from l in _context.HrmLeaveApplicationEntry
                //    where l.Hod == Id && l.IsApproved == "Approved" && l.HodapprovalStatus == ""
                //    join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId
                //    join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode
                //    join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId
                //    join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode
                //    select new
                //    {
                //        l,
                //        e,
                //        type,
                //        DesignationName = desig.DesignationName
                //    }
                //)
                .ToListAsync();




                // Combine data in memory
                var result = leaveEntries.Select(data => new LeavePendingViewModel
                {
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    LeaveTypeId = data.type.ShortName,
                    EmployeeID = data.e.EmployeeId,
                    EmployeeFirstName = data.e.FirstName,
                    DesignationName = data.DesignationName,
                    LeaveAppEntryCode = data.l.AutoId,

                }).ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<string> GetHrIDasync()
        {
            return await Task.FromResult("08060104001");
        }

        public async Task<List<LeavePendingViewModel>> GetPendingLeaveByHodIs(string Id)
        {
            try
            {

                var HrId = await GetHrIDasync();

                var leaveEntries = await (
                    from l in _context.HrmLeaveApplicationEntry
                    where l.BossEmpAutoId == Id && l.IsApproved == ""
                    join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into empJoin
                    from e in empJoin.DefaultIfEmpty()
                    join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                    from type in typeJoin.DefaultIfEmpty()
                    join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                    from oe in oeJoin.DefaultIfEmpty()
                    join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                    from desig in desigJoin.DefaultIfEmpty()
                    join imSup in _context.HrmEmployee on l.BossEmpAutoId equals imSup.EmployeeId into imSupJoin
                    from imSup in imSupJoin.DefaultIfEmpty()
                    join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                    from hod in hodJoin.DefaultIfEmpty()
                    orderby l.AutoId descending
                    select new
                    {
                        l,
                        e,
                        type,
                        DesignationName = desig.DesignationName ?? "N/A",
                        ImSupName = imSup != null ? (imSup.FirstName + " " + imSup.LastName) : "N/A",
                        HodName = hod != null ? (hod.FirstName + " " + hod.LastName) : "N/A"
                    }

                )
                .Concat(
                    from l in _context.HrmLeaveApplicationEntry
                    where l.Hod == Id && l.IsApproved == "Approved" && l.HodapprovalStatus == ""
                    join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into empJoin
                    from e in empJoin.DefaultIfEmpty()
                    join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                    from type in typeJoin.DefaultIfEmpty()
                    join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                    from oe in oeJoin.DefaultIfEmpty()
                    join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                    from desig in desigJoin.DefaultIfEmpty()
                    join imSup in _context.HrmEmployee on l.BossEmpAutoId equals imSup.EmployeeId into imSupJoin
                    from imSup in imSupJoin.DefaultIfEmpty()
                    join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                    from hod in hodJoin.DefaultIfEmpty()

                    orderby l.AutoId descending
                    select new
                    {
                        l,
                        e,
                        type,
                        DesignationName = desig.DesignationName ?? "N/A",
                        ImSupName = imSup != null ? (imSup.FirstName + " " + imSup.LastName) : "N/A",
                        HodName = hod != null ? (hod.FirstName + " " + hod.LastName) : "N/A"
                    }

                )
                .Concat(
                    from l in _context.HrmLeaveApplicationEntry
                    where Id == HrId && l.IsApproved == "Approved" && l.HodapprovalStatus == "Approved" && l.HrapprovalStatus == ""
                    join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into empJoin
                    from e in empJoin.DefaultIfEmpty()
                    join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                    from type in typeJoin.DefaultIfEmpty()
                    join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                    from oe in oeJoin.DefaultIfEmpty()
                    join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                    from desig in desigJoin.DefaultIfEmpty()
                    join imSup in _context.HrmEmployee on l.BossEmpAutoId equals imSup.EmployeeId into imSupJoin
                    from imSup in imSupJoin.DefaultIfEmpty()
                    join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                    from hod in hodJoin.DefaultIfEmpty()

                    orderby l.AutoId descending

                    select new
                    {
                        l,
                        e,
                        type,
                        DesignationName = desig.DesignationName ?? "N/A",
                        ImSupName = imSup != null ? (imSup.FirstName + " " + imSup.LastName) : "N/A",
                        HodName = hod != null ? (hod.FirstName + " " + hod.LastName) : "N/A"
                    }

                )
                .ToListAsync();


                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync();

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList()
                                       );


                // Combine data in memory
                //var result = leaveEntries.Select(data => new LeavePendingViewModel
                //{
                //    LeaveAppEntryId = data.l.LeaveAppEntryId,
                //    LeaveTypeId = data.type.ShortName,
                //    LeaveTypeName = data.type.Name,
                //    EmployeeID = data.e.EmployeeId,
                //    EmployeeFirstName = data.e.FirstName + " " + data.e.LastName,
                //    DesignationName = data.DesignationName,
                //    LeaveAppEntryCode = data.l.AutoId,
                //    IsApprove = data.l.IsApproved,
                //    HodApprove = data.l.HodapprovalStatus,
                //    HrApprove = data.l.HrapprovalStatus,
                //    IsName = data.ImSupName,
                //    HodName = data.HodName,
                //    Reason = data.l.Reason,
                //    FromDate = data.l.StartDate,
                //    ToDate = data.l.EndDate,
                //    NoOfDay = data.l.NoOfDay,
                //    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                //                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                //                : new List<DateTime>(),
                //    NullHelper = null,
                //    LeaveFormat = data.l.ApplyLeaveFormat




                //}).ToList();


                var result = leaveEntries.Select(data => new LeavePendingViewModel
                {
                    LeaveAppEntryId = data.l?.LeaveAppEntryId ?? "",
                    LeaveTypeId = data.type?.ShortName ?? string.Empty,
                    LeaveTypeName = data.type?.Name ?? string.Empty,
                    EmployeeID = data.e?.EmployeeId ?? string.Empty,
                    EmployeeFirstName = (data.e?.FirstName ?? "") + " " + (data.e?.LastName ?? ""),
                    DesignationName = data.DesignationName ?? string.Empty,
                    LeaveAppEntryCode = data.l?.AutoId ?? 0,
                    IsApprove = data.l?.IsApproved ?? "",
                    HodApprove = data.l?.HodapprovalStatus ?? "",
                    HrApprove = data.l?.HrapprovalStatus ?? "",
                    IsName = data.ImSupName ?? string.Empty,
                    HodName = data.HodName ?? string.Empty,
                    Reason = data.l?.Reason ?? string.Empty,
                    FromDate = data.l?.StartDate ?? DateTime.MinValue,
                    ToDate = data.l?.EndDate ?? DateTime.MinValue,
                    NoOfDay = data.l?.NoOfDay ?? 0,
                    Days = (data.l != null && groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId))
                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                : new List<DateTime>(),
                    NullHelper = null,
                    LeaveFormat = data.l?.ApplyLeaveFormat ?? string.Empty
                }).ToList();


                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<List<LeaveApplicartionGridVM>> GetApprovedLeaveByHodIs(string Id)
        {
            try
            {
                // Fetch main data from the database
                var leaveEntries = await (from l in _context.HrmLeaveApplicationEntry
                                          join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId
                                          join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId
                                          join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode
                                          join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId
                                          join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId
                                          join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode
                                          join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode
                                          select new
                                          {
                                              l,
                                              e,
                                              hod,
                                              type,
                                              sup,
                                              DepartmentName = dep.DepartmentName,
                                              DesignationName = desig.DesignationName
                                          }).ToListAsync();

                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync();

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList()
                                       );

                // Combine data in memory
                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.e.EmployeeId,
                    EmployeeFirstName = data.e.FirstName + " " + data.e.LastName,
                    DepartmentName = data.DepartmentName, // Include DepartmentName
                    DesignationName = data.DesignationName, // Include DesignationName
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    LeaveTypeId = data.type.ShortName,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    HODFirstName = data.hod.FirstName + " " + data.hod.LastName,
                    SupervisorFirstName = data.sup.FirstName + " " + data.sup.LastName,
                    Reason = data.l.Reason,
                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                                : new List<DateTime>()
                }).ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }


    

        public async Task<List<HrmEmployee2SetUpViewModel>> GetByCompCodeAsync(string compCode)
        {
            try
            {
                var data = await (from emp in empRepository.All().AsNoTracking()

                                  join company in coreCompanyRepository.All().AsNoTracking()
                                  on emp.CompanyCode equals company.CompanyCode into empCompany
                                  from company in empCompany.DefaultIfEmpty()




                                  where emp.CompanyCode == compCode

                                  select new HrmEmployee2SetUpViewModel
                                  {
                                      AutoId = emp.AutoId,
                                      EmployeeId = emp.EmployeeId,
                                      FirstName = emp.FirstName,
                                      LastName = emp.LastName,
                                      FatherName = emp.FatherName,
                                      MotherName = emp.MotherName,


                                      CompanyCode = company.CompanyCode,
                                      Company = company.CompanyName,

                                      DateOfBirthCertificate = emp.DateOfBirthCertificate,
                                      DateOfBirthOrginal = emp.DateOfBirthOrginal,
                                      BirthCertificateNo = emp.BirthCertificateNo,
                                      PlaceOfBirth = emp.PlaceOfBirth,
                                      PersonalEmail = emp.PersonalEmail,
                                      Telephone = emp.Telephone,
                                      TinNo = emp.TinNo,
                                      ExtraCurriActivities = emp.ExtraCurriActivities,
                                      Remarks = emp.Remarks,
                                      Ldate = emp.Ldate,
                                      ModifyDate = emp.ModifyDate,

                                  }).ToListAsync();

                return data;
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<string> GenerateLeaveApplicationEntryID()
        {
            try
            {
                var lastCustomerNumber = await _context.HrmLeaveApplicationEntry
                .OrderByDescending(c => c.AutoId)
                .Select(c => c.LeaveAppEntryId)
                .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(lastCustomerNumber))
                {
                    return "1";
                    //return "0000001";
                }


                int nextNumber = int.Parse(lastCustomerNumber) + 1;
                // return nextNumber.ToString("D7");
                return nextNumber.ToString();


            }
            catch (Exception ex)
            {

                throw;
            }




        }

        public async Task<CommonReturn> LeaveValidation(LeaveApplicationViewModel? model)
        {
            
            var missingFields = new List<string>();

            if (model.CompanyCode == null) missingFields.Add("CompanyCode");
            if (model.EmployeeId == null) missingFields.Add("EmployeeId");
            if (model.ImmediateSupervisor == null) missingFields.Add("ImmediateSupervisor");
            if (model.HeadOfDepartment == null) missingFields.Add("HeadOfDepartment");
            if (model.LeaveFormat == null) missingFields.Add("LeaveFormat");
            if (model.LeaveType == null) missingFields.Add("LeaveType");
            //if (model.Reason == null) missingFields.Add("Reason");

            if (missingFields.Any())
            {
                return new CommonReturn()
                {
                    Success = false,
                    Message = "Please fill: " + string.Join(", ", missingFields)
                };
            }


            var joiningDate = await _context.HrmEmployeeOfficialInfo
            .Where(e => e.EmployeeId == model.EmployeeId)
            .Select(e => e.JoiningDate)
            .FirstOrDefaultAsync();

            if (!joiningDate.HasValue)
            {
                return new CommonReturn() { Success = false, Message = "Employee not found or joining date is not set." };

            }

            // Step 2: Calculate the current leave cycle
            var today = DateTime.Today;
            var currentCycleStart = new DateTime(today.Year, joiningDate.Value.Month, joiningDate.Value.Day);
            if (today < currentCycleStart)
            {
                currentCycleStart = currentCycleStart.AddYears(-1);
            }
            var currentCycleEnd = currentCycleStart.AddYears(1).AddDays(-1);

            // Step 3: Get the allowed leave days for the leave type
            var leaveType = await _context.HrmAtdLeaveType
                .Where(lt => lt.LeaveTypeCode == model.LeaveType)
                .Select(lt => new { lt.NoOfDay })
                .FirstOrDefaultAsync();

            if (leaveType == null)
            {
                return new CommonReturn() { Success = false, Message = "Leave type  not found" };

            }

            // Step 4: Calculate the total leave taken in the current cycle
            var totalLeaveTaken = await _context.HrmLeaveApplicationEntry
                .Where(la => la.EmployeeId == model.EmployeeId &&
                             la.HrapprovalStatus == "Approved" &&
                             la.LeaveTypeId == model.LeaveType &&
                             la.StartDate >= currentCycleStart &&
                             la.EndDate <= currentCycleEnd)
                .SumAsync(la => la.NoOfDay);

            // Add new Leave
            var newLeaveInCycle = 0;
            if (model.SelectedDates != null)
            {
                newLeaveInCycle = model.SelectedDates
                    .Count(date => date >= currentCycleStart && date <= currentCycleEnd);
            }

            // If you need both in-cycle and out-of-cycle counts
            var leaveDaysOutOfCycle = model.SelectedDates?.Count() - newLeaveInCycle ?? 0;

            // Step 5: Calculate remaining leave
            var remainingLeave = leaveType.NoOfDay - totalLeaveTaken - newLeaveInCycle;

            // Step 6: Determine the message based on leave availability
            if (remainingLeave <= 0)
            {
                return new CommonReturn() { Success = false, Message = "You have no leave available in this category." };

            }




            var SickLeave = await _context.HrmAtdLeaveType.Where(le => le.ShortName.ToLower() == "sl").FirstOrDefaultAsync();
            var CasualLeave = await _context.HrmAtdLeaveType.Where(le => le.ShortName.ToLower() == "cl").FirstOrDefaultAsync();



            if (model.LeaveType == SickLeave.LeaveTypeCode)
            {
                if (model.LeaveFile == null)
                {
                    if (model.SelectedDates != null)
                    {
                        if (model.SelectedDates.Count() > 2)
                        {
                            return new CommonReturn() { Success = false, Message = "File attachment is required for sick leave of more than 2 days." };

                        }
                    }
                }
            }


            if (model.LeaveType == CasualLeave.LeaveTypeCode)
            {
                if (model.SelectedDates != null)
                {
                    if (model.SelectedDates.Count() > 4)
                    {
                        return new CommonReturn() { Success = false, Message = "Your casual leave request exceeds the allowed 4 days. Please review your request." };
                    }
                }

            }

            if (model.LeaveFormat == "shortLeave")
            {
                if (model.LeaveType != CasualLeave.LeaveTypeCode)
                {
                    return new CommonReturn() { Success = false, Message = "All short leave requests must be classified as Casual Leave. Please update your request accordingly." };
                }
            }


            if (model.LeaveFormat == "halfDayLeave")
            {
                if (model.LeaveType != CasualLeave.LeaveTypeCode && model.LeaveType != SickLeave.LeaveTypeCode)
                {
                    return new CommonReturn() { Success = false, Message = "All short leave requests must be classified as Casual Leave. Please update your request accordingly." };
                }
            }



            return new CommonReturn() { Success = true, Message = "ok " };
        }
        #endregion

        #region Permission
        public async Task<bool> PagePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Leave ApplicationEntry Info" && x.TitleCheck);
        }

        public async Task<bool> SavePermissionAsync(string accessCode)
        {
            var a = await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Leave ApplicationEntry Info" && x.CheckAdd);
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Leave ApplicationEntry Info" && x.CheckAdd);
        }

        public async Task<bool> UpdatePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Leave ApplicationEntry Info" && x.CheckEdit);
        }

        public async Task<bool> DeletePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Leave ApplicationEntry Info" && x.CheckDelete);
        }




        public async Task<bool> PagePermissionUniversal(string accessCode, string tittle)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == tittle && x.TitleCheck);

        }

        public async Task<bool> SavePermissionUniversal(string accessCode, string tittle)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == tittle && x.CheckAdd);
        }

        public async Task<bool> UpdatePermissionUniversal(string accessCode, string tittle)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == tittle && x.CheckEdit);
        }

        public async Task<bool> DeletePermissionUniversal(string accessCode, string tittle)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == tittle && x.CheckDelete);
        }

        public async Task<bool> PrintPermissionUniversal(string accessCode, string tittle)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == tittle && x.CheckPrint);
        }


        public async Task<List<CoreCompany>> GetCompanieListAsync()
        {
            var s = await coreCompanyRepository.All().ToListAsync();
            return s;
        }


        #endregion

        #region Create

        public async Task<bool> SaveAsync(LeaveApplicationViewModel model, bool IsManual)
        {
            await _leaveRepository.BeginTransactionAsync();
            try
            {

                HrmLeaveApplicationEntry entry = new HrmLeaveApplicationEntry();

                //entry.LeaveAppEntryId = model.EntryId;
                entry.LeaveAppEntryId = await GenerateLeaveApplicationEntryID();
                entry.EmployeeId = model.EmployeeId;
                entry.LeaveTypeId = model.LeaveType;

                entry.HalfDay = "";

                entry.FirstOrSecondHalf = ""; // not half

                


                if (model.LeaveFormat == "halfDayLeave")
                {
                    entry.ApplyLeaveFormat = "HalfDayLeave";
                    
                    entry.StartDate = model.HalfDate ?? DateTime.Now;
                    entry.EndDate = model.HalfDate ?? DateTime.Now;
                    entry.NoOfDay = 0.5M;


                    entry.HalfDay = "Y";
                    if (model.IsFirstHalf == true)
                    {
                        entry.FirstOrSecondHalf = "1"; // first half
                    }
                    else
                    {
                        entry.FirstOrSecondHalf = "2"; // second half
                    }
                    model.SelectedDates = new List<DateTime>();
                    model.SelectedDates.Add(model.HalfDate ?? DateTime.Now);
                    await _leaveDaysService.SaveAsync(model);

                }
                else if (model.LeaveFormat == "shortLeave")
                {
                    entry.ApplyLeaveFormat = "ShortLeave";
                   
                    entry.StartDate = DateTime.Now;
                    entry.EndDate = DateTime.Now;
                    entry.NoOfDay = 0;
                    entry.StartDate = model.ShortDate ?? DateTime.Now;
                    entry.EndDate = model.ShortDate ?? DateTime.Now;

                    entry.ShortLeaveFrom = model.FromTime;
                    entry.ShortLeaveTo = model.ToTime;

                    if (entry.ShortLeaveFrom.HasValue && entry.ShortLeaveTo.HasValue)
                    {
                        // Calculate the difference between the two times
                        TimeSpan shortLeaveDuration = entry.ShortLeaveTo.Value - entry.ShortLeaveFrom.Value;

                        // Convert the duration to a TimeOnly instance
                        entry.ShortLeaveTime = TimeOnly.FromTimeSpan(shortLeaveDuration);
                    }
                    else
                    {
                        // If either time is null, assign null to ShortLeaveTime
                        entry.ShortLeaveTime = null;
                    }
                    model.SelectedDates = new List<DateTime>();
                    model.SelectedDates.Add(model.ShortDate ?? DateTime.Now);
                    await _leaveDaysService.SaveAsync(model);


                }
                else
                {
                   
                    entry.ApplyLeaveFormat = "FullLeave";
                    entry.StartDate = (DateTime)model.FromDate;
                    entry.EndDate = (DateTime)model.ToDate;
                    entry.NoOfDay = (decimal)model.TotalDays;

                    if (model.SelectedDates != null)
                    {
                        await _leaveDaysService.SaveAsync(model);
                    }
                }


                entry.Reason = model.Reason;


                entry.CompanyCode = model.CompanyCode;
                entry.Hod = model.HeadOfDepartment;
                entry.BossEmpAutoId = model.ImmediateSupervisor;

                //entry.IsApproved = "_Approved";

                entry.SickLeaveFilePath = model.LeaveFileLink;

                entry.Ldate = model.Ldate;
                //entry.LeaveAppEntryCode = model.LeaveAppEntryCode;
                entry.Lip = model.Lip;
                entry.Lmac = model.Lmac;
                entry.Luser = model.Luser;

                entry.ModifyDate = model.ModifyDate;

                if (IsManual)
                {
                    entry.ConfirmationRemarks = "";
                    entry.HodapprovalRemarks = "";
                    entry.HodapprovalStatus = "";
                    entry.HrapprovalStatus = "Approved";
                    entry.HrapprovalRemarks = "";
                    entry.IsApproved = "";

                    entry.LeaveApplyProcess = "Manual";
                }
                else
                {
                    entry.ConfirmationRemarks = "";
                    entry.HodapprovalRemarks = "";
                    entry.HodapprovalStatus = "";
                    entry.HrapprovalStatus = "";
                    entry.HrapprovalRemarks = "";
                    entry.IsApproved = "";

                    entry.LeaveApplyProcess = "Auto";
                }


                await _leaveRepository.AddAsync(entry);

                await _leaveRepository.CommitTransactionAsync();

                // await _leaveDaysRepository.BeginTransactionAsync();




                return true;


            }
            catch (Exception)
            {
                return false;
               
            }

        }

        public async Task<CommonReturn> UpdateAsync(LeaveApplicationViewModel model)
        {
            await _leaveRepository.BeginTransactionAsync();

            try
            {

                var entry = await _leaveRepository.GetByIdAsync(model.LeaveAppEntryCode);
                if (entry == null)
                {
                    await _leaveRepository.RollbackTransactionAsync();
                    return new CommonReturn()
                    {
                        Success = false
                    };
                }

                var dayCount = await _leaveDaysRepository.FindByAsync(e => e.LeaveAppEntryId == model.EntryId);

                if (dayCount != null)
                {
                    _leaveDaysRepository.Delete(dayCount);
                }

                //var entry = await _context.HrmLeaveApplicationEntry.Where(e => e.LeaveAppEntryCode == model.LeaveAppEntryCode).FirstOrDefaultAsync();

                //if (entry == null) return false;

                // entry.LeaveAppEntryId = model.EntryId;
                entry.EmployeeId = model.EmployeeId;
                entry.LeaveTypeId = model.LeaveType;

                if (model.LeaveFormat == "halfDayLeave")
                {
                    entry.ApplyLeaveFormat = "HalfDayLeave";
                }
                else if (model.LeaveFormat == "shortLeave")
                {
                    entry.ApplyLeaveFormat = "ShortLeave";
                }
                else
                {
                    entry.ApplyLeaveFormat = "FullLeave";
                }


                    


                entry.HalfDay = "0";

                entry.FirstOrSecondHalf = "0"; // not half

                if (model.LeaveFormat == "halfDayLeave")
                {
                    entry.StartDate = DateTime.Now;
                    entry.EndDate = DateTime.Now;
                    entry.NoOfDay = 0.5M;


                    entry.HalfDay = "1";
                    if (model.IsFirstHalf == true)
                    {
                        entry.FirstOrSecondHalf = "1"; // first half
                    }
                    else
                    {
                        entry.FirstOrSecondHalf = "2"; // second half
                    }
                }
                else if (model.LeaveFormat == "shortLeave")
                {
                    entry.StartDate = DateTime.Now;
                    entry.EndDate = DateTime.Now;
                    entry.NoOfDay = 0; entry.StartDate = DateTime.Now;
                    entry.EndDate = DateTime.Now;
                    entry.NoOfDay = 0;

                    entry.ShortLeaveFrom = model.FromTime;
                    entry.ShortLeaveTo = model.ToTime;

                    //TODO: time add korte hobe
                    //entry.ShortLeaveTime = Convert.ToDateTime( model.TotalHours);
                }
                else
                {
                    entry.StartDate = (DateTime)model.FromDate;
                    entry.EndDate = (DateTime)model.ToDate;
                    entry.NoOfDay = (decimal)model.TotalDays;

                    if (model.SelectedDates != null)
                    {
                        await _leaveDaysService.UpdateAsync(model);
                    }
                }


                entry.Reason = model.Reason;


                entry.CompanyCode = model.CompanyCode;
                entry.Hod = model.HeadOfDepartment;
                entry.BossEmpAutoId = model.ImmediateSupervisor;

                entry.IsApproved = "_Approved";
                if (model.LeaveFileLink != null)
                {

                    entry.SickLeaveFilePath = model.LeaveFileLink;
                }


                entry.Ldate = model.Ldate;
                //entry.LeaveAppEntryCode = model.LeaveAppEntryCode;
                entry.Lip = model.Lip;
                entry.Lmac = model.Lmac;
                entry.Luser = model.Luser;

                entry.ModifyDate = model.ModifyDate;

                //entry.ConfirmationRemarks = "";
                //entry.HodapprovalRemarks = "";
                //entry.HodapprovalStatus = "";
                //entry.HrapprovalStatus = "";
                //entry.HrapprovalRemarks = "";

                //entry.LeaveApplyProcess = "_done";

                await _leaveRepository.UpdateAsync(entry);

                var empData = empRepository.All().FirstOrDefault(e=>e.EmployeeId == model.EmployeeId);

                await _leaveRepository.CommitTransactionAsync();
                return new CommonReturn()
                {
                    Success = true,
                    Data = new {id = model.EmployeeId, EmpName = empData.FirstName + " " + empData.LastName, edit = true }
                };


            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<List<LeaveApplicartionGridVM>> GetApprovedAPAAsync(string id, DataTableParams? parameters)
        {
            try
            {

                


                var HrId = GetHrIDasync().Result;

                var leaveEntriesQuery =
                        (
                            from l in _context.HrmLeaveApplicationEntry
                            where l.BossEmpAutoId == id
                                  && (l.IsApproved == "Approved" || l.IsApproved == "Rejected")
                            join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into eJoin
                            from e in eJoin.DefaultIfEmpty()
                            join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                            from hod in hodJoin.DefaultIfEmpty()
                            join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                            from type in typeJoin.DefaultIfEmpty()
                            join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId into supJoin
                            from sup in supJoin.DefaultIfEmpty()
                            join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                            from oe in oeJoin.DefaultIfEmpty()
                            join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode into depJoin
                            from dep in depJoin.DefaultIfEmpty()
                            join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                            from desig in desigJoin.DefaultIfEmpty()
                            select new
                            {
                                l,
                                e,
                                hod,
                                type,
                                sup,
                                DepartmentName = dep != null ? dep.DepartmentName : null,
                                DesignationName = desig != null ? desig.DesignationName : null
                            }
                        )
                        .Concat(
                            from l in _context.HrmLeaveApplicationEntry
                            where l.Hod == id
                                  && (l.HodapprovalStatus == "Approved" || l.HodapprovalStatus == "Rejected")
                            join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into eJoin
                            from e in eJoin.DefaultIfEmpty()
                            join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                            from hod in hodJoin.DefaultIfEmpty()
                            join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                            from type in typeJoin.DefaultIfEmpty()
                            join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId into supJoin
                            from sup in supJoin.DefaultIfEmpty()
                            join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                            from oe in oeJoin.DefaultIfEmpty()
                            join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode into depJoin
                            from dep in depJoin.DefaultIfEmpty()
                            join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                            from desig in desigJoin.DefaultIfEmpty()
                            select new
                            {
                                l,
                                e,
                                hod,
                                type,
                                sup,
                                DepartmentName = dep != null ? dep.DepartmentName : null,
                                DesignationName = desig != null ? desig.DesignationName : null
                            }
                        )
                        .Concat(
                            from l in _context.HrmLeaveApplicationEntry
                            where HrId == id
                                  && (l.HrapprovalStatus == "Approved" || l.HrapprovalStatus == "Rejected")
                            join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into eJoin
                            from e in eJoin.DefaultIfEmpty()
                            join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                            from hod in hodJoin.DefaultIfEmpty()
                            join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                            from type in typeJoin.DefaultIfEmpty()
                            join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId into supJoin
                            from sup in supJoin.DefaultIfEmpty()
                            join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                            from oe in oeJoin.DefaultIfEmpty()
                            join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode into depJoin
                            from dep in depJoin.DefaultIfEmpty()
                            join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                            from desig in desigJoin.DefaultIfEmpty()
                            select new
                            {
                                l,
                                e,
                                hod,
                                type,
                                sup,
                                DepartmentName = dep != null ? dep.DepartmentName : null,
                                DesignationName = desig != null ? desig.DesignationName : null
                            }
                        );



                

                

                // Apply search
                if (!string.IsNullOrEmpty(parameters.Search?.Value))
                {
                    string searchValue = parameters.Search.Value.ToLower();
                    leaveEntriesQuery = leaveEntriesQuery.Where(x =>
                       x.e.EmployeeId.ToString().Contains(searchValue) ||
                        x.e.FirstName.ToLower().Contains(searchValue) ||
                        x.type.ShortName.ToLower().Contains(searchValue) ||
                        x.DepartmentName.ToLower().Contains(searchValue) ||
                        x.l.Reason.ToLower().Contains(searchValue));

                }
                ;

                var count = leaveEntriesQuery.Count();

                // Apply sorting
                //if (!string.IsNullOrEmpty(orderColumn) && !string.IsNullOrEmpty(orderDir))
                //{
                //    switch (orderColumn)
                //    {
                //        case "modifyDate":
                //            leaveEntriesQuery = orderDir == "desc" ?
                //                leaveEntriesQuery.OrderByDescending(x => x.l.ModifyDate) :
                //                leaveEntriesQuery.OrderBy(x => x.l.ModifyDate);
                //            break;
                //        case "employeeID":
                //            leaveEntriesQuery = orderDir == "desc" ?
                //                leaveEntriesQuery.OrderByDescending(x => x.e.EmployeeId) :
                //                leaveEntriesQuery.OrderBy(x => x.e.EmployeeId);
                //            break;
                //            // Add more cases as needed
                //    }
                //}


                //-----------------------
                // Apply sorting
                //if (parameters.Order != null && parameters.Order.Any())
                //{
                //    var orderColumn = parameters.Columns[parameters.Order[0].Column].Data;
                //    var orderDir = parameters.Order[0].Dir;

                //    switch (orderColumn)
                //    {
                //        case "modifyDate":
                //            leaveEntriesQuery = orderDir == "desc" ?
                //                leaveEntriesQuery.OrderByDescending(x => x.l.ModifyDate).ToList() :
                //                leaveEntriesQuery.OrderBy(x => x.l.ModifyDate).ToList();
                //            break;
                //        case "employeeID":
                //            leaveEntriesQuery = orderDir == "desc" ?
                //                leaveEntriesQuery.OrderByDescending(x => x.e.EmployeeId).ToList() :
                //                leaveEntriesQuery.OrderBy(x => x.e.EmployeeId).ToList();
                //            break;
                //            // Add other columns as needed
                //    }
                //}

                // Pagination
                var pagedLeaveEntries = await leaveEntriesQuery
                    .Skip(parameters.Start)
                    .Take(parameters.Length)
                    .ToListAsync();




                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync();

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList()
                                       );

                // Combine data in memory
                var result = pagedLeaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    CountTotal = count,
                    EmployeeID = data.e.EmployeeId,
                    //EmployeeFirstName = data.e.FirstName + " " + data.e.LastName,
                    EmployeeFirstName = (data.e?.FirstName ?? "") + " " + (data.e?.LastName ?? ""),
                    HODFirstName = (data.hod?.FirstName ?? "") + " " + (data.hod?.LastName ?? ""),
                    SupervisorFirstName = (data.sup?.FirstName ?? "") + " " + (data.sup?.LastName ?? ""),

                    DepartmentName = data.DepartmentName, // Include DepartmentName
                    DesignationName = data.DesignationName, // Include DesignationName
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    LeaveTypeId = data.type.ShortName,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    IsApproved = data.l.IsApproved,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HODApprovalRemarks = data.l.HodapprovalRemarks,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    //HODFirstName = data.hod.FirstName + " " + data.hod.LastName,
                    //SupervisorFirstName = data.sup.FirstName + " " + data.sup.LastName,
                    Reason = data.l.Reason,
                    Ldate = data.l.Ldate,
                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                                : new List<DateTime>()
                }).ToList();

                return result;
            }
            catch (Exception)
            {
                throw;
            }
        }


        public async Task<List<LeaveApplicartionGridVM>> GetApprovedLeaveByEmpID(string empId, string status = "Approved")
        {
            try
            {
                

                var leaveEntries = await (from l in _context.HrmLeaveApplicationEntry
                                    where l.EmployeeId == empId && l.HrapprovalStatus == status

                                    // LEFT JOIN Employee
                                    join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into empJoin
                                    from e in empJoin.DefaultIfEmpty()

                                        // LEFT JOIN HOD
                                    join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                                    from hod in hodJoin.DefaultIfEmpty()

                                        // LEFT JOIN LeaveType
                                    join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                                    from type in typeJoin.DefaultIfEmpty()

                                        // LEFT JOIN Supervisor
                                    join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId into supJoin
                                    from sup in supJoin.DefaultIfEmpty()

                                        // LEFT JOIN EmployeeOfficialInfo
                                    join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                                    from oe in oeJoin.DefaultIfEmpty()

                                        // LEFT JOIN Department
                                    join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode into depJoin
                                    from dep in depJoin.DefaultIfEmpty()

                                        // LEFT JOIN Designation
                                    join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                                    from desig in desigJoin.DefaultIfEmpty()

                                    select new
                                    {
                                        l,
                                        e,
                                        hod,
                                        type,
                                        sup,
                                        DepartmentName = dep != null ? dep.DepartmentName : null,
                                        DesignationName = desig != null ? desig.DesignationName : null
                                    }
                                ).ToListAsync();


                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync();

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList()
                                       );

                // Combine data in memory
                //var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                //{
                //    EmployeeID = data.e.EmployeeId,
                //    EmployeeFirstName = data.e.FirstName,
                //    DepartmentName = data.DepartmentName, // Include DepartmentName
                //    DesignationName = data.DesignationName, // Include DesignationName
                //    LeaveAppEntryCode = data.l.AutoId,
                //    LeaveAppEntryId = data.l.LeaveAppEntryId,
                //    LeaveTypeId = data.type.ShortName,
                //    StartDate = data.l.StartDate,
                //    EndDate = data.l.EndDate,
                //    NoOfDay = data.l.NoOfDay,
                //    ModifyDate = data.l.ModifyDate,
                //    ConfirmationRemarks = data.l.ConfirmationRemarks,
                //    HODApprovalStatus = data.l.HodapprovalStatus,
                //    HRApprovalRemarks = data.l.HrapprovalRemarks,
                //    SickLeaveFilePath = data.l.SickLeaveFilePath,
                //    HRApprovalStatus = data.l.HrapprovalStatus,
                //    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                //    HODFirstName = data.hod.FirstName,
                //    SupervisorFirstName = data.sup.FirstName,
                //    Reason = data.l.Reason,
                //    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                //                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                //                : new List<DateTime>()
                //}).ToList();


                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.e?.EmployeeId,
                    EmployeeFirstName = data.e?.FirstName ?? string.Empty + " " + data.e?.LastName ?? string.Empty,
                    DepartmentName = data.DepartmentName,
                    DesignationName = data.DesignationName,
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    LeaveTypeId = data.type?.ShortName ?? string.Empty,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    HODFirstName = data.hod?.FirstName ?? string.Empty + " " + data.hod?.LastName ?? string.Empty,
                    SupervisorFirstName = data.sup?.FirstName ?? string.Empty + " " + data.sup?.LastName ?? string.Empty,
                    Reason = data.l.Reason,
                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                : new List<DateTime>()
                }).ToList();


                return result;
            }
            catch (Exception)
            {

                throw;
            }


        }



        public async Task<object> GetPagedAsync(int page, int pageSize, string searchValue, string sortColumn, string sortDir, string empId)
        {
            try
            {
                //var query = from l in _context.HrmLeaveApplicationEntry
                //            join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId
                //            join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId
                //            join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode
                //            join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId
                //            join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId
                //            join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode
                //            join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode
                //            select new
                //            {
                //                l,
                //                e,
                //                hod,
                //                type,
                //                sup,
                //                DepartmentName = dep.DepartmentName,
                //                DesignationName = desig.DesignationName
                //            };

                var query =
                        from l in _context.HrmLeaveApplicationEntry

                        join e in _context.HrmEmployee
                            on l.EmployeeId equals e.EmployeeId into empGroup
                        from e in empGroup.DefaultIfEmpty()

                        join hod in _context.HrmEmployee
                            on l.Hod equals hod.EmployeeId into hodGroup
                        from hod in hodGroup.DefaultIfEmpty()

                        join type in _context.HrmAtdLeaveType
                            on l.LeaveTypeId equals type.LeaveTypeCode into typeGroup
                        from type in typeGroup.DefaultIfEmpty()

                        join sup in _context.HrmEmployee
                            on l.BossEmpAutoId equals sup.EmployeeId into supGroup
                        from sup in supGroup.DefaultIfEmpty()

                        join oe in _context.HrmEmployeeOfficialInfo
                            on l.EmployeeId equals oe.EmployeeId into oeGroup
                        from oe in oeGroup.DefaultIfEmpty()

                        join dep in _context.HrmDefDepartment
                            on oe.DepartmentCode equals dep.DepartmentCode into depGroup
                        from dep in depGroup.DefaultIfEmpty()

                        join desig in _context.HrmDefDesignation
                            on oe.DesignationCode equals desig.DesignationCode into desigGroup
                        from desig in desigGroup.DefaultIfEmpty()

                        select new
                        {
                            l,
                            e,
                            hod,
                            type,
                            sup,
                            DepartmentName = dep != null ? dep.DepartmentName : null,
                            DesignationName = desig != null ? desig.DesignationName : null
                        };


                // Apply employee filter if provided
                if (!string.IsNullOrEmpty(empId))
                {
                    query = query.Where(x => x.e.EmployeeId == empId);
                }


                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchValue))
                {
                    searchValue = searchValue.ToLower();
                    query = query.Where(data =>
                        data.e.EmployeeId.Contains(searchValue) ||
                        data.e.FirstName.ToLower().Contains(searchValue) ||
                        data.type.ShortName.ToLower().Contains(searchValue) ||
                        data.DepartmentName.ToLower().Contains(searchValue) ||
                        data.DesignationName.ToLower().Contains(searchValue) ||
                        data.l.LeaveAppEntryId.ToLower().Contains(searchValue) ||
                        (data.l.Reason != null && data.l.Reason.ToLower().Contains(searchValue))
                    );
                }

                int totalRecords = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                // Apply sorting
                bool isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
                query = (sortColumn?.ToLower()) switch
                {
                    "employeeid" => isDesc ? query.OrderByDescending(d => d.e.EmployeeId) : query.OrderBy(d => d.e.EmployeeId),
                    "employeename" => isDesc ? query.OrderByDescending(d => d.e.FirstName) : query.OrderBy(d => d.e.FirstName),
                    "leavetype" => isDesc ? query.OrderByDescending(d => d.type.ShortName) : query.OrderBy(d => d.type.ShortName),
                    "startdate" => isDesc ? query.OrderByDescending(d => d.l.StartDate) : query.OrderBy(d => d.l.StartDate),
                    "enddate" => isDesc ? query.OrderByDescending(d => d.l.EndDate) : query.OrderBy(d => d.l.EndDate),
                    "noofday" => isDesc ? query.OrderByDescending(d => d.l.NoOfDay) : query.OrderBy(d => d.l.NoOfDay),
                    "approvalstatus" => isDesc ? query.OrderByDescending(d => d.l.HodapprovalStatus) : query.OrderBy(d => d.l.HodapprovalStatus),
                    _ => query.OrderByDescending(d => d.l.StartDate)
                };

                int skip = (page - 1) * pageSize;
                var leaveEntries = await query.Skip(skip).Take(pageSize).ToListAsync();

                // Fetch leave days only for this page's records
                var entryIds = leaveEntries.Select(d => d.l.LeaveAppEntryId).ToList();
                var leaveDays = await _context.HrmLeaveApplicationDays
                    .Where(d => entryIds.Contains(d.LeaveAppEntryId))
                    .ToListAsync();

                var groupedLeaveDays = leaveDays
                    .GroupBy(d => d.LeaveAppEntryId)
                    .ToDictionary(g => g.Key, g => g.Select(d => d.Days).ToList());

                //var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                //{
                //    EmployeeID = data.e.EmployeeId,
                //    EmployeeFirstName = data.e.FirstName + " " + data.e.LastName,
                //    DepartmentName = data.DepartmentName,
                //    DesignationName = data.DesignationName,
                //    LeaveAppEntryCode = data.l.AutoId,
                //    LeaveAppEntryId = data.l.LeaveAppEntryId,
                //    LeaveTypeId = data.type.ShortName,
                //    StartDate = data.l.StartDate,
                //    EndDate = data.l.EndDate,
                //    NoOfDay = data.l.NoOfDay,
                //    ModifyDate = data.l.ModifyDate,
                //    ConfirmationRemarks = data.l.ConfirmationRemarks,
                //    HODApprovalStatus = data.l.HodapprovalStatus,
                //    HRApprovalRemarks = data.l.HrapprovalRemarks,
                //    SickLeaveFilePath = data.l.SickLeaveFilePath,
                //    HRApprovalStatus = data.l.HrapprovalStatus,
                //    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                //    HODFirstName = data.hod.FirstName + " " + data.hod.LastName,
                //    SupervisorFirstName = data.sup.FirstName + " " + data.sup.LastName,
                //    Reason = data.l.Reason,
                //    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                //                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                //                : new List<DateTime>()
                //}).ToList();
                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.e?.EmployeeId ?? "",
                    EmployeeFirstName = (data.e != null
        ? (data.e.FirstName ?? "") + " " + (data.e.LastName ?? "")
        : string.Empty),
                    DepartmentName = data.DepartmentName ?? string.Empty,
                    DesignationName = data.DesignationName ?? string.Empty,
                    LeaveAppEntryCode = data.l?.AutoId ?? 0,
                    LeaveAppEntryId = data.l?.LeaveAppEntryId ?? "",
                    LeaveTypeId = data.type?.ShortName ?? string.Empty,
                    StartDate = data.l.StartDate ,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l?.NoOfDay ?? 0,
                    ModifyDate = data.l?.ModifyDate,
                    ConfirmationRemarks = data.l?.ConfirmationRemarks ?? string.Empty,
                    HODApprovalStatus = data.l?.HodapprovalStatus ?? string.Empty,
                    HRApprovalRemarks = data.l?.HrapprovalRemarks ?? string.Empty,
                    SickLeaveFilePath = data.l?.SickLeaveFilePath ?? string.Empty,
                    HRApprovalStatus = data.l?.HrapprovalStatus ?? string.Empty,
                    ApplyLeaveFormat = data.l?.ApplyLeaveFormat ?? string.Empty,
                    HODFirstName = (data.hod != null
        ? (data.hod.FirstName ?? "") + " " + (data.hod.LastName ?? "")
        : string.Empty),
                    SupervisorFirstName = (data.sup != null
        ? (data.sup.FirstName ?? "") + " " + (data.sup.LastName ?? "")
        : string.Empty),
                    Reason = data.l?.Reason ?? string.Empty,
                    Days = groupedLeaveDays.ContainsKey(data.l?.LeaveAppEntryId ?? "")
                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                : new List<DateTime>()
                }).ToList();

                return new
                {
                    data = result,
                    totalRecords,
                    totalPages,
                    currentPage = page,
                    pageSize
                };
            }
            catch (Exception)
            {
                throw;
            }
        }


        public async Task<List<LeaveApplicartionGridVM>> GetAllAsync()
        {
            try
            {
                // Fetch main data from the database
                var leaveEntries = await (from l in _context.HrmLeaveApplicationEntry
                                          join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId
                                          join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId
                                          join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode
                                          join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId
                                          join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId
                                          join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode
                                          join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode
                                          select new
                                          {
                                              l,
                                              e,
                                              hod,
                                              type,
                                              sup,
                                              DepartmentName = dep.DepartmentName,
                                              DesignationName = desig.DesignationName
                                          }).ToListAsync();

                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync();

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList()
                                       );

                // Combine data in memory
                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.e.EmployeeId,
                    EmployeeFirstName = data.e.FirstName + " " + data.e.LastName ,
                    DepartmentName = data.DepartmentName, // Include DepartmentName
                    DesignationName = data.DesignationName, // Include DesignationName
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    LeaveTypeId = data.type.ShortName,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    HODFirstName = data.hod.FirstName + " " + data.hod.LastName ,
                    SupervisorFirstName = data.sup.FirstName + " " + data.sup.LastName,
                    Reason = data.l.Reason,
                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                                : new List<DateTime>()
                }).ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }


        }

        public async Task<HrmLeaveApplicationEntry> GetByIdAsync(string id)
        {

            //TODO: thik korte hbe
            var a = Convert.ToDecimal(id);
            var result = await _context.HrmLeaveApplicationEntry.Where(e => e.AutoId == a).FirstOrDefaultAsync();
            return result;


        }


        public async Task<List<LeaveApplicartionGridVM>> LeaveTakenByEmpId(string id)
        {
            try
            {
                // Fetch main data from the database
                var leaveEntries = await (from l in _context.HrmLeaveApplicationEntry
                                              // join e in _context.HrmEmployee2 on l.EmployeeId equals e.EmployeeId
                                              // join hod in _context.HrmEmployee2 on l.Hod equals hod.EmployeeId
                                          join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode

                                          //into typeGroup
                                          //from type in typeGroup.DefaultIfEmpty()

                                          // join sup in _context.HrmEmployee2 on l.BossEmpAutoId equals sup.EmployeeId
                                          where l.EmployeeId == id && l.HrapprovalStatus == "Approved"
                                          select new
                                          {
                                              l,
                                              //  e,
                                              //   hod,
                                              type,
                                              //  sup
                                          }).ToListAsync();

                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync(); // Bring all data into memory

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList() // Collect days as List<DateTime>
                                       );

                // Combine data in memory
                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.l.EmployeeId,
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    //LeaveTypeId = data.l.LeaveTypeId,
                    LeaveTypeId = data.type.ShortName,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HODApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    //  HODFirstName = data.hod.FirstName,
                    //   SupervisorFirstName = data.sup.FirstName,
                    //  EmployeeFirstName = data.e.FirstName,
                    Reason = data.l.Reason,
                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                                : new List<DateTime>() // Default to empty list if no days found
                }).ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }



        public async Task<List<LeaveBalanceViewModel>> LeaveBalanceByEmpId(string id)
        {
            try
            {
                using var connection = _context.Database.GetDbConnection();
                var results = await connection.QueryAsync<LeaveBalanceViewModel>(
                    "SP_GetEmployeeLeaveBalance",
                    new { EmployeeID = id },
                    commandType: CommandType.StoredProcedure
                );

                return results.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred while retrieving leave balances for EmployeeId: {id}", ex);
            }
        }



        public async Task<LeaveBalanceViewModel> LeaveBalanceByLeaveIdEmpId(string LeaveTypeId, string EmpId)
        {
            try
            {

                // Get the employee's joining date
                var joiningDate = await _context.HrmEmployeeOfficialInfo
                    .Where(e => e.EmployeeId == EmpId)
                    .Select(m => m.JoiningDate)
                    .FirstOrDefaultAsync();

                if (!joiningDate.HasValue)
                {
                    throw new Exception("Employee not found or joining date is not set.");
                }

                // Calculate the current leave cycle
                var today = DateTime.Today;
                var currentCycleStart = new DateTime(today.Year, joiningDate.Value.Month, joiningDate.Value.Day);
                if (today < currentCycleStart)
                {
                    currentCycleStart = currentCycleStart.AddYears(-1);
                }
                var currentCycleEnd = currentCycleStart.AddYears(1).AddDays(-1);

                // Fetch leave balances with day-level filtering
                var result = from lt in _context.HrmAtdLeaveType
                             where lt.LeaveTypeCode == LeaveTypeId
                             select new LeaveBalanceViewModel
                             {
                                 LeaveName = lt.Name,
                                 ShortName = lt.ShortName,
                                 LeaveTypeCode = lt.LeaveTypeCode,
                                 AvailableLeave = lt.NoOfDay,
                                 TotalLeaveTaken = (from entry in _context.HrmLeaveApplicationEntry
                                                    join days in _context.HrmLeaveApplicationDays
                                                        on entry.LeaveAppEntryId equals days.LeaveAppEntryId
                                                    where entry.EmployeeId == EmpId && entry.HrapprovalStatus == "Approved"
                                                        && entry.LeaveTypeId == lt.LeaveTypeCode
                                                        && days.Days.Date >= currentCycleStart
                                                        && days.Days.Date <= currentCycleEnd
                                                    select days).Count(),
                                 RemainingLeave = lt.NoOfDay - (from entry in _context.HrmLeaveApplicationEntry
                                                                join days in _context.HrmLeaveApplicationDays
                                                                    on entry.LeaveAppEntryId equals days.LeaveAppEntryId
                                                                where entry.EmployeeId == EmpId && entry.HrapprovalStatus == "Approved"
                                                                    && entry.LeaveTypeId == lt.LeaveTypeCode
                                                                    && days.Days.Date >= currentCycleStart
                                                                    && days.Days.Date <= currentCycleEnd
                                                                select days).Count()
                             };

                // Execute and return the results
                var resultList = await result.OrderBy(r => r.LeaveTypeCode).FirstOrDefaultAsync();
                return resultList;

            }
            catch (Exception ex)
            {
                // Log the exception
                //_logger.LogError(ex, "Error retrieving leave balances for EmployeeId: {EmployeeId}", id);

                // Rethrow a meaningful exception
                throw new Exception($"An error occurred while retrieving leave balances for EmployeeId: {EmpId}", ex);
            }
        }





        public async Task<List<LeaveBalanceViewModel>> LeaveBalanceYearlyByEmpId(string id)
        {
            try
            {

                //// Get the employee's joining date
                //var joiningDate = await _context.HrmEmployeeOfficialInfo
                //    .Where(e => e.EmployeeId == id)
                //    .Select(m => m.JoiningDate)
                //    .FirstOrDefaultAsync();

                //if (!joiningDate.HasValue)
                //{
                //    throw new Exception("Employee not found or joining date is not set.");
                //}

                //// Calculate the leave cycle for the current year
                //var today = DateTime.Today;
                //var currentYearStart = new DateTime(today.Year, 1, 1); // January 1st of the current year
                //var currentYearEnd = new DateTime(today.Year, 12, 31); // December 31st of the current year

                //// If the employee joined in the current year, adjust the cycle start
                //var cycleStart = joiningDate.Value.Year == today.Year
                //    ? joiningDate.Value
                //    : currentYearStart;

                //// Fetch leave balances with adjustments for prorated leave
                //var result = from lt in _context.HrmAtdLeaveType
                //             select new LeaveBalanceViewModel
                //             {
                //                 ShortName = lt.ShortName,
                //                 LeaveTypeCode = lt.LeaveTypeCode,

                //                 // Prorate leave for the year of joining
                //                 AvailableLeave = (cycleStart > currentYearStart)
                //                     ? (lt.NoOfDay / 12) * (12 - cycleStart.Month + 1) // Prorated leave
                //                     : lt.NoOfDay, // Full leave

                //                 // Calculate total leave taken in the current cycle
                //                 TotalLeaveTaken = (from entry in _context.HrmLeaveApplicationEntry
                //                                    join days in _context.HrmLeaveApplicationDays
                //                                        on entry.LeaveAppEntryId equals days.LeaveAppEntryId
                //                                    where entry.EmployeeId == id
                //                                        && entry.LeaveTypeId == lt.LeaveTypeCode
                //                                        && days.Days.Date >= cycleStart
                //                                        && days.Days.Date <= currentYearEnd
                //                                    select days).Count(),

                //                 // Calculate remaining leave
                //                 RemainingLeave = ((cycleStart > currentYearStart)
                //                     ? (lt.NoOfDay / 12) * (12 - cycleStart.Month + 1)
                //                     : lt.NoOfDay) - (from entry in _context.HrmLeaveApplicationEntry
                //                                      join days in _context.HrmLeaveApplicationDays
                //                                          on entry.LeaveAppEntryId equals days.LeaveAppEntryId
                //                                      where entry.EmployeeId == id
                //                                          && entry.LeaveTypeId == lt.LeaveTypeCode
                //                                          && days.Days.Date >= cycleStart
                //                                          && days.Days.Date <= currentYearEnd
                //                                      select days).Count()
                //             };

                //// Execute and return the results
                //var resultList = await result.OrderBy(r => r.LeaveTypeCode).ToListAsync();
                //return resultList;


                // Get the employee's joining date
                var joiningDate = await _context.HrmEmployeeOfficialInfo
                    .Where(e => e.EmployeeId == id)
                    .Select(m => m.JoiningDate)
                    .FirstOrDefaultAsync();

                if (!joiningDate.HasValue)
                {
                    throw new Exception("Employee not found or joining date is not set.");
                }

                // Calculate the financial year cycle
                var today = DateTime.Today;
                DateTime cycleStart, cycleEnd;

                // Determine the start and end of the financial year for the current date
                if (today.Month >= 6) // If it's June or later, use the current financial year
                {
                    cycleStart = new DateTime(today.Year, 6, 1); // June 1st of the current year
                    cycleEnd = new DateTime(today.Year + 1, 5, 31); // May 31st of the next year
                }
                else // If it's before June, use the previous financial year
                {
                    cycleStart = new DateTime(today.Year - 1, 6, 1); // June 1st of the previous year
                    cycleEnd = new DateTime(today.Year, 5, 31); // May 31st of the current year
                }

                // Adjust the cycle start if the employee joined mid-financial year
                if (joiningDate.Value > cycleStart)
                {
                    cycleStart = joiningDate.Value;
                }

                // Fetch leave balances with adjustments for prorated leave
                var result = from lt in _context.HrmAtdLeaveType
                             select new LeaveBalanceViewModel
                             {
                                 ShortName = lt.ShortName,
                                 LeaveTypeCode = lt.LeaveTypeCode,

                                 // Prorate leave for the year of joining
                                 AvailableLeave = (cycleStart > new DateTime(cycleStart.Year, 6, 1))
                                     ? (lt.NoOfDay / 12) * (12 - cycleStart.Month + 1) // Prorated leave
                                     : lt.NoOfDay, // Full leave for subsequent years

                                 // Calculate total leave taken in the current cycle
                                 TotalLeaveTaken = (from entry in _context.HrmLeaveApplicationEntry
                                                    join days in _context.HrmLeaveApplicationDays
                                                        on entry.LeaveAppEntryId equals days.LeaveAppEntryId
                                                    where entry.EmployeeId == id
                                                        && entry.LeaveTypeId == lt.LeaveTypeCode
                                                        && days.Days.Date >= cycleStart
                                                        && days.Days.Date <= cycleEnd
                                                    select days).Count(),

                                 // Calculate remaining leave
                                 RemainingLeave = ((cycleStart > new DateTime(cycleStart.Year, 6, 1))
                                     ? (lt.NoOfDay / 12) * (12 - cycleStart.Month + 1)
                                     : lt.NoOfDay) - (from entry in _context.HrmLeaveApplicationEntry
                                                      join days in _context.HrmLeaveApplicationDays
                                                          on entry.LeaveAppEntryId equals days.LeaveAppEntryId
                                                      where entry.EmployeeId == id
                                                          && entry.LeaveTypeId == lt.LeaveTypeCode
                                                          && days.Days.Date >= cycleStart
                                                          && days.Days.Date <= cycleEnd
                                                      select days).Count()
                             };

                // Execute and return the results
                var resultList = await result.OrderBy(r => r.LeaveTypeCode).ToListAsync();
                return resultList;



            }
            catch (Exception ex)
            {
                // Log the exception
                //_logger.LogError(ex, "Error retrieving leave balances for EmployeeId: {EmployeeId}", id);

                // Rethrow a meaningful exception
                throw new Exception($"An error occurred while retrieving leave balances for EmployeeId: {id}", ex);
            }
        }





        public async Task<HrmLeaveApplicationEntry> GetLeaveDtailsByIdAsync(string id)
        {

            //TODO: thik korte hbe
            var a = Convert.ToDecimal(id);
            var result = await _context.HrmLeaveApplicationEntry.Where(e => e.AutoId == a).FirstOrDefaultAsync();
            return result;


        }


        public async Task<LeaveEntryGetViewModel> GetLeaveEntriesByEmpCodeAsync(string empId)
        {
            try
            {

                var leaveEntries = await (from e in _context.HrmLeaveApplicationEntry
                                          where e.EmployeeId == empId
                                          join l in _context.HrmLeaveApplicationDays
                                          on e.LeaveAppEntryId equals l.LeaveAppEntryId into daysGroup
                                          from lg in daysGroup.DefaultIfEmpty()
                                          group lg by new
                                          {
                                              e.AutoId,
                                              e.LeaveAppEntryId,
                                              e.EmployeeId,
                                              e.LeaveTypeId,
                                              e.StartDate,
                                              e.EndDate,
                                              e.NoOfDay,
                                              e.HalfDay,
                                              e.FirstOrSecondHalf,
                                              e.Reason,
                                              e.BossEmpAutoId,
                                              e.Hod,
                                              e.IsApproved,
                                              e.Luser,
                                              e.Ldate,
                                              e.Lip,
                                              e.Lmac,
                                              e.ModifyDate,
                                              e.ConfirmationRemarks,
                                              e.CompanyCode,
                                              e.HodapprovalStatus,
                                              e.HodapprovalRemarks,
                                              e.HrapprovalStatus,
                                              e.HrapprovalRemarks,
                                              e.ApplyLeaveFormat,
                                              e.ShortLeaveFrom,
                                              e.ShortLeaveTo,
                                              e.ShortLeaveTime,
                                              e.LeaveApplyProcess,
                                              e.SickLeaveFilePath
                                          } into grouped
                                          select new LeaveEntryGetViewModel
                                          {
                                              LeaveAppEntryCode = grouped.Key.AutoId,
                                              LeaveAppEntryId = grouped.Key.LeaveAppEntryId,
                                              EmployeeId = grouped.Key.EmployeeId,
                                              LeaveTypeId = grouped.Key.LeaveTypeId,
                                              StartDate = grouped.Key.StartDate,
                                              EndDate = grouped.Key.EndDate,
                                              NoOfDay = grouped.Key.NoOfDay,
                                              HalfDay = grouped.Key.HalfDay,
                                              FirstOrSecondHalf = grouped.Key.FirstOrSecondHalf,
                                              Reason = grouped.Key.Reason,
                                              BossEmpAutoId = grouped.Key.BossEmpAutoId,
                                              HOD = grouped.Key.Hod,
                                              IsApproved = grouped.Key.IsApproved,
                                              Luser = grouped.Key.Luser,
                                              Ldate = grouped.Key.Ldate,
                                              LIP = grouped.Key.Lip,
                                              LMAC = grouped.Key.Lmac,
                                              ModifyDate = grouped.Key.ModifyDate,
                                              ConfirmationRemarks = grouped.Key.ConfirmationRemarks,
                                              CompanyCode = grouped.Key.CompanyCode,
                                              HODApprovalStatus = grouped.Key.HodapprovalStatus,
                                              HODApprovalRemarks = grouped.Key.HodapprovalRemarks,
                                              HRApprovalStatus = grouped.Key.HrapprovalStatus,
                                              HRApprovalRemarks = grouped.Key.HrapprovalRemarks,
                                              ApplyLeaveFormat = grouped.Key.ApplyLeaveFormat,
                                              ShortLeaveFrom = grouped.Key.ShortLeaveFrom,
                                              ShortLeaveTo = grouped.Key.ShortLeaveTo,
                                              ShortLeaveTime = grouped.Key.ShortLeaveTime,
                                              LeaveApplyProcess = grouped.Key.LeaveApplyProcess,
                                              SickLeaveFilePath = grouped.Key.SickLeaveFilePath,
                                              Days = grouped.Where(g => g != null).Select(g => g.Days).Any()
                                                  ? grouped.Where(g => g != null).Select(g => g.Days).ToList()
                                                  : null
                                          }).FirstOrDefaultAsync();





                return leaveEntries;
            }
            catch (Exception)
            {

                throw;
            }

        }


        public async Task<LeaveEntryGetViewModel> GetLeaveEntriesByEntryId(string LeaveEntryid)
        {
            try
            {

                var leaveEntries = await (from e in _context.HrmLeaveApplicationEntry
                                          where e.LeaveAppEntryId == LeaveEntryid
                                          join l in _context.HrmLeaveApplicationDays
                                          on e.LeaveAppEntryId equals l.LeaveAppEntryId into daysGroup
                                          from lg in daysGroup.DefaultIfEmpty()
                                          group lg by new
                                          {
                                              e.AutoId,
                                              e.LeaveAppEntryId,
                                              e.EmployeeId,
                                              e.LeaveTypeId,
                                              e.StartDate,
                                              e.EndDate,
                                              e.NoOfDay,
                                              e.HalfDay,
                                              e.FirstOrSecondHalf,
                                              e.Reason,
                                              e.BossEmpAutoId,
                                              e.Hod,
                                              e.IsApproved,
                                              e.Luser,
                                              e.Ldate,
                                              e.Lip,
                                              e.Lmac,
                                              e.ModifyDate,
                                              e.ConfirmationRemarks,
                                              e.CompanyCode,
                                              e.HodapprovalStatus,
                                              e.HodapprovalRemarks,
                                              e.HrapprovalStatus,
                                              e.HrapprovalRemarks,
                                              e.ApplyLeaveFormat,
                                              e.ShortLeaveFrom,
                                              e.ShortLeaveTo,
                                              e.ShortLeaveTime,
                                              e.LeaveApplyProcess,
                                              e.SickLeaveFilePath
                                          } into grouped
                                          select new LeaveEntryGetViewModel
                                          {
                                              LeaveAppEntryCode = grouped.Key.AutoId,
                                              LeaveAppEntryId = grouped.Key.LeaveAppEntryId,
                                              EmployeeId = grouped.Key.EmployeeId,
                                              LeaveTypeId = grouped.Key.LeaveTypeId,
                                              StartDate = grouped.Key.StartDate,
                                              EndDate = grouped.Key.EndDate,
                                              NoOfDay = grouped.Key.NoOfDay,
                                              HalfDay = grouped.Key.HalfDay,
                                              FirstOrSecondHalf = grouped.Key.FirstOrSecondHalf,
                                              Reason = grouped.Key.Reason,
                                              BossEmpAutoId = grouped.Key.BossEmpAutoId,
                                              HOD = grouped.Key.Hod,
                                              IsApproved = grouped.Key.IsApproved,
                                              Luser = grouped.Key.Luser,
                                              Ldate = grouped.Key.Ldate,
                                              LIP = grouped.Key.Lip,
                                              LMAC = grouped.Key.Lmac,
                                              ModifyDate = grouped.Key.ModifyDate,
                                              ConfirmationRemarks = grouped.Key.ConfirmationRemarks,
                                              CompanyCode = grouped.Key.CompanyCode,
                                              HODApprovalStatus = grouped.Key.HodapprovalStatus,
                                              HODApprovalRemarks = grouped.Key.HodapprovalRemarks,
                                              HRApprovalStatus = grouped.Key.HrapprovalStatus,
                                              HRApprovalRemarks = grouped.Key.HrapprovalRemarks,
                                              ApplyLeaveFormat = grouped.Key.ApplyLeaveFormat,
                                              ShortLeaveFrom = grouped.Key.ShortLeaveFrom,
                                              ShortLeaveTo = grouped.Key.ShortLeaveTo,


                                              //  ShortLeaveTimeStr = grouped.Key.ShortLeaveTime != null ? Convert.ToDecimal(grouped.Key.ShortLeaveTime) : (decimal?)null,

                                              //   ShortLeaveTime = grouped.Key.ShortLeaveTime,
                                              LeaveApplyProcess = grouped.Key.LeaveApplyProcess,
                                              SickLeaveFilePath = grouped.Key.SickLeaveFilePath,
                                              Days = grouped.Where(g => g != null).Select(g => g.Days).Any()
                                                  ? grouped.Where(g => g != null).Select(g => g.Days).ToList()
                                                  : null
                                          }).FirstOrDefaultAsync();





                return leaveEntries;
            }
            catch (Exception)
            {

                throw;
            }

        }


        public async Task<LeaveEntryGetViewModel> GetLeaveEntriesAsync(string id)
        {
            try
            {
                var a = Convert.ToDecimal(id);

                var leaveEntries = await (from e in _context.HrmLeaveApplicationEntry
                                          where e.AutoId == a
                                          join l in _context.HrmLeaveApplicationDays
                                          on e.LeaveAppEntryId equals l.LeaveAppEntryId into daysGroup
                                          from lg in daysGroup.DefaultIfEmpty()

                                          join emp in _context.HrmEmployee
                                          on e.EmployeeId equals emp.EmployeeId into empGroup
                                          from eg in empGroup.DefaultIfEmpty()

                                          group new { lg, eg } by new
                                          {
                                              e.AutoId,
                                              e.LeaveAppEntryId,
                                              e.EmployeeId,
                                              e.LeaveTypeId,
                                              e.StartDate,
                                              e.EndDate,
                                              e.NoOfDay,
                                              e.HalfDay,
                                              e.FirstOrSecondHalf,
                                              e.Reason,
                                              e.BossEmpAutoId,
                                              e.Hod,
                                              e.IsApproved,
                                              e.Luser,
                                              e.Ldate,
                                              e.Lip,
                                              e.Lmac,
                                              e.ModifyDate,
                                              e.ConfirmationRemarks,
                                              e.CompanyCode,
                                              e.HodapprovalStatus,
                                              e.HodapprovalRemarks,
                                              e.HrapprovalStatus,
                                              e.HrapprovalRemarks,
                                              e.ApplyLeaveFormat,
                                              e.ShortLeaveFrom,
                                              e.ShortLeaveTo,
                                              e.ShortLeaveTime,
                                              e.LeaveApplyProcess,
                                              e.SickLeaveFilePath
                                          } into grouped
                                          select new LeaveEntryGetViewModel
                                          {
                                              LeaveAppEntryCode = grouped.Key.AutoId,
                                              LeaveAppEntryId = grouped.Key.LeaveAppEntryId,
                                              EmployeeId = grouped.Key.EmployeeId,
                                              LeaveTypeId = grouped.Key.LeaveTypeId,
                                              StartDate = grouped.Key.StartDate,
                                              EndDate = grouped.Key.EndDate,
                                              NoOfDay = grouped.Key.NoOfDay,
                                              HalfDay = grouped.Key.HalfDay,
                                              FirstOrSecondHalf = grouped.Key.FirstOrSecondHalf,
                                              Reason = grouped.Key.Reason,
                                              BossEmpAutoId = grouped.Key.BossEmpAutoId,
                                              HOD = grouped.Key.Hod,
                                              IsApproved = grouped.Key.IsApproved,
                                              Luser = grouped.Key.Luser,
                                              Ldate = grouped.Key.Ldate,
                                              LIP = grouped.Key.Lip,
                                              LMAC = grouped.Key.Lmac,
                                              ModifyDate = grouped.Key.ModifyDate,
                                              ConfirmationRemarks = grouped.Key.ConfirmationRemarks,
                                              CompanyCode = grouped.Key.CompanyCode,
                                              HODApprovalStatus = grouped.Key.HodapprovalStatus,
                                              HODApprovalRemarks = grouped.Key.HodapprovalRemarks,
                                              HRApprovalStatus = grouped.Key.HrapprovalStatus,
                                              HRApprovalRemarks = grouped.Key.HrapprovalRemarks,
                                              ApplyLeaveFormat = grouped.Key.ApplyLeaveFormat,
                                              ShortLeaveFrom = grouped.Key.ShortLeaveFrom,
                                              ShortLeaveTo = grouped.Key.ShortLeaveTo,
                                              LeaveApplyProcess = grouped.Key.LeaveApplyProcess,
                                              SickLeaveFilePath = grouped.Key.SickLeaveFilePath,

                                              // Employee Name from HrmEmployee
                                              EmployeeName = grouped.Where(g => g.eg != null)
                                                                    .Select(g => g.eg.FirstName + " " + g.eg.LastName)
                                                                    .FirstOrDefault(),

                                              Days = grouped.Where(g => g.lg != null)
                                                            .Select(g => g.lg.Days)
                                                            .Any()
                                                  ? grouped.Where(g => g.lg != null).Select(g => g.lg.Days).ToList()
                                                  : null
                                          }).FirstOrDefaultAsync();

                return leaveEntries;
            }
            catch (Exception)
            {
                throw;
            }
        }


        //public async Task<LeaveEntryGetViewModel> GetLeaveEntriesAsync(string id)
        //{
        //    try
        //    {
        //        var a = Convert.ToDecimal(id);
        //        var leaveEntries = await (from e in _context.HrmLeaveApplicationEntry
        //                                  where e.AutoId == a
        //                                  join l in _context.HrmLeaveApplicationDays
        //                                  on e.LeaveAppEntryId equals l.LeaveAppEntryId into daysGroup
        //                                  from lg in daysGroup.DefaultIfEmpty()
        //                                  group lg by new
        //                                  {
        //                                      e.AutoId,
        //                                      e.LeaveAppEntryId,
        //                                      e.EmployeeId,
        //                                      e.LeaveTypeId,
        //                                      e.StartDate,
        //                                      e.EndDate,
        //                                      e.NoOfDay,
        //                                      e.HalfDay,
        //                                      e.FirstOrSecondHalf,
        //                                      e.Reason,
        //                                      e.BossEmpAutoId,
        //                                      e.Hod,
        //                                      e.IsApproved,
        //                                      e.Luser,
        //                                      e.Ldate,
        //                                      e.Lip,
        //                                      e.Lmac,
        //                                      e.ModifyDate,
        //                                      e.ConfirmationRemarks,
        //                                      e.CompanyCode,
        //                                      e.HodapprovalStatus,
        //                                      e.HodapprovalRemarks,
        //                                      e.HrapprovalStatus,
        //                                      e.HrapprovalRemarks,
        //                                      e.ApplyLeaveFormat,
        //                                      e.ShortLeaveFrom,
        //                                      e.ShortLeaveTo,
        //                                      e.ShortLeaveTime,
        //                                      e.LeaveApplyProcess,
        //                                      e.SickLeaveFilePath
        //                                  } into grouped
        //                                  select new LeaveEntryGetViewModel
        //                                  {
        //                                      LeaveAppEntryCode = grouped.Key.AutoId,
        //                                      LeaveAppEntryId = grouped.Key.LeaveAppEntryId,
        //                                      EmployeeId = grouped.Key.EmployeeId,
        //                                      LeaveTypeId = grouped.Key.LeaveTypeId,
        //                                      StartDate = grouped.Key.StartDate,
        //                                      EndDate = grouped.Key.EndDate,
        //                                      NoOfDay = grouped.Key.NoOfDay,
        //                                      HalfDay = grouped.Key.HalfDay,
        //                                      FirstOrSecondHalf = grouped.Key.FirstOrSecondHalf,
        //                                      Reason = grouped.Key.Reason,
        //                                      BossEmpAutoId = grouped.Key.BossEmpAutoId,
        //                                      HOD = grouped.Key.Hod,
        //                                      IsApproved = grouped.Key.IsApproved,
        //                                      Luser = grouped.Key.Luser,
        //                                      Ldate = grouped.Key.Ldate,
        //                                      LIP = grouped.Key.Lip,
        //                                      LMAC = grouped.Key.Lmac,
        //                                      ModifyDate = grouped.Key.ModifyDate,
        //                                      ConfirmationRemarks = grouped.Key.ConfirmationRemarks,
        //                                      CompanyCode = grouped.Key.CompanyCode,
        //                                      HODApprovalStatus = grouped.Key.HodapprovalStatus,
        //                                      HODApprovalRemarks = grouped.Key.HodapprovalRemarks,
        //                                      HRApprovalStatus = grouped.Key.HrapprovalStatus,
        //                                      HRApprovalRemarks = grouped.Key.HrapprovalRemarks,
        //                                      ApplyLeaveFormat = grouped.Key.ApplyLeaveFormat,
        //                                      ShortLeaveFrom = grouped.Key.ShortLeaveFrom,
        //                                      ShortLeaveTo = grouped.Key.ShortLeaveTo,


        //                                      //  ShortLeaveTimeStr = grouped.Key.ShortLeaveTime != null ? Convert.ToDecimal(grouped.Key.ShortLeaveTime) : (decimal?)null,

        //                                      //   ShortLeaveTime = grouped.Key.ShortLeaveTime,
        //                                      LeaveApplyProcess = grouped.Key.LeaveApplyProcess,
        //                                      SickLeaveFilePath = grouped.Key.SickLeaveFilePath,
        //                                      Days = grouped.Where(g => g != null).Select(g => g.Days).Any()
        //                                          ? grouped.Where(g => g != null).Select(g => g.Days).ToList()
        //                                          : null
        //                                  }).FirstOrDefaultAsync();





        //        return leaveEntries;
        //    }
        //    catch (Exception)
        //    {

        //        throw;
        //    }

        //}

        public async Task<bool> DeleteLeaveApplication(string id, DeleteHistoryViewModel dm)
        {

            try
            {
               await _leaveRepository.BeginTransactionAsync();

                var a = Convert.ToDecimal(id);

                var entry = await _leaveRepository.GetByIdAsync(a);

                List<HrmLeaveApplicationEntry> data = new List<HrmLeaveApplicationEntry>();
                data.Add(entry);

                dm.tableName = _leaveRepository.GetTableName();
                if (entry == null)
                {
                   await  _leaveRepository.RollbackTransactionAsync();
                    return false;
                }

                _leaveRepository.Delete(a);

                await _deleteHistoryService.LogDeletedRecordsAsync(data , dm);

                await _leaveRepository.CommitTransactionAsync();
                return true;
            }
            catch (Exception)
            {

                throw;
            }





        }

        public bool CheckDuplicate(LeaveApplicationViewModel model)
        {
            try
            {


                var duplicateDays = (from entry in _context.HrmLeaveApplicationEntry
                                     join day in _context.HrmLeaveApplicationDays
                                     on entry.LeaveAppEntryId equals day.LeaveAppEntryId
                                     where entry.EmployeeId == model.EmployeeId  //   "EMP001"
                                           && model.SelectedDates.Contains(day.Days)
                                     group day by day.Days into grouped
                                     select new
                                     {
                                         Day = grouped.Key,
                                         DuplicateCount = grouped.Count()
                                     }).ToList();

                return duplicateDays.Any();
                //if (duplicateDays.Count > 0)
                //{
                //    return true;
                //}
                //return false;
            }
            catch (Exception)
            {

                throw;
            }

        }

        public async Task<List<LeaveApplicartionGridVM>> GetReport(LeaveReportViewModel model)
        {
            try
            {
                // Fetch main data from the database
                var leaveEntries = await (from l in _context.HrmLeaveApplicationEntry
                                          where l.HrapprovalStatus == "Approved"
                                          join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into empJoin
                                          from e in empJoin.DefaultIfEmpty()

                                          join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodJoin
                                          from hod in hodJoin.DefaultIfEmpty()

                                          join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into typeJoin
                                          from type in typeJoin.DefaultIfEmpty()

                                          join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId into supJoin
                                          from sup in supJoin.DefaultIfEmpty()

                                          join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oeJoin
                                          from oe in oeJoin.DefaultIfEmpty()

                                          join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode into depJoin
                                          from dep in depJoin.DefaultIfEmpty()

                                          join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into desigJoin
                                          from desig in desigJoin.DefaultIfEmpty()
                                          select new
                                          {
                                              l,
                                              e,
                                              hod,
                                              type,
                                              sup,
                                              DepartmentName = dep.DepartmentName,
                                              DesignationName = desig.DesignationName,
                                              oe
                                          }).ToListAsync();


                // Apply filters dynamically within the same query
                if (!string.IsNullOrEmpty(model.Company))
                    leaveEntries = leaveEntries.Where(data => data.l.CompanyCode == model.Company).ToList();

                if (!string.IsNullOrEmpty(model.Branch))
                    leaveEntries = leaveEntries.Where(data => data.oe.BranchCode == model.Branch).ToList();

                if (!string.IsNullOrEmpty(model.Department))
                    leaveEntries = leaveEntries.Where(data => data.oe.DepartmentCode == model.Department).ToList();

                if (!string.IsNullOrEmpty(model.Employee))
                    leaveEntries = leaveEntries.Where(data => data.l.EmployeeId == model.Employee).ToList();

                if (model.DateFrom != DateTime.MinValue)
                    leaveEntries = leaveEntries.Where(data => data.l.StartDate >= model.DateFrom).ToList();

                if (model.DateTo != DateTime.MinValue)
                    leaveEntries = leaveEntries.Where(data => data.l.EndDate <= model.DateTo).ToList();

                if (!string.IsNullOrEmpty(model.LeaveFormat))
                    leaveEntries = leaveEntries.Where(data => data.l.ApplyLeaveFormat == model.LeaveFormat).ToList();

                if (!string.IsNullOrEmpty(model.LeaveStatus))
                    leaveEntries = leaveEntries.Where(data => data.l.HrapprovalStatus == model.LeaveStatus).ToList();


                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync();

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList()
                                       );

                // Combine data in memory
                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.e.EmployeeId,
                    EmployeeFirstName = data.e.FirstName + " " + data.e.LastName,
                    DepartmentName = data.DepartmentName, // Include DepartmentName
                    DesignationName = data.DesignationName, // Include DesignationName
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    LeaveTypeId = data.type.ShortName,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    HODFirstName = data.hod.FirstName + " " + data.hod.LastName,
                    SupervisorFirstName = data.sup.FirstName + " " + data.sup.LastName ,
                    Reason = data.l.Reason,
                    ShortLeaveFrom = data.l.ShortLeaveFrom,
                    ShortLeaveTo = data.l.ShortLeaveTo,
                    ShortLeaveTime = data.l.ShortLeaveTime,
                    IsApproved = data.l.IsApproved,
                    FirstOrSecondHalf = data.l.FirstOrSecondHalf == "1" ? "First Half" : data.l.FirstOrSecondHalf == "2" ? "Second Half" : null,

                    //ShortLeaveToStr = data.l.ShortLeaveTime != null ? Convert.ToDateTime(data.l.ShortLeaveTime).ToString("HH:mm") : null,




                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                                : new List<DateTime>()
                }).ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        #endregion

        #region pdf array

        /// <summary>
        /// Generates PDF reports for leave applications grouped by company and department
        /// </summary>
        /// <param name="dataTask">Task containing dictionary of company data</param>
        /// <param name="sdate">Start date in string format</param>
        /// <param name="edate">End date in string format</param>
        /// <returns>PDF as byte array</returns>
        public byte[] GenerateLeavePdfReportArray(Task<Dictionary<string, CompanyLeaveDataVM>> dataTask, string sdate, string edate)
        {
            if (dataTask == null)
                throw new ArgumentNullException(nameof(dataTask));

            try
            {
                // Wait for the task to complete and get the data
                var companyData = dataTask.Result;

                if (companyData == null || !companyData.Any())
                    throw new ArgumentException("No data available to generate report");

                var document = QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        // Page setup
                        page.Margin(20);
                        page.Size(432, 279, Unit.Millimetre);

                        // Content - now processing multiple companies
                        page.Content().Element(content =>
                        {
                            content.Column(column =>
                            {
                                foreach (var company in companyData)
                                {
                                    string companyCode = company.Key;
                                    CompanyLeaveDataVM companyInfo = company.Value;

                                    // Company heading
                                    column.Item().Element(companyContainer =>
                                    {
                                        // Header for this company
                                        ComposeHeader(companyContainer, companyCode, sdate, edate);
                                    });

                                    // Process each department in this company
                                    foreach (var departmentEntry in companyInfo.DepartmentData)
                                    {
                                        string departmentName = departmentEntry.Key;
                                        var departmentInfo = departmentEntry.Value;

                                        // Department heading
                                        column.Item().PaddingTop(10).BorderBottom(1).Padding(5)
                                            .Text($"Department Name: {departmentName}")
                                            .FontSize(14)
                                            .Bold();

                                        // Convert leave details to the format needed for the table
                                        List<LeaveApplicartionGridVM> leaveApplications =
                                            ConvertToLeaveApplications(departmentInfo.LeaveDetails);

                                        // Department data table
                                        column.Item().Table(table =>
                                        {
                                            // Define columns
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.RelativeColumn(3);  // Employee ID
                                                columns.RelativeColumn(2);  // Name
                                                columns.RelativeColumn(2);  // Designation
                                                columns.RelativeColumn(2);  // Leave ID
                                                columns.RelativeColumn(2);  // Leave Format
                                                columns.RelativeColumn(2);  // Approval Status
                                                columns.RelativeColumn(2);  // Leave Type
                                                columns.RelativeColumn(3);  // From Date
                                                columns.RelativeColumn(3);  // To Date
                                                columns.RelativeColumn(3);  // Date(s)
                                                columns.RelativeColumn(2);  // No. of Day(s)
                                                columns.RelativeColumn(2);  // 1st/2nd Half
                                                columns.RelativeColumn(2);  // From
                                                columns.RelativeColumn(2);  // To
                                                columns.RelativeColumn(2);  // Time
                                                columns.RelativeColumn(3);  // Reason
                                                columns.RelativeColumn(2);  // IS App. Status
                                                columns.RelativeColumn(2);  // IS App. Remarks
                                                columns.RelativeColumn(2);  // HOD App. Emp. ID Status
                                                columns.RelativeColumn(2);  // HOD App. Remarks
                                                columns.RelativeColumn(2);  // HR App. Status
                                                columns.RelativeColumn(2);  // HR App. Remarks
                                            });

                                            // Add header
                                            table.Header(header =>
                                            {
                                                AddTableHeader(header);
                                            });

                                            // Add rows
                                            foreach (var application in leaveApplications)
                                            {
                                                AddTableRow1(table, application);
                                            }
                                        });

                                        // Add page break after each department except the last one
                                        if (!(company.Key == companyData.Keys.Last() &&
                                              departmentName == companyInfo.DepartmentData.Keys.Last()))
                                        {
                                            column.Item().PageBreak();
                                        }
                                    }
                                }
                            });
                        });

                        // Footer
                        page.Footer().Element(content =>
                        {
                            ComposeFooter(content);
                        });
                    });
                });

                using var memoryStream = new MemoryStream();
                document.GeneratePdf(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                // Log the error details here if you have a logging system
                throw new Exception("Failed to generate leave report PDF", ex);
            }
        }

        /// <summary>
        /// Converts leave details from the API response to the format needed for the PDF report
        /// </summary>
        private List<LeaveApplicartionGridVM> ConvertToLeaveApplications(List<LeaveDetailVM> leaveDetails)
        {
            var a = "";
            var b = "";
            var c = "";

            List<LeaveApplicartionGridVM> result = new List<LeaveApplicartionGridVM>();

            foreach (var leave in leaveDetails)
            {
                //a = Convert.ToDateTime(leave.ShortLeaveFrom).ToString("HH:mm"); // 13
                //b = Convert.ToDateTime(leave.ShortLeaveTo).ToString("HH:mm"); // 14
                //if (leave.ShortLeaveTime != null)
                //{
                //    c = leave.ShortLeaveTime.ToString(); // 15

                //}


                if (leave.ApplyLeaveFormat == "shortLeave")
                {
                    // Convert DateTime values to TimeOnly to extract only time
                    var shortLeaveFromTime = leave.ShortLeaveFrom.Value.TimeOfDay;
                    var shortLeaveToTime = leave.ShortLeaveTo.Value.TimeOfDay;

                    // Convert to HH:mm format
                    a = leave.ShortLeaveFrom.Value.ToString("HH:mm");
                    b = leave.ShortLeaveTo.Value.ToString("HH:mm");

                    // Calculate the difference
                    TimeSpan difference = shortLeaveToTime - shortLeaveFromTime;
                    c = difference.ToString(@"hh\:mm");
                }

                var application = new LeaveApplicartionGridVM
                {
                    EmployeeID = leave.EmployeeId,
                    EmployeeFirstName = leave.EmployeeFirstName ,
                    DesignationName = leave.DesignationName,
                    LeaveAppEntryId = leave.LeaveAppEntryId,
                    ApplyLeaveFormat = leave.ApplyLeaveFormat,
                    HODApprovalStatus = leave.HODApprovalStatus,
                    LeaveTypeId = leave.LeaveTypeId?.ToString() ?? "",
                    // Handle nullable DateTime properties
                    StartDate = leave.StartDate ?? DateTime.MinValue,
                    EndDate = leave.EndDate ?? DateTime.MinValue,
                    NoOfDay = leave.NoOfDay,
                    FirstOrSecondHalf = leave.FirstOrSecondHalf ?? "",
                    // Handle TimeSpan properties - store the string representation
                    //ShortLeaveFrom = leave.ShortLeaveFrom.HasValue ? GetTimeString(leave.ShortLeaveFrom.Value) : null,
                    //ShortLeaveTo = leave.ShortLeaveTo.HasValue ? GetTimeString(leave.ShortLeaveTo.Value) : null,
                    //ShortLeaveTime = leave.ShortLeaveTime?.ToString(),

                    //ShortLeaveFrom = a,
                    //ShortLeaveTo = b,
                    //ShortLeaveTime = c, 

                    ShortLeaveFromStr = a,
                    ShortLeaveToStr = b,
                    ShortLeaveTimeStr = c,

                    Reason = leave.Reason,
                    IsApproved = leave.IsApproved ?? "",
                    ConfirmationRemarks = leave.ConfirmationRemarks,
                    HODApprovalRemarks = "", // May need mapping from source
                    HRApprovalStatus = leave.HRApprovalStatus,
                    HRApprovalRemarks = leave.HRApprovalRemarks
                };

                // Calculate days between start and end dates (only if both dates are valid)
                //if (leave.StartDate.HasValue != leave.EndDate.HasValue)
                if ((double)leave.NoOfDay > 0.5)
                {
                    var dayRes = _leaveDayRepository.FindBy(ld => ld.LeaveAppEntryId == leave.LeaveAppEntryId)
                                        .Select(ld => ld.Days)
                                        .ToList();

                    application.Days = dayRes;


                }
                else
                {
                    application.Days = new List<DateTime>();
                }

                result.Add(application);
            }

            return result;
        }

        /// <summary>
        /// Helper method to convert TimeSpan to formatted time string
        /// </summary>
        private string GetTimeString(TimeSpan time)
        {
            return $"{time.Hours:D2}:{time.Minutes:D2}";
        }



        /// <summary>
        /// Modified AddTableRow method to handle null values
        /// </summary>
        private void AddTableRow1(TableDescriptor table, LeaveApplicartionGridVM application)
        {



            var a = "";
            var b = "";
            var c = "";

            //if (application.ApplyLeaveFormat == "shortLeave" && application.ShortLeaveFrom.HasValue && application.ShortLeaveTo.HasValue)
            if (application.ApplyLeaveFormat == "shortLeave")
            {
                //// Convert DateTime values to TimeOnly to extract only time
                //var shortLeaveFromTime = application.ShortLeaveFrom.Value.TimeOfDay;
                //var shortLeaveToTime = application.ShortLeaveTo.Value.TimeOfDay;

                //// Convert to HH:mm format
                //a = application.ShortLeaveFrom.Value.ToString("HH:mm");
                //b = application.ShortLeaveTo.Value.ToString("HH:mm");

                //// Calculate the difference
                //TimeSpan difference = shortLeaveToTime - shortLeaveFromTime;
                //c = difference.ToString(@"hh\:mm");

                a = application.ShortLeaveFromStr;
                b = application.ShortLeaveToStr;
                c = application.ShortLeaveTimeStr;
            }






            var cellStyle = TextStyle.Default.FontSize(8);

            // Safe dates for display
            string startDateStr = application.StartDate != DateTime.MinValue ?
                application.StartDate.ToString("dd/MM/yyyy") : "";
            string endDateStr = application.EndDate != DateTime.MinValue ?
                application.EndDate.ToString("dd/MM/yyyy") : "";

            // Handle potentially empty days collection
            string datesStr = application.Days != null && application.Days.Any() ?
                string.Join(", ", application.Days.Select(date => date.ToString("dd/MM/yyyy"))) : "";

            foreach (var text in new[]
            {
                application.EmployeeID ?? "",  // 1
                application.EmployeeFirstName ?? "",  // 2
                application.DesignationName ?? "",  // 3
                application.LeaveAppEntryId ?? "",  // 4
                application.ApplyLeaveFormat ?? "",  // 5
                application.HODApprovalStatus ?? "",  // 6
                application.LeaveTypeId ?? "",  // 7
                startDateStr,  // 8
                endDateStr,  // 9
                datesStr,  // 10
                application.NoOfDay.ToString(),  // 11
                application.FirstOrSecondHalf ?? "",  // 12 
                a,  // 13
                b,  // 14
                c,  // 15
                application.Reason ?? "",  // 16
                application.IsApproved ?? "",  // 17
                application.ConfirmationRemarks ?? "",  // 18
                application.HODApprovalStatus ?? "",  // 19
                application.HODApprovalRemarks ?? "",  // 20
                application.HRApprovalStatus ?? "",  // 21
                application.HRApprovalRemarks ?? ""  // 22
            })
            {
                table.Cell().Border(1).BorderColor(Colors.Black)
                    .Padding(5)
                    .AlignCenter()
                    .Text(text)
                    .Style(cellStyle);
            }
        }




        #endregion

        #region excel Array


        public byte[] GenerateLeaveExcelReportArray(Task<Dictionary<string, CompanyLeaveDataVM>> dataTask, string sdate, string edate)
        {
            if (dataTask == null)
                throw new ArgumentNullException(nameof(dataTask));

            try
            {
                // Wait for the task to complete and get the data
                var companyData = dataTask.Result;

                if (companyData == null || !companyData.Any())
                    throw new ArgumentException("No data available to generate report");

                // Set the license context for EPPlus
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

                using var package = new ExcelPackage();

                // Create a workbook with one worksheet per company
                foreach (var company in companyData)
                {
                    string companyCode = company.Key;
                    CompanyLeaveDataVM companyInfo = company.Value;

                    // Create a worksheet for this company
                    string worksheetName = $"{companyCode}_LeaveReport".Replace("/", "_").Substring(0, Math.Min(31, $"{companyCode}_LeaveReport".Length));
                    var worksheet = package.Workbook.Worksheets.Add(worksheetName);

                    // Table headers
                    var headers = new[] {
                        "Employee ID", "Name", "Designation", "Leave ID",
                        "Leave Format", "Approval Status", "Leave Type",
                        "From Date", "To Date", "Date(s)", "No. of Day(s)",
                        "1st/2nd Half", "From", "To", "Time", "Reason",
                        "IS App. Status", "IS App. Remarks", "HOD App. Status",
                        "HOD App. Remarks", "HR App. Status", "HR App. Remarks"
                    };

                    // Column settings
                    var columnSettings = new List<ColumnSetting>
                    {
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // Employee ID
                        new ColumnSetting { Width = 20, Alignment = ExcelHorizontalAlignment.Left, WrapText = true },    // Name
                        new ColumnSetting { Width = 20, Alignment = ExcelHorizontalAlignment.Left, WrapText = true },    // Designation
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // Leave ID
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // Leave Format
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // Approval Status
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // Leave Type
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // From Date
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // To Date
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = true },  // Date(s)
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // No. of Day(s)
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // 1st/2nd Half
                        new ColumnSetting { Width = 10, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // From
                        new ColumnSetting { Width = 10, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // To
                        new ColumnSetting { Width = 12, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // Time
                        new ColumnSetting { Width = 30, Alignment = ExcelHorizontalAlignment.Left, WrapText = true },    // Reason
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // IS App. Status
                        new ColumnSetting { Width = 30, Alignment = ExcelHorizontalAlignment.Left, WrapText = true },    // IS App. Remarks
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // HOD App. Status
                        new ColumnSetting { Width = 30, Alignment = ExcelHorizontalAlignment.Left, WrapText = true },    // HOD App. Remarks
                        new ColumnSetting { Width = 15, Alignment = ExcelHorizontalAlignment.Center, WrapText = false }, // HR App. Status
                        new ColumnSetting { Width = 30, Alignment = ExcelHorizontalAlignment.Left, WrapText = true }     // HR App. Remarks
                    };

                    // Company header row
                    worksheet.Cells[1, 1].Value = $"Company: {companyCode}";
                    worksheet.Cells[1, 1, 1, headers.Length].Merge = true;
                    worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
                    worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Size = 14;
                    worksheet.Cells[1, 1, 1, headers.Length].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // Date range row
                    worksheet.Cells[2, 1].Value = $"Period: {sdate} to {edate}";
                    worksheet.Cells[2, 1, 2, headers.Length].Merge = true;
                    worksheet.Cells[2, 1, 2, headers.Length].Style.Font.Bold = true;
                    worksheet.Cells[2, 1, 2, headers.Length].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // Process leave applications for this company
                    int currentRow = 3;
                    foreach (var departmentEntry in companyInfo.DepartmentData)
                    {
                        string departmentName = departmentEntry.Key;
                        var departmentInfo = departmentEntry.Value;

                        // Department header row
                        worksheet.Cells[currentRow, 1].Value = $"Department: {departmentName}";
                        worksheet.Cells[currentRow, 1, currentRow, headers.Length].Merge = true;
                        worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Font.Bold = true;
                        worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                        worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                        currentRow++;

                        // Table headers row
                        for (int i = 0; i < headers.Length; i++)
                        {
                            var cell = worksheet.Cells[currentRow, i + 1];
                            cell.Value = headers[i];

                            // Apply column-specific settings
                            var colSetting = columnSettings[i];
                            worksheet.Column(i + 1).Width = colSetting.Width;
                        }

                        // Format header row
                        var headerRange = worksheet.Cells[currentRow, 1, currentRow, headers.Length];
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        headerRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        currentRow++;

                        // Convert leave details to the format needed for the report
                        List<LeaveApplicartionGridVM> leaveApplications =
                            ConvertToLeaveApplicationsExcel(departmentInfo.LeaveDetails);

                        // Add data rows for this department
                        foreach (var application in leaveApplications)
                        {
                            // Safe dates for display
                            string startDateStr = application.StartDate != DateTime.MinValue ?
                                application.StartDate.ToString("dd/MM/yyyy") : "";
                            string endDateStr = application.EndDate != DateTime.MinValue ?
                                application.EndDate.ToString("dd/MM/yyyy") : "";

                            // Handle potentially empty days collection
                            string datesStr = application.Days != null && application.Days.Any() ?
                                string.Join(", ", application.Days.Select(date => date.ToString("dd/MM/yyyy"))) : "";

                            // Handle time values for short leaves
                            string fromTime = "", toTime = "", time = "";
                            if (application.ApplyLeaveFormat == "shortLeave")
                            {
                                fromTime = application.ShortLeaveFromStr;
                                toTime = application.ShortLeaveToStr;
                                time = application.ShortLeaveTimeStr;
                            }

                            // Add values to cells
                            object[] rowValues = new object[] {
                                application.EmployeeID ?? "",
                                application.EmployeeFirstName ?? "",
                                application.DesignationName ?? "",
                                application.LeaveAppEntryId ?? "",
                                application.ApplyLeaveFormat ?? "",
                                application.HODApprovalStatus ?? "",
                                application.LeaveTypeId ?? "",
                                startDateStr,
                                endDateStr,
                                datesStr,
                                application.NoOfDay.ToString(),
                                application.FirstOrSecondHalf ?? "",
                                fromTime,
                                toTime,
                                time,
                                application.Reason ?? "",
                                application.IsApproved ?? "",
                                application.ConfirmationRemarks ?? "",
                                application.HODApprovalStatus ?? "",
                                application.HODApprovalRemarks ?? "",
                                application.HRApprovalStatus ?? "",
                                application.HRApprovalRemarks ?? ""
                            };

                            for (int i = 0; i < rowValues.Length; i++)
                            {
                                var cell = worksheet.Cells[currentRow, i + 1];
                                cell.Value = rowValues[i];

                                // Apply column-specific settings
                                var colSetting = columnSettings[i];
                                cell.Style.HorizontalAlignment = colSetting.Alignment;
                                cell.Style.WrapText = colSetting.WrapText;
                            }

                            // Format data row
                            var rowRange = worksheet.Cells[currentRow, 1, currentRow, headers.Length];
                            rowRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                            rowRange.Style.Font.Size = 10;

                            currentRow++;
                        }
                    }

                    // Add footer
                    worksheet.Cells[currentRow, 1].Value = "Report Generated On: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                    worksheet.Cells[currentRow, 1, currentRow, headers.Length].Merge = true;
                    worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Font.Italic = true;
                    worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // A4 Paper settings
                    worksheet.PrinterSettings.PaperSize = ePaperSize.A4;
                    worksheet.PrinterSettings.Orientation = eOrientation.Landscape;
                    worksheet.PrinterSettings.FitToPage = true;
                    worksheet.PrinterSettings.FitToWidth = 1;
                    worksheet.PrinterSettings.FitToHeight = 0;
                    worksheet.PrinterSettings.RepeatRows = new ExcelAddress("$4:$5"); // Repeat department and header rows
                }

                // Return the Excel package as a byte array
                return package.GetAsByteArray();
            }
            catch (Exception ex)
            {
                // Log the error details here if you have a logging system
                throw new Exception("Failed to generate leave report Excel", ex);
            }
        }

        
        // This is the same method from your PDF code
        private List<LeaveApplicartionGridVM> ConvertToLeaveApplicationsExcel(List<LeaveDetailVM> leaveDetails)
        {
            var a = "";
            var b = "";
            var c = "";

            List<LeaveApplicartionGridVM> result = new List<LeaveApplicartionGridVM>();

            foreach (var leave in leaveDetails)
            {
                //a = Convert.ToDateTime(leave.ShortLeaveFrom).ToString("HH:mm"); // 13
                //b = Convert.ToDateTime(leave.ShortLeaveTo).ToString("HH:mm"); // 14
                //if (leave.ShortLeaveTime != null)
                //{
                //    c = leave.ShortLeaveTime.ToString(); // 15
                //}

                if (leave.ApplyLeaveFormat == "shortLeave")
                {
                    // Convert DateTime values to TimeOnly to extract only time
                    var shortLeaveFromTime = leave.ShortLeaveFrom.Value.TimeOfDay;
                    var shortLeaveToTime = leave.ShortLeaveTo.Value.TimeOfDay;

                    // Convert to HH:mm format
                    a = leave.ShortLeaveFrom.Value.ToString("HH:mm");
                    b = leave.ShortLeaveTo.Value.ToString("HH:mm");

                    // Calculate the difference
                    TimeSpan difference = shortLeaveToTime - shortLeaveFromTime;
                    c = difference.ToString(@"hh\:mm");
                }

                var application = new LeaveApplicartionGridVM
                {
                    EmployeeID = leave.EmployeeId,
                    EmployeeFirstName = leave.EmployeeFirstName,
                    DesignationName = leave.DesignationName,
                    LeaveAppEntryId = leave.LeaveAppEntryId,
                    ApplyLeaveFormat = leave.ApplyLeaveFormat,
                    HODApprovalStatus = leave.HODApprovalStatus,
                    LeaveTypeId = leave.LeaveTypeId?.ToString() ?? "",
                    // Handle nullable DateTime properties
                    StartDate = leave.StartDate ?? DateTime.MinValue,
                    EndDate = leave.EndDate ?? DateTime.MinValue,
                    NoOfDay = leave.NoOfDay,
                    FirstOrSecondHalf = leave.FirstOrSecondHalf ?? "",

                    // Handle DateTime properties
                    //ShortLeaveFrom = DateTime.Now,
                    //ShortLeaveTo = DateTime.Now,
                    // ShortLeaveTime =

                    ShortLeaveFromStr = a,
                    ShortLeaveToStr = b,
                    ShortLeaveTimeStr = c,

                    Reason = leave.Reason,
                    IsApproved = leave.IsApproved ?? "",
                    ConfirmationRemarks = leave.ConfirmationRemarks,
                    HODApprovalRemarks = leave.HODApprovalStatus, // May need mapping from source
                    HRApprovalStatus = leave.HRApprovalStatus,
                    HRApprovalRemarks = leave.HRApprovalRemarks
                };

                // Calculate days between start and end dates (only if both dates are valid)
                //if (leave.StartDate.HasValue != leave.EndDate.HasValue)
                if ((double)leave.NoOfDay > 0.5)
                {
                    var dayRes = _leaveDayRepository.FindBy(ld => ld.LeaveAppEntryId == leave.LeaveAppEntryId)
                                        .Select(ld => ld.Days)
                                        .ToList();

                    application.Days = dayRes;
                }
                else
                {
                    application.Days = new List<DateTime>();
                }

                result.Add(application);
            }

            return result;
        }


        #endregion

        #region Genarate PDF

        /// <summary>
        /// Generates PDF reports for leave applications grouped by department
        /// </summary>
        public byte[] GenerateLeavePdfReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                var document = QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        // Page setup
                        page.Margin(20);
                        page.Size(432, 279, Unit.Millimetre);



                        // Header
                        page.Header().Element(content =>
                        {
                            ComposeHeader(content, companyName, sDate, eDate);
                        });

                        // Content
                        page.Content().Element(content =>
                        {
                            ComposeContent(content, data);
                        });


                        // Footer
                        page.Footer().Element(content =>
                        {
                            ComposeFooter(content);
                        });

                    });
                });

                using var memoryStream = new MemoryStream();
                document.GeneratePdf(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                // Log the error details here if you have a logging system
                throw new Exception("Failed to generate leave report PDF", ex);
            }
        }

        private void ComposeFooter(QuestPDF.Infrastructure.IContainer content)
        {
            content.Row(row =>
            {
                // Left side: Generated date
                row.RelativeItem().AlignLeft().Text(text =>
                {
                    text.Span($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}");
                });

                // Right side: Page numbers
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }


        /// <summary>
        /// Composes the header section of the PDF
        /// </summary>
        private void ComposeHeader(QuestPDF.Infrastructure.IContainer container, string companyName, string sDate, string eDate)
        {

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(companyName)
                        .FontSize(20)
                        .Bold()
                        .AlignCenter();

                    column.Item().Text("Leave Application Report")
                        .FontSize(12)
                        .Bold()
                        .Underline()
                        .AlignCenter();

                    column.Item().Text($"Date : {sDate} - {eDate}")
                        .FontSize(10)
                        .AlignCenter();

                    //column.Item().Text($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}")
                    //    .FontSize(10)
                    //    .AlignCenter();
                });
            });
        }

        /// <summary>
        /// Composes the main content of the PDF with department-wise leave data
        /// </summary>
        private void ComposeContent(QuestPDF.Infrastructure.IContainer container, List<LeaveReportGridVM> data)
        {
            container.Column(column =>
            {
                foreach (var department in data)
                {
                    // Department heading
                    column.Item().PaddingTop(10).BorderBottom(1).Padding(5)
                        .Text($"Department Name : {department.Department}")
                        .FontSize(16)
                        .Bold();

                    // Department data table
                    column.Item().Table(table =>
                    {

                        // Define columns
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);  // Employee ID
                            columns.RelativeColumn(2);  // Name
                            columns.RelativeColumn(2);  // Designation
                            columns.RelativeColumn(1);  // Leave ID
                            columns.RelativeColumn(2);  // Leave Format
                            columns.RelativeColumn(2);  // Approval Status
                            columns.RelativeColumn(1);  // Leave Type
                            columns.RelativeColumn(2);  // From Date
                            columns.RelativeColumn(2);  // To Date
                            columns.RelativeColumn(3);  // Date(s)
                            columns.RelativeColumn(1);  // No. of Day(s)
                            columns.RelativeColumn(2);  // 1st/2nd Half
                            columns.RelativeColumn(2);  // From
                            columns.RelativeColumn(2);  // To
                            columns.RelativeColumn(2);  // Time
                            columns.RelativeColumn(3);  // Reason
                            columns.RelativeColumn(2);  // IS App. Status
                            columns.RelativeColumn(2);  // IS App. Remarks
                            columns.RelativeColumn(2);  // HOD App. Emp. ID Status
                            columns.RelativeColumn(2);  // HOD App. Remarks
                            columns.RelativeColumn(2);  // HR App. Status
                            columns.RelativeColumn(2);  // HR App. Remarks


                        });

                        // Add header
                        table.Header(header =>
                        {
                            AddTableHeader(header);
                        });

                        // Add rows
                        foreach (var application in department.Data)
                        {
                            AddTableRow(table, application);
                        }
                    });

                    // Add page break after each department except the last one
                    //if (department != data[data.Count - 1])
                    //    column.Item().PageBreak();
                }
            });
        }


        private void AddTableHeader(TableCellDescriptor header)
        {
            var headerStyle = TextStyle.Default.FontSize(10).Bold();

            // First row of headers
            foreach (var text in new[]
            {
                "Emp. ID",            // 1
                "Name",              // 2
                "Designation",       // 3
                "Leave ID",          // 4
                "Leave\r\nFormat",   // 5
                "Approval\r\nStatus", // 6
                "Leave\r\nType",     // 7
                "From\r\nDate",      // 8
                "To\r\nDate",        // 9
                "Date(s)",           // 10
                "No.\r\nof\r\nDay(s)", // 11
                "1st/\r\n2nd\r\nHalf", // 12
            })
            {
                header.Cell()
                    .RowSpan(2)
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(5)
                    .AlignCenter()
                    .Text(text)
                    .Style(headerStyle);
            }

            // Add the "Short Leave" header spanning three columns
            header.Cell()
                .ColumnSpan(3) // Spans across three child columns
                .Border(1)
                .BorderColor(Colors.Black)
                .Padding(5)
                .AlignCenter()
                .Text("Short Leave") // Parent Header
                .Style(headerStyle);

            // Add placeholders for alignment with other columns in the first row
            foreach (var text in new[] { "Reason", "IS App.\r\nStatus", "IS App. Remarks", "HOD App.\r\n Status", "HOD App. Remarks", "HR App. Status", "HR App. Remarks" })
            {
                header.Cell()
                    .RowSpan(2)
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(5)
                    .AlignCenter()
                    .Text(text)
                    .Style(headerStyle);
            }



            // Second row of child headers under "Short Leave"
            header.Cell()
                .Border(1)
                .BorderColor(Colors.Black)
                .Padding(5)
                .AlignCenter()
                .Text("From") // Child Header 1
                .Style(headerStyle);

            header.Cell()
                .Border(1)
                .BorderColor(Colors.Black)
                .Padding(5)
                .AlignCenter()
                .Text("To") // Child Header 2
                .Style(headerStyle);

            header.Cell()
                .Border(1)
                .BorderColor(Colors.Black)
                .Padding(5)
                .AlignCenter()
                .Text("Time") // Child Header 3
                .Style(headerStyle);
        }


        /// <summary>
        /// Adds a data row to the table
        /// </summary>
        private void AddTableRow(TableDescriptor table, LeaveApplicartionGridVM application)
        {
            var a = "";
            var b = "";
            var c = "";

            if (application.ApplyLeaveFormat == "shortLeave")
            {
                a = Convert.ToDateTime(application.ShortLeaveFrom).ToString("HH:mm"); // 13
                b = Convert.ToDateTime(application.ShortLeaveTo).ToString("HH:mm"); // 14
                if (application.ShortLeaveTime != null)
                {
                    c = application.ShortLeaveTime.ToString(); // 15

                }
            }
            var cellStyle = TextStyle.Default.FontSize(8);

            foreach (var text in new[]
            {
                application.EmployeeID,  // 1
                application.EmployeeFirstName,  // 2
                application.DesignationName,  // 3
                application.LeaveAppEntryId,  // 4
                application.ApplyLeaveFormat,  // 5
                application.HODApprovalStatus,  // 6
                application.LeaveTypeId,  // 7
                application.StartDate.ToString("dd/MM/yyyy"),  // 8
                application.EndDate.ToString("dd/MM/yyyy"),  // 9
                string.Join(", ", application.Days.Select(date => date.ToString("dd/MM/yyyy"))),  // 10
                application.NoOfDay.ToString(),  // 11
                application.FirstOrSecondHalf,  // 12 first or second half
                a,
                b,
                c,
                //application.ShortLeaveFromStr,
                //application.ShortLeaveToStr,
                //application.ShortLeaveTimeStr,
                //Convert.ToDateTime(application.ShortLeaveFrom).ToString("HH:mm"), // 13
                //Convert.ToDateTime(application.ShortLeaveTo).ToString("HH:mm"), // 14
                //Convert.ToDateTime(application.ShortLeaveTime).ToString("HH:mm"), // 15
                application.Reason,  // 16 reason
                application.IsApproved,  // 17 Is App. Status
                application.ConfirmationRemarks,  // 18 Is App. Remarks
                application.HODApprovalStatus,  // 19
                application.HODApprovalRemarks,  // 20
                application.HRApprovalStatus,  // 21
                application.HRApprovalRemarks  // 22
            })
            {
                table.Cell().Border(1).BorderColor(Colors.Black)
                                    .Padding(5)
                                    .AlignCenter()

                    .Text(text)
                    .Style(cellStyle);
            }
        }

        /// <summary>
        /// Returns styled status text based on approval status
        /// </summary>
        //private IContainer GetStatusWithStyle(TableDescriptor table, string status)
        //{
        //    return table.Cell().Element(container =>
        //    {
        //        var color = status.ToLower() switch
        //        {
        //            "approved" => "#28a745",
        //            "rejected" => "#dc3545",
        //            "pending" => "#ffc107",
        //            _ => "#6c757d"
        //        };

        //        container.Text(status).FontColor(color);
        //    });
        //}


        #endregion

        #region Excel

        public byte[] GenerateLeaveExcelReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Leave Report");

                // Set column widths
                SetColumnWidths(worksheet);

                // Add Header
                int currentRow = 1;
                currentRow = ComposeHeader(worksheet, companyName, sDate, eDate, currentRow);

                // Add Column Headers
                // currentRow = ComposeColumnHeaders(worksheet, currentRow);

                // Add Content
                foreach (var department in data)
                {
                    currentRow = ComposeDepartmentData(worksheet, department, currentRow);
                }

                // Add Footer
                ComposeFooter(worksheet, currentRow + 1);

                // Apply general styling
                ApplyGeneralStyling(worksheet, currentRow);

                return package.GetAsByteArray();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to generate leave report Excel", ex);
            }
        }

        private void SetColumnWidths(ExcelWorksheet worksheet)
        {
            worksheet.Column(1).Width = 12;   // Emp. ID
            worksheet.Column(2).Width = 15;   // Name
            worksheet.Column(3).Width = 15;   // Designation
            worksheet.Column(4).Width = 5;   // Leave ID
            worksheet.Column(5).Width = 12;   // Leave Format
            worksheet.Column(6).Width = 12;   // Approval Status
            worksheet.Column(7).Width = 10;   // Leave Type
            worksheet.Column(8).Width = 12;   // From Date
            worksheet.Column(9).Width = 12;   // To Date
            worksheet.Column(10).Width = 10;  // Date(s)
            worksheet.Column(11).Width = 10;  // No. of Day(s)
            worksheet.Column(12).Width = 10;  // 1st/2nd Half
            worksheet.Column(13).Width = 10;  // Short Leave - From
            worksheet.Column(14).Width = 10;  // Short Leave - To
            worksheet.Column(15).Width = 10;  // Short Leave - Time
            worksheet.Column(16).Width = 20;  // Reason
            worksheet.Column(17).Width = 12;  // IS App. Status
            worksheet.Column(18).Width = 15;  // IS App. Remarks
            worksheet.Column(19).Width = 15;  // HOD App. Status
            worksheet.Column(20).Width = 15;  // HOD App. Remarks
            worksheet.Column(21).Width = 12;  // HR App. Status
            worksheet.Column(22).Width = 15;  // HR App. Remarks

            //for (int i = 1; i <= 9; i++)
            //{
            //    worksheet.Column(i).AutoFit();
            //}

            //for (int i = 10; i <= 22; i++)
            //{
            //    worksheet.Column(i).AutoFit();
            //}
        }

        private int ComposeHeader(ExcelWorksheet worksheet, string companyName, string sDate, string eDate, int startRow)
        {

            // Company Name
            using (var range = worksheet.Cells[startRow, 1, startRow, 22])
            {
                range.Merge = true;
                range.Value = companyName;
                range.Style.Font.Size = 20;
                range.Style.Font.Bold = true;
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // Report Title
            using (var range = worksheet.Cells[startRow + 1, 1, startRow + 1, 22])
            {
                range.Merge = true;
                range.Value = "Leave Application Report";
                range.Style.Font.Size = 12;
                range.Style.Font.Bold = true;
                range.Style.Font.UnderLine = true;
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // Date Range
            using (var range = worksheet.Cells[startRow + 2, 1, startRow + 2, 22])
            {
                range.Merge = true;
                range.Value = $"Date: {sDate} - {eDate}";
                range.Style.Font.Size = 10;
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            using (var range = worksheet.Cells[startRow + 3, 1, startRow + 3, 22])
            {
                range.Merge = true;
                //range.Value = $"Date: {sDate} - {eDate}";
                // range.Style.Font.Size = 10;
                // range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                range.Style.Border.Top.Style = ExcelBorderStyle.None;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.None;
                range.Style.Border.Left.Style = ExcelBorderStyle.None;
                range.Style.Border.Right.Style = ExcelBorderStyle.None;
            }

            return startRow + 4; // Return next available row
        }



        //private int ComposeColumnHeaders(ExcelWorksheet worksheet, int startRow)
        //{
        //    // First row headers
        //    string[] headers = {
        //        "Emp. ID", "Name", "Designation", "Leave ID", "Leave Format",
        //        "Approval Status", "Leave Type", "From Date", "To Date",
        //        "Date(s)", "No. of Day(s)", "1st/2nd Half"
        //    };

        //    // Add regular headers
        //    for (int i = 0; i < headers.Length; i++)
        //    {
        //       // worksheet.Cells[startRow, i + 1].Style.WrapText = true;
        //        worksheet.Cells[startRow , i + 1, startRow + 1 , i + 1 ].Merge = true;
        //        var cell = worksheet.Cells[startRow, i + 1];
        //        cell.Value = headers[i];
        //       // cell.Style.WrapText = true;

        //        worksheet.Column(i + 1).Width = 15;




        //    }


        //    // Add Short Leave section
        //    worksheet.Cells[startRow, 13, startRow, 15].Merge = true;
        //    worksheet.Cells[startRow, 13].Value = "Short Leave";

        //    // Add Short Leave sub-headers
        //    worksheet.Cells[startRow + 1, 13].Value = "From";
        //    worksheet.Cells[startRow + 1, 14].Value = "To";
        //    worksheet.Cells[startRow + 1, 15].Value = "Time";

        //    // Add remaining headers
        //    string[] remainingHeaders = {
        //        "Reason", "IS App. Status", "IS App. Remarks",
        //        "HOD App. Emp. ID Status", "HOD App. Remarks",
        //        "HR App. Status", "HR App. Remarks"
        //    };

        //    for (int i = 0; i < remainingHeaders.Length; i++)
        //    {
        //        worksheet.Cells[startRow, i + 16, startRow + 1, i + 16].Merge = true;
        //        var cell = worksheet.Cells[startRow, i + 16];
        //        cell.Value = remainingHeaders[i];
        //        cell.Style.WrapText = true;
        //    }

        //    // Style headers
        //    var headerRange = worksheet.Cells[startRow, 1, startRow + 1, 22];
        //    headerRange.Style.Font.Bold = true;
        //    headerRange.Style.Font.Size = 10;
        //    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        //    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        //    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        //    headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

        //    return startRow + 2; // Return next available row
        //}

        private int ComposeDepartmentData(ExcelWorksheet worksheet, LeaveReportGridVM department, int startRow)
        {



            // Department Header
            using (var headerRange = worksheet.Cells[startRow, 1, startRow + 1, 22])
            {

                headerRange.Merge = true;
                headerRange.Value = $"Department Name : {department.Department}";
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Size = 12;
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 240, 240));
                headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            }

            startRow = startRow + 1;

            startRow++;

            // Add table headers after department name
            startRow = ComposeTableHeaders(worksheet, startRow);

            // Add department data
            foreach (var item in department.Data)
            {
                // Fill data
                worksheet.Cells[startRow, 1].Value = item.EmployeeID;
                worksheet.Cells[startRow, 2].Value = item.EmployeeFirstName;
                worksheet.Cells[startRow, 3].Value = item.DesignationName;
                worksheet.Cells[startRow, 4].Value = item.LeaveAppEntryId;
                worksheet.Cells[startRow, 5].Value = item.ApplyLeaveFormat;
                worksheet.Cells[startRow, 6].Value = item.HODApprovalStatus;
                worksheet.Cells[startRow, 7].Value = item.LeaveTypeId;
                worksheet.Cells[startRow, 8].Value = item.StartDate.ToString("dd/MM/yyyy");
                worksheet.Cells[startRow, 9].Value = item.EndDate.ToString("dd/MM/yyyy");

                // Set Days column with text wrapping
                var daysCell = worksheet.Cells[startRow, 10];
                daysCell.Value = string.Join(", ", item.Days.Select(date => date.ToString("dd/MM/yyyy")));
                daysCell.Style.WrapText = true;
                daysCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;


                worksheet.Cells[startRow, 11].Value = item.NoOfDay;
                worksheet.Cells[startRow, 12].Value = item.HRApprovalRemarks;

                worksheet.Cells[startRow, 13].Value = item.ShortLeaveFrom != null ? item.ShortLeaveFrom.Value.ToString("HH:mm") : null;

                worksheet.Cells[startRow, 14].Value = item.ShortLeaveTo != null ? item.ShortLeaveTo.Value.ToString("HH:mm") : null;
                worksheet.Cells[startRow, 15].Value = item.ShortLeaveTime != null ? item.ShortLeaveTime.Value.ToString("HH:mm") : null;


                worksheet.Cells[startRow, 16].Value = item.Reason;
                worksheet.Cells[startRow, 17].Value = item.IsApproved;
                worksheet.Cells[startRow, 18].Value = item.ConfirmationRemarks;
                worksheet.Cells[startRow, 19].Value = item.HODApprovalStatus;
                worksheet.Cells[startRow, 20].Value = item.HODApprovalRemarks;
                worksheet.Cells[startRow, 21].Value = item.HRApprovalStatus;
                worksheet.Cells[startRow, 22].Value = item.HRApprovalRemarks;

                // Style row
                using (var row = worksheet.Cells[startRow, 1, startRow, 22])
                {
                    row.Style.Font.Size = 9;
                    row.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    row.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    row.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    row.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    row.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    row.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                }

                // Adjust row height based on content
                worksheet.Row(startRow).CustomHeight = true;
                worksheet.Row(startRow).Height = 30; // Set a reasonable default height

                startRow++;
            }

            return startRow;
        }



        private int ComposeTableHeaders(ExcelWorksheet worksheet, int startRow)
        {
            // First row headers
            string[] headers = {
            "Emp. ID", "Name", "Designation", "Leave ID", "Leave Format",
            "Approval Status", "Leave Type", "From Date", "To Date",
            "Date(s)", "No. of Day(s)", "1st/2nd Half"
        };

            // Add regular headers
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[startRow, i + 1, startRow + 1, i + 1].Merge = true;
                var cell = worksheet.Cells[startRow, i + 1];
                cell.Value = headers[i];
                cell.Style.WrapText = true;
            }

            // Add Short Leave section
            worksheet.Cells[startRow, 13, startRow, 15].Merge = true;
            worksheet.Cells[startRow, 13].Value = "Short Leave";

            // Add Short Leave sub-headers
            worksheet.Cells[startRow + 1, 13].Value = "From";
            worksheet.Cells[startRow + 1, 14].Value = "To";
            worksheet.Cells[startRow + 1, 15].Value = "Time";

            // Add remaining headers
            string[] remainingHeaders = {
            "Reason", "IS App. Status", "IS App. Remarks",
            "HOD App. Status", "HOD App. Remarks",
            "HR App. Status", "HR App. Remarks"
        };

            for (int i = 0; i < remainingHeaders.Length; i++)
            {
                worksheet.Cells[startRow, i + 16, startRow + 1, i + 16].Merge = true;
                var cell = worksheet.Cells[startRow, i + 16];
                cell.Value = remainingHeaders[i];
                cell.Style.WrapText = true;
            }

            // Style all headers
            var headerRange = worksheet.Cells[startRow, 1, startRow + 1, 22];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.Size = 10;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            // Adjust row height for headers
            worksheet.Row(startRow).Height = 30;
            worksheet.Row(startRow + 1).Height = 30;

            return startRow + 2; // Return next available row after headers
        }


        private void ComposeFooter(ExcelWorksheet worksheet, int row)
        {
            worksheet.Cells[row, 1].Value = $"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}";

            var pageNumberCell = worksheet.Cells[row, 21, row, 22];
            pageNumberCell.Merge = true;
            pageNumberCell.Value = "Page 1 of 1";
            pageNumberCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        }

        private void ApplyGeneralStyling(ExcelWorksheet worksheet, int lastRow)
        {
            var borderRange = worksheet.Cells[1, 1, lastRow, 22];
            borderRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            borderRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            borderRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            borderRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            worksheet.PrinterSettings.FitToPage = true;
            worksheet.PrinterSettings.FitToWidth = 1;
            worksheet.PrinterSettings.FitToHeight = 0;
            worksheet.PrinterSettings.PaperSize = ePaperSize.A4;
            worksheet.PrinterSettings.Orientation = eOrientation.Landscape;
        }

        #endregion

        #region Word

        public byte[] GenerateLeaveWordReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (WordprocessingDocument document = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
                    {
                        MainDocumentPart mainPart = document.AddMainDocumentPart();
                        mainPart.Document = new Word.Document();
                        Word.Body body = mainPart.Document.AppendChild(new Word.Body());

                        // Add header
                        AddHeader(body, companyName, sDate, eDate);

                        // Add content for each department
                        foreach (var department in data)
                        {
                            // Department heading
                            Word.Paragraph deptHeading = new Word.Paragraph(
                                new Word.Run(
                                    new Word.Text($"Department Name: {department.Department}")
                                )
                            );
                            deptHeading.ParagraphProperties = new Word.ParagraphProperties(
                                new Word.SpacingBetweenLines() { Before = "400" }
                            );
                            body.AppendChild(deptHeading);

                            // Create table
                            Word.Table table = new Word.Table();
                            Word.TableProperties tblProps = new Word.TableProperties(
                                new Word.TableBorders(
                                    new Word.TopBorder() { Val = BorderValues.Single },
                                    new Word.BottomBorder() { Val = BorderValues.Single },
                                    new Word.LeftBorder() { Val = BorderValues.Single },
                                    new Word.RightBorder() { Val = BorderValues.Single },
                                    new Word.InsideHorizontalBorder() { Val = BorderValues.Single },
                                    new Word.InsideVerticalBorder() { Val = BorderValues.Single }
                                )
                            );
                            table.AppendChild(tblProps);

                            // Add header row
                            AddTableHeader(table);

                            // Add data rows
                            foreach (var application in department.Data)
                            {
                                AddTableRow(table, application);
                            }

                            body.AppendChild(table);

                            // Add spacing after table
                            body.AppendChild(new Word.Paragraph(new Word.Run()));
                        }

                        // Add footer
                        AddFooter(body);
                    }

                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to generate leave report Word document", ex);
            }
        }

        private void AddHeader(Word.Body body, string companyName, string sDate, string eDate)
        {
            // Company name
            Word.Paragraph companyPara = new Word.Paragraph(
                new Word.Run(new Word.Text(companyName))
            );
            companyPara.ParagraphProperties = new Word.ParagraphProperties(
                new Word.Justification() { Val = Word.JustificationValues.Center }
            );
            body.AppendChild(companyPara);

            // Report title
            Word.Paragraph titlePara = new Word.Paragraph(
                new Word.Run(new Word.Text("Leave Application Report"))
            );
            titlePara.ParagraphProperties = new Word.ParagraphProperties(
                new Word.Justification() { Val = Word.JustificationValues.Center }
            );
            body.AppendChild(titlePara);

            // Date range
            Word.Paragraph datePara = new Word.Paragraph(
                new Word.Run(new Word.Text($"Date: {sDate} - {eDate}"))
            );
            datePara.ParagraphProperties = new Word.ParagraphProperties(
                new Word.Justification() { Val = Word.JustificationValues.Center }
            );
            body.AppendChild(datePara);
        }

        private void AddTableHeader(Word.Table table)
        {
            Word.TableRow headerRow = new Word.TableRow();
            string[] headers = new[]
            {
            "Emp. ID", "Name", "Designation", "Leave ID", "Leave Format",
            "Approval Status", "Leave Type", "From Date", "To Date",
            "Date(s)", "No. of Day(s)", "1st/2nd Half", "From", "To",
            "Time", "Reason", "IS App. Status", "IS App. Remarks",
            "HOD App. Emp. ID Status", "HOD App. Remarks", "HR App. Status",
            "HR App. Remarks"
        };

            foreach (string header in headers)
            {
                Word.TableCell cell = new Word.TableCell(new Word.Paragraph(new Word.Run(new Word.Text(header))));
                cell.TableCellProperties = new Word.TableCellProperties(
                    new Word.TableCellWidth() { Type = Word.TableWidthUnitValues.Auto }
                );
                headerRow.AppendChild(cell);
            }

            table.AppendChild(headerRow);
        }

        private void AddTableRow(Word.Table table, LeaveApplicartionGridVM application)
        {
            var a = "";
            var b = "";
            var c = "";

            if (application.ApplyLeaveFormat == "shortLeave")
            {
                a = Convert.ToDateTime(application.ShortLeaveFrom).ToString("HH:mm"); // 13
                b = Convert.ToDateTime(application.ShortLeaveTo).ToString("HH:mm"); // 14
                c = Convert.ToDateTime(application.ShortLeaveTime).ToString("HH:mm"); // 15
            }

            Word.TableRow row = new Word.TableRow();
            var values = new[]
            {
            application.EmployeeID,
            application.EmployeeFirstName,
            application.DesignationName,
            application.LeaveAppEntryId,
            application.ApplyLeaveFormat,
            application.HODApprovalStatus,
            application.LeaveTypeId,
            application.StartDate.ToString("dd/MM/yyyy"),
            application.EndDate.ToString("dd/MM/yyyy"),
            string.Join(", ", application.Days.Select(date => date.ToString("dd/MM/yyyy"))),
            application.NoOfDay.ToString(),
            application.FirstOrSecondHalf,

            a,b,c,
            //application.ShortLeaveFromStr,
            //application.ShortLeaveToStr,
            //application.ShortLeaveTimeStr,

            //Convert.ToDateTime(application.ShortLeaveFrom).ToString("HH:mm"),
            //Convert.ToDateTime(application.ShortLeaveTo).ToString("HH:mm"),
            //Convert.ToDateTime(application.ShortLeaveTime).ToString("HH:mm"),
            application.Reason,
            application.IsApproved,
            application.Reason,
            application.HODApprovalStatus,
            application.HODApprovalRemarks,
            application.HRApprovalStatus,
            application.HRApprovalRemarks
        };

            foreach (string value in values)
            {
                Word.TableCell cell = new Word.TableCell(new Word.Paragraph(new Word.Run(new Word.Text(value ?? ""))));
                cell.TableCellProperties = new Word.TableCellProperties(
                    new Word.TableCellWidth() { Type = Word.TableWidthUnitValues.Auto }
                );
                row.AppendChild(cell);
            }

            table.AppendChild(row);
        }

        private void AddFooter(Word.Body body)
        {
            Word.Paragraph footerPara = new Word.Paragraph(
                new Word.Run(new Word.Text($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}"))
            );
            body.AppendChild(footerPara);
        }

        public async Task<List<LeaveApplicartionGridVM>> LeaveStatusByEmpId(string id, string status)
        {
            try
            {
                // Fetch main data from the database
                var leaveEntries = await (from l in _context.HrmLeaveApplicationEntry
                                              // join e in _context.HrmEmployee2 on l.EmployeeId equals e.EmployeeId
                                              // join hod in _context.HrmEmployee2 on l.Hod equals hod.EmployeeId
                                          join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode
                                          // join sup in _context.HrmEmployee2 on l.BossEmpAutoId equals sup.EmployeeId
                                          where l.EmployeeId == id && l.HrapprovalStatus == status && l.IsApproved != "Rejected"
                                          && l.HodapprovalStatus != "Rejected"
                                          select new
                                          {
                                              l,
                                              //  e,
                                              //   hod,
                                              type,
                                              //  sup
                                          }).ToListAsync();

                // Fetch all leave days into memory
                var leaveDays = await _context.HrmLeaveApplicationDays
                                              .ToListAsync(); // Bring all data into memory

                // Group leave days by LeaveAppEntryId on the client side
                var groupedLeaveDays = leaveDays
                                       .GroupBy(d => d.LeaveAppEntryId)
                                       .ToDictionary(
                                           g => g.Key,
                                           g => g.Select(d => d.Days).ToList() // Collect days as List<DateTime>
                                       );

                // Combine data in memory
                var result = leaveEntries.Select(data => new LeaveApplicartionGridVM
                {
                    EmployeeID = data.l.EmployeeId,
                    LeaveAppEntryCode = data.l.AutoId,
                    LeaveAppEntryId = data.l.LeaveAppEntryId,
                    //LeaveTypeId = data.l.LeaveTypeId,
                    LeaveTypeId = data.type.ShortName,
                    StartDate = data.l.StartDate,
                    EndDate = data.l.EndDate,
                    NoOfDay = data.l.NoOfDay,
                    ModifyDate = data.l.ModifyDate,
                    ConfirmationRemarks = data.l.ConfirmationRemarks,
                    HODApprovalStatus = data.l.HodapprovalStatus,
                    HODApprovalRemarks = data.l.HrapprovalRemarks,
                    SickLeaveFilePath = data.l.SickLeaveFilePath,
                    HRApprovalStatus = data.l.HrapprovalStatus,
                    HRApprovalRemarks = data.l.HrapprovalRemarks,
                    ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                    //  HODFirstName = data.hod.FirstName,
                    //   SupervisorFirstName = data.sup.FirstName,
                    //  EmployeeFirstName = data.e.FirstName,
                    Reason = data.l.Reason,
                    Days = groupedLeaveDays.ContainsKey(data.l.LeaveAppEntryId)
                                ? groupedLeaveDays[data.l.LeaveAppEntryId]
                                : new List<DateTime>() // Default to empty list if no days found
                }).ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<bool> NotifyBulk(LeaveRequest leaveIds, string status)
        {
            if (leaveIds == null)
            {
                return false;
            }

            foreach (var item in leaveIds.LeaveIds)
            {
                LeaveApprovalViewModel approvalViewModel = new LeaveApprovalViewModel()
                {
                    LeaveAppEntryId = item,
                    ApprovalStatus = status,
                    ConfirmationRemark = "_bulk Approval"
                };

                var a = NotifyMailOnApprove(approvalViewModel);
            }

            return true;
        }

















        #endregion

        #region Other
        public List<CoreBranch> GetBranchesByCompCode(string compCode)
        {
            try
            {
                var result = _branchRepository.All().Where(e => e.CompanyCode == compCode).ToList();
                return result;
            }
            catch (Exception)
            {

                throw;
            }

        }

        public List<HrmDefDepartment> GetDepartmentByCompCode(string compCode)
        {
            try
            {
                var result = _departmentRepository.All().Where(e => e.CompanyCode == compCode).ToList();
                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        //public async List<CoreBranch> GetBranchesByMultiCompCode(List<string> companyCode)
        //{
        //    try
        //    {
        //        var result = await _branchRepository.FindByAsync(e=>e.CompanyCode.Contains(companyCode));
        //        return result;
        //    }
        //    catch (Exception)
        //    {

        //        throw;
        //    }
        //}


        //public async Task<List<CoreBranch>> GetBranchesByMultiCompCode(List<string> companyCodes)
        //{
        //    try
        //    {
        //        var result = await _branchRepository.FindByAsync(e => companyCodes.Any(code => e.CompanyCode.Contains(code)));
        //        return result.ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle the exception as needed
        //        throw;
        //    }
        //}



        //public async Task<List<LeaveApplicartionGridVM>> GetReportArray(LeaveReportArrayViewModel model)
        //{
        //    var connectionString = _configuration.GetConnectionString("ApplicationDbConnection");
        //    using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
        //    {
        //        // Ensure connection is open


        //        try
        //        {

        //            await connection.OpenAsync();

        //            var parameters = new DynamicParameters();

        //            parameters.Add("@DateFrom", model.DateFrom, DbType.Date);
        //            parameters.Add("@DateTo", model.DateTo, DbType.Date);
        //            parameters.Add("@Company", model.Company != null ? string.Join(", ", model.Company) : (object)DBNull.Value, DbType.String);
        //            parameters.Add("@Branch", model.Branch != null ? string.Join(", ", model.Branch) : (object)DBNull.Value, DbType.String);
        //            parameters.Add("@Department", model.Department != null ? string.Join(", ", model.Department) : (object)DBNull.Value, DbType.String);
        //            parameters.Add("@Employee", model.Employee != null ? string.Join(", ", model.Employee) : (object)DBNull.Value, DbType.String);
        //            parameters.Add("@LeaveFormat", model.LeaveFormat != null ? string.Join(", ", model.LeaveFormat) : (object)DBNull.Value, DbType.String);
        //            parameters.Add("@LeaveStatus", model.LeaveStatus != null ? string.Join(", ", model.LeaveStatus) : (object)DBNull.Value, DbType.String);
        //            parameters.Add("@ReportFormat", model.ReportFormat, DbType.String);


        //            string query = BuildQueryString(parameters);

        //            Console.WriteLine($"Generated SQL Query: {query}");

        //            var results = (await connection.QueryAsync<vm>(query, parameters)).ToList();

        //            return results;

        //        }
        //        catch (SqlException sqlEx)
        //        {
        //            // Log the error (for debugging purposes)
        //            Console.WriteLine($"SQL Error: {sqlEx.Message}");
        //            throw;
        //        }
        //        catch (Exception ex)
        //        {
        //            // Log unexpected errors
        //            Console.WriteLine($"Unexpected Error: {ex.Message}");
        //            throw;
        //        }
        //        finally
        //        {
        //            // Ensure the connection is closed safely
        //            if (connection.State == ConnectionState.Open)
        //            {
        //                await connection.CloseAsync();
        //            }
        //        }
        //    }

        //}
        #endregion

        #region Proc 

        public async Task<Dictionary<string, CompanyLeaveDataVM>> GetReportArray(LeaveReportArrayViewModel model)
        {
            var connectionString = _configuration.GetConnectionString("ApplicationDbConnection");
            using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    var parameters = new DynamicParameters();
                    parameters.Add("@DateFrom", model.DateFrom == DateTime.MinValue ? (object)DBNull.Value : model.DateFrom, DbType.DateTime);
                    parameters.Add("@DateTo", model.DateTo == DateTime.MinValue ? (object)DBNull.Value : model.DateTo, DbType.DateTime);
                    parameters.Add("@Company", model.Company != null && model.Company.Length > 0 ? string.Join(", ", model.Company) : (object)DBNull.Value, DbType.String);
                    parameters.Add("@Branch", model.Branch != null && model.Branch.Length > 0 ? string.Join(", ", model.Branch) : (object)DBNull.Value, DbType.String);
                    parameters.Add("@Department", model.Department != null && model.Department.Length > 0 ? string.Join(", ", model.Department) : (object)DBNull.Value, DbType.String);
                    parameters.Add("@Employee", model.Employee != null && model.Employee.Any() ? string.Join(", ", model.Employee) : (object)DBNull.Value, DbType.String);
                    parameters.Add("@LeaveFormat", model.LeaveFormat != null && model.LeaveFormat.Length > 0 ? string.Join(", ", model.LeaveFormat) : (object)DBNull.Value, DbType.String);
                    parameters.Add("@LeaveStatus", model.LeaveStatus != null && model.LeaveStatus.Length > 0 ? string.Join(", ", model.LeaveStatus) : (object)DBNull.Value, DbType.String);
                    parameters.Add("@ReportType", "Report", DbType.String);

                    var results = await connection.QueryAsync<LeaveDetailVM>(
                        "GetLeaveReport100",
                        parameters,
                        commandType: CommandType.StoredProcedure
                    );

                    // Transform flat results into hierarchical structure
                    var groupedData = new Dictionary<string, CompanyLeaveDataVM>();




                    foreach (var item in results)
                    {
                        if ((double)item.NoOfDay > 0.5)
                        {
                            var dayRes = _leaveDayRepository.FindBy(ld => ld.LeaveAppEntryId == item.LeaveAppEntryId)
                                .Select(ld => ld.Days.ToString("dd/MM/yyyy"))
                                .ToList();
                            item.DaysStr = dayRes;
                        }

                        var companyKey = string.IsNullOrWhiteSpace(item.CompanyName) ? "Unknown Company" : item.CompanyName;
                        if (!groupedData.ContainsKey(companyKey))
                        {
                            groupedData[companyKey] = new CompanyLeaveDataVM
                            {
                                CompanyCode = companyKey, // Match commented code
                                DepartmentData = new Dictionary<string, DepartmentLeaveDataVM>()
                            };
                        }

                        var companyData = groupedData[companyKey];
                        var departmentKey = string.IsNullOrWhiteSpace(item.DepartmentName) ? "Unknown Department" : item.DepartmentName;
                        if (!companyData.DepartmentData.ContainsKey(departmentKey))
                        {
                            companyData.DepartmentData[departmentKey] = new DepartmentLeaveDataVM
                            {
                                DepartmentName = departmentKey, // Match commented code
                                LeaveDetails = new List<LeaveDetailVM>()
                            };
                        }

                        var departmentData = companyData.DepartmentData[departmentKey];
                        departmentData.LeaveDetails.Add(item);
                    }

                    return groupedData;
                }
                catch (SqlException sqlEx)
                {
                    Console.WriteLine($"SQL Error: {sqlEx.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected Error: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
        }




        private string BuildQueryString(DynamicParameters parameters)
        {
            // Base SQL stored procedure call with dynamic parameters concatenated
            var queryBuilder = new StringBuilder();
            queryBuilder.Append("EXEC GetLeaveReport100 ");

            // Loop through all parameters and append them to the query
            foreach (var param in parameters.ParameterNames)
            {
                var value = parameters.Get<object>(param) ?? "NULL"; // Get the parameter value

                if (value == "NULL")
                {
                    queryBuilder.Append($"@{param} = {value}, ");
                }
                else
                {
                    queryBuilder.Append($"@{param} = '{value}', ");
                }



            }

            // Remove the last comma and space
            if (queryBuilder.Length > 0)
            {
                queryBuilder.Length -= 2;
            }

            return queryBuilder.ToString();
        }



        #endregion

        #region Multi DD

        public async Task<List<CoreBranch>> GetBranchesByMultiCompCode(List<string> companyCodes, bool isAll)
        {
            try
            {
                if (isAll)
                {
                    var result1 = await _branchRepository.AllAsync();

                    return result1.ToList();
                }
                var result = await _branchRepository.FindByAsync(e => companyCodes.Contains(e.CompanyCode));

                return result.ToList();
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<List<HrmDefDepartment>> GetDeptByMultiCompBranch(List<string> companyCodes, List<string> branchCodes, bool isAll)
        {
            try
            {
                if (companyCodes != null && companyCodes.Count > 0 && companyCodes[0] == null)
                {
                    companyCodes.RemoveAt(0);
                }

                if (branchCodes != null && branchCodes.Count > 0 && branchCodes[0] == null)
                {
                    branchCodes.RemoveAt(0);
                }



                var c = companyCodes.Count();
                var b = branchCodes.Count();

                if (isAll)
                {
                    var result1 = await _departmentRepository.AllAsync();

                    return result1.ToList();
                }

                var query = (from off in empOfficalRepository.All().AsNoTracking()
                             join dep in _departmentRepository.All().AsNoTracking()
                             on off.DepartmentCode equals dep.DepartmentCode into depOffice
                             from dep in depOffice.DefaultIfEmpty()
                             select new { off, dep });

                if (c == 0 && b != 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode));
                }
                else if (c != 0 && b == 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode));
                }
                else if (c != 0 && b != 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode));
                }



                var result = await query.Select(q => new HrmDefDepartment
                {
                    DepartmentCode = q.dep.DepartmentCode,
                    DepartmentName = q.dep.DepartmentName,
                    DepartmentShortName = q.dep.DepartmentShortName
                }).Distinct().ToListAsync();

                return result;

                ////var result = await _departmentRepository.FindByAsync(e => companyCodes.Contains(e.CompanyCode));
                //var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                    join dep in _departmentRepository.All().AsNoTracking()
                //                    on off.DepartmentCode equals dep.DepartmentCode
                //                    into depOffice
                //                    from dep in depOffice.DefaultIfEmpty()
                //                    where companyCodes.Contains(off.CompanyCode) && branchCodes.Contains(off.BranchCode)
                //                    select new HrmDefDepartment
                //                    {
                //                        DepartmentCode = dep.DepartmentCode,
                //                        DepartmentName = dep.DepartmentName,
                //                        DepartmentShortName = dep.DepartmentShortName
                //                    }).Distinct().ToListAsync();

                //return result.ToList();
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<List<EmpInfoViewModel>> GetEmpMultiCompBranchDept(List<string> companyCodes, List<string> branchCodes, List<string> departmentCode, bool isAll)
        {
            try
            {


                if (isAll)
                {
                    var result1 = await (from e in empRepository.All().AsNoTracking()
                                         select new EmpInfoViewModel
                                         {
                                             EmployeeFirstName = e.FirstName + " " + e.LastName,
                                             EmployeeId = e.EmployeeId
                                         }).ToListAsync();

                    return result1;
                }


                if (companyCodes != null && companyCodes.Count > 0 && companyCodes[0] == null)
                {
                    companyCodes.RemoveAt(0);
                }

                if (branchCodes != null && branchCodes.Count > 0 && branchCodes[0] == null)
                {
                    branchCodes.RemoveAt(0);
                }

                if (departmentCode != null && departmentCode.Count > 0 && departmentCode[0] == null)
                {
                    departmentCode.RemoveAt(0);
                }

                var c = companyCodes.Count();
                var b = branchCodes.Count();
                var d = departmentCode.Count();



                var query = from off in empOfficalRepository.All().AsNoTracking()
                            join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                            from emp in offEmp.DefaultIfEmpty()
                            select new { off, emp };

                if (c == 0 && b == 0 && d != 0)
                {
                    query = query.Where(q => departmentCode.Contains(q.off.DepartmentCode));
                }
                else if (c == 0 && b != 0 && d == 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode));
                }
                else if (c == 0 && b != 0 && d == 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode));
                }
                else if (c != 0 && b == 0 && d == 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode));
                }
                else if (c != 0 && b != 0 && d == 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode));
                }
                else if (c == 0 && b != 0 && d != 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode) && departmentCode.Contains(q.off.DepartmentCode));
                }
                else if (c != 0 && b == 0 && d != 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && departmentCode.Contains(q.off.DepartmentCode));
                }
                else if (c != 0 && b != 0 && d != 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode) && departmentCode.Contains(q.off.DepartmentCode));
                }

                var result = await query.Select(q => new EmpInfoViewModel
                {
                    EmployeeFirstName = q.emp.FirstName + " " + q.emp.LastName,
                    EmployeeId = q.emp.EmployeeId
                }).ToListAsync();

                return result;


                #region Backup

                //if (c == 0 && b  == 0 && d != 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where departmentCode.Contains(off.DepartmentCode)
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                     ).ToListAsync();

                //    return result;
                //}
                //else if (c == 0 && b != 0 && d == 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where  branchCodes.Contains(off.BranchCode) 
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //     ).ToListAsync();

                //    return result;
                //}
                //else if (c == 0 && b != 0 && d == 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where branchCodes.Contains(off.BranchCode) 
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                         ).ToListAsync();

                //    return result;
                //}
                //else if (c != 0 && b == 0 && d == 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where companyCodes.Contains(off.CompanyCode) 
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                         ).ToListAsync();

                //    return result;
                //}

                //else if (c != 0 && b != 0 && d == 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where companyCodes.Contains(off.CompanyCode) && branchCodes.Contains(off.BranchCode) 
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                         ).ToListAsync();

                //    return result;
                //}
                //else if (c == 0 && b != 0 && d != 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where branchCodes.Contains(off.BranchCode) && departmentCode.Contains(off.DepartmentCode)
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                         ).ToListAsync();

                //    return result;
                //}
                //else if (c != 0 && b == 0 && d != 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where companyCodes.Contains(off.CompanyCode)  && departmentCode.Contains(off.DepartmentCode)
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                         ).ToListAsync();

                //    return result;
                //}

                //else if (c != 0 && b != 0 && d != 0)
                //{
                //    var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                        join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //                        from emp in offEmp.DefaultIfEmpty()
                //                        where companyCodes.Contains(off.CompanyCode) && branchCodes.Contains(off.BranchCode) && departmentCode.Contains(off.DepartmentCode)
                //                        select new EmpInfoViewModel
                //                        {
                //                            EmployeeFirstName = emp.FirstName + " " + emp.LastName,
                //                            EmployeeId = emp.EmployeeId
                //                        }
                //                         ).ToListAsync();

                //    return result;
                //}



                //var result = await (from off in empOfficalRepository.All().AsNoTracking()
                //                    join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp from emp in offEmp.DefaultIfEmpty()
                //                    where companyCodes.Contains(off.CompanyCode) && branchCodes.Contains(off.BranchCode) && departmentCode.Contains(off.DepartmentCode)
                //                    select new EmpInfoViewModel
                //                    {
                //                        EmployeeFirstName = emp.FirstName +" "+ emp.LastName,
                //                        EmployeeId = emp.EmployeeId
                //                    }
                //                     ).ToListAsync();

                //return result;

                #endregion
            }
            catch (Exception)
            {

                throw;
            }
        }


        public async Task<List<HrmDefDesignation>> GetDesigMultiCompBranchDept(List<string> companyCodes, List<string> branchCodes, List<string> departmentCode, bool isAll)
        {
            try
            {


                if (isAll)
                {
                    var result1 = await (from e in _designationRepository.All().AsNoTracking()
                                         select new HrmDefDesignation
                                         {
                                             DesignationName = e.DesignationName,
                                             DesignationCode = e.DesignationCode
                                         }).Distinct().ToListAsync();

                    return result1;
                }


                if (companyCodes != null && companyCodes.Count > 0 && companyCodes[0] == null)
                {
                    companyCodes.RemoveAt(0);
                }

                if (branchCodes != null && branchCodes.Count > 0 && branchCodes[0] == null)
                {
                    branchCodes.RemoveAt(0);
                }

                if (departmentCode != null && departmentCode.Count > 0 && departmentCode[0] == null)
                {
                    departmentCode.RemoveAt(0);
                }

                var c = companyCodes.Count();
                var b = branchCodes.Count();
                var d = departmentCode.Count();



                //var query = from off in empOfficalRepository.All().AsNoTracking()
                //            join emp in empRepository.All().AsNoTracking() on off.EmployeeId equals emp.EmployeeId into offEmp
                //            from emp in offEmp.DefaultIfEmpty()
                //            select new { off, emp };

                var query = (from off in empOfficalRepository.All().AsNoTracking()
                             join des in _designationRepository.All().AsNoTracking()
                             on off.DesignationCode equals des.DesignationCode into desOffice
                             from des in desOffice.DefaultIfEmpty()
                             select new { off, des });

                if (c == 0 && b == 0 && d != 0)
                {
                    query = query.Where(q => departmentCode.Contains(q.off.DepartmentCode));
                }
                else if (c == 0 && b != 0 && d == 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode));
                }
                else if (c == 0 && b != 0 && d == 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode));
                }
                else if (c != 0 && b == 0 && d == 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode));
                }
                else if (c != 0 && b != 0 && d == 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode));
                }
                else if (c == 0 && b != 0 && d != 0)
                {
                    query = query.Where(q => branchCodes.Contains(q.off.BranchCode) && departmentCode.Contains(q.off.DepartmentCode));
                }
                else if (c != 0 && b == 0 && d != 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && departmentCode.Contains(q.off.DepartmentCode));
                }
                else if (c != 0 && b != 0 && d != 0)
                {
                    query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode) && departmentCode.Contains(q.off.DepartmentCode));
                }

                var result = await query.Select(e => new HrmDefDesignation
                {
                    DesignationName = e.des.DesignationName,
                    DesignationCode = e.des.DesignationCode
                }).Distinct().ToListAsync();

                return result;
            }
            catch (Exception ex)
            {

                throw;
            }

        }




        #endregion

    }
}
