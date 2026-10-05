using System.ComponentModel.DataAnnotations;

namespace GCTL.Core.ViewModels.HrmAtdShifts
{
    public class HrmAtdShiftSetupViewModel : BaseViewModel
    {
        public decimal AutoId { get; set; }
        public string ShiftCode { get; set; }
        [Required(ErrorMessage = "Please enter shif type name")]
        public string ShiftName { get; set; }
        public string ShiftShortName { get; set; }
        [DisplayFormat(DataFormatString = "{0:hh:mm:ss tt}", ApplyFormatInEditMode = true)]
        public DateTime ShiftStartTime { get; set; }

        [DisplayFormat(DataFormatString = "{0:hh:mm:ss tt}", ApplyFormatInEditMode = true)]
        public DateTime ShiftEndTime { get; set; }
        [DisplayFormat(DataFormatString = "{0:hh:mm:ss tt}", ApplyFormatInEditMode = true)]
        public DateTime LateTime { get; set; }
        [DisplayFormat(DataFormatString = "{0:hh:mm:ss tt}", ApplyFormatInEditMode = true)]
        public DateTime AbsentTime { get; set; }
        public string Description { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime Wef { get; set; }
        public string Remarks { get; set; }
        public string ShiftTypeId { get; set; }
        public string ShiftTypeName { get; set; }
        public DateTime LunchInTime { get; set; }
        public DateTime LunchOutTime { get; set; }
        public decimal LunchBreakHour { get; set; }

        public string EarlyLeaveTime { get; set; }
        public bool IsCrossMidnight { get; set; }
        
        // Comma separated Lists or arrays can be mapped directly. For UI, we often use arrays.
        public List<string> CompanyIdsList { get; set; } = new List<string>();
        public List<string> BranchIdsList { get; set; } = new List<string>();
        public List<string> DepartmentIdsList { get; set; } = new List<string>();
        public List<string> EmployeeIdsList { get; set; } = new List<string>();
        
        public string CompanyIds { get; set; }
        public string BranchIds { get; set; }
        public string DepartmentIds { get; set; }
        public string EmployeeIds { get; set; }
        
        public string OvertimeRuleID { get; set; }
        public string OvertimeRuleName { get; set; } // For display in Grid if needed
        public string IsActiveStatus { get; set; } // Map to 'Active' or 'Inactive'
        public bool IsActive { get; set; }

    }
}
