using System;

namespace GCTL.Data.Models
{
    public partial class HrmAtdShiftDetails
    {
        public decimal AutoId { get; set; }
        public string ShiftCode { get; set; }
        public DateTime ShiftStartTime { get; set; }
        public DateTime ShiftEndTime { get; set; }
        public DateTime LateTime { get; set; }
        public DateTime AbsentTime { get; set; }
        public DateTime LunchOutTime { get; set; }
        public DateTime LunchInTime { get; set; }
        public decimal LunchBreakHour { get; set; }
        public string EarlyLeaveTime { get; set; }
        public bool? IsCrossMidnight { get; set; }
        public string CompanyIds { get; set; }
        public string BranchIds { get; set; }
        public string DepartmentIds { get; set; }
        public string EmployeeIds { get; set; }
        public string OvertimeRuleId { get; set; }
        public int? OtGraceTime { get; set; }
        public decimal? MaxOtPerDay { get; set; }
        public int? MinOtMinutes { get; set; }
        public string WeeklyOffOtRule { get; set; }
        public string HolidayOtRule { get; set; }
        public DateTime Wef { get; set; }
        public string Remarks { get; set; }
        public bool? IsActive { get; set; }
        public string Luser { get; set; }
        public DateTime? Ldate { get; set; }
        public string Lip { get; set; }
        public string Lmac { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}

