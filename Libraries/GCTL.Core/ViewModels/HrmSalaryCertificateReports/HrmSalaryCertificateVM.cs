using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmSalaryCertificateReports
{
    public class HrmSalaryCertificateVM: BaseViewModel
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Designation { get; set; }
        public string Department { get; set; }
        public string JoiningDate { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HouseRentAllowence { get; set; }
        public decimal MedicalAllowence { get; set; }
        public decimal ConveyanceAllowence { get; set; }
        public decimal OtherAllowence { get; set; }
        public decimal TotalAllowence { get; set; }
        public decimal TaxDeduction { get; set; }
        public decimal PFDeduction { get; set; }
        public decimal OtherDeduction { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal FestivalBonus { get; set; }
        public decimal YearlyBonus { get; set; }
        public decimal Incentives { get; set; }
        public decimal ProvidentFund { get; set; }
        public decimal NetPayment { get; set; }
        public string BankACName { get; set; }
        public string BankACNo { get; set; }
        public string BankACName2 { get; set; }
        public string BankACNo2 { get; set; }

        public string ApproverName { get; set; }
        public string ApproverDesignation { get; set; }
        public string ApproverCell { get; set; }
        public string ApproverEmail { get; set; }
    }

    public class SalaryCertificateFilterData
    {
        public string AccessCode { get; set; }
        public string EmployeeId { get; set; }
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
    public class SalaryCertificateReportRequest
    {
        public SalaryCertificateFilterData FilterData { get; set; }
        public string ExportFormat { get; set; }
    }
}
