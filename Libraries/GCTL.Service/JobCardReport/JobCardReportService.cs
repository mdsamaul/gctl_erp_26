// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Service/JobCardReport/JobCardReportService.cs
// ═══════════════════════════════════════════════════════════════════
using Dapper;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.JobCardReport;
using GCTL.Data.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Drawing;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Service.JobCardReport
{
    public class JobCardReportService : IJobCardReportService
    {
        private readonly IRepository<CoreAccessCode> _accessCodeRepo;
        private readonly string _connectionString;

        public JobCardReportService(
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
                            && (x.Title == "Job Card Report" || x.Title == "Attendance /Job Card Report" || x.Title == "Daily Attendance Details")
                            && x.TitleCheck);

            return hasExplicitPermission || accessCode == "0002" || accessCode == "0003" || accessCode == "0005";
        }

        // ── Get Report Data ──────────────────────────────────────────
        public async Task<JobCardReportResultDto> GetReportDataAsync(JobCardReportFilterDto filter)
        {
            try
            {
                var result = new JobCardReportResultDto();

                var empCsv = filter.EmployeeIds != null && filter.EmployeeIds.Any()
                    ? string.Join(",", filter.EmployeeIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
                    : null;

                var companyCode = string.IsNullOrWhiteSpace(filter.CompanyCode) ? null : filter.CompanyCode.Trim();
                var loginEmployeeId = string.IsNullOrWhiteSpace(filter.LoginEmployeeId) ? null : filter.LoginEmployeeId.Trim();
                var accessCodeId = string.IsNullOrWhiteSpace(filter.AccessCodeId) ? null : filter.AccessCodeId.Trim();

                DateTime? fromDate = ParseDate(filter.FromDate);
                DateTime? toDate = ParseDate(filter.ToDate);

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var param = new DynamicParameters();
                param.Add("@CompanyCode", companyCode, DbType.String);
                param.Add("@EmployeeIds", string.IsNullOrWhiteSpace(empCsv) ? null : empCsv, DbType.String);
                param.Add("@FromDate", fromDate, DbType.Date);
                param.Add("@ToDate", toDate, DbType.Date);
                param.Add("@LoginEmployeeId", loginEmployeeId, DbType.String);
                param.Add("@AccessCodeId", accessCodeId, DbType.String);

                using var multi = await conn.QueryMultipleAsync(
                    "usp_JobCardReport",
                    param,
                    commandType: CommandType.StoredProcedure);

                result.Rows = (await multi.ReadAsync<JobCardReportRowDto>()).ToList();
                foreach (var r in result.Rows)
                {
                    r.InTime = CleanZero(r.InTime);
                    r.Late = CleanZero(r.Late);
                    r.OutTime = CleanZero(r.OutTime);
                    r.EarlyOut = CleanZero(r.EarlyOut);
                    r.WorkHours = CleanZero(r.WorkHours);
                }

                var header = await multi.ReadFirstOrDefaultAsync<JobCardReportHeaderDto>();
                if (header != null)
                {
                    result.Header = header;
                }
                else
                {
                    result.Header = new JobCardReportHeaderDto
                    {
                        CompanyName = "DataPath Ltd.",
                        FromDate = fromDate.HasValue ? fromDate.Value.ToString("dd/MM/yyyy") : "",
                        ToDate = toDate.HasValue ? toDate.Value.ToString("dd/MM/yyyy") : "",
                        DateRangeDisplay = $"Date: {(fromDate.HasValue ? fromDate.Value.ToString("dd/MM/yyyy") : "")} - {(toDate.HasValue ? toDate.Value.ToString("dd/MM/yyyy") : "")}"
                    };
                }

                // Group by employee for convenience
                var grouped = result.Rows
                    .GroupBy(r => r.EmployeeId)
                    .Select(g => new EmployeeJobCardGroupDto
                    {
                        EmployeeId = g.Key,
                        EmployeeName = g.First().EmployeeName,
                        Designation = g.First().Designation,
                        DepartmentName = g.First().DepartmentName,
                        CompanyName = g.First().CompanyName,
                        Rows = g.ToList()
                    })
                    .ToList();

                result.EmployeeGroups = grouped;

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        // ── Excel Export ─────────────────────────────────────────────
        public async Task<byte[]> ExportExcelAsync(JobCardReportFilterDto filter, string? logoPhysicalPath = null)
        {
            var data = await GetReportDataAsync(filter);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Job Card Report");

            int colCount = 10;
            int currentRow = 1;

            // Add Logo if available
            if (!string.IsNullOrWhiteSpace(logoPhysicalPath) && File.Exists(logoPhysicalPath))
            {
                try
                {
                    var fileInfo = new FileInfo(logoPhysicalPath);
                    var pic = ws.Drawings.AddPicture("Logo", fileInfo);
                    pic.SetPosition(0, 5, 0, 5);
                    pic.SetSize(110, 45);
                }
                catch { }
            }

            // Company Title
            ws.Cells[currentRow, 1, currentRow, colCount].Merge = true;
            ws.Cells[currentRow, 1].Value = data.Header?.CompanyName ?? "DataPath Ltd.";
            ws.Cells[currentRow, 1].Style.Font.Bold = true;
            ws.Cells[currentRow, 1].Style.Font.Size = 14;
            ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            currentRow++;

            // Subtitle
            ws.Cells[currentRow, 1, currentRow, colCount].Merge = true;
            ws.Cells[currentRow, 1].Value = "Attendance Job Card Report";
            ws.Cells[currentRow, 1].Style.Font.Bold = true;
            ws.Cells[currentRow, 1].Style.Font.Size = 11;
            ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            currentRow++;

            // Date Range
            ws.Cells[currentRow, 1, currentRow, colCount].Merge = true;
            ws.Cells[currentRow, 1].Value = data.Header?.DateRangeDisplay ?? "";
            ws.Cells[currentRow, 1].Style.Font.Bold = true;
            ws.Cells[currentRow, 1].Style.Font.Size = 10;
            ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            currentRow += 2;

            var groups = data.EmployeeGroups.Any() ? data.EmployeeGroups : new List<EmployeeJobCardGroupDto>();

            string[] headers = new[] { "Date", "Day", "Shift", "In Time", "Late", "Out Time", "W. Hour(s)", "Status", "Remarks" };
            colCount = headers.Length;

            foreach (var emp in groups)
            {
                // Employee Details Header Block
                ws.Cells[currentRow, 1].Value = "Emp. ID";
                ws.Cells[currentRow, 2, currentRow, colCount].Merge = true;
                ws.Cells[currentRow, 2].Value = ": " + emp.EmployeeId;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                ws.Cells[currentRow, 1].Value = "Name";
                ws.Cells[currentRow, 2, currentRow, colCount].Merge = true;
                ws.Cells[currentRow, 2].Value = ": " + emp.EmployeeName;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                ws.Cells[currentRow, 1].Value = "Designation";
                ws.Cells[currentRow, 2, currentRow, colCount].Merge = true;
                ws.Cells[currentRow, 2].Value = ": " + emp.Designation;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                ws.Cells[currentRow, 1].Value = "Department";
                ws.Cells[currentRow, 2, currentRow, colCount].Merge = true;
                ws.Cells[currentRow, 2].Value = ": " + emp.DepartmentName;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                currentRow++; // Empty row before table

                // Table Header
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cells[currentRow, c + 1];
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(240, 240, 240));
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
                currentRow++;

                // Data Rows
                foreach (var r in emp.Rows)
                {
                    SetCell(ws, currentRow, 1, r.DateDisplay);
                    SetCell(ws, currentRow, 2, r.Day);
                    SetCell(ws, currentRow, 3, r.Shift);
                    SetCell(ws, currentRow, 4, CleanZero(r.InTime));
                    SetCell(ws, currentRow, 5, CleanZero(r.Late));
                    SetCell(ws, currentRow, 6, CleanZero(r.OutTime));
                    SetCell(ws, currentRow, 7, CleanZero(r.WorkHours));

                    var statusCell = ws.Cells[currentRow, 8];
                    statusCell.Value = r.Status;
                    statusCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    statusCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    statusCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    statusCell.Style.Font.Bold = true;

                    if (r.Status == "P")
                        statusCell.Style.Font.Color.SetColor(Color.FromArgb(40, 167, 69)); // Green
                    else if (r.Status == "L")
                        statusCell.Style.Font.Color.SetColor(Color.FromArgb(217, 130, 43)); // Amber
                    else if (r.Status == "A")
                        statusCell.Style.Font.Color.SetColor(Color.FromArgb(220, 53, 69)); // Red

                    SetCell(ws, currentRow, 9, r.Remarks, leftAlign: true);
                    currentRow++;
                }

                // ── Employee Summary Stats for Excel ──
                int presentCount = 0;
                int lateCount = 0;
                int absentCount = 0;
                int weekendCount = 0;
                int holidayCount = 0;
                int leaveCount = 0;
                decimal totalWorkHrs = 0;

                foreach (var r in emp.Rows)
                {
                    var st = (r.Status ?? "").Trim();
                    if (st == "P") presentCount++;
                    else if (st == "L") lateCount++;
                    else if (st == "A") absentCount++;
                    else if (st == "W") weekendCount++;
                    else if (st == "H") holidayCount++;
                    else leaveCount++;

                    if (!string.IsNullOrWhiteSpace(r.WorkHours) && r.WorkHours != "00:00:00" && r.WorkHours != "0.00" && r.WorkHours != "0")
                    {
                        if (r.WorkHours.Contains(':'))
                        {
                            var wp = r.WorkHours.Split(':');
                            if (wp.Length == 3 && int.TryParse(wp[0], out int wh) && int.TryParse(wp[1], out int wm))
                            {
                                long sec = (wh * 3600) + (wm * 60);
                                totalWorkHrs += Math.Round((decimal)sec / 3600m, 2);
                            }
                            else if (wp.Length == 2 && int.TryParse(wp[0], out int wh2) && int.TryParse(wp[1], out int wm2))
                            {
                                long sec = (wh2 * 3600) + (wm2 * 60);
                                totalWorkHrs += Math.Round((decimal)sec / 3600m, 2);
                            }
                        }
                        else if (decimal.TryParse(r.WorkHours, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var whDec))
                        {
                            totalWorkHrs += whDec;
                        }
                    }
                }

                int totalPresent = presentCount + lateCount;
                int totalAtt = totalPresent + weekendCount + holidayCount + leaveCount;
                decimal aveWorkHrs = totalPresent > 0 ? Math.Round(totalWorkHrs / totalPresent, 2) : 0;

                // Summary Stats Box (Exact match to Excel template)
                currentRow++;
                ws.Cells[currentRow, 1].Value = "Total Present :";
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 2].Value = totalPresent;
                ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.Cells[currentRow, 4].Value = "Total Weekend:";
                ws.Cells[currentRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 5].Value = weekendCount;
                ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Absent :";
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 2].Value = absentCount;
                ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.Cells[currentRow, 4].Value = "Total Holiday:";
                ws.Cells[currentRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 5].Value = holidayCount;
                ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Late :";
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 2].Value = lateCount;
                ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.Cells[currentRow, 4].Value = "Total Att.:";
                ws.Cells[currentRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 5].Value = totalAtt;
                ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Leave :";
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 2].Value = leaveCount;
                ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.Cells[currentRow, 4].Value = "Ave. W. Hrs/Day.:";
                ws.Cells[currentRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 5].Value = aveWorkHrs;
                ws.Cells[currentRow, 5].Style.Numberformat.Format = "0.00";
                ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total W. Hrs :";
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                ws.Cells[currentRow, 2].Value = totalWorkHrs;
                ws.Cells[currentRow, 2].Style.Numberformat.Format = "0.00";
                ws.Cells[currentRow, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

                currentRow += 3; // Spacing before next employee
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            return await package.GetAsByteArrayAsync();
        }

        // ── CSV Export ───────────────────────────────────────────────
        public async Task<byte[]> ExportCsvAsync(JobCardReportFilterDto filter)
        {
            var data = await GetReportDataAsync(filter);

            var sb = new StringBuilder();

            // UTF-8 BOM for Excel compatibility
            var bom = Encoding.UTF8.GetPreamble();

            // CSV Header
            sb.AppendLine("Emp. ID,Name,Designation,Department,Date,Day,Shift,In Time,Late,Out Time,Early Out,W. Hour(s),Status,Remarks");

            foreach (var r in data.Rows)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(r.EmployeeId),
                    EscapeCsv(r.EmployeeName),
                    EscapeCsv(r.Designation),
                    EscapeCsv(r.DepartmentName),
                    EscapeCsv(r.DateDisplay),
                    EscapeCsv(r.Day),
                    EscapeCsv(r.Shift),
                    EscapeCsv(r.InTime),
                    EscapeCsv(r.Late),
                    EscapeCsv(r.OutTime),
                    EscapeCsv(r.EarlyOut),
                    EscapeCsv(r.WorkHours),
                    EscapeCsv(r.Status),
                    EscapeCsv(r.Remarks)
                ));
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[bom.Length + csvBytes.Length];
            Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
            Buffer.BlockCopy(csvBytes, 0, result, bom.Length, csvBytes.Length);

            return result;
        }

        // ── Helpers ──────────────────────────────────────────────────
        private static void SetCell(ExcelWorksheet ws, int row, int col, object? value, bool leftAlign = false)
        {
            var cell = ws.Cells[row, col];
            cell.Value = value;
            cell.Style.HorizontalAlignment = leftAlign ? ExcelHorizontalAlignment.Left : ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        private static string EscapeCsv(string? val)
        {
            if (string.IsNullOrEmpty(val)) return "\"\"";
            val = val.Replace("\"", "\"\"");
            return $"\"{val}\"";
        }

        private static DateTime? ParseDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;

            string[] formats = new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(dateStr.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.Date;

            if (DateTime.TryParse(dateStr.Trim(), out var parsed))
                return parsed.Date;

            return null;
        }

        private static string CleanZero(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            var v = val.Trim();
            if (v == "0" || v == "0.0" || v == "0.00" || v == "00:00:00" || v == "0:00:00" || v == "00:00" || v == "0:00" || v == "--:--" || v == "--:--:--")
                return "";
            if (decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d == 0m)
                return "";
            return v;
        }
    }
}
