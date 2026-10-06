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

                currentRow += 2; // Spacing before next employee
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
