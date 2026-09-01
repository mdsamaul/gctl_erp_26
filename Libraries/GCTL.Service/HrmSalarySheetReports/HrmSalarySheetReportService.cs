using Dapper;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.HrmSalarySheetReports;
using GCTL.Data.Models;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Service.HrmSalarySheetReports
{
    public class HrmSalarySheetReportService : AppService<HrmPaySalaryData>, IHrmSalarySheetReportService
    {
        private readonly IRepository<HrmPaySalaryData> reportRepo;
        private readonly IConfiguration configuration;
        private readonly IRepository<CoreAccessCode> accessCodeRepo;

        public HrmSalarySheetReportService(
            IRepository<HrmPaySalaryData> reportRepo,
            IConfiguration configuration,
            IRepository<CoreAccessCode> accessCodeRepo) : base(reportRepo)
        {
            this.reportRepo = reportRepo;
            this.configuration = configuration;
            this.accessCodeRepo = accessCodeRepo;
        }

        public async Task<List<HrmSalarySheetReportVM>> GetSalarySheetDataAsync(SalarySheetFilterData filter)

        {
            try
            {
                using var connection = new SqlConnection(configuration.GetConnectionString("ApplicationDbConnection"));
                var param = new DynamicParameters();

                param.Add("@AccessCode", !string.IsNullOrWhiteSpace(filter.AccessCode) ? filter.AccessCode : DBNull.Value);
                param.Add("@EmployeeId", !string.IsNullOrWhiteSpace(filter.UserId ?? filter.EmployeeId) ? (filter.UserId ?? filter.EmployeeId) : DBNull.Value);
                param.Add("@CompanyCodes", filter.CompanyCodes != null && filter.CompanyCodes.Any() ? string.Join(",", filter.CompanyCodes) : null);
                param.Add("@BranchCodes", filter.BranchCodes != null && filter.BranchCodes.Any() ? string.Join(",", filter.BranchCodes) : null);
                param.Add("@DepartmentCodes", filter.DepartmentCodes != null && filter.DepartmentCodes.Any() ? string.Join(",", filter.DepartmentCodes) : null);
                param.Add("@DesignationCodes", filter.DesignationCodes != null && filter.DesignationCodes.Any() ? string.Join(",", filter.DesignationCodes) : null);
                param.Add("@EmployeeIDs", filter.EmployeeIDs != null && filter.EmployeeIDs.Any() ? string.Join(",", filter.EmployeeIDs) : null);
                param.Add("@FromDate", filter.FromDate);
                param.Add("@ToDate", filter.ToDate);
                param.Add("@Months", filter.MonthIDs != null && filter.MonthIDs.Any() ? string.Join(",", filter.MonthIDs) : null);
                param.Add("@Years", filter.YearIDs != null && filter.YearIDs.Any() ? string.Join(",", filter.YearIDs) : null);

                var result = await connection.QueryAsync<HrmSalarySheetReportVM>(
                    "SP_GetSalarySheetReportData",
                    param,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 120
                );

                return result.ToList();
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<bool> PagePermissionAsync(string accessCode)
        {
            return await accessCodeRepo.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Salary Sheet Report" && x.TitleCheck);
        }

        public byte[] GenerateSalarySheetExcel(List<HrmSalarySheetReportVM> data)
        {
            if (data == null) data = new List<HrmSalarySheetReportVM>();

            var monthYears = data
                .Select(d => $"{d.MonthName} {d.YearName}".Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            string reportPeriod = monthYears.Any() ? string.Join(", ", monthYears) : DateTime.Now.ToString("MMMM,yyyy");

            var sb = new StringBuilder();
            sb.Append("<style> .textmode { } </style>");
            sb.Append("<table style='border-collapse: collapse;width:100%;table-layout: auto !important;' border = '1' id='tableId' runat='server' class='List table table-bordered table-striped table-hover'>");
            sb.Append("<tr><td colspan='36' style='text-align:center;font-weight:bold;font-size:22px'>DataPath Ltd.</td></tr>");
            sb.Append("<tr><td colspan='36' style='text-align:center;font-weight:bold;font-size:16px'>Salary Statement</td></tr>");
            sb.Append($"<tr><td colspan='36' style='text-align:center;font-weight:bold;font-size:16px'>For the month of {reportPeriod}</td></tr>");

            sb.Append("<tr>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:30px; background-color:#376091; color:#f6f0e6;'>SL.</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:100px; height:160px; background-color:#376091; color:#f6f0e6;'>Employee ID</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:100px; height:160px; background-color:#376091; color:#f6f0e6;'>Pay ID</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:150px; height:50px; background-color:#376091; color:#f6f0e6;'>Bank A/c Number DBBL</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:150px; height:50px; background-color:#376091; color:#f6f0e6;'>Bank A/c Number SIBL</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:150px; height:50px; background-color:#376091; color:#f6f0e6;'>Bank A/c Number UCBL</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:120px; background-color:#376091; color:#f6f0e6;'>Employee Name</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:80px; background-color:#376091; color:#f6f0e6;'>DOH</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:100px; background-color:#376091; color:#f6f0e6;'>DEPARTMENT</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:120px; background-color:#376091; color:#f6f0e6;'>DESIGNATION</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Gross Salary </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:80px; background-color:#376091; color:#f6f0e6;'>Rate of Allowances for Holiday</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:85px; background-color:#376091; color:#f6f0e6;'>RPF Contribution Employee </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:45px; background-color:#376091; color:#f6f0e6;'>No. of Extra day</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:55px; background-color:#376091; color:#f6f0e6;'>No. of Holiday Worked</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:80px; background-color:#376091; color:#f6f0e6;'>Total Allowances for Holiday</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:80px; background-color:#376091; color:#f6f0e6;'>Rate of Allowances for Night Shift</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:50px; background-color:#376091; color:#f6f0e6;'>No. of Night Worked</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:80px; background-color:#376091; color:#f6f0e6;'>Total Allowances for Night Shift</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>WFH Allowance</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>USD</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>OT_Eid</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:80px; background-color:#376091; color:#f6f0e6;'>Total Allowances for OT</th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Yearly Bonus </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Incentive </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Quarterly Incentive </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Gratuity </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Arrear </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Eid Bonus DBBL </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'> Eid Bonus UCB </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>TDS  </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:70px; background-color:#376091; color:#f6f0e6;'>Adjustment if any </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:130px; background-color:#376091; color:#f6f0e6;'>Salary DBBL </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:120px; background-color:#376091; color:#f6f0e6;'>Net Payment UCBL </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:120px; background-color:#376091; color:#f6f0e6;'>Net Payment (Round) UCBL </th>");
            sb.Append("<th style='text-align:center;vertical-align: middle;width:120px; background-color:#376091; color:#f6f0e6;'>Remarks </th>");
            sb.Append("</tr>");

            static string FmtAmt(decimal val) => val == 0 ? "-" : val.ToString("0.00");
            static string FmtCnt(decimal val) => val == 0 ? "-" : val.ToString("0.00");
            static string FmtRnd(decimal val) => val == 0 ? "-" : val.ToString("0");

            int sl = 1;
            foreach (var item in data)
            {
                sb.Append("<tr border='1'>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;'>{sl++}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format:\\@;'>{item.Code ?? ""}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format:\\@;'>{item.PayId ?? ""}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.BankAcNo ?? ""}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.BankAcNoSibl ?? ""}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.BankAcNoUcbl ?? ""}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.EmployeeName ?? ""}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format:\\@;'>{item.JoiningDate?.ToString("dd/MM/yyyy") ?? ""}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.DepartmentName ?? ""}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.DesignationName ?? ""}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.GrossSalary)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.RateOfAllowancesForHoliday)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.RpfContributionEmployee)}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtCnt(item.NoOfExtraDay)}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtCnt(item.NoOfHolidayWorked)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.TotalAllowancesForHoliday)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.RateOfAllowancesForNightShift)}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtCnt(item.NoOfNightWorked)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.TotalAllowancesForNightShift)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.WfhAllowance)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.Usd)}</td>");
                sb.Append($"<td style='text-align:center;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.OtEid)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.TotalAllowancesForOt)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.YearlyBonus)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.Incentive)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.QuarterlyIncentive)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.Gratuity)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.Arrear)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.EidBonusDbbl)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.EidBonusUcb)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.Tds)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.AdjustmentIfAny)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.SalaryDbbl)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtAmt(item.NetPaymentUcbl)}</td>");
                sb.Append($"<td style='text-align:right;vertical-align: middle;mso-number-format: \\#\\,\\#\\#0\\;'>{FmtRnd(item.NetPaymentRoundUcbl)}</td>");
                sb.Append($"<td style='text-align:left;vertical-align: middle;'>{item.Remarks ?? ""}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table>");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public byte[] GenerateSalarySheetPdf(List<HrmSalarySheetReportVM> data)
        {
            if (data == null || !data.Any())
                return Array.Empty<byte>();

            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            using var doc = new Document(pdf, PageSize.A3.Rotate());

            var timesRegular = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            var timesBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);

            // Margins for top header (55pt) and bottom footer (40pt)
            doc.SetMargins(55, 5, 40, 5);

            var colWidths = new float[]
            {
                1.2f, // 1. SL.
                3.2f, // 2. Employee ID (prevents wrapping)
                1.2f, // 3. Pay ID (max 4 digits)
                4.0f, // 4. Bank A/c DBBL (prevents wrapping)
                4.0f, // 5. Bank A/c SIBL (prevents wrapping)
                4.0f, // 6. Bank A/c UCBL (prevents wrapping)
                4.7f, // 7. Employee Name
                2.8f, // 8. DOH (prevents date wrapping)
                3.5f, // 9. Designation
                2.8f, // 10. Gross Salary
                2.5f, // 11. Rate Allow. Holiday
                2.8f, // 12. RPF Contrib. Emp.
                1.2f, // 13. No. Extra Day
                1.2f, // 14. No. Holiday Worked
                2.8f, // 15. Total Allow. Holiday
                2.5f, // 16. Rate Allow. Night
                1.2f, // 17. No. Night Worked
                2.8f, // 18. Total Allow. Night
                2.5f, // 19. WFH Allow.
                2.2f, // 20. USD
                2.2f, // 21. OT_Eid
                2.8f, // 22. Total Allow. OT
                2.5f, // 23. Yearly Bonus
                2.5f, // 24. Incentive
                2.5f, // 25. Quarterly Incentive
                2.5f, // 26. Gratuity
                2.5f, // 27. Arrear
                2.5f, // 28. Eid Bonus DBBL
                2.5f, // 29. Eid Bonus UCB
                2.5f, // 30. TDS
                2.5f, // 31. Adjustment
                2.8f, // 32. Salary DBBL
                2.8f, // 33. Net Pay UCBL
                2.8f, // 34. Net Pay Round UCBL
                3.0f  // 35. Remarks
            };

            string[] headers = new string[]
            {
                "SN.", "Employee ID", "Pay ID", "Bank A/c DBBL", "Bank A/c SIBL", "Bank A/c UCBL",
                "Name", "DOH", "Designation", "Gross Salary", "Rate Allow. Holiday",
                "RPF Contrib. Emp.", "No. Extra Day", "No. Holiday Worked", "Total Allow. Holiday",
                "Rate Allow. Night", "No. Night Worked", "Total Allow. Night", "WFH Allow.", "USD", "OT Eid",
                "Total Allow. OT", "Yearly Bonus", "Incentive", "Quarterly Incentive", "Gratuity", "Arrear",
                "Eid Bonus DBBL", "Eid Bonus UCB", "TDS", "Adjustment", "Salary DBBL", "Net Pay UCBL",
                "Net Pay (Round) UCBL", "Remarks"
            };

            var groupData = data
                .GroupBy(x => string.IsNullOrWhiteSpace(x.DepartmentName) ? "Unknown Department" : x.DepartmentName)
                .OrderBy(g => g.Key == "Unknown Department") // false first, true last
                .ThenBy(g => g.Key);

            int serialNo = 1;

            foreach (var dpGroup in groupData)
            {
                var table = new Table(UnitValue.CreatePercentArray(colWidths));
                table.SetWidth(UnitValue.CreatePercentValue(100));
                table.SetFixedLayout();

                var departmentHeaderRow = new Cell(1, 35)
                    .Add(new Paragraph($"Department: {dpGroup.Key}"))
                    .SetFontSize(10f).SetTextAlignment(TextAlignment.LEFT)
                    .SetPadding(3f).SetPaddingLeft(0).SetFont(timesBold)
                    .SetBorder(Border.NO_BORDER);

                table.AddHeaderCell(departmentHeaderRow);

                foreach (var header in headers)
                {
                    var headerCell = new Cell()
                        .Add(new Paragraph(header).SetFont(timesBold).SetFontSize(6f))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetPaddingTop(0.5f).SetPaddingBottom(0.5f).SetPaddingLeft(0.5f).SetPaddingRight(0.5f)
                        .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.2f));
                    table.AddHeaderCell(headerCell);
                }

                table.SetSkipFirstHeader(false);
                table.SetSkipLastFooter(false);

                var orderedItems = dpGroup.OrderBy(x => x.Code).ToList();

                foreach (var item in orderedItems)
                {
                    var values = new string[]
                    {
                        serialNo++.ToString(),
                        item.Code ?? "",
                        item.PayId ?? "",
                        item.BankAcNo ?? "",
                        item.BankAcNoSibl ?? "",
                        item.BankAcNoUcbl ?? "",
                        item.EmployeeName ?? "",
                        item.JoiningDate?.ToString("dd/MM/yyyy") ?? "",
                        item.DesignationName ?? "",
                        item.GrossSalary.ToString("N2"),
                        item.RateOfAllowancesForHoliday.ToString("N2"),
                        item.RpfContributionEmployee.ToString("N2"),
                        item.NoOfExtraDay.ToString("0.##"),
                        item.NoOfHolidayWorked.ToString("0.##"),
                        item.TotalAllowancesForHoliday.ToString("N2"),
                        item.RateOfAllowancesForNightShift.ToString("N2"),
                        item.NoOfNightWorked.ToString("0.##"),
                        item.TotalAllowancesForNightShift.ToString("N2"),
                        item.WfhAllowance.ToString("N2"),
                        item.Usd.ToString("N2"),
                        item.OtEid.ToString("N2"),
                        item.TotalAllowancesForOt.ToString("N2"),
                        item.YearlyBonus.ToString("N2"),
                        item.Incentive.ToString("N2"),
                        item.QuarterlyIncentive.ToString("N2"),
                        item.Gratuity.ToString("N2"),
                        item.Arrear.ToString("N2"),
                        item.EidBonusDbbl.ToString("N2"),
                        item.EidBonusUcb.ToString("N2"),
                        item.Tds.ToString("N2"),
                        item.AdjustmentIfAny.ToString("N2"),
                        item.SalaryDbbl.ToString("N2"),
                        item.NetPaymentUcbl.ToString("N2"),
                        item.NetPaymentRoundUcbl.ToString("N0"),
                        item.Remarks ?? ""
                    };

                    for (int i = 0; i < values.Length; i++)
                    {
                        var cell = new Cell().Add(new Paragraph(values[i]))
                            .SetFontSize(6f).SetFont(timesRegular)
                            .SetPaddingTop(0.5f).SetPaddingBottom(0.5f).SetPaddingLeft(0.2f).SetPaddingRight(0.2f)
                            .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.2f));

                        // Alignment logic matching HrmPayMonthlyOtReport
                        if (i == 6 || i == 8 || i == 34) // Name, Designation, Remarks
                        {
                            cell.SetTextAlignment(TextAlignment.LEFT);
                        }
                        else if (i >= 9 && i <= 33 && i != 12 && i != 13 && i != 16) // Amounts
                        {
                            cell.SetTextAlignment(TextAlignment.RIGHT);
                        }
                        else // SN, IDs, Counts, Dates
                        {
                            cell.SetTextAlignment(TextAlignment.CENTER);
                        }

                        table.AddCell(cell);
                    }
                }

                // Department Subtotal Row
                table.AddCell(CreateBorderCell($"Subtotal ({dpGroup.Key})", timesBold, TextAlignment.CENTER, colspan: 9));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.GrossSalary).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.RateOfAllowancesForHoliday).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.RpfContributionEmployee).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.NoOfExtraDay).ToString("0.##"), timesBold, TextAlignment.CENTER));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.NoOfHolidayWorked).ToString("0.##"), timesBold, TextAlignment.CENTER));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.TotalAllowancesForHoliday).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.RateOfAllowancesForNightShift).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.NoOfNightWorked).ToString("0.##"), timesBold, TextAlignment.CENTER));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.TotalAllowancesForNightShift).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.WfhAllowance).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.Usd).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.OtEid).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.TotalAllowancesForOt).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.YearlyBonus).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.Incentive).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.QuarterlyIncentive).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.Gratuity).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.Arrear).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.EidBonusDbbl).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.EidBonusUcb).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.Tds).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.AdjustmentIfAny).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.SalaryDbbl).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.NetPaymentUcbl).ToString("N2"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell(dpGroup.Sum(x => x.NetPaymentRoundUcbl).ToString("N0"), timesBold, TextAlignment.RIGHT));
                table.AddCell(CreateBorderCell("", timesBold, TextAlignment.LEFT));

                doc.Add(table);
            }

            // Summary / Grand Total Table
            var summaryTable = new Table(UnitValue.CreatePercentArray(colWidths));
            summaryTable.SetWidth(UnitValue.CreatePercentValue(100)).SetMarginTop(10);
            summaryTable.SetFixedLayout();

            summaryTable.AddCell(CreateBorderCell("Grand Total", timesBold, TextAlignment.CENTER, colspan: 9));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.GrossSalary).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.RateOfAllowancesForHoliday).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.RpfContributionEmployee).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.NoOfExtraDay).ToString("0.##"), timesBold, TextAlignment.CENTER));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.NoOfHolidayWorked).ToString("0.##"), timesBold, TextAlignment.CENTER));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.TotalAllowancesForHoliday).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.RateOfAllowancesForNightShift).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.NoOfNightWorked).ToString("0.##"), timesBold, TextAlignment.CENTER));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.TotalAllowancesForNightShift).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.WfhAllowance).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.Usd).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.OtEid).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.TotalAllowancesForOt).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.YearlyBonus).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.Incentive).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.QuarterlyIncentive).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.Gratuity).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.Arrear).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.EidBonusDbbl).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.EidBonusUcb).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.Tds).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.AdjustmentIfAny).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.SalaryDbbl).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.NetPaymentUcbl).ToString("N2"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell(data.Sum(x => x.NetPaymentRoundUcbl).ToString("N0"), timesBold, TextAlignment.RIGHT));
            summaryTable.AddCell(CreateBorderCell("", timesBold, TextAlignment.LEFT));

            doc.Add(summaryTable);
            doc.Close();

            var monthYears = data
                .Select(d => $"{d.MonthName} {d.YearName}".Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            string reportPeriod = monthYears.Any() ? string.Join(", ", monthYears) : DateTime.Now.ToString("MMMM yyyy");

            // Pass 2: Draw Header, Logo, and Footer on EVERY page using PdfCanvas (exact match to HrmPayMonthlyOtReport)
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

                // 1. Draw Logo (Left Top)
                try
                {
                    string logoPath = "wwwroot/images/DP_logo.png";
                    if (File.Exists(logoPath))
                    {
                        var imageData = ImageDataFactory.Create(logoPath);
                        var logo = new Image(imageData);

                        float logoWidth = 90;
                        float logoHeight = 25;
                        float logoX = 15;
                        float logoY = pageSize.GetTop() - 37;

                        logo.SetFixedPosition(i, logoX, logoY);
                        logo.ScaleAbsolute(logoWidth, logoHeight);

                        var logoCanvas = new Canvas(canvas, pageSize);
                        logoCanvas.Add(logo);
                        logoCanvas.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not load logo: {ex.Message}");
                }

                // 2. Draw Company Name (Center Top)
                float headerStartX = pageSize.GetWidth() / 2;
                float headerTopY = pageSize.GetTop() - 25;

                var companyFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
                float companyFontSize = 14;
                string companyText = "DataPath Ltd.";
                float companyTextWidth = companyFont.GetWidth(companyText, companyFontSize);

                canvas.BeginText()
                    .SetFontAndSize(companyFont, companyFontSize)
                    .MoveText(headerStartX - (companyTextWidth / 2), headerTopY)
                    .ShowText(companyText)
                    .EndText();

                // 3. Draw Report Name
                var reportFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
                float reportFontSize = 12;
                string reportText = "Salary Statement";
                float reportTextWidth = reportFont.GetWidth(reportText, reportFontSize);

                canvas.BeginText()
                    .SetFontAndSize(reportFont, reportFontSize)
                    .MoveText(headerStartX - (reportTextWidth / 2), headerTopY - 15)
                    .ShowText(reportText)
                    .EndText();

                // 4. Draw Period Text ("For the month of MonthName YearName")
                var periodFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                float periodFontSize = 9.5f;
                string periodText = $"For the month of {reportPeriod}";
                float periodTextWidth = periodFont.GetWidth(periodText, periodFontSize);

                canvas.BeginText()
                    .SetFontAndSize(periodFont, periodFontSize)
                    .MoveText(headerStartX - (periodTextWidth / 2), headerTopY - 27)
                    .ShowText(periodText)
                    .EndText();

                // 4. Draw Footer (Bottom of page)
                var footerFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                float footerFontSize = 8;
                float margin = 15;
                float yPosition = pageSize.GetBottom() + 20;
                float pageWidth = pageSize.GetWidth();

                // Left: Print Datetime
                canvas.BeginText()
                    .SetFontAndSize(footerFont, footerFontSize)
                    .MoveText(margin, yPosition)
                    .ShowText($"Print Datetime: {DateTime.Now:dd/MM/yyyy hh:mm:ss tt}")
                    .EndText();

                // Center: System Name
                string userText = "GCTL Infosys - HRM & Finance System";
                float userTextWidth = footerFont.GetWidth(userText, footerFontSize);
                canvas.BeginText()
                    .SetFontAndSize(footerFont, footerFontSize)
                    .MoveText((pageWidth - userTextWidth) / 2, yPosition)
                    .SetFillColor(ColorConstants.GRAY)
                    .ShowText(userText)
                    .EndText();

                // Right: Page X of Y
                string pageText = $"Page {i} of {totalPages}";
                float pageTextWidth = footerFont.GetWidth(pageText, footerFontSize);
                canvas.BeginText()
                    .SetFontAndSize(footerFont, footerFontSize)
                    .MoveText(pageWidth - margin - pageTextWidth, yPosition)
                    .SetFillColor(ColorConstants.BLACK)
                    .ShowText(pageText)
                    .EndText();
            }

            outputPdf.Close();
            return outputStream.ToArray();
        }

        private static Cell CreateBorderCell(string text, PdfFont font, TextAlignment alignment, int colspan = 1, float fontSize = 6f)
        {
            var cell = new Cell(1, colspan)
                .Add(new Paragraph(text ?? string.Empty).SetFont(font).SetFontSize(fontSize))
                .SetTextAlignment(alignment)
                .SetPaddingTop(0.5f).SetPaddingBottom(0.5f).SetPaddingLeft(0.5f).SetPaddingRight(0.5f)
                .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.2f));

            return cell;
        }
    }
}
