using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;

namespace GCTL.Service.EmailService
{
    public interface ILeaveEmailService
    {
        string GenerateLeaveRequestEmail(LeaveApplicationViewModel leaveApplication);
        Task<string> GenerateLeaveRejectionEmail(LeaveEntryGetViewModel leaveApplication , string empName, string reMark);
        string GenerateLeaveApprovalEmail(LeaveEntryGetViewModel leaveApplication);
        Task<string> GenerateNotifyHrEmail(LeaveApplicationViewModel leaveApplication,   LeaveBalanceViewModel leaveBalDetails, bool isFirstNotify, HrmEmployeeOfficialInfo resEmp);
        Task<string> HrEmailForWard(LeaveApplicationViewModel leaveApplication, string leaveStatus);
        Task<string> GenerateSecondNotifyHrEmail(LeaveEntryGetViewModel leave, LeaveBalanceViewModel leaveBalDetails, bool isSecondNotify, HrmEmployeeOfficialInfo resEmp);
        string GenerateLeaveApprovalOfcNotify(LeaveEntryGetViewModel leave, HrmEmployeeOfficialInfo resEmpOfc);
        string GenerateLeaveCancelEmail(LeaveEntryGetViewModel leaveApplication , string cancelledBy);
        string GenerateLeaveCancelOfcNotify(LeaveEntryGetViewModel leave, HrmEmployeeOfficialInfo hrOfc);
    }
}
