using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.Dashboard;
using GCTL.Core.ViewModels.EachGcFilterRequest;
using GCTL.Core.ViewModels.HrmLeaveSummaryReports;

namespace GCTL.Service.HrmLeaveSummaryReports
{
    public interface IHrmLeaveSummaryReportService
    {
        Task<LeaveSummaryFilterListViewModel> GetDataAsync(LeaveSummaryFilterViewModel filter);
        Task<PagedResultDto<GcItemDto>> GetEmployeesAsync(LeaveSummaryFilterViewModel req);

        Task<LeaveDashboardResponseDto> GetLeaveDashboardAsync(string companyCode, string branchCode, string departmentCode, int year, int page, int pageSize, string search, string employeeId = null);

        Task<byte[]> GeneratePdfReport(List<LeaveSummaryRowDto> data, List<LeaveTypeDto2> leaveTypes, BaseViewModel model);
        Task<byte[]> GenerateExcelReport(List<LeaveSummaryRowDto> data, List<LeaveTypeDto2> leaveTypes);
        Task<bool> PagePermissionAsync(string accessCode);
    }
}
