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
            ws.Cells[currentRow, 1].Value = "Job Card Report";
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

            string[] headers = new[] { "Date", "Day", "Shift", "In Time", "Late", "Out Time", "Early Out", "W. Hour(s)", "Status", "Remarks" };

            foreach (var emp in groups)
            {
                // Employee Details Header Block
                ws.Cells[currentRow, 1].Value = "Emp. ID";
                ws.Cells[currentRow, 2].Value = ": " + emp.EmployeeId;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Name";
                ws.Cells[currentRow, 2].Value = ": " + emp.EmployeeName;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Designation";
                ws.Cells[currentRow, 2].Value = ": " + emp.Designation;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Department";
                ws.Cells[currentRow, 2].Value = ": " + emp.DepartmentName;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                currentRow++;

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
                    SetCell(ws, currentRow, 4, r.InTime);
                    SetCell(ws, currentRow, 5, r.Late);
                    SetCell(ws, currentRow, 6, r.OutTime);
                    SetCell(ws, currentRow, 7, r.EarlyOut);
                    SetCell(ws, currentRow, 8, r.WorkHours);

                    var statusCell = ws.Cells[currentRow, 9];
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

                    SetCell(ws, currentRow, 10, r.Remarks, leftAlign: true);
                    currentRow++;
                }

                // ── Employee Summary Stats for Excel ──
                int totalRows = emp.Rows.Count;
                int presentCount = 0;
                int lateCount = 0;
                int absentCount = 0;
                int weekendCount = 0;
                int holidayCount = 0;
                int leaveCount = 0;
                int earlyOutCount = 0;
                long totalLateSec = 0;
                long totalWorkSec = 0;

                foreach (var r in emp.Rows)
                {
                    var st = (r.Status ?? "").Trim();
                    if (st == "P") presentCount++;
                    else if (st == "L") lateCount++;
                    else if (st == "A") absentCount++;
                    else if (st == "W") weekendCount++;
                    else if (st == "H") holidayCount++;
                    else leaveCount++;

                    if (!string.IsNullOrWhiteSpace(r.Late) && r.Late != "00:00:00")
                    {
                        var lp = r.Late.Split(':');
                        if (lp.Length == 3 && int.TryParse(lp[0], out int h) && int.TryParse(lp[1], out int m) && int.TryParse(lp[2], out int s))
                            totalLateSec += (h * 3600) + (m * 60) + s;
                    }

                    if (!string.IsNullOrWhiteSpace(r.EarlyOut) && r.EarlyOut != "00:00:00")
                        earlyOutCount++;

                    if (!string.IsNullOrWhiteSpace(r.WorkHours) && r.WorkHours != "00:00:00")
                    {
                        var wp = r.WorkHours.Split(':');
                        if (wp.Length == 3 && int.TryParse(wp[0], out int wh) && int.TryParse(wp[1], out int wm) && int.TryParse(wp[2], out int wsec))
                            totalWorkSec += (wh * 3600) + (wm * 60) + wsec;
                    }
                }

                int totalPresent = presentCount + lateCount;
                int totalWorkingDays = totalRows - weekendCount - holidayCount;
                int totalAtt = totalPresent + weekendCount + holidayCount + leaveCount;

                long hL = totalLateSec / 3600;
                long mL = (totalLateSec % 3600) / 60;
                long sL = totalLateSec % 60;
                string totalLateStr = $"{hL}:{mL:D2}:{sL:D2}";

                long hW = totalWorkSec / 3600;
                long mW = (totalWorkSec % 3600) / 60;
                long sW = totalWorkSec % 60;
                string totalWorkStr = $"{hW}:{mW:D2}:{sW:D2}";

                string avgTimeStr = "0:00";
                if (totalPresent > 0)
                {
                    long avgSec = totalWorkSec / totalPresent;
                    long avgH = avgSec / 3600;
                    long avgM = (avgSec % 3600) / 60;
                    avgTimeStr = $"{avgH}:{avgM:D2}";
                }

                // Totals Row
                currentRow++;
                ws.Cells[currentRow, 5].Value = totalLateStr;
                ws.Cells[currentRow, 5].Style.Font.Bold = true;
                ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[currentRow, 8].Value = totalWorkStr;
                ws.Cells[currentRow, 8].Style.Font.Bold = true;
                ws.Cells[currentRow, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[currentRow, 10].Value = "Average Time: " + avgTimeStr;
                ws.Cells[currentRow, 10].Style.Font.Bold = true;

                // Attendance Stats Box
                currentRow += 2;
                ws.Cells[currentRow, 1].Value = "Total Working Days"; ws.Cells[currentRow, 2].Value = totalWorkingDays;
                ws.Cells[currentRow, 3].Value = "Total Weekend:"; ws.Cells[currentRow, 4].Value = weekendCount;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Present :"; ws.Cells[currentRow, 2].Value = totalPresent;
                ws.Cells[currentRow, 3].Value = "Total Holiday:"; ws.Cells[currentRow, 4].Value = holidayCount;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Absent :"; ws.Cells[currentRow, 2].Value = absentCount;
                ws.Cells[currentRow, 3].Value = "Total Early Out:"; ws.Cells[currentRow, 4].Value = earlyOutCount;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Late :"; ws.Cells[currentRow, 2].Value = lateCount;
                ws.Cells[currentRow, 3].Value = "Total Att.:"; ws.Cells[currentRow, 4].Value = totalAtt;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Leave :"; ws.Cells[currentRow, 2].Value = leaveCount;
                currentRow += 3;

                // Signatures
                ws.Cells[currentRow, 1].Value = "Prepared by";
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[currentRow, 5].Value = "Checked by";
                ws.Cells[currentRow, 5].Style.Font.Bold = true;
                ws.Cells[currentRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[currentRow, 9].Value = "Authorized by";
                ws.Cells[currentRow, 9].Style.Font.Bold = true;
                ws.Cells[currentRow, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                currentRow += 2;

                // Legend
                ws.Cells[currentRow, 1, currentRow, colCount].Merge = true;
                ws.Cells[currentRow, 1].Value = "Status Legend: P-Present, L- Late, A- Absent, W- Weekend, H- Holiday, CL- Casual Leave, SL- Sick Leave, UL- Unpaid Leave, ML- Maternity Leave, PL- Paternity Leave, MarL- Marriage Leave, HL- Hajj Leave, UmrL- Umrah Leave";
                ws.Cells[currentRow, 1].Style.Font.Size = 8;
                ws.Cells[currentRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

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
    }
}
