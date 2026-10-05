using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveRequest : BaseViewModel 
    { 
        public List<string> LeaveIds { get; set; } 
        public string? Remarks { get; set; } 
    }

}
