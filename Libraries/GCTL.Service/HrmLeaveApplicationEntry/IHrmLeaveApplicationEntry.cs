using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GCTL.Core.DataTables;
using GCTL.Core.ViewModels.Common;
using GCTL.Core.ViewModels.DeleteHistories;
using GCTL.Core.ViewModels.HrmEmployees2;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;

namespace GCTL.Service.HrmLeaveApplicationEntrys
{
    public interface IHrmLeaveApplicationEntry
    {
        byte[] GenerateLeavePdfReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate);
        byte[] GenerateLeaveExcelReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate);
        byte[] GenerateLeaveWordReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate);
        Task<List<LeaveApplicartionGridVM>> GetAllAsync();
        //Task<object> GetPagedAsync(int draw, int start, int length, string searchValue);
        Task<object> GetPagedAsync(int page, int pageSize, string searchValue, string sortColumn, string sortDir, string empId);
        Task<List<LeaveApplicartionGridVM>> GetApprovedLeaveByEmpID(string empId, string status);
        Task<List<LeaveApplicartionGridVM>> GetApprovedAPAAsync(string id, DataTableParams? parameters);
        Task<List<LeavePendingViewModel>> GetPendingLeaveByHodIs(string Id);
        Task<List<LeaveApplicartionGridVM>> GetApprovedLeaveByHodIs(string Id);
        Task<List<LeavePendingViewModel>> GetPendingLeaveForHR();
        Task<bool> SaveLeaveApproval(LeaveApprovalViewModel model);

        Task<bool> SaveAsync(LeaveApplicationViewModel model , bool IsManual);
        bool CheckDuplicate(LeaveApplicationViewModel model);

        Task<CommonReturn> UpdateAsync(LeaveApplicationViewModel model);
        Task<string> GetHrIDasync();

        Task<bool> DeleteLeaveApplication(string id, DeleteHistoryViewModel dm);

        Task<HrmLeaveApplicationEntry> GetByIdAsync(string id);

        Task<HrmLeaveApplicationEntry> GetLeaveDtailsByIdAsync(string id);
        Task<LeaveEntryGetViewModel> GetLeaveEntriesByEmpCodeAsync(string empId);
        Task<LeaveEntryGetViewModel> GetLeaveEntriesAsync(string id);
       
        Task<LeaveEntryGetViewModel> GetLeaveEntriesByEntryId(string LeaveEntryid);
        Task<LeaveBalanceViewModel> LeaveBalanceByLeaveIdEmpId(string LeaveTypeId, string EmpId);
        Task<List<LeaveBalanceViewModel>> LeaveBalanceByEmpId(string id);
        Task<List<LeaveBalanceViewModel>> LeaveBalanceYearlyByEmpId(string id);
        Task<List<LeaveApplicartionGridVM>> LeaveTakenByEmpId(string id);
        Task<List<LeaveApplicartionGridVM>> LeaveStatusByEmpId(string id, string status);
        Task<CommonReturn> LeaveValidation(LeaveApplicationViewModel? model);
        Task<List<LeaveApplicartionGridVM>> GetReport(LeaveReportViewModel model);
        Task<string> GenerateLeaveApplicationEntryID();
        Task<List<HrmEmployee2SetUpViewModel>> GetByCompCodeAsync(string compCode);

        #region Permission
        Task<bool> PagePermissionAsync(string accessCode);
        Task<bool> SavePermissionAsync(string accessCode);
        Task<bool> UpdatePermissionAsync(string accessCode);
        Task<bool> DeletePermissionAsync(string accessCode);


        Task<bool> PagePermissionUniversal(string accessCode, string tittle);
        Task<bool> SavePermissionUniversal(string accessCode, string tittle);
        Task<bool> UpdatePermissionUniversal(string accessCode, string tittle);
        Task<bool> DeletePermissionUniversal(string accessCode, string tittle);
        Task<bool> PrintPermissionUniversal(string accessCode, string leaveEntryReport);



        Task<List<CoreCompany>> GetCompanieListAsync();



        #endregion

        Task<bool> SaveBulkApproval(LeaveRequest leaveIds);
        Task<bool> SaveBulkReject(LeaveRequest leaveIds);
        Task<bool> NotifyMailAsync(LeaveApplicationViewModel model);
        Task<bool> NotifyMailOnApply(LeaveApplicationViewModel model);
        Task<bool> NotifyMailOnApprove(LeaveApprovalViewModel model);
        Task<bool> ValidRequest(string leaveId, int apvStage);
        Task <bool> NotifyBulk(LeaveRequest leaveIds, string v);



        List<HrmDefDepartment> GetDepartmentByCompCode(string compCode);
        List<CoreBranch> GetBranchesByCompCode(string compCode);
       
        //Task<List<LeaveApplicartionGridVM>> GetReportArray(LeaveReportArrayViewModel model);
        Task<Dictionary<string, CompanyLeaveDataVM>> GetReportArray(LeaveReportArrayViewModel model);
        byte[] GenerateLeavePdfReportArray(Task<Dictionary<string, CompanyLeaveDataVM>> data, string sdate, string edate);
       
        byte[] GenerateLeaveExcelReportArray(Task<Dictionary<string, CompanyLeaveDataVM>> data, string sdate, string edate);


        #region Multi DD

        Task<List<CoreBranch>> GetBranchesByMultiCompCode(List<string> companyCodes, bool isAll);
        Task<List<HrmDefDepartment>> GetDeptByMultiCompBranch(List<string> companyCodes, List<string> branchCodes, bool isAll);
        Task<List<EmpInfoViewModel>> GetEmpMultiCompBranchDept(List<string> companyCodes, List<string> branchCodes, List<string> departmentCode, bool isAll);
        Task<List<HrmDefDesignation>> GetDesigMultiCompBranchDept(List<string> companyCodes, List<string> branchCodes, List<string> departmentCode, bool isAll);

        #endregion

        Task<object> GetByCompCodePagedAsync(string compCode, string search, int page, int pageSize = 20);

    }
}
