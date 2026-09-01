
namespace GCTL.Core.ViewModels.HrmPaySlipReports
{
    public class HrmPaySlipReportVM : BaseViewModel
    {
        // Period
        public int MonthID { get; set; }
        public string MonthName { get; set; }      // e.g. "July"
        public int YearID { get; set; }

        // Employee Info
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string DepartmentCode { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationCode { get; set; }
        public string DesignationName { get; set; }

        // Pays & Benefits Part 1
        public decimal Basic { get; set; }
        public decimal HouseRent { get; set; }
        public decimal Medical { get; set; }
        public decimal Conveyance { get; set; }
        public decimal GrossPay { get; set; }       // Basic + HouseRent + Medical + Conveyance

        // Other Allowance
        public int HolidayWorkDays { get; set; }
        public decimal HolidayWorkAmount { get; set; } // TODO: placeholder 0 for now, source column to be confirmed later
        public decimal Incentive { get; set; }
        public decimal OthersBenefits { get; set; }
        public decimal AttenBonus { get; set; }
        public decimal TotalAddition { get; set; }  // A = GrossPay + HolidayWorkAmount + Incentive + OthersBenefits + AttenBonus

        // Pays & Benefits Part 2 (Deductions) — each shown as its own line
        public decimal PFDeduction { get; set; }
        public decimal LateDeduction { get; set; }
        public decimal AdvanceDeduction { get; set; }
        public decimal StampDeduction { get; set; }
        public decimal Lunch { get; set; }
        public decimal TDS { get; set; }
        public decimal MobAndInt { get; set; }
        public decimal Transport { get; set; }
        public decimal Insurance { get; set; }
        public decimal Penalty { get; set; }
        public decimal Food { get; set; }
        public decimal OtherDed { get; set; }
        public decimal TotalDeduction { get; set; } // B = sum of all deduction lines above

        // Net
        public decimal NetPayable { get; set; }     // A - B
        public string NetPayableInWords { get; set; }

        // Bank transfer info (can have multiple accounts per employee)
        public string BankName1 { get; set; }
        public string AccountNumber1 { get; set; }
        public string BankName2 { get; set; }
        public string AccountNumber2 { get; set; }

        // Sign-off / approver block
        public string ApproverName { get; set; }
        public string ApproverDesignation { get; set; }
        public string ApproverCell { get; set; }
        public string ApproverEmail { get; set; }
    }

    public class PaySlipFilterData
    {
        public string AccessCode { get; set; }
        public string EmployeeId {  get; set; }
        public List<string> CompanyCodes { get; set; }
        public List<string> BranchCodes { get; set; }
        public List<string> DepartmentCodes { get; set; }
        public List<string> DesignationCodes { get; set; }
        public List<string> EmployeeIDs { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<string> MonthIDs { get; set; }
        public List<int> YearIDs { get; set; }
    }
    public class PaySlipReportRequest
    {
        public PaySlipFilterData FilterData { get; set; }
        public string ExportFormat { get; set; }
    }
}
