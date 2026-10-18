// ═══════════════════════════════════════════════════════════════════
// File: GCTL.UI.Core/ViewModels/JobCardBulkReport/JobCardBulkReportViewModel.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.JobCardBulkReport;

namespace GCTL.UI.Core.ViewModels.JobCardBulkReport
{
    public class JobCardBulkReportViewModel : BaseViewModel
    {
        public JobCardBulkReportFilterDto Setup { get; set; } = new JobCardBulkReportFilterDto();
    }
}
