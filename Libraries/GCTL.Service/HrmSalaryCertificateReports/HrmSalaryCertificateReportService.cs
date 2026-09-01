using Dapper;
using GCTL.Core.Data;
using GCTL.Core.Helpers;
using GCTL.Core.ViewModels.HrmPaySlipReports;
using GCTL.Core.ViewModels.HrmSalaryCertificateReports;
using GCTL.Data.Models;
using iText.Commons.Actions;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Event;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System.Data.SqlClient;
using System.Text;
using Table = iText.Layout.Element.Table;
using HorizontalAlignment = NPOI.SS.UserModel.HorizontalAlignment;
using VerticalAlignment = NPOI.SS.UserModel.VerticalAlignment;

namespace GCTL.Service.HrmSalaryCertificateReports
{
    public class HrmSalaryCertificateReportService : AppService<HrmPaySalaryData>, IHrmSalaryCertificateReportService
    {
        private readonly IRepository<HrmPaySalaryData> reportRepo;
        private readonly IConfiguration configuration;
        private readonly IRepository<CoreAccessCode> accessCodeRepo;

        public HrmSalaryCertificateReportService(IRepository<HrmPaySalaryData> reportRepo, IConfiguration configuration, IRepository<CoreAccessCode> accessCodeRepo) : base(reportRepo)
        {
            this.reportRepo = reportRepo;
            this.configuration = configuration;
            this.accessCodeRepo = accessCodeRepo;
        }

        public async Task<List<HrmSalaryCertificateVM>> GetPaySlipDataAsync(SalaryCertificateFilterData filter)
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

                var result = await connection.QueryAsync<HrmSalaryCertificateVM>(
                    "SP_GetSalaryCertificateData",
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
            return await accessCodeRepo.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Salary Certificate Report" && x.TitleCheck);
        }

        // Was: private static readonly DeviceRgb AccentColor = new DeviceRgb(30, 58, 95);
        private static readonly DeviceRgb AccentColor = new DeviceRgb(0, 0, 0);
        private static readonly DeviceRgb HeaderBg = new DeviceRgb(40, 40, 40);      // dark gray for grid header row
        private static readonly DeviceRgb TotalBg = new DeviceRgb(240, 240, 240);   // soft gray for total/section row
        private static readonly DeviceRgb BorderGray = new DeviceRgb(220, 220, 220);  // gray for borders
        private static readonly SolidBorder GridBorder = new SolidBorder(BorderGray, 0.5f);
        public byte[] GenerateSalaryCertificatePdf(List<HrmSalaryCertificateVM> data)
        {
            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            {
                var document = new Document(pdf, PageSize.A4);
                document.SetMargins(50, 40, 40, 40);

                var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                for (int i = 0; i < data.Count; i++)
                {
                    BuildCertificate(document, data[i], regularFont, boldFont);

                    if (i < data.Count - 1)
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                }

                document.Close();
            }

            return ms.ToArray();
        }

