namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class LeaveDetailVM
    {
        




        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string LeaveAppEntryCode { get; set; }
        public string LeaveAppEntryId { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeFirstName { get; set; }
        //public string EmployeeLastName { get; set; }
        public string DesignationName { get; set; }
        public string? LeaveTypeId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal NoOfDay { get; set; }
        public DateTime? ModifyDate { get; set; }
        public string ConfirmationRemarks { get; set; }
        public string HODApprovalStatus { get; set; }
        public string HRApprovalRemarks { get; set; }
        public string SickLeaveFilePath { get; set; }
        public string HRApprovalStatus { get; set; }
        public string ApplyLeaveFormat { get; set; }
        public string HODFirstName { get; set; }
        public string SupervisorFirstName { get; set; }
        public string Reason { get; set; }
        public DateTime? ShortLeaveFrom { get; set; }
        public DateTime? ShortLeaveTo { get; set; }
        public TimeSpan? ShortLeaveTime { get; set; }
        public string IsApproved { get; set; }
        public string FirstOrSecondHalf { get; set; }
        public int TotalLeaves { get; set; }
        public int PendingLeaves { get; set; }
        public int ApprovedLeaves { get; set; }
        public int RejectedLeaves { get; set; }

        public List<string>? DaysStr { get; set; }
        //public string Days { get; set; }



    }
}