using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GCTL.Core.Helpers;
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.HrmLeaveSummaryReports;
using GCTL.Service.HrmLeaveSummaryReports;
using GCTL.UI.Core.ViewModels.HrmLeaveSummaryReports;
using Microsoft.AspNetCore.Mvc;

namespace GCTL.UI.Core.Controllers
{
    public class HrmLeaveSummaryReportController : BaseController
    {
        private readonly IHrmLeaveSummaryReportService reportService;
        private readonly bool developerMode = true;

        public HrmLeaveSummaryReportController(IHrmLeaveSummaryReportService reportService)
        {
            this.reportService = reportService;
        }

        public async Task<IActionResult> Index()
        {
            var hasPermission = await reportService.PagePermissionAsync(LoginInfo.AccessCode);
            if (!hasPermission && !developerMode)
                return RedirectToAction("Login", "Accounts");

            var model = new LeaveSummaryPageViewModel
            {
                PageUrl = Url.Action(nameof(Index)),
                PageTitle = "Leave Summery Report",
                Breadcrumb = "Leave Summery Report"
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GetAllFilterEmp([FromBody] LeaveSummaryFilterViewModel filterDto)
        {
            try
            {
                var result = await reportService.GetDataAsync(filterDto);
                if (result != null)
                    return Json(new { isSuccess = true, message = "Data loaded successfully", data = result });

                return Json(new { isSuccess = false, message = "Data load failed" });
            }
            catch (Exception)
            {
                return Json(new { isSuccess = false, message = "An error occurred while loading data" });
            }
        }

        [HttpPost("HrmLeaveSummaryReport/PreviewReport")]
        public async Task<IActionResult> PreviewReport([FromBody] LeaveSummaryExportRequest? request)
        {
            try
            {
                if (request == null)
                    request = new LeaveSummaryExportRequest { ExportFormat = "pdf" };
                if (request.FilterData == null)
                    request.FilterData = new LeaveSummaryFilterViewModel();
                if (request.FilterData.CompanyCodes == null || !request.FilterData.CompanyCodes.Any())
                    request.FilterData.CompanyCodes = new List<string> { "001" };

                BaseViewModel model = new BaseViewModel();
                model.ToAudit(LoginInfo);

                var data = await reportService.GetDataAsync(request.FilterData);
                if (data == null || data.LeaveSummary == null || !data.LeaveSummary.Any())
                    return NotFound();

                var pdfBytes = await reportService.GeneratePdfReport(data.LeaveSummary, data.LeaveTypes, model);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPost("HrmLeaveSummaryReport/ExportReport")]
        public async Task<IActionResult> ExportReport([FromBody] LeaveSummaryExportRequest? request)
        {
            try
            {
                if (request == null)
                    request = new LeaveSummaryExportRequest { ExportFormat = "pdf" };
                if (request.FilterData == null)
                    request.FilterData = new LeaveSummaryFilterViewModel();
                if (request.FilterData.CompanyCodes == null || !request.FilterData.CompanyCodes.Any())
                    request.FilterData.CompanyCodes = new List<string> { "001" };

                var format = string.IsNullOrWhiteSpace(request.ExportFormat) ? "pdf" : request.ExportFormat.ToLower();

                BaseViewModel model = new BaseViewModel();
                model.ToAudit(LoginInfo);

                var data = await reportService.GetDataAsync(request.FilterData);
                if (data == null || data.LeaveSummary == null || !data.LeaveSummary.Any())
                    return NotFound();

                byte[] fileBytes;
                string fileName;
                string contentType;
                string baseFileName = $"LeaveSummaryReport_{DateTime.Now:yyyyMMdd_HHmmss}";

                switch (format)
                {
                    case "pdf":
                        fileBytes = await reportService.GeneratePdfReport(data.LeaveSummary, data.LeaveTypes, model);
                        fileName = $"{baseFileName}.pdf";
                        contentType = "application/pdf";
                        break;

                    case "excel":
                        fileBytes = await reportService.GenerateExcelReport(data.LeaveSummary, data.LeaveTypes);
                        fileName = $"{baseFileName}.xlsx";
                        contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        break;

                    case "word":
                        fileBytes = GenerateWordReport(data.LeaveSummary, data.LeaveTypes);
                        fileName = $"{baseFileName}.docx";
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        break;

                    case "download":
                        fileBytes = await reportService.GeneratePdfReport(data.LeaveSummary, data.LeaveTypes, model);
                        fileName = $"{baseFileName}.pdf";
                        contentType = "application/pdf";
                        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                        return File(fileBytes, contentType);

                    default:
                        fileBytes = await reportService.GeneratePdfReport(data.LeaveSummary, data.LeaveTypes, model);
                        fileName = $"{baseFileName}.pdf";
                        contentType = "application/pdf";
                        break;
                }
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }

        [HttpPost("HrmLeaveSummaryReport/employee")]
        public async Task<IActionResult> Employee([FromBody] LeaveSummaryFilterViewModel req)
            => Json(new { isSuccess = true, data = await reportService.GetEmployeesAsync(req) });

        private byte[] GenerateWordReport(List<LeaveSummaryRowDto> data, List<LeaveTypeDto2> leaveTypes)
        {
            using var stream = new MemoryStream();
            using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);

            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            var sectionProps = new SectionProperties(
                new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape },
                new PageMargin { Top = 518, Right = 518, Bottom = 518, Left = 518 }
            );

            //TableCell CreateCell(string text, int width, bool bold = false, TableCellBorders borders = null, bool centerAlign = false)
            //{
            //    var props = new TableCellProperties(
            //        new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = width.ToString() }
            //    );
            //    if (borders != null)
            //        props.Append(borders);

            //    var runProps = new RunProperties(
            //        new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
            //        new FontSize() { Val = "16" }
            //    );
            //    if (bold)
            //        runProps.Append(new Bold());

            //    var run = new Run(runProps, new Text(text));

            //    var paragraphProps = new ParagraphProperties(
            //        new SpacingBetweenLines() { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Exact }
            //    );
            //    if (centerAlign)
            //        paragraphProps.Append(new Justification() { Val = JustificationValues.Center });

            //    return new TableCell(props, new Paragraph(paragraphProps, run));
            //}

            TableCell CreateCell(string text, int width, bool bold = false, TableCellBorders borders = null, bool centerAlign = false)
            {
                var props = new TableCellProperties(
                    new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = width.ToString() }
                );

                // ???????? ???? borders instance ??????? ????
                if (borders != null)
                    props.Append(borders.CloneNode(true));

                var runProps = new RunProperties(
                    new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                    new FontSize() { Val = "16" }
                );
                if (bold)
                    runProps.Append(new Bold());

                var run = new Run(runProps, new Text(text));

                var paragraphProps = new ParagraphProperties(
                    new SpacingBetweenLines() { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Exact }
                );
                if (centerAlign)
                    paragraphProps.Append(new Justification() { Val = JustificationValues.Center });

                return new TableCell(props, new Paragraph(paragraphProps, run));
            }


            body.Append(
                new Paragraph(
                    new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "240" }
                    ),
                    new Run(new RunProperties(new Bold(), new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" }, new FontSize() { Val = "28" }), new Text(data.Select(e => e.CompanyName).Distinct().FirstOrDefault())),
                    new Run(new Break()),
                    new Run(new RunProperties(new Bold(), new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" }, new FontSize() { Val = "24" }), new Text("Leave Summary Report"))
                )
            );

            var table = new Table();
            var tableProps = new TableProperties(
                new TableWidth() { Width = "15120", Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                    new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                    new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                    new RightBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                    new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                    new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" }
                )
            );
            table.AppendChild(tableProps);

            var fixedHeaders = new[] { "SL", "Employee ID", "Name", "Designation", "Department", "Branch", "Joining Date" };
            var headers = fixedHeaders
                .Concat(leaveTypes.Select(t => $"G-{t.Code}"))
                .Concat(leaveTypes.Select(t => $"A-{t.Code}"))
                .Concat(leaveTypes.Select(t => $"B-{t.Code}"))
                .ToArray();

            int fixedWidth = 1300;
            int typeWidth = leaveTypes.Count > 0
                ? Math.Max(600, (15120 - fixedWidth * fixedHeaders.Length) / (leaveTypes.Count * 3))
                : 0;
            var columnWidths = Enumerable.Repeat(fixedWidth, fixedHeaders.Length)
                .Concat(Enumerable.Repeat(typeWidth, leaveTypes.Count * 3)).ToArray();

            var headerBorders = new TableCellBorders(
                new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                new RightBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" }
            );

            var headerRow = new TableRow();
            for (int i = 0; i < headers.Length; i++)
                headerRow.Append(CreateCell(headers[i], columnWidths[i], true, headerBorders, true));
            table.Append(headerRow);

            int sl = 1;
            foreach (var emp in data.OrderBy(x => x.EmployeeId))
            {
                var values = new List<string> { sl.ToString(), emp.EmployeeId ?? "", emp.EmployeeName ?? "", emp.DesignationName ?? "", emp.DepartmentName ?? "", emp.BranchName ?? "", emp.JoiningDate ?? "" };
                values.AddRange(leaveTypes.Select(t => (emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == t.Code)?.Granted ?? 0).ToString("G29")));
                values.AddRange(leaveTypes.Select(t => (emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == t.Code)?.Availed ?? 0).ToString("G29")));
                values.AddRange(leaveTypes.Select(t => (emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == t.Code)?.Balance ?? 0).ToString("G29")));

                var dataRow = new TableRow();
                for (int j = 0; j < values.Count; j++)
                    dataRow.Append(CreateCell(values[j], columnWidths[j], false, headerBorders, true));
                table.Append(dataRow);
                sl++;
            }

            body.Append(table);
            body.Append(sectionProps);

            mainPart.Document.Append(body);
            mainPart.Document.Save();
            document.Dispose();

            return stream.ToArray();
        }


    }
}
