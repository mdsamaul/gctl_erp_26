using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveApplicartionGridVM : BaseViewModel
    {
      
        public string EmployeeID { get; set; }
        public decimal LeaveAppEntryCode { get; set; }
        public string LeaveAppEntryId { get; set; }
        public string LeaveTypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal NoOfDay { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ConfirmationRemarks { get; set; }
        public string HODApprovalStatus { get; set; }
        public string HODApprovalRemarks { get; set; }
        public string SickLeaveFilePath { get; set; }
        public string HRApprovalStatus { get; set; }
        public string HRApprovalRemarks { get; set; }
        public string ApplyLeaveFormat { get; set; }
        public string HODFirstName { get; set; }
        public string SupervisorFirstName { get; set; }
        public string EmployeeFirstName { get; set; }
        public string Reason { get; set; }
        public List<DateTime> Days { get; set; } // Changed to a list of integers
        public List<string> DaysStr { get; set; } // Changed to a list of integers
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
        public DateTime? ShortLeaveFrom { get; set; }
        public DateTime? ShortLeaveTo { get; set; }
        public TimeOnly? ShortLeaveTime { get; set; }
       // public string? ShortLeaveTime { get; set; }
        public string IsApproved { get; set; }
        public string FirstOrSecondHalf { get; set; }


        public string? ShortLeaveFromStr { get; set; }
        public string? ShortLeaveToStr { get; set; }
        public string? ShortLeaveTimeStr { get; set; }
        public int CountTotal { get; set; }
    }
}