        private void BuildCertificate(Document document, HrmSalaryCertificateVM vm, PdfFont regular, PdfFont bold)
        {
            // Date at top left
            string currentDate = DateTime.Now.ToString("dd MMMM yyyy");
            document.Add(new Paragraph(currentDate)
                .SetFont(regular).SetFontSize(10)
                .SetTextAlignment(TextAlignment.LEFT).SetMarginBottom(12));

            // Title - Bold and Underlined
            document.Add(new Paragraph("SALARY CERTIFICATE")
                .SetFont(bold).SetFontSize(14).SetFontColor(AccentColor)
                .SetUnderline()
                .SetTextAlignment(TextAlignment.CENTER).SetMarginBottom(10));

            // Intro with Employee Name bold
            document.Add(new Paragraph()
                .Add(new Text("This is to certify that ").SetFont(regular))
                .Add(new Text(vm.EmployeeName ?? "").SetFont(bold))
                .Add(new Text(" has been working as a permanent employee in our organization. His employment details are as follows:").SetFont(regular))
                .SetFontSize(10.5f).SetTextAlignment(TextAlignment.JUSTIFIED).SetMarginBottom(10));

            var detailsTable = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 3.8f })).UseAllAvailableWidth().SetMarginBottom(12);
            detailsTable.SetBorder(Border.NO_BORDER);
            AddDetailRow(detailsTable, "Designation", vm.Designation, bold, regular);
            AddDetailRow(detailsTable, "Department", vm.Department, bold, regular);
            AddDetailRow(detailsTable, "Date of Hire", vm.JoiningDate, bold, regular);
            AddDetailRow(detailsTable, "Office ID", vm.EmployeeId, bold, regular);
            document.Add(detailsTable);

            var grossTable = new Table(UnitValue.CreatePercentArray(new float[] { 30f, 20f, 30f, 20f })).UseAllAvailableWidth().SetMarginBottom(12);

            grossTable.AddCell(HeaderCell("Monthly Gross Salary", bold, TextAlignment.LEFT));
            grossTable.AddCell(HeaderCell("(in BDT)", bold, TextAlignment.CENTER));
            grossTable.AddCell(HeaderCell("Monthly Deductions", bold, TextAlignment.LEFT));
            grossTable.AddCell(HeaderCell("(in BDT)", bold, TextAlignment.CENTER));

            grossTable.AddCell(GridCell("Basic", regular));
            grossTable.AddCell(AmountCell(FormatBdtOrBlank(vm.BasicSalary), regular));
            grossTable.AddCell(GridCell("Tax", regular));
            grossTable.AddCell(AmountCell(vm.TaxDeduction > 0 ? FormatBdt(vm.TaxDeduction) : "Paid by the Company", regular));

            grossTable.AddCell(GridCell("House Rent Allowance", regular));
            grossTable.AddCell(AmountCell(FormatBdtOrBlank(vm.HouseRentAllowence), regular));
            grossTable.AddCell(GridCell("Provident Fund", regular));
            grossTable.AddCell(AmountCell(FormatBdtOrBlank(vm.PFDeduction), regular));

            grossTable.AddCell(GridCell("Medical Allowance", regular));
            grossTable.AddCell(AmountCell(FormatBdtOrBlank(vm.MedicalAllowence), regular));
            grossTable.AddCell(GridCell("Others (Please Specify)", regular));
            grossTable.AddCell(AmountCell(vm.OtherDeduction > 0 ? FormatBdt(vm.OtherDeduction) : "N/A", regular));

            grossTable.AddCell(GridCell("Conveyance Allowance", regular));
            grossTable.AddCell(AmountCell(FormatBdtOrBlank(vm.ConveyanceAllowence), regular));
            grossTable.AddCell(GridCell("", regular));
            grossTable.AddCell(GridCell("", regular));

            grossTable.AddCell(GridCell("Others (Please Specify)", regular));
            grossTable.AddCell(AmountCell(FormatBdtOrBlank(vm.OtherAllowence), regular));
            grossTable.AddCell(GridCell("", regular));
            grossTable.AddCell(GridCell("", regular));

            grossTable.AddCell(TotalCell("Total", bold));
            grossTable.AddCell(AmountCell(FormatBdt(vm.TotalAllowence), bold, bgColor: TotalBg));
            grossTable.AddCell(TotalCell("Total", bold));
            var deductionTotalShown = vm.PFDeduction + vm.OtherDeduction + (vm.TaxDeduction > 0 ? vm.TaxDeduction : 0);
            grossTable.AddCell(AmountCell(FormatBdt(deductionTotalShown), bold, bgColor: TotalBg));

            document.Add(grossTable);

            document.Add(new Paragraph("Annual Pay and Benefits in (BDT):")
                .SetFont(bold).SetFontSize(10.5f).SetFontColor(AccentColor)
                .SetMarginBottom(3));

            var annualTable = new Table(UnitValue.CreatePercentArray(new float[] { 32f, 68f })).UseAllAvailableWidth().SetMarginBottom(6);
            annualTable.AddCell(GridCell("Festival Bonus", regular));
            annualTable.AddCell(AmountCell(FormatBdtOrBlank(vm.FestivalBonus), regular));
            annualTable.AddCell(GridCell("Yearly Bonus", regular));
            annualTable.AddCell(AmountCell(FormatBdtOrBlank(vm.YearlyBonus), regular));
            annualTable.AddCell(GridCell("Incentives", regular));
            annualTable.AddCell(AmountCell(FormatBdtOrBlank(vm.Incentives), regular));
            annualTable.AddCell(GridCell("Provident Fund", regular));
            annualTable.AddCell(AmountCell(FormatBdtOrBlank(vm.ProvidentFund), regular));
            document.Add(annualTable);

            var totalYearly = (vm.TotalAllowence * 12) + vm.FestivalBonus + vm.YearlyBonus + vm.Incentives + vm.ProvidentFund;
            var boxedTotal = new Table(1).UseAllAvailableWidth().SetMarginBottom(10);
            var boxedCell = new Cell()
                .Add(new Paragraph()
                    .Add(new Text("Total (Yearly):   ").SetFont(bold).SetFontSize(10))
                    .Add(new Text(FormatBdt(totalYearly)).SetFont(bold).SetFontSize(10))
                    .Add(new Text($"  (In Words: {NumberToWordsBdt(totalYearly)})").SetFont(regular).SetFontSize(9.5f)))
                .SetBorder(GridBorder)
                .SetPadding(6)
                .SetTextAlignment(TextAlignment.LEFT);
            boxedTotal.AddCell(boxedCell);
            document.Add(boxedTotal);

            var certBox = new Div()
                .SetPadding(0)
                .SetMarginTop(4).SetMarginBottom(0);

            certBox.Add(new Paragraph("Net Payment has been transferred to the following Banks:")
                .SetFont(bold).SetFontSize(10).SetMarginBottom(6));

            var bankTable = new Table(UnitValue.CreatePercentArray(new float[] { 2.5f, 7.5f })).UseAllAvailableWidth().SetMarginBottom(10);
            bankTable.AddCell(new Cell().Add(new Paragraph("Bank Name:").SetFont(bold).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
            bankTable.AddCell(new Cell().Add(new Paragraph(vm.BankACName ?? "").SetFont(regular).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
            bankTable.AddCell(new Cell().Add(new Paragraph("Account Number:").SetFont(bold).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
            bankTable.AddCell(new Cell().Add(new Paragraph(vm.BankACNo ?? "").SetFont(regular).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));

            if (!string.IsNullOrWhiteSpace(vm.BankACName2))
            {
                bankTable.AddCell(new Cell().Add(new Paragraph("Bank Name:").SetFont(bold).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
                bankTable.AddCell(new Cell().Add(new Paragraph(vm.BankACName2 ?? "").SetFont(regular).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
                bankTable.AddCell(new Cell().Add(new Paragraph("Account Number:").SetFont(bold).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
                bankTable.AddCell(new Cell().Add(new Paragraph(vm.BankACNo2 ?? "").SetFont(regular).SetFontSize(9.5f)).SetBorder(Border.NO_BORDER).SetPadding(2));
            }
            certBox.Add(bankTable);

            certBox.Add(new Paragraph("Best Regards,").SetFont(bold).SetFontSize(10).SetMarginBottom(4));

            var sigTable = new Table(UnitValue.CreatePercentArray(new float[] { 50, 50 }))
                .UseAllAvailableWidth()
                .SetMarginTop(10);

            var signatureCell = new Cell()
                .SetBorder(Border.NO_BORDER)
                .SetPaddingRight(10);

            signatureCell.Add(new Paragraph("______________________________").SetFont(regular).SetFontSize(9).SetMarginTop(24).SetMarginBottom(2));
            if (!string.IsNullOrWhiteSpace(vm.ApproverName))
                signatureCell.Add(new Paragraph(vm.ApproverName).SetFont(bold).SetFontSize(10).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverDesignation))
                signatureCell.Add(new Paragraph(vm.ApproverDesignation).SetFont(regular).SetFontSize(9.5f).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverCell))
                signatureCell.Add(new Paragraph($"Cell: {vm.ApproverCell}").SetFont(regular).SetFontSize(9.5f).SetMarginBottom(0));
            if (!string.IsNullOrWhiteSpace(vm.ApproverEmail))
                signatureCell.Add(new Paragraph($"Email: {vm.ApproverEmail}").SetFont(regular).SetFontSize(9.5f).SetMarginBottom(0));

            var spacerCell = new Cell().SetBorder(Border.NO_BORDER);

            sigTable.AddCell(signatureCell);
            sigTable.AddCell(spacerCell);

            certBox.Add(sigTable);
            document.Add(certBox);
        }

        private Paragraph SectionHeader(string text, PdfFont bold)
        {
            return new Paragraph(text)
                .SetFont(bold).SetFontSize(11).SetFontColor(AccentColor)
                .SetPaddingBottom(2).SetMarginBottom(6);
        }

        private void AddDetailRow(Table table, string label, string value, PdfFont labelFont, PdfFont valFont)
        {
            table.AddCell(new Cell().Add(new Paragraph(label).SetFont(labelFont).SetFontSize(10.5f)).SetBorder(Border.NO_BORDER).SetPadding(1.5f));
            table.AddCell(new Cell().Add(new Paragraph($": {value}").SetFont(valFont).SetFontSize(10.5f)).SetBorder(Border.NO_BORDER).SetPadding(1.5f));
        }

        private Cell GridCell(string text, PdfFont font, TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text ?? string.Empty).SetFont(font).SetFontSize(9.5f))
                .SetTextAlignment(align)
                .SetBorder(GridBorder)
                .SetPadding(3);
        }

        private Cell HeaderCell(string text, PdfFont bold, TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(bold).SetFontSize(9.5f).SetFontColor(ColorConstants.BLACK))
                .SetTextAlignment(align)
                .SetBorder(GridBorder)
                .SetPadding(3);
        }

        private Cell TotalCell(string text, PdfFont bold, TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text ?? string.Empty).SetFont(bold).SetFontSize(9.5f))
                .SetTextAlignment(align)
                .SetBackgroundColor(TotalBg)
                .SetBorder(GridBorder)
                .SetPadding(3);
        }

        private Cell AmountCell(string formattedAmount, PdfFont font, bool bold = false, DeviceRgb bgColor = null)
        {
            var p = new Paragraph(formattedAmount ?? string.Empty)
                .SetFont(font)
                .SetFontSize(9.5f)
                .SetTextAlignment(TextAlignment.LEFT);

            var outer = new Cell().Add(p).SetBorder(GridBorder).SetPadding(3);
            if (bgColor != null) outer.SetBackgroundColor(bgColor);
            return outer;
        }

        // Blank (not "0.00", not "N/A") for zero/empty amounts, per confirmed requirement
        private string FormatBdtOrBlank(decimal amount) => amount > 0 ? FormatBdt(amount) : "";

        // Lakh/Lac-style grouping: e.g. 437760.00 -> "BDT 4,37,760.00"
        private string FormatBdt(decimal amount)
        {
            bool negative = amount < 0;
            amount = Math.Abs(amount);

            string wholeStr = ((long)amount).ToString();
            string paise = ((amount - (long)amount) * 100m).ToString("00");

            string grouped;
            if (wholeStr.Length <= 3)
            {
                grouped = wholeStr;
            }
            else
            {
                string lastThree = wholeStr.Substring(wholeStr.Length - 3);
                string rest = wholeStr.Substring(0, wholeStr.Length - 3);
                var sb = new StringBuilder();
                for (int i = 0; i < rest.Length; i++)
                {
                    if (i > 0 && (rest.Length - i) % 2 == 0)
                        sb.Append(',');
                    sb.Append(rest[i]);
                }
                grouped = sb.ToString() + "," + lastThree;
            }

            return $"BDT {(negative ? "-" : "")}{grouped}.{paise}";
        }

        // South Asian (Lac/Crore) number-to-words for Taka amounts — single "Taka Only" (no duplicate word)
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

        public byte[] GenerateSalaryCertificateExcel(List<HrmSalaryCertificateVM> data)
        {
            IWorkbook workbook = new XSSFWorkbook();

            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                string sheetName = string.IsNullOrWhiteSpace(item.EmployeeId)
                    ? $"Certificate_{i + 1}"
                    : $"{item.EmployeeId}".Replace(":", "").Replace("\\", "").Replace("/", "").Replace("?", "").Replace("*", "").Replace("[", "").Replace("]", "");

                if (sheetName.Length > 30) sheetName = sheetName.Substring(0, 30);
                int sheetIndex = 1;
                string originalSheetName = sheetName;
                while (workbook.GetSheet(sheetName) != null)
                {
                    sheetName = $"{originalSheetName}_{sheetIndex++}";
                }

                ISheet sheet = workbook.CreateSheet(sheetName);
                sheet.DisplayGridlines = true;

                sheet.SetColumnWidth(0, 32 * 256);
                sheet.SetColumnWidth(1, 26 * 256);
                sheet.SetColumnWidth(2, 32 * 256);
                sheet.SetColumnWidth(3, 26 * 256);

                IFont titleFont = workbook.CreateFont();
                titleFont.FontName = "Calibri";
                titleFont.FontHeightInPoints = 14;
                titleFont.IsBold = true;
                titleFont.Underline = FontUnderlineType.Single;

                IFont boldFont = workbook.CreateFont();
                boldFont.FontName = "Calibri";
                boldFont.FontHeightInPoints = 11;
                boldFont.IsBold = true;

                IFont regularFont = workbook.CreateFont();
                regularFont.FontName = "Calibri";
                regularFont.FontHeightInPoints = 11;

                ICellStyle dateStyle = workbook.CreateCellStyle();
                dateStyle.SetFont(regularFont);
                dateStyle.Alignment = HorizontalAlignment.Left;
                dateStyle.VerticalAlignment = VerticalAlignment.Center;

                ICellStyle titleStyle = workbook.CreateCellStyle();
                titleStyle.SetFont(titleFont);
                titleStyle.Alignment = HorizontalAlignment.Center;
                titleStyle.VerticalAlignment = VerticalAlignment.Center;

                ICellStyle introStyle = workbook.CreateCellStyle();
                introStyle.SetFont(regularFont);
                introStyle.WrapText = true;
                introStyle.VerticalAlignment = VerticalAlignment.Center;

                ICellStyle labelBoldStyle = workbook.CreateCellStyle();
                labelBoldStyle.SetFont(boldFont);

                ICellStyle valueRegularStyle = workbook.CreateCellStyle();
                valueRegularStyle.SetFont(regularFont);

                ICellStyle gridHeaderStyle = workbook.CreateCellStyle();
                gridHeaderStyle.SetFont(boldFont);
                gridHeaderStyle.Alignment = HorizontalAlignment.Left;
                gridHeaderStyle.VerticalAlignment = VerticalAlignment.Center;
                SetThinBorders(gridHeaderStyle);

                ICellStyle gridHeaderCenterStyle = workbook.CreateCellStyle();
                gridHeaderCenterStyle.SetFont(boldFont);
                gridHeaderCenterStyle.Alignment = HorizontalAlignment.Center;
                gridHeaderCenterStyle.VerticalAlignment = VerticalAlignment.Center;
                SetThinBorders(gridHeaderCenterStyle);

                ICellStyle gridRegularStyle = workbook.CreateCellStyle();
                gridRegularStyle.SetFont(regularFont);
                gridRegularStyle.Alignment = HorizontalAlignment.Left;
                gridRegularStyle.VerticalAlignment = VerticalAlignment.Center;
                SetThinBorders(gridRegularStyle);

                ICellStyle gridAmountStyle = workbook.CreateCellStyle();
                gridAmountStyle.SetFont(regularFont);
                gridAmountStyle.Alignment = HorizontalAlignment.Left;
                gridAmountStyle.VerticalAlignment = VerticalAlignment.Center;
                SetThinBorders(gridAmountStyle);

                ICellStyle gridTotalStyle = workbook.CreateCellStyle();
                gridTotalStyle.SetFont(boldFont);
                gridTotalStyle.Alignment = HorizontalAlignment.Left;
                gridTotalStyle.VerticalAlignment = VerticalAlignment.Center;
                SetThinBorders(gridTotalStyle);

                ICellStyle boxedSummaryStyle = workbook.CreateCellStyle();
                boxedSummaryStyle.SetFont(boldFont);
                boxedSummaryStyle.Alignment = HorizontalAlignment.Left;
                boxedSummaryStyle.VerticalAlignment = VerticalAlignment.Center;
                boxedSummaryStyle.WrapText = true;

                // Date at Row 8 (Index 7)
                IRow row8 = GetOrCreateRow(sheet, 7);
                row8.HeightInPoints = 20;
                ICell cellDate = GetOrCreateCell(row8, 0);
                cellDate.SetCellValue(DateTime.Now.ToString("dd MMMM yyyy"));
                cellDate.CellStyle = dateStyle;

                // Title (Row 11 -> Index 10)
                IRow row11 = GetOrCreateRow(sheet, 10);
                row11.HeightInPoints = 25;
                ICell cellTitle = GetOrCreateCell(row11, 0);
                cellTitle.SetCellValue("SALARY CERTIFICATE");
                cellTitle.CellStyle = titleStyle;
                sheet.AddMergedRegion(new CellRangeAddress(10, 10, 0, 3));

                // Paragraph intro (Row 12 -> Index 11) with rich text for bold name
                IRow row12 = GetOrCreateRow(sheet, 11);
                row12.HeightInPoints = 28;
                ICell cellIntro = GetOrCreateCell(row12, 0);

                var richIntro = new XSSFRichTextString(
                    $"This is to certify that {item.EmployeeName ?? ""} has been working as a permanent employee in our organization. His employment details are as follows:");
                richIntro.ApplyFont(0, "This is to certify that ".Length, regularFont);
                richIntro.ApplyFont("This is to certify that ".Length, "This is to certify that ".Length + (item.EmployeeName ?? "").Length, boldFont);
                richIntro.ApplyFont("This is to certify that ".Length + (item.EmployeeName ?? "").Length, richIntro.Length, regularFont);

                cellIntro.SetCellValue(richIntro);
                cellIntro.CellStyle = introStyle;
                sheet.AddMergedRegion(new CellRangeAddress(11, 11, 0, 3));

                // Employment Details (Rows 13-16 -> Index 12-15)
                AddDetailRowExcel(sheet, 12, "Designation", item.Designation, labelBoldStyle, valueRegularStyle);
                AddDetailRowExcel(sheet, 13, "Department", item.Department, labelBoldStyle, valueRegularStyle);
                AddDetailRowExcel(sheet, 14, "Date of Hire", item.JoiningDate, labelBoldStyle, valueRegularStyle);
                AddDetailRowExcel(sheet, 15, "Office ID", item.EmployeeId, labelBoldStyle, valueRegularStyle);

                // Monthly Gross & Deductions Table Header (Row 19 -> Index 18)
                IRow row19 = GetOrCreateRow(sheet, 18);
                row19.HeightInPoints = 20;
                SetGridCell(row19, 0, "Monthly Gross Salary", gridHeaderStyle);
                SetGridCell(row19, 1, "(in BDT)", gridHeaderCenterStyle);
                SetGridCell(row19, 2, "Monthly Deductions", gridHeaderStyle);
                SetGridCell(row19, 3, "(in BDT)", gridHeaderCenterStyle);

                // Monthly Gross & Deductions Rows (Rows 20-24 -> Index 19-23)
                var deductionTotalShown = item.PFDeduction + item.OtherDeduction + (item.TaxDeduction > 0 ? item.TaxDeduction : 0);

                IRow row20 = GetOrCreateRow(sheet, 19);
                SetGridCell(row20, 0, "Basic", gridRegularStyle);
                SetGridCell(row20, 1, FormatBdtOrBlank(item.BasicSalary), gridAmountStyle);
                SetGridCell(row20, 2, "Tax", gridRegularStyle);
                SetGridCell(row20, 3, item.TaxDeduction > 0 ? FormatBdt(item.TaxDeduction) : "Paid by the Company", gridAmountStyle);

                IRow row21 = GetOrCreateRow(sheet, 20);
                SetGridCell(row21, 0, "House Rent Allowance", gridRegularStyle);
                SetGridCell(row21, 1, FormatBdtOrBlank(item.HouseRentAllowence), gridAmountStyle);
                SetGridCell(row21, 2, "Provident Fund", gridRegularStyle);
                SetGridCell(row21, 3, FormatBdtOrBlank(item.PFDeduction), gridAmountStyle);

                IRow row22 = GetOrCreateRow(sheet, 21);
                SetGridCell(row22, 0, "Medical Allowance", gridRegularStyle);
                SetGridCell(row22, 1, FormatBdtOrBlank(item.MedicalAllowence), gridAmountStyle);
                SetGridCell(row22, 2, "Others (Please Specify)", gridRegularStyle);
                SetGridCell(row22, 3, item.OtherDeduction > 0 ? FormatBdt(item.OtherDeduction) : "N/A", gridAmountStyle);

                IRow row23 = GetOrCreateRow(sheet, 22);
                SetGridCell(row23, 0, "Conveyance Allowance", gridRegularStyle);
                SetGridCell(row23, 1, FormatBdtOrBlank(item.ConveyanceAllowence), gridAmountStyle);
                SetGridCell(row23, 2, "", gridRegularStyle);
                SetGridCell(row23, 3, "", gridAmountStyle);

                IRow row24 = GetOrCreateRow(sheet, 23);
                SetGridCell(row24, 0, "Others (Please Specify)", gridRegularStyle);
                SetGridCell(row24, 1, FormatBdtOrBlank(item.OtherAllowence), gridAmountStyle);
                SetGridCell(row24, 2, "", gridRegularStyle);
                SetGridCell(row24, 3, "", gridAmountStyle);

                IRow row25 = GetOrCreateRow(sheet, 24);
                SetGridCell(row25, 0, "", gridRegularStyle);
                SetGridCell(row25, 1, "", gridAmountStyle);
                SetGridCell(row25, 2, "", gridRegularStyle);
                SetGridCell(row25, 3, "", gridAmountStyle);

                // Total Row (Row 26 -> Index 25)
                IRow row26 = GetOrCreateRow(sheet, 25);
                SetGridCell(row26, 0, "Total", gridTotalStyle);
                SetGridCell(row26, 1, FormatBdt(item.TotalAllowence), gridTotalStyle);
                SetGridCell(row26, 2, "Total", gridTotalStyle);
                SetGridCell(row26, 3, FormatBdt(deductionTotalShown), gridTotalStyle);

                // Annual Pay and Benefits Header (Row 28 -> Index 27)
                IRow row28 = GetOrCreateRow(sheet, 27);
                ICell cellAnnualHeader = GetOrCreateCell(row28, 0);
                cellAnnualHeader.SetCellValue("Annual Pay and Benefits in (BDT):");
                cellAnnualHeader.CellStyle = labelBoldStyle;

                // Annual Pay and Benefits Rows (Rows 29-32 -> Index 28-31)
                AddAnnualRowExcel(sheet, 28, "Festival Bonus", FormatBdtOrBlank(item.FestivalBonus), gridRegularStyle, gridAmountStyle);
                AddAnnualRowExcel(sheet, 29, "Yearly Bonus", FormatBdtOrBlank(item.YearlyBonus), gridRegularStyle, gridAmountStyle);
                AddAnnualRowExcel(sheet, 30, "Incentives", FormatBdtOrBlank(item.Incentives), gridRegularStyle, gridAmountStyle);
                AddAnnualRowExcel(sheet, 31, "Provident Fund", FormatBdtOrBlank(item.ProvidentFund), gridRegularStyle, gridAmountStyle);

                // Boxed Total Yearly (Row 34 -> Index 33)
                var totalYearly = (item.TotalAllowence * 12) + item.FestivalBonus + item.YearlyBonus + item.Incentives + item.ProvidentFund;
                IRow row34 = GetOrCreateRow(sheet, 33);
                row34.HeightInPoints = 28;
                ICell cellBoxed = GetOrCreateCell(row34, 0);
                cellBoxed.SetCellValue($"Total (Yearly):   {FormatBdt(totalYearly)} (In Words: {NumberToWordsBdt(totalYearly)})");
                cellBoxed.CellStyle = boxedSummaryStyle;
                sheet.AddMergedRegion(new CellRangeAddress(33, 33, 0, 3));

                // Bank Details Header (Row 36 -> Index 35)
                IRow row36 = GetOrCreateRow(sheet, 35);
                ICell cellBankHeader = GetOrCreateCell(row36, 0);
                cellBankHeader.SetCellValue("Net Payment has been transferred to the following Banks:");
                cellBankHeader.CellStyle = labelBoldStyle;

                // Bank Details Rows (Rows 37-41 -> Index 36-40)
                IRow row37 = GetOrCreateRow(sheet, 36);
                GetOrCreateCell(row37, 0).SetCellValue("Bank Name:");
                GetOrCreateCell(row37, 0).CellStyle = labelBoldStyle;
                GetOrCreateCell(row37, 1).SetCellValue(item.BankACName);
                GetOrCreateCell(row37, 1).CellStyle = valueRegularStyle;

                IRow row38 = GetOrCreateRow(sheet, 37);
                GetOrCreateCell(row38, 0).SetCellValue("Account Number:");
                GetOrCreateCell(row38, 0).CellStyle = labelBoldStyle;
                GetOrCreateCell(row38, 1).SetCellValue($"'{item.BankACNo}");
                GetOrCreateCell(row38, 1).CellStyle = valueRegularStyle;

                if (!string.IsNullOrWhiteSpace(item.BankACName2))
                {
                    IRow row40 = GetOrCreateRow(sheet, 39);
                    GetOrCreateCell(row40, 0).SetCellValue("Bank Name:");
                    GetOrCreateCell(row40, 0).CellStyle = labelBoldStyle;
                    GetOrCreateCell(row40, 1).SetCellValue(item.BankACName2);
                    GetOrCreateCell(row40, 1).CellStyle = valueRegularStyle;

                    IRow row41 = GetOrCreateRow(sheet, 40);
                    GetOrCreateCell(row41, 0).SetCellValue("Account Number:");
                    GetOrCreateCell(row41, 0).CellStyle = labelBoldStyle;
                    GetOrCreateCell(row41, 1).SetCellValue($"'{item.BankACNo2}");
                    GetOrCreateCell(row41, 1).CellStyle = valueRegularStyle;
                }

                // Signatory Block (Rows 44-51 -> Index 43-50)
                IRow row44 = GetOrCreateRow(sheet, 43);
                GetOrCreateCell(row44, 0).SetCellValue("Best Regards,");
                GetOrCreateCell(row44, 0).CellStyle = labelBoldStyle;

                IRow row47 = GetOrCreateRow(sheet, 46);
                GetOrCreateCell(row47, 0).SetCellValue("______________________________");

                if (!string.IsNullOrWhiteSpace(item.ApproverName))
                {
                    IRow row48 = GetOrCreateRow(sheet, 47);
                    GetOrCreateCell(row48, 0).SetCellValue(item.ApproverName);
                    GetOrCreateCell(row48, 0).CellStyle = labelBoldStyle;
                }

                if (!string.IsNullOrWhiteSpace(item.ApproverDesignation))
                {
                    IRow row49 = GetOrCreateRow(sheet, 48);
                    GetOrCreateCell(row49, 0).SetCellValue(item.ApproverDesignation);
                    GetOrCreateCell(row49, 0).CellStyle = introStyle;
                }

                if (!string.IsNullOrWhiteSpace(item.ApproverCell))
                {
                    IRow row50 = GetOrCreateRow(sheet, 49);
                    GetOrCreateCell(row50, 0).SetCellValue($"Cell: {item.ApproverCell}");
                    GetOrCreateCell(row50, 0).CellStyle = introStyle;
                }

                if (!string.IsNullOrWhiteSpace(item.ApproverEmail))
                {
                    IRow row51 = GetOrCreateRow(sheet, 50);
                    GetOrCreateCell(row51, 0).SetCellValue($"Email: {item.ApproverEmail}");
                    GetOrCreateCell(row51, 0).CellStyle = introStyle;
                }
            }

            using var stream = new MemoryStream();
            workbook.Write(stream);
            return stream.ToArray();
        }

        private static void SetThinBorders(ICellStyle style)
        {
            style.BorderTop = BorderStyle.Thin;
            style.BorderBottom = BorderStyle.Thin;
            style.BorderLeft = BorderStyle.Thin;
            style.BorderRight = BorderStyle.Thin;
        }

        private static IRow GetOrCreateRow(ISheet sheet, int rowIndex)
        {
            return sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        }

        private static ICell GetOrCreateCell(IRow row, int cellIndex)
        {
            return row.GetCell(cellIndex) ?? row.CreateCell(cellIndex);
        }

        private static void AddDetailRowExcel(ISheet sheet, int rowIndex, string label, string value, ICellStyle labelStyle, ICellStyle valueStyle)
        {
            IRow row = GetOrCreateRow(sheet, rowIndex);
            ICell cellLabel = GetOrCreateCell(row, 0);
            cellLabel.SetCellValue(label);
            cellLabel.CellStyle = labelStyle;

            ICell cellVal = GetOrCreateCell(row, 1);
            cellVal.SetCellValue($": {value}");
            cellVal.CellStyle = valueStyle;
        }

        private static void SetGridCell(IRow row, int cellIndex, string value, ICellStyle style)
        {
            ICell cell = GetOrCreateCell(row, cellIndex);
            cell.SetCellValue(value ?? string.Empty);
            cell.CellStyle = style;
        }

        private static void AddAnnualRowExcel(ISheet sheet, int rowIndex, string label, string value, ICellStyle labelStyle, ICellStyle amountStyle)
        {
            IRow row = GetOrCreateRow(sheet, rowIndex);
            SetGridCell(row, 0, label, labelStyle);
            SetGridCell(row, 1, value, amountStyle);
            SetGridCell(row, 2, "", labelStyle);
            SetGridCell(row, 3, "", amountStyle);
            sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 1, 3));
        }

    }
}
