// ═══════════════════════════════════════════════════════════════════
// File: GCTL.Service/JobCardBulkReport/JobCardBulkReportService.cs
// ═══════════════════════════════════════════════════════════════════
using Dapper;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.JobCardBulkReport;
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

namespace GCTL.Service.JobCardBulkReport
{
    public class JobCardBulkReportService : IJobCardBulkReportService
    {
        private readonly IRepository<CoreAccessCode> _accessCodeRepo;
        private readonly string _connectionString;

        public JobCardBulkReportService(
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
                            && (x.Title == "Job Card Bulk Report" || x.Title == "Job Card Report" || x.Title == "Attendance /Job Card Report")
                            && x.TitleCheck);

            return hasExplicitPermission || accessCode == "0002" || accessCode == "0003" || accessCode == "0005";
        }

        private static string? ToCsv(List<string>? list)
        {
            if (list == null || !list.Any()) return null;
            var clean = list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            return clean.Any() ? string.Join(",", clean) : null;
        }

        // ── Get Report Data ──────────────────────────────────────────
        public async Task<JobCardBulkReportResultDto> GetReportDataAsync(JobCardBulkReportFilterDto filter)
        {
            try
            {
                var result = new JobCardBulkReportResultDto();

                var compCsv = filter.CompanyCodes != null && filter.CompanyCodes.Any()
                    ? ToCsv(filter.CompanyCodes)
                    : (!string.IsNullOrWhiteSpace(filter.CompanyCode) ? filter.CompanyCode.Trim() : null);

                var deptCsv = ToCsv(filter.DepartmentCodes);
                var desigCsv = ToCsv(filter.DesignationCodes);
                var empCsv = ToCsv(filter.EmployeeIds);

                var loginEmployeeId = string.IsNullOrWhiteSpace(filter.LoginEmployeeId) ? null : filter.LoginEmployeeId.Trim();
                var accessCodeId = string.IsNullOrWhiteSpace(filter.AccessCodeId) ? null : filter.AccessCodeId.Trim();

                DateTime? fromDate = ParseDate(filter.FromDate);
                DateTime? toDate = ParseDate(filter.ToDate);

                // If ByMonth was selected and dates are empty, calculate from Month and Year
                if ((!fromDate.HasValue || !toDate.HasValue) && filter.Month.HasValue && filter.Year.HasValue && filter.Month.Value >= 1 && filter.Month.Value <= 12 && filter.Year.Value > 1900)
                {
                    fromDate = new DateTime(filter.Year.Value, filter.Month.Value, 1);
                    toDate = new DateTime(filter.Year.Value, filter.Month.Value, DateTime.DaysInMonth(filter.Year.Value, filter.Month.Value));
                }

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var param = new DynamicParameters();
                param.Add("@CompanyCode", compCsv, DbType.String);
                param.Add("@DepartmentCodes", deptCsv, DbType.String);
                param.Add("@DesignationCodes", desigCsv, DbType.String);
                param.Add("@EmployeeIds", empCsv, DbType.String);
                param.Add("@FromDate", fromDate, DbType.Date);
                param.Add("@ToDate", toDate, DbType.Date);
                param.Add("@Month", filter.Month, DbType.Int32);
                param.Add("@Year", filter.Year, DbType.Int32);
                param.Add("@LoginEmployeeId", loginEmployeeId, DbType.String);
                param.Add("@AccessCodeId", accessCodeId, DbType.String);

                using var multi = await conn.QueryMultipleAsync(
                    "usp_JobCardBulkReport",
                    param,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 300);

                result.Rows = (await multi.ReadAsync<JobCardBulkReportRowDto>()).ToList();

                var header = await multi.ReadFirstOrDefaultAsync<JobCardBulkReportHeaderDto>();
                if (header != null)
                {
                    result.Header = header;
                }
                else
                {
                    result.Header = new JobCardBulkReportHeaderDto
                    {
                        CompanyName = "DataPath Ltd.",
                        FromDate = fromDate.HasValue ? fromDate.Value.ToString("dd/MM/yyyy") : "",
                        ToDate = toDate.HasValue ? toDate.Value.ToString("dd/MM/yyyy") : "",
                        DateRangeDisplay = $"Date: {(fromDate.HasValue ? fromDate.Value.ToString("dd/MM/yyyy") : "")} - {(toDate.HasValue ? toDate.Value.ToString("dd/MM/yyyy") : "")}"
                    };
                }

                // ── Group by Employee and calculate summary statistics ────
                var employeeGroups = result.Rows
                    .GroupBy(r => r.EmployeeId)
                    .Select(g =>
                    {
                        var first = g.First();
                        var rows = g.ToList();

                        int presentCount = 0;
                        int absentCount = 0;
                        int lateCount = 0;
                        int weekendCount = 0;
                        int holidayCount = 0;
                        int leaveCount = 0;
                        decimal totalWorkHrs = 0;
                        long totalLateSec = 0;
                        long totalWorkSec = 0;

                        foreach (var r in rows)
                        {
                            var st = (r.Status ?? "").Trim().ToUpperInvariant();
                            if (st == "P") presentCount++;
                            else if (st == "L") lateCount++;
                            else if (st == "A") absentCount++;
                            else if (st == "W") weekendCount++;
                            else if (st == "H") holidayCount++;
                            else leaveCount++;

                            if (st == "L" && !string.IsNullOrWhiteSpace(r.Late) && r.Late != "00:00:00" && r.Late != "0.00" && r.Late != "0")
                            {
                                if (r.Late.Contains(':'))
                                {
                                    var lp = r.Late.Split(':');
                                    if (lp.Length == 3 && int.TryParse(lp[0], out int lh) && int.TryParse(lp[1], out int lm) && int.TryParse(lp[2], out int ls))
                                        totalLateSec += (lh * 3600) + (lm * 60) + ls;
                                    else if (lp.Length == 2 && int.TryParse(lp[0], out int lh2) && int.TryParse(lp[1], out int lm2))
                                        totalLateSec += (lh2 * 3600) + (lm2 * 60);
                                }
                                else if (decimal.TryParse(r.Late, NumberStyles.Any, CultureInfo.InvariantCulture, out var decL))
                                {
                                    totalLateSec += (long)Math.Round(decL * 3600);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(r.WorkHours) && r.WorkHours != "00:00:00" && r.WorkHours != "0.00" && r.WorkHours != "0")
                            {
                                if (r.WorkHours.Contains(':'))
                                {
                                    var wp = r.WorkHours.Split(':');
                                    if (wp.Length == 3 && int.TryParse(wp[0], out int wh) && int.TryParse(wp[1], out int wm) && int.TryParse(wp[2], out int ws))
                                    {
                                        long sec = (wh * 3600) + (wm * 60) + ws;
                                        totalWorkSec += sec;
                                        totalWorkHrs += Math.Round((decimal)sec / 3600m, 2);
                                    }
                                    else if (wp.Length == 2 && int.TryParse(wp[0], out int wh2) && int.TryParse(wp[1], out int wm2))
                                    {
                                        long sec = (wh2 * 3600) + (wm2 * 60);
                                        totalWorkSec += sec;
                                        totalWorkHrs += Math.Round((decimal)sec / 3600m, 2);
                                    }
                                }
                                else if (decimal.TryParse(r.WorkHours, NumberStyles.Any, CultureInfo.InvariantCulture, out var whDec))
                                {
                                    totalWorkHrs += whDec;
                                    totalWorkSec += (long)Math.Round(whDec * 3600);
                                }
                            }
                        }

                        int totalPresentAll = presentCount + lateCount;
                        int totalWorkingDays = rows.Count - weekendCount - holidayCount;
                        int totalAtt = totalPresentAll + weekendCount + holidayCount + leaveCount;
                        decimal aveWorkHrs = totalPresentAll > 0
                            ? Math.Round(totalWorkHrs / totalPresentAll, 2)
                            : 0;

                        long hL = totalLateSec / 3600;
                        long mL = (totalLateSec % 3600) / 60;
                        long sL = totalLateSec % 60;
                        string totalLateStr = $"{hL}:{mL:D2}:{sL:D2}";

                        long hW = totalWorkSec / 3600;
                        long mW = (totalWorkSec % 3600) / 60;
                        long sW = totalWorkSec % 60;
                        string totalWorkStr = $"{hW}:{mW:D2}:{sW:D2}";

                        string avgTimeStr = "0:00";
                        if (totalPresentAll > 0)
                        {
                            long avgSec = totalWorkSec / totalPresentAll;
                            long avgH = avgSec / 3600;
                            long avgM = (avgSec % 3600) / 60;
                            avgTimeStr = $"{avgH}:{avgM:D2}";
                        }

                        return new EmployeeJobCardBulkGroupDto
                        {
                            EmployeeId = g.Key,
                            EmployeeName = first.EmployeeName,
                            Designation = first.Designation,
                            DesignationCode = first.DesignationCode,
                            DepartmentName = first.DepartmentName,
                            DepartmentCode = first.DepartmentCode,
                            CompanyName = first.CompanyName,
                            Rows = rows,
                            TotalWorkingDays = totalWorkingDays,
                            TotalPresent = totalPresentAll,
                            TotalAbsent = absentCount,
                            TotalLate = lateCount,
                            TotalLeave = leaveCount,
                            TotalWeekend = weekendCount,
                            TotalHoliday = holidayCount,
                            TotalWorkHours = totalWorkHrs,
                            TotalAtt = totalAtt,
                            AveWorkHoursPerDay = aveWorkHrs,
                            TotalLateDuration = totalLateStr,
                            TotalWorkDuration = totalWorkStr,
                            AveWorkDuration = avgTimeStr
                        };
                    })
                    .ToList();

                result.EmployeeGroups = employeeGroups;

                // ── Group by Department (Department A completes, then Department B starts) ──
                result.DepartmentGroups = employeeGroups
                    .GroupBy(e => new { e.DepartmentCode, e.DepartmentName })
                    .Select(dg => new DepartmentJobCardBulkGroupDto
                    {
                        DepartmentCode = dg.Key.DepartmentCode,
                        DepartmentName = dg.Key.DepartmentName,
                        Employees = dg.ToList()
                    })
                    .OrderBy(dg => dg.DepartmentName)
                    .ToList();

                return result;
            }
            catch (Exception)
            {

                throw;
            }
        }

        // ═════════════════════════════════════════════════════════════
        // ── iText7 High-Speed Pure Managed PDF Generation ────────────
        // ═════════════════════════════════════════════════════════════
        public async Task<byte[]> GeneratePdfAsync(JobCardBulkReportFilterDto filter, string? logoPhysicalPath = null)
        {
            var data = await GetReportDataAsync(filter);
            if (data == null || (!data.Rows.Any() && !data.EmployeeGroups.Any()))
                return Array.Empty<byte>();

            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            using var document = new PdfDocument2(pdf, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(20, 20, 20, 20);

            var regularFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);

            iText.Layout.Element.Image? logo = null;
            if (!string.IsNullOrWhiteSpace(logoPhysicalPath) && File.Exists(logoPhysicalPath))
            {
                try
                {
                    var imgData = ImageDataFactory.Create(logoPhysicalPath);
                    logo = new iText.Layout.Element.Image(imgData);
                    logo.ScaleToFit(80, 24);
                }
                catch { }
            }

            bool isFirstPage = true;
            var printTimeStr = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt");

            foreach (var dept in data.DepartmentGroups)
            {
                foreach (var emp in dept.Employees)
                {
                    if (!isFirstPage)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                    }
                    isFirstPage = false;

                    // Header table with Logo and Company Info
                    var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 20, 60, 20 }))
                        .UseAllAvailableWidth()
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    var logoCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                    if (logo != null) logoCell.Add(logo);
                    headerTable.AddCell(logoCell);

