// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Service/JobCardReport/IJobCardReportService.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels.JobCardReport;
using System.Threading.Tasks;

namespace GCTL.Service.JobCardReport
{
    public interface IJobCardReportService
    {
        Task<bool> PagePermissionAsync(string accessCode);
        Task<JobCardReportResultDto> GetReportDataAsync(JobCardReportFilterDto filter);
        Task<byte[]> ExportExcelAsync(JobCardReportFilterDto filter, string? logoPhysicalPath = null);
        Task<byte[]> ExportCsvAsync(JobCardReportFilterDto filter);
    }
}
