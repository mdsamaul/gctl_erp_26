// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Core/ViewModels/JobCardBulkReport/JobCardBulkReportDto.cs
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;

namespace GCTL.Core.ViewModels.JobCardBulkReport
{
    // ── Filter sent from UI ──────────────────────────────────────────
    public class JobCardBulkReportFilterDto
    {
        public string? CompanyCode { get; set; }
        public List<string>? CompanyCodes { get; set; }
        public List<string>? DepartmentCodes { get; set; }
        public List<string>? DesignationCodes { get; set; }
        public List<string>? EmployeeIds { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
        public string? DurationType { get; set; } // "ByDate" or "ByMonth"
        public string? LoginEmployeeId { get; set; }
        public string? AccessCodeId { get; set; }
        public string? ExportFormat { get; set; } // "downloadPdf", "downloadExcel", "downloadCsv", "preview"
    }

    // ── Header info returned by SP (2nd result set) ──────────────────
    public class JobCardBulkReportHeaderDto
    {
        public string CompanyName { get; set; } = "DataPath Ltd.";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string DateRangeDisplay { get; set; } = "";
    }

    // ── Job Card Bulk Report Row (1st result set) ────────────────────
    public class JobCardBulkReportRowDto
    {
        public string DepartmentCode { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string DesignationCode { get; set; } = "";
        public string Designation { get; set; } = "";
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public DateTime? Date { get; set; }
        public string DateDisplay { get; set; } = "";
        public string Day { get; set; } = "";
        public string Shift { get; set; } = "";
        public string InTime { get; set; } = "";
        public string Late { get; set; } = "00:00:00";
        public string OutTime { get; set; } = "";
        public string EarlyOut { get; set; } = "00:00:00";
        public string WorkHours { get; set; } = "0.00";
        public string Status { get; set; } = "";
        public string Remarks { get; set; } = "";
    }

    // ── Grouped Employee Job Card ────────────────────────────────────
    public class EmployeeJobCardBulkGroupDto
    {
        public string DepartmentCode { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string DesignationCode { get; set; } = "";
        public string Designation { get; set; } = "";
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public List<JobCardBulkReportRowDto> Rows { get; set; } = new();

        // ── Summary Totals per Employee ──────────────────────────────
        public int TotalWorkingDays { get; set; }
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalLate { get; set; }
        public int TotalLeave { get; set; }
        public decimal TotalWorkHours { get; set; }
        public int TotalWeekend { get; set; }
        public int TotalHoliday { get; set; }
        public int TotalAtt { get; set; }
        public decimal AveWorkHoursPerDay { get; set; }
        public string TotalLateDuration { get; set; } = "0:00:00";
        public string TotalWorkDuration { get; set; } = "0:00:00";
        public string AveWorkDuration { get; set; } = "0:00";
    }

    // ── Department Group Wrapper (Department A finishes, then Department B starts) ──
    public class DepartmentJobCardBulkGroupDto
    {
        public string DepartmentCode { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public List<EmployeeJobCardBulkGroupDto> Employees { get; set; } = new();
    }

    // ── Unified Response Wrapper ─────────────────────────────────────
    public class JobCardBulkReportResultDto
    {
        public JobCardBulkReportHeaderDto Header { get; set; } = new();
        public List<JobCardBulkReportRowDto> Rows { get; set; } = new();
        public List<EmployeeJobCardBulkGroupDto> EmployeeGroups { get; set; } = new();
        public List<DepartmentJobCardBulkGroupDto> DepartmentGroups { get; set; } = new();
    }
}