                    var centerCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .Add(new Paragraph(data.Header?.CompanyName ?? "DataPath Ltd.").SetFont(boldFont).SetFontSize(12))
                        .Add(new Paragraph("Job Card Report").SetFont(boldFont).SetFontSize(10))
                        .Add(new Paragraph(data.Header?.DateRangeDisplay ?? "").SetFont(regularFont).SetFontSize(8.5f));
                    headerTable.AddCell(centerCell);

                    var rightCell = new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                    headerTable.AddCell(rightCell);

                    document.Add(headerTable);

                    // Employee Info Block
                    var empTable = new Table(UnitValue.CreatePercentArray(new float[] { 15, 85 }))
                        .SetWidth(UnitValue.CreatePointValue(280))
                        .SetMarginTop(6)
                        .SetMarginBottom(8)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    void AddEmpRow(string label, string val)
                    {
                        empTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(1).Add(new Paragraph(label).SetFont(boldFont).SetFontSize(8)));
                        empTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(1).Add(new Paragraph(": " + (val ?? "")).SetFont(regularFont).SetFontSize(8)));
                    }

                    AddEmpRow("Emp. ID", emp.EmployeeId);
                    AddEmpRow("Name", emp.EmployeeName);
                    AddEmpRow("Designation", emp.Designation);
                    AddEmpRow("Department", emp.DepartmentName);
                    document.Add(empTable);

                    // 9-column Grid
                    float[] colWidths = new float[] { 50, 48, 75, 58, 48, 58, 48, 35, 90 };
                    var table = new Table(UnitValue.CreatePointArray(colWidths)).UseAllAvailableWidth();

                    string[] ths = { "Date", "Day", "Shift", "In Time", "Late", "Out Time", "W. Hour(s)", "Status", "Remarks" };
                    foreach (var th in ths)
                    {
                        table.AddHeaderCell(new Cell()
                            .SetPadding(2)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.5f))
                            .Add(new Paragraph(th).SetFont(boldFont).SetFontSize(7)));
                    }

                    foreach (var r in emp.Rows)
                    {
                        Cell MakeCell(string txt, TextAlignment align = TextAlignment.CENTER, iText.Kernel.Colors.Color? color = null, bool bold = false)
                        {
                            var p = new Paragraph(txt ?? "").SetFont(bold ? boldFont : regularFont).SetFontSize(6.5f);
                            if (color != null) p.SetFontColor(color);
                            return new Cell()
                                .SetPadding(2)
                                .SetTextAlignment(align)
                                .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.3f))
                                .Add(p);
                        }

                        table.AddCell(MakeCell(r.DateDisplay));
                        table.AddCell(MakeCell(r.Day));
                        table.AddCell(MakeCell(r.Shift));
                        table.AddCell(MakeCell(r.InTime));
                        table.AddCell(MakeCell(r.Late));
                        table.AddCell(MakeCell(r.OutTime));
                        table.AddCell(MakeCell(r.WorkHours));

                        iText.Kernel.Colors.Color? statusColor = r.Status switch
                        {
                            "P" => new DeviceRgb(40, 167, 69),
                            "L" => new DeviceRgb(217, 130, 43),
                            "A" => new DeviceRgb(220, 53, 69),
                            _ => ColorConstants.BLACK
                        };
                        table.AddCell(MakeCell(r.Status, TextAlignment.CENTER, statusColor, true));
                        table.AddCell(MakeCell(r.Remarks, TextAlignment.LEFT));
                    }

                    document.Add(table);

                    // Attendance Summary Box
                    var sumTable = new Table(UnitValue.CreatePercentArray(new float[] { 25, 25, 25, 25 }))
                        .SetWidth(UnitValue.CreatePointValue(240))
                        .SetMarginTop(8)
                        .SetBorder(new SolidBorder(ColorConstants.GRAY, 0.5f));

                    void AddSumCell(string lbl, string val)
                    {
                        sumTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(2).Add(new Paragraph(lbl).SetFont(boldFont).SetFontSize(7)));
                        sumTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(2).SetTextAlignment(TextAlignment.RIGHT).Add(new Paragraph(val).SetFont(regularFont).SetFontSize(7)));
                    }

                    AddSumCell("Total Present:", emp.TotalPresent.ToString());
                    AddSumCell("Total Weekend:", emp.TotalWeekend.ToString());
                    AddSumCell("Total Absent:", emp.TotalAbsent.ToString());
                    AddSumCell("Total Holiday:", emp.TotalHoliday.ToString());
                    AddSumCell("Total Late:", emp.TotalLate.ToString());
                    AddSumCell("Total Att.:", emp.TotalAtt.ToString());
                    AddSumCell("Total Leave:", emp.TotalLeave.ToString());
                    AddSumCell("Ave W.Hour:", emp.AveWorkHoursPerDay.ToString("F2"));

                    document.Add(sumTable);

                    // Signatures
                    var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 33, 34, 33 }))
                        .UseAllAvailableWidth()
                        .SetMarginTop(25)
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                    sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER)
                        .Add(new Paragraph("____________________\nPrepared by").SetFont(regularFont).SetFontSize(7.5f)));
                    sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER)
                        .Add(new Paragraph("____________________\nChecked by").SetFont(regularFont).SetFontSize(7.5f)));
                    sigTable.AddCell(new Cell().SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER)
                        .Add(new Paragraph("____________________\nAuthorized by").SetFont(regularFont).SetFontSize(7.5f)));
                    document.Add(sigTable);

                    // Status Legend
                    var legend = new Paragraph("Status Legend: P-Present, L- Late, A- Absent, W- Weekend, H- Holiday, CL- Casual Leave, SL- Sick Leave, EL- Earn Leave")
                        .SetFont(regularFont)
                        .SetFontSize(6.5f)
                        .SetFontColor(ColorConstants.DARK_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginTop(10);
                    document.Add(legend);
                }
            }

            document.Close();

            // 2nd pass: stamp page numbers and print datetime on each page
            using var readerStream = new MemoryStream(stream.ToArray());
            using var reader = new PdfReader(readerStream);
            using var outputStream = new MemoryStream();
            using var outputWriter = new PdfWriter(outputStream);
            using var outputPdf = new PdfDocument(reader, outputWriter);

            var totalPages = outputPdf.GetNumberOfPages();
            for (int i = 1; i <= totalPages; i++)
            {
                var page = outputPdf.GetPage(i);
                var pageSize = page.GetPageSize();
                var canvas = new PdfCanvas(page);

                // Footer left: Print Datetime
                canvas.BeginText()
                    .SetFontAndSize(regularFont, 7.5f)
                    .MoveText(22, 12)
                    .ShowText($"Print Datetime: {printTimeStr}")
                    .EndText();

                // Footer right: Page X of Y
                string pageStr = $"Page {i} of {totalPages}";
                float strWidth = regularFont.GetWidth(pageStr, 7.5f);
                canvas.BeginText()
                    .SetFontAndSize(regularFont, 7.5f)
                    .MoveText(pageSize.GetWidth() - 22 - strWidth, 12)
                    .ShowText(pageStr)
                    .EndText();
            }

            outputPdf.Close();
            return outputStream.ToArray();
        }

        // ═════════════════════════════════════════════════════════════
        // ── Excel Export (EPPlus) ────────────────────────────────────
        // ═════════════════════════════════════════════════════════════
        public async Task<byte[]> ExportExcelAsync(JobCardBulkReportFilterDto filter, string? logoPhysicalPath = null)
        {
            var data = await GetReportDataAsync(filter);
            if (data == null || (!data.Rows.Any() && !data.EmployeeGroups.Any()))
                return Array.Empty<byte>();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();

            var employees = data.DepartmentGroups != null && data.DepartmentGroups.Any()
                ? data.DepartmentGroups.SelectMany(d => d.Employees).ToList()
                : data.EmployeeGroups;

            if (employees == null || !employees.Any())
                return Array.Empty<byte>();

            string[] headers = new[] { "Date", "Day", "Shift", "In Time", "Late", "Out Time", "W. Hour(s)", "Status", "Remarks" };
            int colCount = headers.Length;

            foreach (var emp in employees)
            {
                var sheetName = GetUniqueSheetName(package, emp.EmployeeId);
                var ws = package.Workbook.Worksheets.Add(sheetName);

                int currentRow = 1;

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

                // Employee Info Block
                ws.Cells[currentRow, 1].Value = "Emp. ID"; ws.Cells[currentRow, 2].Value = ": " + emp.EmployeeId;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                ws.Cells[currentRow, 1].Value = "Name"; ws.Cells[currentRow, 2].Value = ": " + emp.EmployeeName;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                ws.Cells[currentRow, 1].Value = "Designation"; ws.Cells[currentRow, 2].Value = ": " + emp.Designation;
                ws.Cells[currentRow, 1].Style.Font.Bold = true; currentRow++;

                ws.Cells[currentRow, 1].Value = "Department"; ws.Cells[currentRow, 2].Value = ": " + emp.DepartmentName;
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
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 240, 240));
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
                currentRow++;

                // Rows
                foreach (var r in emp.Rows)
                {
                    SetCell(ws, currentRow, 1, r.DateDisplay);
                    SetCell(ws, currentRow, 2, r.Day);
                    SetCell(ws, currentRow, 3, r.Shift);
                    SetCell(ws, currentRow, 4, r.InTime);
                    SetCell(ws, currentRow, 5, r.Late);
                    SetCell(ws, currentRow, 6, r.OutTime);
                    SetCell(ws, currentRow, 7, r.WorkHours);

                    var statusCell = ws.Cells[currentRow, 8];
                    statusCell.Value = r.Status;
                    statusCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    statusCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    statusCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    statusCell.Style.Font.Bold = true;

                    if (r.Status == "P")
                        statusCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(40, 167, 69)); // Green
                    else if (r.Status == "L")
                        statusCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(217, 130, 43)); // Amber
                    else if (r.Status == "A")
                        statusCell.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(220, 53, 69)); // Red

                    SetCell(ws, currentRow, 9, r.Remarks, leftAlign: true);
                    currentRow++;
                }

                // Summary Stats Box
                currentRow++;
                ws.Cells[currentRow, 1].Value = "Total Present :"; ws.Cells[currentRow, 2].Value = emp.TotalPresent;
                ws.Cells[currentRow, 4].Value = "Total Weekend:"; ws.Cells[currentRow, 5].Value = emp.TotalWeekend;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Absent :"; ws.Cells[currentRow, 2].Value = emp.TotalAbsent;
                ws.Cells[currentRow, 4].Value = "Total Holiday:"; ws.Cells[currentRow, 5].Value = emp.TotalHoliday;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Late :"; ws.Cells[currentRow, 2].Value = emp.TotalLate;
                ws.Cells[currentRow, 4].Value = "Total Att.:"; ws.Cells[currentRow, 5].Value = emp.TotalAtt;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total Leave :"; ws.Cells[currentRow, 2].Value = emp.TotalLeave;
                ws.Cells[currentRow, 4].Value = "Ave. W. Hrs/Day.:"; ws.Cells[currentRow, 5].Value = emp.AveWorkHoursPerDay;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Total W. Hrs :"; ws.Cells[currentRow, 2].Value = emp.TotalWorkHours;

                if (ws.Dimension != null)
                {
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();
                    for (int col = 1; col <= colCount; col++)
                    {
                        if (ws.Column(col).Width < 12)
                            ws.Column(col).Width = 12;
                    }
                }
            }

            return await package.GetAsByteArrayAsync();
        }

        // ═════════════════════════════════════════════════════════════
        // ── CSV Export ───────────────────────────────────────────────
        // ═════════════════════════════════════════════════════════════
        public async Task<byte[]> ExportCsvAsync(JobCardBulkReportFilterDto filter)
        {
            var data = await GetReportDataAsync(filter);
            var sb = new StringBuilder();
            var bom = Encoding.UTF8.GetPreamble();

            sb.AppendLine("Emp. ID,Name,Department,Designation,Date,Day,Shift,In Time,Late,Out Time,W. Hour(s),Status,Remarks");

            foreach (var r in data.Rows)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(r.EmployeeId),
                    EscapeCsv(r.EmployeeName),
                    EscapeCsv(r.DepartmentName),
                    EscapeCsv(r.Designation),
                    EscapeCsv(r.DateDisplay),
                    EscapeCsv(r.Day),
                    EscapeCsv(r.Shift),
                    EscapeCsv(r.InTime),
                    EscapeCsv(r.Late),
                    EscapeCsv(r.OutTime),
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

        private static string SanitizeSheetName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Employee";

            var invalidChars = new[] { '\\', '/', '*', '?', ':', '[', ']' };
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            name = name.Trim();
            if (name.Length > 31)
                name = name.Substring(0, 31);

            return string.IsNullOrWhiteSpace(name) ? "Employee" : name;
        }

        private static string GetUniqueSheetName(ExcelPackage package, string? baseName)
        {
            string sanitized = SanitizeSheetName(baseName);
            string sheetName = sanitized;
            int counter = 1;

            while (package.Workbook.Worksheets.Any(s => string.Equals(s.Name, sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                string suffix = $"_{counter}";
                int maxLen = 31 - suffix.Length;
                sheetName = (sanitized.Length > maxLen ? sanitized.Substring(0, maxLen) : sanitized) + suffix;
                counter++;
            }

            return sheetName;
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
