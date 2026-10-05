using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class EmpInfoViewModel : BaseViewModel
    {
        public string? ReportingTo { get; set; }
        public string? HOD { get; set; }
        public string? DepartmentCode { get; set; }
        public string? DesignationCode { get; set; }
        public string? DepartmentName { get; set; }
        public string? DepartmentShortName { get; set; }
        public string? DesignationName { get; set; }
        public string? DesignationShortName { get; set; }
        public string? EmployeeFirstName { get; set; }
        public string? EmployeeLastName { get; set; }
        public string? ReportingFirstName { get; set; }
        public string? ReportingLastName { get; set; }
        public string? HODFirstName { get; set; }
        //public string HodFirstName { get; set; }
        public string? HODLastName { get; set; }
        //public string HodLastName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyCode { get; set; }
        public string? EmployeeId { get; set; }
        public DateTime? JoiningDate { get; set; }
        public string? BranchName { get; set; }
        public decimal GrossSalary { get; set; }
        public DateTime? ConfirmationDate { get; set; }

        public string? IsExpatriate { get; set; }
        public decimal? ExpatriateBasicSalary { get; set; }
        public decimal? ExpatriateHouseRent { get; set; }
        public decimal? ExpatriateConveyance { get; set; }
        public decimal? ExpatriateMedical { get; set; }
        public decimal? Lfa { get; set; }
        public decimal? MobileAllowance { get; set; }
        //public string HodId { get; set; }
        //public string HodName { get; set; }
        //public string ISuperVisorId { get; set; }
        //public string ISuperVisorName { get; set; }
    }
}
