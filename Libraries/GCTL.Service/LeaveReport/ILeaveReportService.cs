using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;

namespace GCTL.Service.LeaveReport
{
    public interface ILeaveReportService
    {
        // ─── Permissions ─────────────────────────────────────────────────────
        Task<bool> HasPagePermissionAsync(string accessCode, string title);
        Task<bool> HasPrintPermissionAsync(string accessCode, string title);

        // ─── Report Data ──────────────────────────────────────────────────────
        /// <summary>
        /// Runs GetLeaveReport100 stored proc with safe Dapper parameters.
        /// Returns data grouped by Company → Department.
        /// </summary>
        Task<Dictionary<string, CompanyLeaveDataVM>> GetReportArrayAsync(LeaveReportArrayViewModel model);

        /// <summary>
        /// EF Core query with in-DB filtering (no load-all-then-filter).
        /// </summary>
        Task<List<LeaveApplicartionGridVM>> GetReportAsync(LeaveReportViewModel model);


        Task<List<LeaveApplicartionGridVM>> GetLeaveReportArrayAsync(LeaveReportArrayViewModel model);

        // ─── PDF ──────────────────────────────────────────────────────────────
        byte[] GeneratePdfReportArray(Dictionary<string, CompanyLeaveDataVM> data, string sdate, string edate);
        byte[] GeneratePdfReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate);

        // ─── Excel ────────────────────────────────────────────────────────────
        byte[] GenerateExcelReportArray(Dictionary<string, CompanyLeaveDataVM> data, string sdate, string edate);
        byte[] GenerateExcelReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate);

        // ─── Word ─────────────────────────────────────────────────────────────
        byte[] GenerateWordReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate);

        // ─── Dropdowns ────────────────────────────────────────────────────────
        Task<List<CoreBranch>> GetBranchesByMultiCompAsync(List<string> companyCodes, bool isAll);
        Task<List<HrmDefDepartment>> GetDeptByMultiCompBranchAsync(List<string> companyCodes, List<string> branchCodes, bool isAll);
        Task<List<EmpInfoViewModel>> GetEmpByMultiCompBranchDeptAsync(List<string> companyCodes, List<string> branchCodes, List<string> departmentCodes, bool isAll);
    }
}
