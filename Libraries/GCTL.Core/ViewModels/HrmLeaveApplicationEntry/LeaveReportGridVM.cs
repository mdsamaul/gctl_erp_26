using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveReportGridVM
    {
        public string Department { get; set; }
        public List<LeaveApplicartionGridVM> Data { get; set; }
    }
}
