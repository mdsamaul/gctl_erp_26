using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveEntryGetViewModel
    {
        public decimal LeaveAppEntryCode { get; set; }
        public string LeaveAppEntryId { get; set; }
        public string EmployeeId { get; set; }
        public string LeaveTypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal NoOfDay { get; set; }
        public string HalfDay { get; set; }
        public string FirstOrSecondHalf { get; set; }
        public string Reason { get; set; }
        public string BossEmpAutoId { get; set; }
        public string HOD { get; set; }
        public string IsApproved { get; set; }
        public string Luser { get; set; }
        public DateTime? Ldate { get; set; }
        public string LIP { get; set; }
        public string LMAC { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ConfirmationRemarks { get; set; }
        public string CompanyCode { get; set; }
        public string HODApprovalStatus { get; set; }
        public string HODApprovalRemarks { get; set; }
        public string HRApprovalStatus { get; set; }
        public string HRApprovalRemarks { get; set; }
        public string ApplyLeaveFormat { get; set; }
        public DateTime? ShortLeaveFrom { get; set; }
        public DateTime? ShortLeaveTo { get; set; }
        public TimeOnly? ShortLeaveTime { get; set; }
        public decimal? ShortLeaveTimeStr { get; set; }
        public string LeaveApplyProcess { get; set; }
        public string SickLeaveFilePath { get; set; }
        public List<DateTime> Days { get; set; } = new List<DateTime>();
        public string EmployeeName { get; set; }
    }

}
