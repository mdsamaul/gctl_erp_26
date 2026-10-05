using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    
    public class LeaveReportArrayViewModel
    {
        public string[] Company { get; set; }
      
        public string[] Branch { get; set; }
        public string[] Department { get; set; }
        //public string[] Employee { get; set; }
        public IEnumerable< string> Employee { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string DateToStr { get; set; }
        public string DateFromStr { get; set; }


        public string[] LeaveFormat { get; set; }
        public string[] LeaveStatus { get; set; }
        public string ReportFormat { get; set; }
    }
}
