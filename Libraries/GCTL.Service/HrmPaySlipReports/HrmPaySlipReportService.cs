using Dapper;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.HrmPaySlipReports;
using GCTL.Data.Models;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace GCTL.Service.HrmPaySlipReports
{
    public class HrmPaySlipReportService:AppService<HrmPaySalaryData>, IHrmPaySlipReportService
    {
        private readonly IRepository<HrmPaySalaryData> reportRepo;
        private readonly IConfiguration configuration;
        private readonly IRepository<CoreAccessCode> accessCodeRepo;

        public HrmPaySlipReportService(IRepository<HrmPaySalaryData> reportRepo, IConfiguration configuration, IRepository<CoreAccessCode> accessCodeRepo) : base(reportRepo)
        {
            this.reportRepo = reportRepo;
            this.configuration = configuration;
            this.accessCodeRepo = accessCodeRepo;
        }

        public async Task<List<HrmPaySlipReportVM>> GetPaySlipDataAsync(PaySlipFilterData filter)
        {
            try
            {
                using var connection = new SqlConnection(configuration.GetConnectionString("ApplicationDbConnection"));
                var param = new DynamicParameters();

                param.Add("@AccessCode",
                    !string.IsNullOrWhiteSpace(filter.AccessCode)
                    ? (object)filter.AccessCode
                    : DBNull.Value);

                param.Add("@EmployeeId",
                    !string.IsNullOrWhiteSpace(filter.EmployeeId)
                    ? (object)filter.EmployeeId
                    : DBNull.Value);

                param.Add("@CompanyCodes", filter.CompanyCodes != null && filter.CompanyCodes.Any()
                    ? string.Join(",", filter.CompanyCodes) : null);

                param.Add("@BranchCodes", filter.BranchCodes != null && filter.BranchCodes.Any()
                    ? string.Join(",", filter.BranchCodes) : null);

                param.Add("@DepartmentCodes", filter.DepartmentCodes != null && filter.DepartmentCodes.Any()
                    ? string.Join(",", filter.DepartmentCodes) : null);

                param.Add("@DesignationCodes", filter.DesignationCodes != null && filter.DesignationCodes.Any()
                    ? string.Join(",", filter.DesignationCodes) : null);

                param.Add("@EmployeeIDs", filter.EmployeeIDs != null && filter.EmployeeIDs.Any()
                    ? string.Join(",", filter.EmployeeIDs) : null);

                param.Add("@FromDate", filter.FromDate);
                param.Add("@ToDate", filter.ToDate);

                param.Add("@Months", filter.MonthIDs != null && filter.MonthIDs.Any()
                    ? string.Join(",", filter.MonthIDs) : null);

                param.Add("@Years", filter.YearIDs != null && filter.YearIDs.Any()
                    ? string.Join(",", filter.YearIDs) : null);

                var result = await connection.QueryAsync<HrmPaySlipReportVM>(
                    "SP_GetPaySlipReportData",
                    param,
                    commandType: System.Data.CommandType.StoredProcedure,
                    commandTimeout: 120
                    );

                return result.ToList();
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<bool> PagePermissionAsync(string accessCode)
        {
            return await accessCodeRepo.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Pay Slip Report" && x.TitleCheck);
        }

        private static readonly DeviceRgb AccentColor = new DeviceRgb(0, 0, 0);
        private static readonly DeviceRgb SectionGray = new DeviceRgb(240, 240, 240); // Centralized soft gray background
        private static readonly DeviceRgb BorderGray = new DeviceRgb(220, 220, 220);  // Centralized gray for borders
        private static readonly DeviceRgb HeaderBlue = new DeviceRgb(0x4F, 0x81, 0xBD);
        private static readonly SolidBorder GridBorder = new SolidBorder(BorderGray, 0.5f);

        public byte[] GeneratePaySlipPdf(List<HrmPaySlipReportVM> data)
        {
            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            {
                var document = new Document(pdf, PageSize.A4);
                // 80pt top margin for company letterhead pad
                document.SetMargins(80, 40, 30, 40);

                var regularFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                var boldFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);

                for (int i = 0; i < data.Count; i++)
                {
                    BuildSlip(document, data[i], regularFont, boldFont);

                    if (i < data.Count - 1)
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                }

                document.Close();
            }

            return ms.ToArray();
        }

        private void BuildSlip(Document document, HrmPaySlipReportVM vm, PdfFont regular, PdfFont bold)
        {
            // Standardized column widths for all 3-column tables so "BDT" and Amounts align perfectly across rows:
            // Column 1 (Label): 60%, Column 2 (BDT): 15%, Column 3 (Amount): 25%
            var standardColWidths = new float[] { 6f, 1.5f, 2.5f };

            // 1. Top Title Header Block
            var topBar = new Table(1).UseAllAvailableWidth().SetMarginBottom(4);
            topBar.AddCell(new Cell().SetHeight(4).SetBackgroundColor(HeaderBlue).SetBorder(Border.NO_BORDER));
            document.Add(topBar);

            var titleTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();
            titleTable.AddCell(new Cell().Add(new Paragraph("PAY SLIP FOR THE MONTH OF").SetFont(bold).SetFontSize(10.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
            titleTable.AddCell(new Cell().Add(new Paragraph($"{vm.MonthName}  {vm.YearID}").SetFont(bold).SetFontSize(10.5f)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER).SetPadding(2));
            document.Add(titleTable);

            var bottomBar = new Table(1).UseAllAvailableWidth().SetMarginBottom(10);
            bottomBar.AddCell(new Cell().SetHeight(4).SetBackgroundColor(HeaderBlue).SetBorder(Border.NO_BORDER));
            document.Add(bottomBar);

            // 2. Employee Info Section
            document.Add(new Paragraph("Employee Info:").SetFont(bold).SetFontSize(9.5f).SetMarginBottom(4));

            var infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 1.5f, 3.5f, 1.5f, 2.5f })).UseAllAvailableWidth().SetMarginBottom(8);
            infoTable.AddCell(GridCell("Name", bold, 9));
            infoTable.AddCell(GridCell(vm.EmployeeName, bold, 9, colspan: 3));
            infoTable.AddCell(GridCell("Department", bold, 9));
            infoTable.AddCell(GridCell(vm.DepartmentName, bold, 9, align: TextAlignment.CENTER));
            infoTable.AddCell(GridCell("Designation", bold, 9));
            infoTable.AddCell(GridCell(vm.DesignationName, bold, 9, align: TextAlignment.CENTER));
            document.Add(infoTable);

            // 3. Pays & Benefits Part 1
            document.Add(SectionHeader("Pays & Benefits Part 1", bold));
            var part1 = new Table(UnitValue.CreatePercentArray(standardColWidths)).UseAllAvailableWidth().SetMarginBottom(4);
            AddAmountRow(part1, "Basic", vm.Basic, regular, bold);
            AddAmountRow(part1, "House Rent", vm.HouseRent, regular, bold);
            AddAmountRow(part1, "Medical", vm.Medical, regular, bold);
            AddAmountRow(part1, "Conveyance Allowance", vm.Conveyance, regular, bold);
            AddAmountRow(part1, "Gross Pay", vm.GrossPay, bold, bold, isHighlightRow: true);
            document.Add(part1);

            // 4. Other Allowance
            document.Add(SectionHeader("Other Allowance", bold));
            var other = new Table(UnitValue.CreatePercentArray(new float[] { 4.5f, 1.5f, 1.5f, 2.5f })).UseAllAvailableWidth().SetMarginBottom(4);

            other.AddCell(PlainCell("Payment for Holiday Work", bold, 9));
            if(vm.HolidayWorkDays > 0 || vm.HolidayWorkAmount > 0)
            {
                other.AddCell(PlainCell(vm.HolidayWorkDays > 0 ? $"Days: {vm.HolidayWorkDays}" : "", bold, 9, align: TextAlignment.RIGHT));
                other.AddCell(PlainCell("BDT", bold, 9, align: TextAlignment.RIGHT));
                other.AddCell(PlainCell(vm.HolidayWorkAmount.ToString("N0"), bold, 9, align: TextAlignment.RIGHT));
            }
            else
            {
                other.AddCell(PlainCell("", bold, 9, align: TextAlignment.RIGHT));
                other.AddCell(PlainCell("", bold, 9, align: TextAlignment.RIGHT));
                other.AddCell(PlainCell("", bold, 9, align: TextAlignment.RIGHT));
            }

            other.AddCell(PlainCell("Incentives/Others:", bold, 9, colspan: 2));
            other.AddCell(PlainCell("BDT", bold, 9, align: TextAlignment.RIGHT));
            other.AddCell(PlainCell((vm.Incentive + vm.OthersBenefits + vm.AttenBonus).ToString("N0"), bold, 9, align: TextAlignment.RIGHT));

            // A- Total Addition Row
            var totAddVal = vm.TotalAddition > 0 ? vm.TotalAddition : (vm.GrossPay + vm.HolidayWorkAmount + vm.Incentive + vm.OthersBenefits + vm.AttenBonus);
            other.AddCell(PlainCell("A- Total Addition", bold, 9, isHighlightRow: true));
            other.AddCell(PlainCell("", bold, 9, isHighlightRow: true));
            other.AddCell(PlainCell("BDT", bold, 9, align: TextAlignment.RIGHT, isHighlightRow: true));
            other.AddCell(PlainCell(totAddVal.ToString("N0"), bold, 9, align: TextAlignment.RIGHT, isHighlightRow: true));
            document.Add(other);

            // 5. Pays & Benefits Part -2
            document.Add(SectionHeader("Pays & Benefits Part -2", bold));
            var part2 = new Table(UnitValue.CreatePercentArray(standardColWidths)).UseAllAvailableWidth().SetMarginBottom(4);
            AddAmountRow(part2, "PF Deduction", vm.PFDeduction, regular, bold);
            var otherDed = vm.LateDeduction + vm.AdvanceDeduction + vm.StampDeduction
                + vm.Lunch + vm.TDS + vm.MobAndInt + vm.Transport + vm.Insurance + vm.Penalty + vm.Food + vm.OtherDed;
            AddAmountRow(part2, "Others", otherDed, regular, bold);

            // B- Total Deduction Row
            var totDedVal = vm.TotalDeduction > 0 ? vm.TotalDeduction : (vm.PFDeduction + otherDed);
            part2.AddCell(PlainCell("B- Total Deduction", bold, 9, isHighlightRow: true));
            part2.AddCell(PlainCell("BDT", bold, 9, align: TextAlignment.RIGHT, isHighlightRow: true));
            part2.AddCell(PlainCell(totDedVal.ToString("N0"), bold, 9, align: TextAlignment.RIGHT, isHighlightRow: true));
            document.Add(part2);

            // 6. Net Payable (A-B)
            var netTable = new Table(UnitValue.CreatePercentArray(standardColWidths)).UseAllAvailableWidth().SetMarginBottom(8);
            var netPayVal = vm.NetPayable > 0 ? vm.NetPayable : (totAddVal - totDedVal);
            netTable.AddCell(PlainCell("Net Payable (A-B)", bold, 9, isHighlightRow: true));
            netTable.AddCell(PlainCell("BDT", bold, 9, align: TextAlignment.RIGHT, isHighlightRow: true));
            netTable.AddCell(PlainCell(netPayVal.ToString("N0"), bold, 9, align: TextAlignment.RIGHT, isHighlightRow: true));
            document.Add(netTable);

            // 7. In Word
            var wordsTable = new Table(UnitValue.CreatePercentArray(new float[] { 2.5f, 7.5f })).UseAllAvailableWidth().SetMarginBottom(14);
            wordsTable.AddCell(GridCell("In Word:", bold, 9));
            var inWordsVal = !string.IsNullOrWhiteSpace(vm.NetPayableInWords) ? vm.NetPayableInWords : NumberToWordsBdt(netPayVal);
            wordsTable.AddCell(GridCell(inWordsVal, bold, 9, align: TextAlignment.CENTER));
            document.Add(wordsTable);

            // 8. Bank Transfer Section
            document.Add(new Paragraph("Net Payment has been transferred to Bank:").SetFont(regular).SetFontSize(9).SetMarginBottom(6));

            var bankTable = new Table(UnitValue.CreatePercentArray(new float[] { 2.5f, 7.5f })).UseAllAvailableWidth().SetMarginBottom(16);
            bankTable.AddCell(PlainCell("Bank Name:", regular, 9));
            bankTable.AddCell(PlainCell(vm.BankName1 ?? "", bold, 9));
            bankTable.AddCell(PlainCell("Account Number:", regular, 9));
            bankTable.AddCell(PlainCell(vm.AccountNumber1 ?? "", bold, 9));

            if (!string.IsNullOrWhiteSpace(vm.BankName2))
            {
                bankTable.AddCell(PlainCell("Bank Name:", regular, 9));
                bankTable.AddCell(PlainCell(vm.BankName2 ?? "", bold, 9));
                bankTable.AddCell(PlainCell("Account Number:", regular, 9));
                bankTable.AddCell(PlainCell(vm.AccountNumber2 ?? "", bold, 9));
            }
            document.Add(bankTable);

            // 9. Best Regards & Signature Section with Dotted Line
            document.Add(new Paragraph("Best Regards,").SetFont(bold).SetFontSize(10.5f).SetMarginBottom(6));

            var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                .UseAllAvailableWidth()
                .SetMarginTop(10);

            var signatureCell = new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetPaddingRight(10);

            signatureCell.Add(new Paragraph("---------------------------------------------------").SetFont(regular).SetFontSize(9).SetMarginTop(32).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverName))
                signatureCell.Add(new Paragraph(vm.ApproverName).SetFont(bold).SetFontSize(10.5f).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverDesignation))
                signatureCell.Add(new Paragraph(vm.ApproverDesignation).SetFont(regular).SetFontSize(10).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverCell))
                signatureCell.Add(new Paragraph($"Cell: {vm.ApproverCell}").SetFont(regular).SetFontSize(10).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverEmail))
                signatureCell.Add(new Paragraph($"Email: {vm.ApproverEmail}").SetFont(regular).SetFontSize(10).SetMarginBottom(0));

            var spacerCell = new Cell().SetBorder(Border.NO_BORDER);

            sigTable.AddCell(signatureCell);
            sigTable.AddCell(spacerCell);

            document.Add(sigTable);
        }

        private Paragraph SectionHeader(string text, PdfFont font)
        {
            return new Paragraph(text)
                .SetFont(font)
                .SetFontSize(9)
                .SetBackgroundColor(SectionGray)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(2)
                .SetMarginTop(4)
                .SetMarginBottom(4);
        }

        private void AddAmountRow(Table table, string label, decimal amount, PdfFont labelFont, PdfFont amountFont, bool isHighlightRow = false)
        {
            table.AddCell(PlainCell(label, isHighlightRow ? amountFont : labelFont, 9, isHighlightRow: isHighlightRow));
            table.AddCell(PlainCell("BDT", amountFont, 9, align: TextAlignment.RIGHT, isHighlightRow: isHighlightRow));
            table.AddCell(PlainCell(amount.ToString("N0"), amountFont, 9, align: TextAlignment.RIGHT, isHighlightRow: isHighlightRow));
        }

        private Cell PlainCell(string text, PdfFont font, float fontSize = 9, int colspan = 1,
            TextAlignment align = TextAlignment.LEFT, bool isHighlightRow = false)
        {
            var cell = new Cell(1, colspan)
                .Add(new Paragraph(text ?? string.Empty).SetFont(font).SetFontSize(fontSize))
                .SetTextAlignment(align)
                .SetPadding(2);

            if (isHighlightRow)
            {
                cell.SetBackgroundColor(SectionGray);
                cell.SetBorderLeft(Border.NO_BORDER);
                cell.SetBorderRight(Border.NO_BORDER);
                cell.SetBorderTop(new SolidBorder(BorderGray, 0.75f));
                cell.SetBorderBottom(new SolidBorder(BorderGray, 0.75f));
            }
            else
            {
                cell.SetBorder(Border.NO_BORDER);
            }

            return cell;
        }

        private Cell GridCell(string text, PdfFont font, float fontSize = 9, int colspan = 1,
            TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell(1, colspan)
                .Add(new Paragraph(text ?? string.Empty).SetFont(font).SetFontSize(fontSize))
                .SetTextAlignment(align)
                .SetBorder(GridBorder)
                .SetPadding(3);
        }
        public byte[] GeneratePaySlipExcel(List<HrmPaySlipReportVM> data)
        {
            NPOI.SS.UserModel.IWorkbook workbook = new NPOI.XSSF.UserModel.XSSFWorkbook();

            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                string sheetName = string.IsNullOrWhiteSpace(item.EmployeeId)
                    ? $"PaySlip_{i + 1}"
                    : $"{item.EmployeeId}".Replace(":", "").Replace("\\", "").Replace("/", "").Replace("?", "").Replace("*", "").Replace("[", "").Replace("]", "");

                if (sheetName.Length > 30) sheetName = sheetName.Substring(0, 30);
                int sheetIndex = 1;
                string originalSheetName = sheetName;
                while (workbook.GetSheet(sheetName) != null)
                {
                    sheetName = $"{originalSheetName}_{sheetIndex++}";
                }

                NPOI.SS.UserModel.ISheet sheet = workbook.CreateSheet(sheetName);
                sheet.DisplayGridlines = true;

                sheet.SetColumnWidth(0, 32 * 256);
                sheet.SetColumnWidth(1, 15 * 256);
                sheet.SetColumnWidth(2, 25 * 256);
                sheet.SetColumnWidth(3, 25 * 256);

                NPOI.SS.UserModel.IFont boldFont = workbook.CreateFont();
                boldFont.FontName = "Calibri";
                boldFont.FontHeightInPoints = 11;
                boldFont.IsBold = true;

                NPOI.SS.UserModel.IFont regularFont = workbook.CreateFont();
                regularFont.FontName = "Calibri";
                regularFont.FontHeightInPoints = 11;

                NPOI.SS.UserModel.ICellStyle headerStyle = workbook.CreateCellStyle();
                headerStyle.SetFont(boldFont);
                headerStyle.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Center;

                NPOI.SS.UserModel.ICellStyle regularStyle = workbook.CreateCellStyle();
                regularStyle.SetFont(regularFont);

                NPOI.SS.UserModel.ICellStyle boldStyle = workbook.CreateCellStyle();
                boldStyle.SetFont(boldFont);

                NPOI.SS.UserModel.ICellStyle amountStyle = workbook.CreateCellStyle();
                amountStyle.SetFont(regularFont);
                amountStyle.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Right;

                NPOI.SS.UserModel.ICellStyle boldAmountStyle = workbook.CreateCellStyle();
                boldAmountStyle.SetFont(boldFont);
                boldAmountStyle.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Right;

                // Title
                NPOI.SS.UserModel.IRow r0 = sheet.CreateRow(0);
                r0.CreateCell(0).SetCellValue($"PAY SLIP FOR THE MONTH OF {item.MonthName} {item.YearID}");
                r0.GetCell(0).CellStyle = headerStyle;
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(0, 0, 0, 3));

                // Employee Info
                NPOI.SS.UserModel.IRow r2 = sheet.CreateRow(2);
                r2.CreateCell(0).SetCellValue("Employee Info:");
                r2.GetCell(0).CellStyle = boldStyle;

                NPOI.SS.UserModel.IRow r3 = sheet.CreateRow(3);
                r3.CreateCell(0).SetCellValue("Name:"); r3.GetCell(0).CellStyle = boldStyle;
                r3.CreateCell(1).SetCellValue(item.EmployeeName); r3.GetCell(1).CellStyle = regularStyle;

                NPOI.SS.UserModel.IRow r4 = sheet.CreateRow(4);
                r4.CreateCell(0).SetCellValue("Department:"); r4.GetCell(0).CellStyle = boldStyle;
                r4.CreateCell(1).SetCellValue(item.DepartmentName); r4.GetCell(1).CellStyle = regularStyle;
                r4.CreateCell(2).SetCellValue("Designation:"); r4.GetCell(2).CellStyle = boldStyle;
                r4.CreateCell(3).SetCellValue(item.DesignationName); r4.GetCell(3).CellStyle = regularStyle;

                // Pays & Benefits Part 1
                NPOI.SS.UserModel.IRow r6 = sheet.CreateRow(6);
                r6.CreateCell(0).SetCellValue("Pays & Benefits Part 1"); r6.GetCell(0).CellStyle = boldStyle;

                int curRow = 7;
                string[,] p1 = {
                    { "Basic", item.Basic.ToString("N0") },
                    { "House Rent", item.HouseRent.ToString("N0") },
                    { "Medical", item.Medical.ToString("N0") },
                    { "Conveyance Allowance", item.Conveyance.ToString("N0") },
                    { "Gross Pay", item.GrossPay.ToString("N0") }
                };

                for (int k = 0; k < 5; k++)
                {
                    bool isGross = k == 4;
                    NPOI.SS.UserModel.IRow r = sheet.CreateRow(curRow++);
                    r.CreateCell(0).SetCellValue(p1[k, 0]); r.GetCell(0).CellStyle = isGross ? boldStyle : regularStyle;
                    r.CreateCell(1).SetCellValue("BDT"); r.GetCell(1).CellStyle = isGross ? boldAmountStyle : amountStyle;
                    r.CreateCell(2).SetCellValue(p1[k, 1]); r.GetCell(2).CellStyle = isGross ? boldAmountStyle : amountStyle;
                }

                // Other Allowance
                curRow++;
                NPOI.SS.UserModel.IRow rOther = sheet.CreateRow(curRow++);
                rOther.CreateCell(0).SetCellValue("Other Allowance"); rOther.GetCell(0).CellStyle = boldStyle;

                NPOI.SS.UserModel.IRow rHol = sheet.CreateRow(curRow++);
                rHol.CreateCell(0).SetCellValue("Payment for Holiday Work"); rHol.GetCell(0).CellStyle = regularStyle;
                rHol.CreateCell(1).SetCellValue($"Days: {item.HolidayWorkDays}"); rHol.GetCell(1).CellStyle = regularStyle;
                rHol.CreateCell(2).SetCellValue("BDT"); rHol.GetCell(2).CellStyle = amountStyle;
                rHol.CreateCell(3).SetCellValue(item.HolidayWorkAmount.ToString("N0")); rHol.GetCell(3).CellStyle = amountStyle;

                var totAdd = item.TotalAddition > 0 ? item.TotalAddition : (item.GrossPay + item.HolidayWorkAmount + item.Incentive + item.OthersBenefits + item.AttenBonus);
                NPOI.SS.UserModel.IRow rTotAdd = sheet.CreateRow(curRow++);
                rTotAdd.CreateCell(0).SetCellValue("A- Total Addition"); rTotAdd.GetCell(0).CellStyle = boldStyle;
                rTotAdd.CreateCell(2).SetCellValue("BDT"); rTotAdd.GetCell(2).CellStyle = boldAmountStyle;
                rTotAdd.CreateCell(3).SetCellValue(totAdd.ToString("N0")); rTotAdd.GetCell(3).CellStyle = boldAmountStyle;

                // Pays & Benefits Part -2
                curRow++;
                NPOI.SS.UserModel.IRow rPart2 = sheet.CreateRow(curRow++);
                rPart2.CreateCell(0).SetCellValue("Pays & Benefits Part -2"); rPart2.GetCell(0).CellStyle = boldStyle;

                NPOI.SS.UserModel.IRow rPf = sheet.CreateRow(curRow++);
                rPf.CreateCell(0).SetCellValue("PF Deduction"); rPf.GetCell(0).CellStyle = regularStyle;
                rPf.CreateCell(1).SetCellValue("BDT"); rPf.GetCell(1).CellStyle = amountStyle;
                rPf.CreateCell(2).SetCellValue(item.PFDeduction.ToString("N0")); rPf.GetCell(2).CellStyle = amountStyle;

                var otherDed = item.LateDeduction + item.AdvanceDeduction + item.StampDeduction
                    + item.Lunch + item.TDS + item.MobAndInt + item.Transport + item.Insurance + item.Penalty + item.Food + item.OtherDed;
                var totDed = item.TotalDeduction > 0 ? item.TotalDeduction : (item.PFDeduction + otherDed);
                NPOI.SS.UserModel.IRow rTotDed = sheet.CreateRow(curRow++);
                rTotDed.CreateCell(0).SetCellValue("B- Total Deduction"); rTotDed.GetCell(0).CellStyle = boldStyle;
                rTotDed.CreateCell(1).SetCellValue("BDT"); rTotDed.GetCell(1).CellStyle = boldAmountStyle;
                rTotDed.CreateCell(2).SetCellValue(totDed.ToString("N0")); rTotDed.GetCell(2).CellStyle = boldAmountStyle;

                // Net Payable
                curRow++;
                var netVal = item.NetPayable > 0 ? item.NetPayable : (totAdd - totDed);
                NPOI.SS.UserModel.IRow rNet = sheet.CreateRow(curRow++);
                rNet.CreateCell(0).SetCellValue("Net Payable (A-B)"); rNet.GetCell(0).CellStyle = boldStyle;
                rNet.CreateCell(1).SetCellValue("BDT"); rNet.GetCell(1).CellStyle = boldAmountStyle;
                rNet.CreateCell(2).SetCellValue(netVal.ToString("N0")); rNet.GetCell(2).CellStyle = boldAmountStyle;

                // In Word
                curRow++;
                NPOI.SS.UserModel.IRow rWord = sheet.CreateRow(curRow++);
                rWord.CreateCell(0).SetCellValue("In Word:"); rWord.GetCell(0).CellStyle = boldStyle;
                var wordVal = !string.IsNullOrWhiteSpace(item.NetPayableInWords) ? item.NetPayableInWords : NumberToWordsBdt(netVal);
                rWord.CreateCell(1).SetCellValue(wordVal); rWord.GetCell(1).CellStyle = regularStyle;

                // Bank
                curRow++;
                NPOI.SS.UserModel.IRow rBankH = sheet.CreateRow(curRow++);
                rBankH.CreateCell(0).SetCellValue("Net Payment has been transferred to Bank:"); rBankH.GetCell(0).CellStyle = regularStyle;

                NPOI.SS.UserModel.IRow rB1 = sheet.CreateRow(curRow++);
                rB1.CreateCell(0).SetCellValue("Bank Name:"); rB1.GetCell(0).CellStyle = regularStyle;
                rB1.CreateCell(1).SetCellValue(item.BankName1 ?? ""); rB1.GetCell(1).CellStyle = boldStyle;
                NPOI.SS.UserModel.IRow rA1 = sheet.CreateRow(curRow++);
                rA1.CreateCell(0).SetCellValue("Account Number:"); rA1.GetCell(0).CellStyle = regularStyle;
                rA1.CreateCell(1).SetCellValue($"'{ (item.AccountNumber1 ?? "") }"); rA1.GetCell(1).CellStyle = boldStyle;

                // Signatory
                curRow += 2;
                NPOI.SS.UserModel.IRow rBest = sheet.CreateRow(curRow++);
                rBest.CreateCell(0).SetCellValue("Best Regards,"); rBest.GetCell(0).CellStyle = boldStyle;

                curRow += 2;
                NPOI.SS.UserModel.IRow rSigLine = sheet.CreateRow(curRow++);
                rSigLine.CreateCell(0).SetCellValue("---------------------------------------------------");

                if (!string.IsNullOrWhiteSpace(item.ApproverName))
                {
                    NPOI.SS.UserModel.IRow rName = sheet.CreateRow(curRow++);
                    rName.CreateCell(0).SetCellValue(item.ApproverName); rName.GetCell(0).CellStyle = boldStyle;
                }

                if (!string.IsNullOrWhiteSpace(item.ApproverDesignation))
                {
                    NPOI.SS.UserModel.IRow rDesig = sheet.CreateRow(curRow++);
                    rDesig.CreateCell(0).SetCellValue(item.ApproverDesignation); rDesig.GetCell(0).CellStyle = regularStyle;
                }

                if (!string.IsNullOrWhiteSpace(item.ApproverCell))
                {
                    NPOI.SS.UserModel.IRow rCell = sheet.CreateRow(curRow++);
                    rCell.CreateCell(0).SetCellValue($"Cell: {item.ApproverCell}"); rCell.GetCell(0).CellStyle = regularStyle;
                }

                if (!string.IsNullOrWhiteSpace(item.ApproverEmail))
                {
                    NPOI.SS.UserModel.IRow rEmail = sheet.CreateRow(curRow++);
                    rEmail.CreateCell(0).SetCellValue($"Email: {item.ApproverEmail}"); rEmail.GetCell(0).CellStyle = regularStyle;
                }
            }

            using var stream = new MemoryStream();
            workbook.Write(stream);
            return stream.ToArray();
        }

        private string NumberToWordsBdt(decimal amount)
        {
            long whole = (long)amount;
            if (whole == 0) return "Zero Taka Only";

            long crore = whole / 10000000; whole %= 10000000;
            long lac = whole / 100000; whole %= 100000;
            long thousand = whole / 1000; whole %= 1000;
            long hundred = whole / 100; whole %= 100;
            long remainder = whole;

            var parts = new List<string>();
            if (crore > 0) parts.Add(TwoDigitGroupToWords(crore) + " Crore");
            if (lac > 0) parts.Add(TwoDigitGroupToWords(lac) + " Lac");
            if (thousand > 0) parts.Add(TwoDigitGroupToWords(thousand) + " Thousand");
            if (hundred > 0) parts.Add(OnesToWords(hundred) + " Hundred");
            if (remainder > 0) parts.Add(TwoDigitGroupToWords(remainder));

            return string.Join(" ", parts) + " Taka Only";
        }

        private string TwoDigitGroupToWords(long n)
        {
            if (n < 20) return OnesToWords(n);
            long tens = n / 10, ones = n % 10;
            return ones == 0 ? TensToWords(tens) : $"{TensToWords(tens)} {OnesToWords(ones)}";
        }

        private static readonly string[] Ones =
        {
            "Zero","One","Two","Three","Four","Five","Six","Seven","Eight","Nine","Ten",
            "Eleven","Twelve","Thirteen","Fourteen","Fifteen","Sixteen","Seventeen","Eighteen","Nineteen"
        };

        private static readonly string[] Tens =
        {
            "","","Twenty","Thirty","Forty","Fifty","Sixty","Seventy","Eighty","Ninety"
        };

        private string OnesToWords(long n) => Ones[n];
        private string TensToWords(long n) => Tens[n];
    }
}
