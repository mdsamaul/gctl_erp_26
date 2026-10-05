using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeavePendingViewModel
    {
        public string EmployeeID { get; set; }
        public string LeaveAppEntryId { get; set; }
        public string LeaveTypeId { get; set; }
        public string EmployeeFirstName { get; set; }
        public string DesignationName { get; set; }
        public decimal LeaveAppEntryCode { get; set; }
        public string IsApprove { get; set; }
        public string HodApprove { get; set; }
        public string HrApprove { get; set; }
        public string IsName { get; set; }
        public string HodName { get; set; }
        public string Reason { get; set; }
        public decimal NoOfDay { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<DateTime> Days { get; set; }
        public string NullHelper { get; set; }
        public string LeaveFormat { get; set; }
        public string LeaveTypeName { get; set; }
    }
}
