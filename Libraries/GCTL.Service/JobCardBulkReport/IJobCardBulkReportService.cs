// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Service/JobCardBulkReport/IJobCardBulkReportService.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels.JobCardBulkReport;
using System.Threading.Tasks;

namespace GCTL.Service.JobCardBulkReport
{
    public interface IJobCardBulkReportService
    {
        Task<bool> PagePermissionAsync(string accessCode);
        Task<JobCardBulkReportResultDto> GetReportDataAsync(JobCardBulkReportFilterDto filter);
        Task<byte[]> GeneratePdfAsync(JobCardBulkReportFilterDto filter, string? logoPhysicalPath = null);
        Task<byte[]> ExportExcelAsync(JobCardBulkReportFilterDto filter, string? logoPhysicalPath = null);
        Task<byte[]> ExportCsvAsync(JobCardBulkReportFilterDto filter);
    }
}
