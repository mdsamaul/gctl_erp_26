namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class DepartmentLeaveDataVM
    {
        public string DepartmentName { get; set; }
        public List<LeaveDetailVM> LeaveDetails { get; set; } = new();
    }
}