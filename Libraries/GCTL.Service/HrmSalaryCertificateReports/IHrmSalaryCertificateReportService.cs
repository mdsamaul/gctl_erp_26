
using GCTL.Core.ViewModels.HrmSalaryCertificateReports;

namespace GCTL.Service.HrmSalaryCertificateReports
{
    public interface IHrmSalaryCertificateReportService
    {
        Task<List<HrmSalaryCertificateVM>> GetPaySlipDataAsync(SalaryCertificateFilterData filter);
        Task<bool> PagePermissionAsync(string accessCode);
        byte[] GenerateSalaryCertificatePdf(List<HrmSalaryCertificateVM> data);
        byte[] GenerateSalaryCertificateExcel(List<HrmSalaryCertificateVM> data);
    }
}
