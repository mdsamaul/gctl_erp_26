using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;

namespace GCTL.Service.LeaveAppDay
{
    public interface ILeaveApplicationDay
    {
        Task<bool> SaveAsync(LeaveApplicationViewModel model);
        Task<bool> UpdateAsync(LeaveApplicationViewModel model);
        Task<bool> DeleteAsync(string id);
    }
}
