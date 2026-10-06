// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Core/ViewModels/JobCardReport/JobCardReportDto.cs
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;

namespace GCTL.Core.ViewModels.JobCardReport
{
    // ── Filter sent from UI ──────────────────────────────────────────
    public class JobCardReportFilterDto
    {
        public string? CompanyCode { get; set; }
        public List<string>? EmployeeIds { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? LoginEmployeeId { get; set; }
        public string? AccessCodeId { get; set; }
    }

    // ── Header info returned by SP (2nd result set) ──────────────────
    public class JobCardReportHeaderDto
    {
        public string CompanyName { get; set; } = "DataPath Ltd.";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string DateRangeDisplay { get; set; } = "";
    }

    // ── Job Card Report Row (1st result set) ─────────────────────────
    public class JobCardReportRowDto
    {
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string Designation { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public DateTime? Date { get; set; }
        public string DateDisplay { get; set; } = "";
        public string Day { get; set; } = "";
        public string Shift { get; set; } = "";
        public string InTime { get; set; } = "";
        public string Late { get; set; } = "00:00:00";
        public string OutTime { get; set; } = "";
        public string EarlyOut { get; set; } = "00:00:00";
        public string WorkHours { get; set; } = "00:00:00";
        public string Status { get; set; } = "";
        public string Remarks { get; set; } = "";
    }

    // ── Grouped Employee Job Card for Rendering ──────────────────────
    public class EmployeeJobCardGroupDto
    {
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string Designation { get; set; } = "";
        public string DepartmentName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public List<JobCardReportRowDto> Rows { get; set; } = new();
    }

    // ── Unified Response Wrapper ─────────────────────────────────────
    public class JobCardReportResultDto
    {
        public JobCardReportHeaderDto Header { get; set; } = new();
        public List<JobCardReportRowDto> Rows { get; set; } = new();
        public List<EmployeeJobCardGroupDto> EmployeeGroups { get; set; } = new();
    }
}
