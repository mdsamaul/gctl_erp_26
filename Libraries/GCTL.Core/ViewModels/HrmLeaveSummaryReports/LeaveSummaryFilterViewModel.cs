using System;
using System.Collections.Generic;

namespace GCTL.Core.ViewModels.HrmLeaveSummaryReports
{
    public class LeaveSummaryFilterViewModel
    {
        public List<string> CompanyCodes { get; set; }
        public List<string> BranchCodes { get; set; }
        public List<string> DepartmentCodes { get; set; }
        public List<string> DesignationCodes { get; set; }
        public List<string> EmployeeIDs { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
    }

    public class LookupDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
    }

    // Granted quota per type, sourced from HrmAtdLeaveType.NoOfDay
    public class LeaveTypeDto2
    {
        public string Code { get; set; }   // HrmAtdLeaveType.LeaveTypeCode
        public string Name { get; set; }   // HrmAtdLeaveType.Name
        public decimal GrantedDays { get; set; } // HrmAtdLeaveType.NoOfDay
    }

    public class LeaveTypeBalanceDto
    {
        public string LeaveTypeCode { get; set; }
        public decimal Granted { get; set; }
        public decimal Availed { get; set; }
        public decimal Balance { get; set; }
    }

    public class LeaveSummaryRowDto
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string DesignationName { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName { get; set; }
        public string CompanyName { get; set; }
        public string JoiningDate { get; set; }       // formatted dd/MM/yyyy
        public DateTime CycleStart { get; set; }       // joining-date anniversary cycle
        public DateTime CycleEnd { get; set; }
        public List<LeaveTypeBalanceDto> LeaveBalances { get; set; } = new();
    }

    public class LeaveSummaryFilterListViewModel
    {
        public List<LookupDto> Companies { get; set; }
        public List<LookupDto> Branches { get; set; }
        public List<LookupDto> Departments { get; set; }
        public List<LookupDto> Designations { get; set; }
        public List<LookupDto> Employees { get; set; }
        public List<LeaveTypeDto2> LeaveTypes { get; set; }
        public List<LeaveSummaryRowDto> LeaveSummary { get; set; }
    }

    public class LeaveSummaryExportRequest
    {
        public LeaveSummaryFilterViewModel FilterData { get; set; }
        public string ExportFormat { get; set; } // "pdf", "excel", "word", "download"
    }
}
