using GCTL.Core.ViewModels.HrmPaySlipReports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Service.HrmPaySlipReports
{
    public interface IHrmPaySlipReportService
    {
        Task<List<HrmPaySlipReportVM>> GetPaySlipDataAsync(PaySlipFilterData filter);
        Task<bool> PagePermissionAsync(string accessCode);
        byte[] GeneratePaySlipPdf(List<HrmPaySlipReportVM> data);
        byte[] GeneratePaySlipExcel(List<HrmPaySlipReportVM> data);
    }
}
