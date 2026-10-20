// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Core/ViewModels/MonthlyAttendance/MonthlyAttendanceDto.cs
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;

namespace GCTL.Core.ViewModels.MonthlyAttendance
{
    public class MonthlyAttendanceFilterDto
    {
        public string? CompanyCode { get; set; }
        public List<string>? CompanyCodes { get; set; }
        public string? BranchCode { get; set; }
        public List<string>? BranchCodes { get; set; }
        public List<string>? DepartmentCodes { get; set; }
        public List<string>? DesignationCodes { get; set; }
        public List<string>? EmploymentNatureCodes { get; set; }
        public List<string>? EmployeeIds { get; set; }
        public string DurationType { get; set; } = "ByDate"; // "ByDate" or "ByMonth"
        public string? FromDate { get; set; } // dd/MM/yyyy
        public string? ToDate { get; set; }   // dd/MM/yyyy
        public int? Month { get; set; }
        public int? Year { get; set; }
        public string? ExportFormat { get; set; } = "downloadPdf";
        public string? LoginEmployeeId { get; set; }
        public string? AccessCodeId { get; set; }
    }

    public class MonthlyAttendanceRawRecordDto
    {
        public string DepartmentCode { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string DesignationCode { get; set; } = "";
        public string Designation { get; set; } = "";
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public DateTime Date { get; set; }
        public string DateDisplay { get; set; } = "";
        public string Day { get; set; } = ""; // "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"
        public int DayNumber { get; set; }    // 1..31
        public string Shift { get; set; } = "";
        public string InTime { get; set; } = "";
        public string Late { get; set; } = "";
        public string OutTime { get; set; } = "";
        public string WorkHours { get; set; } = "";
        public string Status { get; set; } = ""; // 'P', 'L', 'A', 'W', 'H', 'CL', 'SL', etc.
        public string Remarks { get; set; } = "";
    }

    public class MonthlyAttendanceDayHeaderDto
    {
        public int DayNumber { get; set; }        // 1..31
        public string DayName { get; set; } = ""; // "Th", "Fr", "Sa", etc.
        public DateTime Date { get; set; }
        public string DateDisplay { get; set; } = "";
        public bool IsWeekend { get; set; }
    }

    public class MonthlyAttendanceEmployeeRowDto
    {
        public int SerialNo { get; set; }
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string Designation { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public Dictionary<int, string> DailyStatuses { get; set; } = new Dictionary<int, string>();

        // 13 Summary counts matching Excel:
        public int TotalPresent { get; set; }    // P
        public int TotalLate { get; set; }       // L
        public int TotalAbsent { get; set; }     // A
        public int TotalWeekend { get; set; }    // W
        public int TotalHoliday { get; set; }    // H
        public int TotalCL { get; set; }         // CL
        public int TotalSL { get; set; }         // SL
        public int TotalUL { get; set; }         // UL
        public int TotalPL { get; set; }         // PL
        public int TotalMRL { get; set; }        // MRL
        public int TotalEL { get; set; }         // EL
        public int TotalFL { get; set; }         // FL
        public int TotalML { get; set; }         // ML
    }

    public class MonthlyAttendanceDepartmentGroupDto
    {
        public string DepartmentCode { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public List<MonthlyAttendanceEmployeeRowDto> Employees { get; set; } = new List<MonthlyAttendanceEmployeeRowDto>();
        public int TotalEmployees => Employees.Count;
    }

    public class MonthlyAttendanceHeaderDto
    {
        public string CompanyName { get; set; } = "DataPath Ltd.";
        public string ReportTitle { get; set; } = "Monthly Attendance Details Report";
        public string Subtitle { get; set; } = ""; // e.g. "October,2026"
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string DateRangeDisplay { get; set; } = "";
    }

    public class MonthlyAttendanceResultDto
    {
        public MonthlyAttendanceHeaderDto Header { get; set; } = new MonthlyAttendanceHeaderDto();
        public List<MonthlyAttendanceDayHeaderDto> Days { get; set; } = new List<MonthlyAttendanceDayHeaderDto>();
        public List<MonthlyAttendanceDepartmentGroupDto> DepartmentGroups { get; set; } = new List<MonthlyAttendanceDepartmentGroupDto>();
        public int GrandTotalEmployees { get; set; }
        public List<MonthlyAttendanceRawRecordDto> RawRows { get; set; } = new List<MonthlyAttendanceRawRecordDto>();
    }
}
