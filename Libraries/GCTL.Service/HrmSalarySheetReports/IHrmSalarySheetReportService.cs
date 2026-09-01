using System.Collections.Generic;
using System.Threading.Tasks;
using GCTL.Core.ViewModels.HrmSalarySheetReports;

namespace GCTL.Service.HrmSalarySheetReports
{
    public interface IHrmSalarySheetReportService
    {
        Task<List<HrmSalarySheetReportVM>> GetSalarySheetDataAsync(SalarySheetFilterData filter);
        byte[] GenerateSalarySheetPdf(List<HrmSalarySheetReportVM> data);
        byte[] GenerateSalarySheetExcel(List<HrmSalarySheetReportVM> data);
        Task<bool> PagePermissionAsync(string accessCode);
    }
}
