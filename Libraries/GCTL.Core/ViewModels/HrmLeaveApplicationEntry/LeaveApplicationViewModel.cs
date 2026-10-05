using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveApplicationViewModel : BaseViewModel
    {
        public decimal? LeaveAppEntryCode { get; set; }
        public string? CompanyCode { get; set; }
        public string? EntryId { get; set; }
        public string? EmployeeId { get; set; }
        public string? ImmediateSupervisor { get; set; }
        public string? HeadOfDepartment { get; set; }
        public string? LeaveFormat { get; set; }
        public string? LeaveType { get; set; }
        public string? Reason { get; set; }

        // Full day leave properties
        public DateTime? HalfDate { get; set; }
        public DateTime? ShortDate { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal? TotalDays { get; set; }
      //  public List<string>? SelectedDates { get; set; }
        public List<DateTime>? SelectedDates { get; set; }

        // Half day leave properties
        public bool? IsHalfDay { get; set; }
        public bool? IsFirstHalf { get; set; }
        public bool? IsSecondHalf { get; set; }
        public DateTime? LeaveDate { get; set; }

        // One day leave properties
        public DateTime? FromTime { get; set; }
        public DateTime? ToTime { get; set; }
        public decimal? TotalHours { get; set; }
        public IFormFile? LeaveFile { get; set; }
        public string? LeaveFileLink { get; set; }
        public string? EmployeeName { get; set; }
        //public string? HrRemarks { get; set; }

    }
}
