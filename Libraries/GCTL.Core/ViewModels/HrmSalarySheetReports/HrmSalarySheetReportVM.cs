using System;
using System.Collections.Generic;

namespace GCTL.Core.ViewModels.HrmSalarySheetReports
{
    public class HrmSalarySheetReportVM : BaseViewModel
    {
        public string MonthName { get; set; }
        public string YearName { get; set; }
        public string Code { get; set; }
        public string PayId { get; set; }
        public string BankAcNo { get; set; }
        public string BankAcNoSibl { get; set; }
        public string BankAcNoUcbl { get; set; }
        public string EmployeeName { get; set; }
        public DateTime? JoiningDate { get; set; }
        public string DepartmentName { get; set; }
        public string DesignationName { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal RateOfAllowancesForHoliday { get; set; }
        public decimal RpfContributionEmployee { get; set; }
        public decimal NoOfExtraDay { get; set; }
        public decimal NoOfHolidayWorked { get; set; }
        public decimal TotalAllowancesForHoliday { get; set; }
        public decimal RateOfAllowancesForNightShift { get; set; }
        public decimal NoOfNightWorked { get; set; }
        public decimal TotalAllowancesForNightShift { get; set; }
        public decimal WfhAllowance { get; set; }
        public decimal Usd { get; set; }
        public decimal OtEid { get; set; }
        public decimal TotalAllowancesForOt { get; set; }
        public decimal YearlyBonus { get; set; }
        public decimal Incentive { get; set; }
        public decimal QuarterlyIncentive { get; set; }
        public decimal Gratuity { get; set; }
        public decimal Arrear { get; set; }
        public decimal EidBonusDbbl { get; set; }
        public decimal EidBonusUcb { get; set; }
        public decimal Tds { get; set; }
        public decimal AdjustmentIfAny { get; set; }
        public decimal SalaryDbbl { get; set; }
        public decimal NetPaymentUcbl { get; set; }
        public decimal NetPaymentRoundUcbl { get; set; }
        public string Remarks { get; set; }
    }

    public class HrmSalarySheetReportPageVM : BaseViewModel
    {
        public string AccessCode { get; set; }
    }

    public class SalarySheetFilterData
    {
        public string AccessCode { get; set; }
        public string EmployeeId { get; set; }
        public string UserId { get; set; }
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

    public class SalarySheetReportRequest
    {
        public SalarySheetFilterData FilterData { get; set; }
        public string ExportFormat { get; set; }
    }
}
