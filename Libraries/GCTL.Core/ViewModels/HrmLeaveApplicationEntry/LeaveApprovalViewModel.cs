using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveApprovalViewModel : BaseViewModel
    {
        public string LeaveAppEntryId { get; set; }
        public string ApprovalStatus { get; set; }
        public string ConfirmationRemark { get; set; }
    }

    public class LeaveSummaryPageViewModel : BaseViewModel
    {
        public string PageUrl { get; set; }
    }
}
