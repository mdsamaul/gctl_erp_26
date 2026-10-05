using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class CompanyLeaveDataVM
    {
        public string CompanyCode { get; set; }
        public Dictionary<string, DepartmentLeaveDataVM> DepartmentData { get; set; } = new();
    }
}
