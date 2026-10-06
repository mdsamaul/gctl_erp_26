// ═══════════════════════════════════════════════════════════════════
// File: GCTL.UI.Core/ViewModels/JobCardReport/JobCardReportViewModel.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.JobCardReport;

namespace GCTL.UI.Core.ViewModels.JobCardReport
{
    public class JobCardReportViewModel : BaseViewModel
    {
        public JobCardReportFilterDto Setup { get; set; } = new JobCardReportFilterDto();
    }
}
