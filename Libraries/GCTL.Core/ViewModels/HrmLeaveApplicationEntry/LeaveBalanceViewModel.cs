using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveBalanceViewModel
    {
        public string ShortName { get; set; }
        public string LeaveTypeCode { get; set; }
        public decimal AvailableLeave { get; set; }
        public decimal TotalLeaveTaken { get; set; }
        public decimal RemainingLeave { get; set; }
        public double TotalShortLeaveTimeHours { get; set; }
        public object TotalShortLeaveTime { get; set; }
        public DateTime? JoiningDate { get; set; }
        public string LeaveName { get; set; }
    }
}
