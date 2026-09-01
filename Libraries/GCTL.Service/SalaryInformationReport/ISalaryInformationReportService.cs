// GCTL.Service.SalaryInformationReport/ISalaryInformationReportService.cs

using GCTL.Core.ViewModels.SalaryInformationReport;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GCTL.Service.SalaryInformationReport
{
    public interface ISalaryInformationReportService
    {
     
        Task<List<SalaryInformationReportDto>> GetPayrollMasterFileAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportGratuityDto>> GetPayrollMasterFileGratuityAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelGratuityAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportYearlyBonusDto>> GetPayrollMasterFileYearlyBonusAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelYearlyBonusAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportExtraDayDto>> GetPayrollMasterFileExtraDayAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelExtraDayAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportTerminatedDto>> GetPayrollMasterFileTerminatedAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelTerminatedAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportIncentivesDto>> GetPayrollMasterFileIncentivesAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelIncentivesAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportNewHireDto>> GetPayrollMasterFileNewHireAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelNewHireAsync(SalaryInformationReportFilterDto filter);
        Task<List<SalaryInformationReportWfhDto>> GetPayrollMasterFileWfhAsync(SalaryInformationReportFilterDto filter);
        Task<byte[]> ExportToExcelWfhAsync(SalaryInformationReportFilterDto filter);
    }
}