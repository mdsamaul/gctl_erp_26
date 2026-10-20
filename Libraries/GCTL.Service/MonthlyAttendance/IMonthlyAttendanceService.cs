// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Service/MonthlyAttendance/IMonthlyAttendanceService.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels.MonthlyAttendance;
using System.Threading.Tasks;

namespace GCTL.Service.MonthlyAttendance
{
    public interface IMonthlyAttendanceService
    {
        Task<bool> PagePermissionAsync(string accessCode);
        Task<MonthlyAttendanceResultDto> GetReportDataAsync(MonthlyAttendanceFilterDto filter);
        Task<byte[]> ExportExcelAsync(MonthlyAttendanceFilterDto filter, string? logoPhysicalPath = null);
        Task<byte[]> GeneratePdfAsync(MonthlyAttendanceFilterDto filter, string? logoPhysicalPath = null);
        Task<byte[]> ExportCsvAsync(MonthlyAttendanceFilterDto filter);
    }
}
