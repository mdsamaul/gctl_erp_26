// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Service/MonthlyAttendance/MonthlyAttendanceService.cs
// ═══════════════════════════════════════════════════════════════════
using Dapper;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.MonthlyAttendance;
using GCTL.Data.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using PdfDocument2 = iText.Layout.Document;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Service.MonthlyAttendance
{
    public class MonthlyAttendanceService : IMonthlyAttendanceService
    {
        private readonly IRepository<CoreAccessCode> _accessCodeRepo;
        private readonly string _connectionString;

        public MonthlyAttendanceService(
            IRepository<CoreAccessCode> accessCodeRepo,
            IConfiguration configuration)
        {
            _accessCodeRepo = accessCodeRepo;
            _connectionString = configuration.GetConnectionString("ApplicationDbConnection")!;
        }

        // ── Page Permission ──────────────────────────────────────────
        public async Task<bool> PagePermissionAsync(string accessCode)
        {
            if (string.IsNullOrWhiteSpace(accessCode))
                return true;

            if (accessCode == "0001")
                return true;

            var hasExplicitPermission = await _accessCodeRepo.All()
                .AnyAsync(x => x.AccessCodeId == accessCode
                            && (x.Title == "Monthly Attendance Details Report"
                                || x.Title == "Monthly Attendance Report"
                                || x.Title == "Monthly Attendance"
                                || x.Title == "Job Card Bulk Report")
                            && x.TitleCheck);

            return hasExplicitPermission || accessCode == "0002" || accessCode == "0003" || accessCode == "0005";
        }

        private static string? ToCsv(List<string>? list)
        {
            if (list == null || !list.Any()) return null;
            var clean = list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            return clean.Any() ? string.Join(",", clean) : null;
        }

        private static DateTime? ParseDate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            if (DateTime.TryParseExact(input.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }
            if (DateTime.TryParse(input.Trim(), out var dt2))
            {
                return dt2;
            }
            return null;
        }

        // ── Get Report Data ──────────────────────────────────────────
        public async Task<MonthlyAttendanceResultDto> GetReportDataAsync(MonthlyAttendanceFilterDto filter)
        {
            try
            {
                var result = new MonthlyAttendanceResultDto();

                var compCsv = filter.CompanyCodes != null && filter.CompanyCodes.Any()
                    ? ToCsv(filter.CompanyCodes)
                    : (!string.IsNullOrWhiteSpace(filter.CompanyCode) ? filter.CompanyCode.Trim() : null);

                var branchCsv = filter.BranchCodes != null && filter.BranchCodes.Any()
                    ? ToCsv(filter.BranchCodes)
                    : (!string.IsNullOrWhiteSpace(filter.BranchCode) ? filter.BranchCode.Trim() : null);

                var deptCsv = ToCsv(filter.DepartmentCodes);
                var desigCsv = ToCsv(filter.DesignationCodes);
                var empNatureCsv = ToCsv(filter.EmploymentNatureCodes);
                var empCsv = ToCsv(filter.EmployeeIds);

                var loginEmployeeId = string.IsNullOrWhiteSpace(filter.LoginEmployeeId) ? null : filter.LoginEmployeeId.Trim();
                var accessCodeId = string.IsNullOrWhiteSpace(filter.AccessCodeId) ? null : filter.AccessCodeId.Trim();

                DateTime? fromDate = ParseDate(filter.FromDate);
                DateTime? toDate = ParseDate(filter.ToDate);

                if (filter.DurationType == "ByMonth" && filter.Month.HasValue && filter.Year.HasValue && filter.Month.Value >= 1 && filter.Month.Value <= 12 && filter.Year.Value > 1900)
                {
                    fromDate = new DateTime(filter.Year.Value, filter.Month.Value, 1);
                    toDate = new DateTime(filter.Year.Value, filter.Month.Value, DateTime.DaysInMonth(filter.Year.Value, filter.Month.Value));
                }
                else if (fromDate.HasValue)
                {
                    // Monthly Attendance Report always encompasses the full month (1st of month to DaysInMonth)
                    var refDate = fromDate.Value;
                    fromDate = new DateTime(refDate.Year, refDate.Month, 1);
                    var endYear = toDate?.Year ?? refDate.Year;
                    var endMonth = toDate?.Month ?? refDate.Month;
                    toDate = new DateTime(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));
                }
                else
                {
                    var now = DateTime.Now;
                    fromDate = new DateTime(now.Year, now.Month, 1);
                    toDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
                }

                // ── Compute Unique Days Header ──────────────────────────
                var daysList = new List<MonthlyAttendanceDayHeaderDto>();
                var dayDateMap = new Dictionary<int, DateTime>();
                DateTime cur = fromDate.Value;
                while (cur <= toDate.Value)
                {
                    string day2 = cur.ToString("ddd", CultureInfo.InvariantCulture).Substring(0, 2);
                    bool isWk = cur.DayOfWeek == DayOfWeek.Friday || cur.DayOfWeek == DayOfWeek.Saturday;

                    daysList.Add(new MonthlyAttendanceDayHeaderDto
                    {
                        DayNumber = cur.Day,
                        DayName = day2,
                        Date = cur,
                        DateDisplay = cur.ToString("dd/MM/yyyy"),
                        IsWeekend = isWk
                    });
                    dayDateMap[cur.Day] = cur;
                    cur = cur.AddDays(1);
                }
                result.Days = daysList;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "usp_MonthlyAttendanceReport";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 300;

                cmd.Parameters.Add(new SqlParameter("@CompanyCode", (object?)compCsv ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@BranchCodes", (object?)branchCsv ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@DepartmentCodes", (object?)deptCsv ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@DesignationCodes", (object?)desigCsv ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@EmploymentNatureCodes", (object?)empNatureCsv ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@EmployeeIds", (object?)empCsv ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@FromDate", fromDate.HasValue ? (object)fromDate.Value.ToString("yyyy-MM-dd") : DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ToDate", toDate.HasValue ? (object)toDate.Value.ToString("yyyy-MM-dd") : DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Month", filter.DurationType == "ByMonth" && filter.Month.HasValue ? (object)filter.Month.Value : DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@Year", filter.DurationType == "ByMonth" && filter.Year.HasValue ? (object)filter.Year.Value : DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@LoginEmployeeId", (object?)loginEmployeeId ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@AccessCodeId", (object?)accessCodeId ?? DBNull.Value));

                using var reader = await cmd.ExecuteReaderAsync();

                var deptGroups = new List<MonthlyAttendanceDepartmentGroupDto>();
                MonthlyAttendanceDepartmentGroupDto? currentDept = null;
                MonthlyAttendanceEmployeeRowDto? currentEmp = null;
                string lastDeptName = "";
                string lastEmpId = "";
                int serialNo = 0;
                DateTime today = DateTime.Today;

                int ordDeptCode = reader.GetOrdinal("DepartmentCode");
                int ordDeptName = reader.GetOrdinal("DepartmentName");
                int ordDesig = reader.GetOrdinal("Designation");
                int ordEmpId = reader.GetOrdinal("EmployeeId");
                int ordEmpName = reader.GetOrdinal("EmployeeName");
                int ordDayNum = reader.GetOrdinal("DayNumber");
                int ordStatus = reader.GetOrdinal("Status");

                while (await reader.ReadAsync())
                {
                    string deptName = reader.IsDBNull(ordDeptName) ? "General Department" : reader.GetString(ordDeptName);
                    string empId = reader.IsDBNull(ordEmpId) ? "" : reader.GetString(ordEmpId);

                    if (deptName != lastDeptName || currentDept == null)
                    {
                        lastDeptName = deptName;
                        currentDept = new MonthlyAttendanceDepartmentGroupDto
                        {
                            DepartmentCode = reader.IsDBNull(ordDeptCode) ? "" : reader.GetString(ordDeptCode),
                            DepartmentName = !string.IsNullOrWhiteSpace(deptName) ? deptName : "General Department"
                        };
                        deptGroups.Add(currentDept);
                        serialNo = 1;
                        lastEmpId = "";
                    }

                    if (empId != lastEmpId || currentEmp == null)
                    {
                        lastEmpId = empId;
                        currentEmp = new MonthlyAttendanceEmployeeRowDto
                        {
                            SerialNo = serialNo++,
                            EmployeeId = empId,
                            EmployeeName = reader.IsDBNull(ordEmpName) ? "" : reader.GetString(ordEmpName),
                            Designation = reader.IsDBNull(ordDesig) ? "" : reader.GetString(ordDesig),
                            DepartmentName = currentDept.DepartmentName
                        };
                        foreach (var d in daysList)
                        {
                            currentEmp.DailyStatuses[d.DayNumber] = "";
                        }
                        currentDept.Employees.Add(currentEmp);
                    }

                    int dayNum = reader.GetInt32(ordDayNum);
                    string status = reader.IsDBNull(ordStatus) ? "" : reader.GetString(ordStatus).Trim();

                    // Future days must remain empty/blank ("faka thakbe")
                    if (dayDateMap.TryGetValue(dayNum, out var dateVal) && dateVal > today && (string.IsNullOrWhiteSpace(status) || status.ToUpperInvariant() == "A"))
                    {
                        status = "";
                    }

                    currentEmp.DailyStatuses[dayNum] = status;

                    var s = status.ToUpperInvariant();
                    if (s == "P") currentEmp.TotalPresent++;
                    else if (s == "L") currentEmp.TotalLate++;
                    else if (s == "A") currentEmp.TotalAbsent++;
                    else if (s == "W") currentEmp.TotalWeekend++;
                    else if (s == "H") currentEmp.TotalHoliday++;
                    else if (s == "CL") currentEmp.TotalCL++;
                    else if (s == "SL") currentEmp.TotalSL++;
                    else if (s == "UL") currentEmp.TotalUL++;
                    else if (s == "PL") currentEmp.TotalPL++;
                    else if (s == "MRL") currentEmp.TotalMRL++;
                    else if (s == "EL") currentEmp.TotalEL++;
                    else if (s == "FL") currentEmp.TotalFL++;
                    else if (s == "ML") currentEmp.TotalML++;
                    else if (s.StartsWith("C")) currentEmp.TotalCL++;
                    else if (s.StartsWith("S")) currentEmp.TotalSL++;
                    else if (s.StartsWith("M")) currentEmp.TotalML++;
                }

                result.DepartmentGroups = deptGroups;
                result.GrandTotalEmployees = deptGroups.Sum(d => d.TotalEmployees);

                // 2nd Result Set: Header metadata
                if (await reader.NextResultAsync() && await reader.ReadAsync())
                {
                    result.Header = new MonthlyAttendanceHeaderDto
                    {
                        CompanyName = reader["CompanyName"]?.ToString() ?? "DataPath Ltd.",
                        ReportTitle = reader["ReportTitle"]?.ToString() ?? "Monthly Attendance Details Report",
                        Subtitle = reader["Subtitle"]?.ToString() ?? "",
                        FromDate = reader["FromDate"]?.ToString() ?? "",
                        ToDate = reader["ToDate"]?.ToString() ?? "",
                        DateRangeDisplay = reader["DateRangeDisplay"]?.ToString() ?? ""
                    };
                }
                else
                {
                    result.Header = new MonthlyAttendanceHeaderDto
                    {
                        CompanyName = "DataPath Ltd.",
                        ReportTitle = "Monthly Attendance Details Report",
                        Subtitle = "",
                        FromDate = fromDate?.ToString("dd/MM/yyyy") ?? "",
                        ToDate = toDate?.ToString("dd/MM/yyyy") ?? "",
                        DateRangeDisplay = $"Date: {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}"
                    };
                }

                if (filter.DurationType == "ByMonth" && filter.Month.HasValue && filter.Year.HasValue)
                {
                    var mName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(filter.Month.Value);
                    result.Header.Subtitle = $"{mName}, {filter.Year.Value}";
                    result.Header.DateRangeDisplay = $"{mName}, {filter.Year.Value}";
                }
                else if (fromDate.HasValue && toDate.HasValue)
                {
                    result.Header.Subtitle = $"{fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";
                    result.Header.DateRangeDisplay = $"Date: {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";
                }

                return result;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // ═════════════════════════════════════════════════════════════
        // ── Excel Export (EPPlus) - 100% Matching MonthlyAttendance (9).xlsx
        // ═════════════════════════════════════════════════════════════
        public async Task<byte[]> ExportExcelAsync(MonthlyAttendanceFilterDto filter, string? logoPhysicalPath = null)
        {
            var data = await GetReportDataAsync(filter);
            if (data == null || !data.DepartmentGroups.Any())
                return Array.Empty<byte>();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();

            var sheetName = "Monthly Attendance";
            var ws = package.Workbook.Worksheets.Add(sheetName);

            // Set global default font for blazing speed
            ws.Cells.Style.Font.Name = "Calibri";
            ws.Cells.Style.Font.Size = 8;

            // Palette definitions matching Excel:
            var headerBgColor = System.Drawing.Color.FromArgb(217, 234, 211);     // #D9EAD3 (Soft green)
            var presentBgColor = System.Drawing.Color.FromArgb(198, 239, 206);    // #C6EFCE (Soft green)
            var weekendBgColor = System.Drawing.Color.FromArgb(217, 234, 211);    // #D9EAD3 (Soft green)
            var lateBgColor = System.Drawing.Color.FromArgb(255, 242, 204);       // #FFF2CC (Soft yellow)
            var absentBgColor = System.Drawing.Color.FromArgb(244, 204, 204);     // #F4CCCC (Soft red/pink)
            var subtotalBgColor = System.Drawing.Color.FromArgb(239, 239, 239);   // #EFEFEF (Soft gray)

            var summaryHeaders = new[] { "P", "L", "A", "W", "H", "CL", "SL", "UL", "PL", "MRL", "EL", "FL", "ML" };

            int dayCount = data.Days.Count;
            int spacerCol = 5 + dayCount;
            int totalCols = spacerCol + summaryHeaders.Length;

            // 1. Left: Add Company Logo
            if (!string.IsNullOrWhiteSpace(logoPhysicalPath) && File.Exists(logoPhysicalPath))
            {
                try
                {
                    var logoInfo = new FileInfo(logoPhysicalPath);
                    var pic = ws.Drawings.AddPicture("CompanyLogo", logoInfo);
                    pic.SetPosition(0, 4, 0, 4); // Row 0, Col 0 with 4px offset
                    pic.SetSize(115, 48);
                }
                catch { }
            }

            int currentRow = 1;

            // 2. Top: Company Name (Calibri 16pt Bold, Centered)
            ws.Cells[currentRow, 1, currentRow, totalCols].Merge = true;
            ws.Cells[currentRow, 1].Value = !string.IsNullOrWhiteSpace(data.Header.CompanyName) ? data.Header.CompanyName : "DataPath Ltd.";
            ws.Cells[currentRow, 1].Style.Font.Size = 16;
            ws.Cells[currentRow, 1].Style.Font.Bold = true;
            ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[currentRow, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Row(currentRow).Height = 24;
            currentRow++;

            // 3. Report Name (Calibri 13pt Bold, Centered)
            ws.Cells[currentRow, 1, currentRow, totalCols].Merge = true;
            ws.Cells[currentRow, 1].Value = !string.IsNullOrWhiteSpace(data.Header.ReportTitle) ? data.Header.ReportTitle : "Monthly Attendance Details Report";
            ws.Cells[currentRow, 1].Style.Font.Size = 13;
            ws.Cells[currentRow, 1].Style.Font.Bold = true;
            ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[currentRow, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Row(currentRow).Height = 20;
            currentRow++;

            // 4. Date (Calibri 10.5pt Bold, Centered)
            ws.Cells[currentRow, 1, currentRow, totalCols].Merge = true;
            var dateDisplay = !string.IsNullOrWhiteSpace(data.Header.DateRangeDisplay) ? data.Header.DateRangeDisplay : data.Header.Subtitle;
            ws.Cells[currentRow, 1].Value = dateDisplay;
            ws.Cells[currentRow, 1].Style.Font.Size = 10.5f;
            ws.Cells[currentRow, 1].Style.Font.Bold = true;
            ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[currentRow, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Row(currentRow).Height = 18;
            currentRow++;

            // Blank spacer row
            ws.Row(currentRow).Height = 8;
            currentRow++;

            int gridStartRow = currentRow;

            foreach (var dept in data.DepartmentGroups)
            {
                // Department Header Banner (Calibri 13.5pt Bold, Left-aligned)
                ws.Cells[currentRow, 1, currentRow, totalCols].Merge = true;
                ws.Cells[currentRow, 1].Value = "Department: " + dept.DepartmentName;
                ws.Cells[currentRow, 1].Style.Font.Size = 13.5f;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.Cells[currentRow, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                ws.Row(currentRow).Height = 22;
                currentRow++;

                int headerR1 = currentRow;
                int headerR2 = currentRow + 1;

                // Col 1: SN (merged R1:R2)
                ws.Cells[headerR1, 1, headerR2, 1].Merge = true;
                ws.Cells[headerR1, 1].Value = "SN";

                // Col 2: Emp ID (merged R1:R2)
                ws.Cells[headerR1, 2, headerR2, 2].Merge = true;
                ws.Cells[headerR1, 2].Value = "Emp ID";

                // Col 3: Name (merged R1:R2)
                ws.Cells[headerR1, 3, headerR2, 3].Merge = true;
                ws.Cells[headerR1, 3].Value = "Name";

                // Col 4: Designation (merged R1:R2)
                ws.Cells[headerR1, 4, headerR2, 4].Merge = true;
                ws.Cells[headerR1, 4].Value = "Designation";

                // Day columns
                for (int i = 0; i < data.Days.Count; i++)
                {
                    int c = 5 + i;
                    var d = data.Days[i];
                    ws.Cells[headerR1, c].Value = d.DayNumber;
                    ws.Cells[headerR2, c].Value = d.DayName;
                }

                // Spacer Column
                ws.Cells[headerR1, spacerCol, headerR2, spacerCol].Merge = true;
                ws.Cells[headerR1, spacerCol].Value = "";

                // Summary Columns (P..ML)
                for (int s = 0; s < summaryHeaders.Length; s++)
                {
                    int sc = spacerCol + 1 + s;
                    ws.Cells[headerR1, sc, headerR2, sc].Merge = true;
                    ws.Cells[headerR1, sc].Value = summaryHeaders[s];
                }

                // Style Header Block in batch
                ws.Row(headerR1).Height = 15;
                ws.Row(headerR2).Height = 15;
                var headerRange = ws.Cells[headerR1, 1, headerR2, totalCols];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(headerBgColor);
                headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                headerRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

                currentRow += 2;

                int empStartRow = currentRow;

                // Employee Rows - Fast Value Insertion without individual style mutations
                foreach (var emp in dept.Employees)
                {
                    ws.Row(currentRow).Height = 15;

                    ws.Cells[currentRow, 1].Value = emp.SerialNo;
                    ws.Cells[currentRow, 2].Value = emp.EmployeeId;
                    ws.Cells[currentRow, 3].Value = emp.EmployeeName;
                    ws.Cells[currentRow, 4].Value = emp.Designation;

                    // Day Statuses
                    for (int i = 0; i < data.Days.Count; i++)
                    {
                        int c = 5 + i;
                        int dayNum = data.Days[i].DayNumber;

                        if (emp.DailyStatuses.TryGetValue(dayNum, out var val) && !string.IsNullOrEmpty(val))
                        {
                            ws.Cells[currentRow, c].Value = val;
                        }
                    }

                    // Spacer cell
                    ws.Cells[currentRow, spacerCol].Value = "";

                    // Summary counts
                    int[] counts = new[]
                    {
                        emp.TotalPresent, emp.TotalLate, emp.TotalAbsent, emp.TotalWeekend,
                        emp.TotalHoliday, emp.TotalCL, emp.TotalSL, emp.TotalUL,
                        emp.TotalPL, emp.TotalMRL, emp.TotalEL, emp.TotalFL, emp.TotalML
                    };

                    for (int s = 0; s < counts.Length; s++)
                    {
                        int sc = spacerCol + 1 + s;
                        ws.Cells[currentRow, sc].Value = counts[s];
                    }

                    currentRow++;
                }

                int empEndRow = currentRow - 1;

                if (empEndRow >= empStartRow)
                {
                    // Batch format employee rows borders and alignments (O(1) in EPPlus)
                    var empRange = ws.Cells[empStartRow, 1, empEndRow, totalCols];
                    empRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    empRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    empRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    empRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    empRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    ws.Cells[empStartRow, 1, empEndRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[empStartRow, 3, empEndRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    ws.Cells[empStartRow, 5, empEndRow, 4 + dayCount].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[empStartRow, spacerCol + 1, empEndRow, totalCols].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ws.Cells[empStartRow, spacerCol + 1, empEndRow, totalCols].Style.Font.Bold = true;
                }

                // Department Total Employees Row (Calibri 8pt/11pt Bold, Background #EFEFEF)
                ws.Row(currentRow).Height = 16;
                ws.Cells[currentRow, 1, currentRow, 2].Merge = true;
                ws.Cells[currentRow, 1].Value = "Total Employees:";
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.Cells[currentRow, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                ws.Cells[currentRow, 3].Value = dept.TotalEmployees;
                ws.Cells[currentRow, 3].Style.Font.Size = 11;
                ws.Cells[currentRow, 3].Style.Font.Bold = true;
                ws.Cells[currentRow, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[currentRow, 3].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                var subtotalRange = ws.Cells[currentRow, 1, currentRow, totalCols];
                subtotalRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                subtotalRange.Style.Fill.BackgroundColor.SetColor(subtotalBgColor);
                subtotalRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                subtotalRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                subtotalRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                subtotalRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

                currentRow += 2; // Blank row after department
            }

            int gridEndRow = currentRow;

            // Blazing-fast Conditional Formatting for all day columns (Rule-based, 0ms style registry overhead)
            if (gridEndRow >= gridStartRow)
            {
                var dayRangeAddress = new ExcelAddress(gridStartRow, 5, gridEndRow, 4 + dayCount);

                var cfP = ws.ConditionalFormatting.AddEqual(dayRangeAddress);
                cfP.Formula = "\"P\"";
                cfP.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cfP.Style.Fill.BackgroundColor.Color = presentBgColor;

                var cfW = ws.ConditionalFormatting.AddEqual(dayRangeAddress);
                cfW.Formula = "\"W\"";
                cfW.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cfW.Style.Fill.BackgroundColor.Color = weekendBgColor;

                var cfL = ws.ConditionalFormatting.AddEqual(dayRangeAddress);
                cfL.Formula = "\"L\"";
                cfL.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cfL.Style.Fill.BackgroundColor.Color = lateBgColor;

                var cfA = ws.ConditionalFormatting.AddEqual(dayRangeAddress);
                cfA.Formula = "\"A\"";
                cfA.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cfA.Style.Fill.BackgroundColor.Color = absentBgColor;

                var cfH = ws.ConditionalFormatting.AddEqual(dayRangeAddress);
                cfH.Formula = "\"H\"";
                cfH.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cfH.Style.Fill.BackgroundColor.Color = weekendBgColor;
            }

            // Set Column Widths matching Excel
            ws.Column(1).Width = 4.5;   // SN
            ws.Column(2).Width = 14.5;  // Emp ID
            ws.Column(3).Width = 28.0;  // Name
            ws.Column(4).Width = 18.0;  // Designation

            for (int i = 0; i < data.Days.Count; i++)
            {
                ws.Column(5 + i).Width = 3.6; // Days
            }

            ws.Column(spacerCol).Width = 3.2; // Spacer

            for (int s = 0; s < summaryHeaders.Length; s++)
            {
                int sc = spacerCol + 1 + s;
                ws.Column(sc).Width = (summaryHeaders[s] == "MRL" ? 4.5 : 3.6);
            }

            return await package.GetAsByteArrayAsync();
        }

        // ═════════════════════════════════════════════════════════════
        // ── PDF Export (iText7) - Landscape Layout
        // ═════════════════════════════════════════════════════════════
        public async Task<byte[]> GeneratePdfAsync(MonthlyAttendanceFilterDto filter, string? logoPhysicalPath = null)
        {
            var data = await GetReportDataAsync(filter);
            if (data == null || !data.DepartmentGroups.Any())
                return Array.Empty<byte>();

            using var firstPassStream = new MemoryStream();
            var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            using (var writer = new PdfWriter(firstPassStream))
            using (var pdfDoc = new PdfDocument(writer))
            {
                pdfDoc.SetDefaultPageSize(PageSize.A4.Rotate());
                using (var doc = new PdfDocument2(pdfDoc))
                {
                    doc.SetMargins(15, 15, 25, 15);
                    doc.SetFont(regularFont).SetFontSize(5.5f);

                    var headerBgColor = new DeviceRgb(217, 234, 211);     // #D9EAD3
                    var presentBgColor = new DeviceRgb(198, 239, 206);    // #C6EFCE
                    var weekendBgColor = new DeviceRgb(217, 234, 211);    // #D9EAD3
                    var lateBgColor = new DeviceRgb(255, 242, 204);       // #FFF2CC
                    var absentBgColor = new DeviceRgb(244, 204, 204);     // #F4CCCC
                    var subtotalBgColor = new DeviceRgb(239, 239, 239);   // #EFEFEF
                    var borderColor = new DeviceRgb(180, 180, 180);
                    var cellBorder = new SolidBorder(borderColor, 0.4f);

                    var summaryHeaders = new[] { "P", "L", "A", "W", "H", "CL", "SL", "UL", "PL", "MRL", "EL", "FL", "ML" };

                    // ── Fast Cell Helpers ──
                    Cell CreateCell(string text, TextAlignment align = TextAlignment.CENTER, DeviceRgb? bg = null)
                    {
                        var cell = new Cell()
                            .SetTextAlignment(align)
                            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SetBorder(cellBorder)
                            .SetPadding(0.8f);
                        if (!string.IsNullOrEmpty(text))
                        {
                            cell.Add(new Paragraph(text));
                        }
                        if (bg != null) cell.SetBackgroundColor(bg);
                        return cell;
                    }

                    Cell CreateHeaderCell(string text, int rowSpan = 1, int colSpan = 1, float fontSize = 5.5f)
                    {
                        var p = new Paragraph(text).SetFont(boldFont).SetFontSize(fontSize);
                        return new Cell(rowSpan, colSpan).Add(p)
                            .SetBackgroundColor(headerBgColor)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SetBorder(cellBorder)
                            .SetPadding(1f);
                    }

                    // ── Top Header Table: Left Logo, Center (Company Name, Report Title, Date) ──
                    var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 20, 60, 20 }))
                        .UseAllAvailableWidth()
                        .SetMarginBottom(8)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    var logoCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetVerticalAlignment(VerticalAlignment.MIDDLE);
                    if (!string.IsNullOrWhiteSpace(logoPhysicalPath) && File.Exists(logoPhysicalPath))
                    {
                        try
                        {
                            var imgData = ImageDataFactory.Create(logoPhysicalPath);
                            var logoImg = new iText.Layout.Element.Image(imgData);
                            logoImg.ScaleToFit(110, 42);
                            logoCell.Add(logoImg);
                        }
                        catch { }
                    }
                    headerTable.AddCell(logoCell);

                    var centerCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                        .Add(new Paragraph(data.Header?.CompanyName ?? "DataPath Ltd.").SetFont(boldFont).SetFontSize(14).SetMarginBottom(1))
                        .Add(new Paragraph(data.Header?.ReportTitle ?? "Monthly Attendance Details Report").SetFont(boldFont).SetFontSize(11).SetMarginBottom(1))
                        .Add(new Paragraph(!string.IsNullOrWhiteSpace(data.Header?.Subtitle) ? data.Header.Subtitle : (data.Header?.DateRangeDisplay ?? "")).SetFont(boldFont).SetFontSize(9).SetMarginBottom(0));
                    headerTable.AddCell(centerCell);

                    var rightCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                    headerTable.AddCell(rightCell);

                    doc.Add(headerTable);

                    int dayCount = data.Days.Count;
                    int totalCols = 4 + dayCount + 1 + summaryHeaders.Length;

                    // Column width ratios
                    float[] colWidths = new float[totalCols];
                    colWidths[0] = 16f;  // SN
                    colWidths[1] = 52f;  // Emp ID
                    colWidths[2] = 95f;  // Name
                    colWidths[3] = 65f;  // Designation
                    for (int i = 0; i < dayCount; i++) colWidths[4 + i] = 14.5f; // Days
                    colWidths[4 + dayCount] = 10f; // Spacer
                    for (int s = 0; s < summaryHeaders.Length; s++) colWidths[5 + dayCount + s] = 14.5f; // Summary

                    foreach (var dept in data.DepartmentGroups)
                    {
                        var deptBanner = new Paragraph("Department: " + dept.DepartmentName)
                            .SetFont(boldFont)
                            .SetFontSize(10)
                            .SetTextAlignment(TextAlignment.LEFT)
                            .SetMarginTop(8)
                            .SetMarginBottom(3)
                            .SetKeepWithNext(true);
                        doc.Add(deptBanner);

                        var table = new Table(UnitValue.CreatePercentArray(colWidths));
                        table.SetWidth(UnitValue.CreatePercentValue(100));
                        table.SetFixedLayout();

                        // Header Row 1: Day numbers & names
                        table.AddHeaderCell(CreateHeaderCell("SN", 2, 1, 6f));
                        table.AddHeaderCell(CreateHeaderCell("Emp ID", 2, 1, 6f));
                        table.AddHeaderCell(CreateHeaderCell("Name", 2, 1, 6f));
                        table.AddHeaderCell(CreateHeaderCell("Designation", 2, 1, 6f));

                        // Days Row 1: Day numbers
                        for (int i = 0; i < dayCount; i++)
                        {
                            table.AddHeaderCell(CreateHeaderCell(data.Days[i].DayNumber.ToString(), 1, 1, 5.5f));
                        }

                        // Spacer Row 1
                        table.AddHeaderCell(CreateHeaderCell("", 2, 1));

                        // Summary Row 1: P..ML
                        for (int s = 0; s < summaryHeaders.Length; s++)
                        {
                            table.AddHeaderCell(CreateHeaderCell(summaryHeaders[s], 2, 1, 5.5f));
                        }

                        // Header Row 2: Day short names
                        for (int i = 0; i < dayCount; i++)
                        {
                            table.AddHeaderCell(CreateHeaderCell(data.Days[i].DayName, 1, 1, 5f));
                        }

                        // Employees Rows
                        foreach (var emp in dept.Employees)
                        {
                            table.AddCell(CreateCell(emp.SerialNo.ToString(), TextAlignment.CENTER));
                            table.AddCell(CreateCell(emp.EmployeeId, TextAlignment.CENTER));
                            table.AddCell(CreateCell(emp.EmployeeName, TextAlignment.LEFT));
                            table.AddCell(CreateCell(emp.Designation, TextAlignment.LEFT));

                            // Days statuses
                            for (int i = 0; i < dayCount; i++)
                            {
                                int dayNum = data.Days[i].DayNumber;
                                string st = emp.DailyStatuses.TryGetValue(dayNum, out var v) ? v : "";
                                DeviceRgb? bg = null;
                                var stU = st.Trim().ToUpperInvariant();
                                if (stU == "P") bg = presentBgColor;
                                else if (stU == "W") bg = weekendBgColor;
                                else if (stU == "L") bg = lateBgColor;
                                else if (stU == "A") bg = absentBgColor;
                                else if (stU == "H") bg = weekendBgColor;

                                table.AddCell(CreateCell(st, TextAlignment.CENTER, bg));
                            }

                            // Spacer
                            table.AddCell(CreateCell(""));

                            // Summary counts
                            int[] counts = new[]
                            {
                                emp.TotalPresent, emp.TotalLate, emp.TotalAbsent, emp.TotalWeekend,
                                emp.TotalHoliday, emp.TotalCL, emp.TotalSL, emp.TotalUL,
                                emp.TotalPL, emp.TotalMRL, emp.TotalEL, emp.TotalFL, emp.TotalML
                            };

                            for (int s = 0; s < counts.Length; s++)
                            {
                                table.AddCell(CreateCell(counts[s].ToString(), TextAlignment.CENTER));
                            }
                        }

                        // Department Total Employees Row
                        var totalLabel = new Cell(1, 2).Add(new Paragraph("Total Employees:").SetFont(boldFont).SetFontSize(6f))
                            .SetBackgroundColor(subtotalBgColor).SetTextAlignment(TextAlignment.LEFT).SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SetBorder(cellBorder).SetPadding(1.2f);
                        table.AddCell(totalLabel);

                        var totalVal = new Cell().Add(new Paragraph(dept.TotalEmployees.ToString()).SetFont(boldFont).SetFontSize(7f))
                            .SetBackgroundColor(subtotalBgColor).SetTextAlignment(TextAlignment.CENTER).SetVerticalAlignment(VerticalAlignment.MIDDLE)
                            .SetBorder(cellBorder).SetPadding(1.2f);
                        table.AddCell(totalVal);

                        for (int rem = 4; rem <= totalCols; rem++)
                        {
                            table.AddCell(CreateCell("", TextAlignment.CENTER, subtotalBgColor));
                        }

                        doc.Add(table);
                    }

                    doc.Close();
                }
            }

            // 2nd pass: stamp page numbers and print datetime on each page
            using var readerStream = new MemoryStream(firstPassStream.ToArray());
            using var reader = new PdfReader(readerStream);
            using var outputStream = new MemoryStream();
            using var outputWriter = new PdfWriter(outputStream);
            using var outputPdf = new PdfDocument(reader, outputWriter);

            var totalPages = outputPdf.GetNumberOfPages();
            string printTimeStr = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt");

            // CRITICAL: Fresh font instance for outputPdf to prevent PdfException: PdfPages tree could be generated only once
            var footerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            for (int i = 1; i <= totalPages; i++)
            {
                var page = outputPdf.GetPage(i);
                var pageSize = page.GetPageSize();
                var canvas = new PdfCanvas(page);

                canvas.BeginText()
                    .SetFontAndSize(footerFont, 7f)
                    .MoveText(18, 12)
                    .ShowText($"Print Datetime: {printTimeStr}")
                    .EndText();

                string pageStr = $"Page {i} of {totalPages}";
                float strWidth = footerFont.GetWidth(pageStr, 7f);
                canvas.BeginText()
                    .SetFontAndSize(footerFont, 7f)
                    .MoveText(pageSize.GetWidth() - 18 - strWidth, 12)
                    .ShowText(pageStr)
                    .EndText();
            }

            outputPdf.Close();
            return outputStream.ToArray();
        }

        // ═════════════════════════════════════════════════════════════
        // ── CSV Export ───────────────────────────────────────────────
        // ═════════════════════════════════════════════════════════════
        public async Task<byte[]> ExportCsvAsync(MonthlyAttendanceFilterDto filter)
        {
            var data = await GetReportDataAsync(filter);
            var sb = new StringBuilder();
            var bom = Encoding.UTF8.GetPreamble();

            sb.AppendLine($"\"{data.Header.ReportTitle}\"");
            sb.AppendLine($"\"{data.Header.Subtitle}\"");
            sb.AppendLine();

            var summaryHeaders = new[] { "P", "L", "A", "W", "H", "CL", "SL", "UL", "PL", "MRL", "EL", "FL", "ML" };

            // Days header line
            var headerCols = new List<string> { "SN", "Emp ID", "Name", "Designation", "Department" };
            foreach (var d in data.Days)
            {
                headerCols.Add($"{d.DayNumber} ({d.DayName})");
            }
            headerCols.Add(""); // Spacer
            headerCols.AddRange(summaryHeaders);

            sb.AppendLine(string.Join(",", headerCols.Select(EscapeCsv)));

            foreach (var dept in data.DepartmentGroups)
            {
                sb.AppendLine($"\"Department: {dept.DepartmentName}\"");

                foreach (var emp in dept.Employees)
                {
                    var row = new List<string>
                    {
                        emp.SerialNo.ToString(),
                        emp.EmployeeId,
                        emp.EmployeeName,
                        emp.Designation,
                        emp.DepartmentName
                    };

                    foreach (var d in data.Days)
                    {
                        string st = emp.DailyStatuses.TryGetValue(d.DayNumber, out var val) ? val : "";
                        row.Add(st);
                    }

                    row.Add(""); // spacer
                    row.Add(emp.TotalPresent.ToString());
                    row.Add(emp.TotalLate.ToString());
                    row.Add(emp.TotalAbsent.ToString());
                    row.Add(emp.TotalWeekend.ToString());
                    row.Add(emp.TotalHoliday.ToString());
                    row.Add(emp.TotalCL.ToString());
                    row.Add(emp.TotalSL.ToString());
                    row.Add(emp.TotalUL.ToString());
                    row.Add(emp.TotalPL.ToString());
                    row.Add(emp.TotalMRL.ToString());
                    row.Add(emp.TotalEL.ToString());
                    row.Add(emp.TotalFL.ToString());
                    row.Add(emp.TotalML.ToString());

                    sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
                }

                sb.AppendLine($"\"Total Employees:\",,\"{dept.TotalEmployees}\"");
                sb.AppendLine();
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[bom.Length + csvBytes.Length];
            Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
            Buffer.BlockCopy(csvBytes, 0, result, bom.Length, csvBytes.Length);

            return result;
        }

        private static string EscapeCsv(string? val)
        {
            if (string.IsNullOrEmpty(val)) return "\"\"";
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
    }
}
