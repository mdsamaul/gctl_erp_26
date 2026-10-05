using Dapper;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Wordprocessing;
using GCTL.Core.Data;
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.Dashboard;
using GCTL.Core.ViewModels.EachGcFilterRequest;
using GCTL.Core.ViewModels.HrmLeaveSummaryReports;
using GCTL.Data.Models;
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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Data;
using System.Drawing;
using PdfDocument2 = iText.Layout.Document;

namespace GCTL.Service.HrmLeaveSummaryReports
{
    public class HrmLeaveSummaryReportService : AppService<HrmLeaveApplicationEntry>, IHrmLeaveSummaryReportService
    {
        private readonly IRepository<HrmLeaveApplicationEntry> entryRepo;
        private readonly IRepository<HrmEmployee> employeeRepo;
        private readonly IRepository<HrmEmployeeOfficialInfo> empOffRepo;
        private readonly IRepository<HrmDefDesignation> desiRepo;
        private readonly IRepository<HrmDefDepartment> depRepo;
        private readonly IRepository<CoreBranch> branchRepo;
        private readonly IRepository<CoreCompany> companyRepo;
        private readonly IRepository<HrmAtdLeaveType> leaveTypeRepo;
        private readonly IRepository<CoreAccessCode> accessCodeRepo;
        private readonly string _configuration;

     

        // TODO: confirm this is the exact stored value for HrapprovalStatus on an HR-approved leave entry
        private const string ApprovedStatus = "Approved";

        public HrmLeaveSummaryReportService(
            IRepository<HrmLeaveApplicationEntry> entryRepo,
            IRepository<HrmEmployee> employeeRepo,
            IRepository<HrmEmployeeOfficialInfo> empOffRepo,
            IRepository<HrmDefDesignation> desiRepo,
            IRepository<HrmDefDepartment> depRepo,
            IRepository<CoreBranch> branchRepo,
            IRepository<CoreCompany> companyRepo,
            IRepository<HrmAtdLeaveType> leaveTypeRepo,
            IRepository<CoreAccessCode> accessCodeRepo,
            IConfiguration configuration) : base(entryRepo)
        {
            this.entryRepo = entryRepo;
            this.employeeRepo = employeeRepo;
            this.empOffRepo = empOffRepo;
            this.desiRepo = desiRepo;
            this.depRepo = depRepo;
            this.branchRepo = branchRepo;
            this.companyRepo = companyRepo;
            this.leaveTypeRepo = leaveTypeRepo;
            this.accessCodeRepo = accessCodeRepo;
            _configuration = configuration.GetConnectionString("ApplicationDbConnection");
        }

        // Joining-date anniversary cycle: e.g. joined 16/04/2019 -> current cycle is the most
        // recent 16-Apr through the next 16-Apr that contains "asOf".
        private static (DateTime CycleStart, DateTime CycleEnd) GetCurrentCycle(DateTime joiningDate, DateTime asOf)
        {
            var cycleStart = joiningDate.Date;
            if (cycleStart > asOf)
                return (cycleStart, cycleStart.AddYears(1));

            while (cycleStart.AddYears(1) <= asOf)
                cycleStart = cycleStart.AddYears(1);

            return (cycleStart, cycleStart.AddYears(1));
        }

        public async Task<LeaveDashboardResponseDto> GetLeaveDashboardAsync(string companyCode, string branchCode, string departmentCode, int year,
     int page, int pageSize, string search, string employeeId = null)   // ← নতুন parameter
        {
            using var con = new SqlConnection(_configuration);
            await con.OpenAsync();

            var param = new DynamicParameters();
            param.Add("@CompanyCode", string.IsNullOrEmpty(companyCode) ? null : companyCode, DbType.String);
            param.Add("@BranchCode", string.IsNullOrEmpty(branchCode) ? null : branchCode, DbType.String);
            param.Add("@DepartmentCode", string.IsNullOrEmpty(departmentCode) ? null : departmentCode, DbType.String);
            param.Add("@Year", year, DbType.Int32);
            param.Add("@Page", page, DbType.Int32);
            param.Add("@PageSize", pageSize, DbType.Int32);
            param.Add("@Search", string.IsNullOrEmpty(search) ? null : search, DbType.String);
            param.Add("@EmployeeId", string.IsNullOrEmpty(employeeId) ? null : employeeId, DbType.String);  // ← নতুন

            using var multi = await con.QueryMultipleAsync(
                "usp_GetLeaveDashboard",
                param,
                commandType: CommandType.StoredProcedure
            );

            // RS1 — Summary
            var summary = await multi.ReadFirstOrDefaultAsync<LeaveSummaryCardDto>()
                          ?? new LeaveSummaryCardDto();

            // RS2 — Leave types
            var leaveTypes = (await multi.ReadAsync<LeaveTypeDto>()).ToList();

            // RS3 — Paged flat rows
            var employees = (await multi.ReadAsync<EmployeeLeaveRowDto>()).ToList();

            int totalCount = employees.FirstOrDefault()?.TotalCount ?? 0;

            return new LeaveDashboardResponseDto
            {
                Summary = summary,
                LeaveTypes = leaveTypes,
                Employees = employees,
                TotalCount = totalCount
            };
        }

        public async Task<LeaveSummaryFilterListViewModel> GetDataAsync(LeaveSummaryFilterViewModel filter)
        {
            var asOf = DateTime.Now.Date;

            var empQuery = from eoi in empOffRepo.All().AsNoTracking()
                           join e in employeeRepo.All().AsNoTracking() on eoi.EmployeeId equals e.EmployeeId
                           join dg in desiRepo.All().AsNoTracking() on eoi.DesignationCode equals dg.DesignationCode into dgJoin
                           from dg in dgJoin.DefaultIfEmpty()
                           join cb in branchRepo.All().AsNoTracking() on eoi.BranchCode equals cb.BranchCode into cbJoin
                           from cb in cbJoin.DefaultIfEmpty()
                           join dp in depRepo.All().AsNoTracking() on eoi.DepartmentCode equals dp.DepartmentCode into dpJoin
                           from dp in dpJoin.DefaultIfEmpty()
                           join cp in companyRepo.All().AsNoTracking() on eoi.CompanyCode equals cp.CompanyCode into cpJoin
                           from cp in cpJoin.DefaultIfEmpty()
                           select new
                           {
                               EmpId = e.EmployeeId,
                               EmpName = (e.FirstName ?? "") + " " + (e.LastName ?? ""),
                               CompanyCode = eoi.CompanyCode ?? "",
                               CompanyName = cp.CompanyName ?? "",
                               BranchCode = cb.BranchCode ?? "",
                               BranchName = cb.BranchName ?? "",
                               DesignationCode = dg.DesignationCode ?? "",
                               DesignationName = dg.DesignationName ?? "",
                               DepartmentCode = dp.DepartmentCode ?? "",
                               DepartmentName = dp.DepartmentName ?? "",
                               JoiningDate = eoi.JoiningDate
                           };

            if (filter.CompanyCodes?.Any() == true)
                empQuery = empQuery.Where(x => x.CompanyCode != null && filter.CompanyCodes.Contains(x.CompanyCode));
            if (filter.BranchCodes?.Any() == true)
                empQuery = empQuery.Where(x => x.BranchCode != null && filter.BranchCodes.Contains(x.BranchCode));
            if (filter.DepartmentCodes?.Any() == true)
                empQuery = empQuery.Where(x => x.DepartmentCode != null && filter.DepartmentCodes.Contains(x.DepartmentCode));
            if (filter.DesignationCodes?.Any() == true)
                empQuery = empQuery.Where(x => x.DesignationCode != null && filter.DesignationCodes.Contains(x.DesignationCode));
            if (filter.EmployeeIDs?.Any() == true)
                empQuery = empQuery.Where(x => x.EmpId != null && filter.EmployeeIDs.Contains(x.EmpId));

            var employees = await empQuery.Where(x => x.EmpId != null).Distinct().ToListAsync();

            var leaveTypes = await leaveTypeRepo.All().AsNoTracking()
                .OrderBy(lt => lt.LeaveTypeCode)
                .Select(lt => new LeaveTypeDto2
                {
                    Code = lt.ShortName,
                    Name = lt.Name,
                    GrantedDays = lt.NoOfDay
                }).ToListAsync();

            var empIds = employees.Select(x => x.EmpId).Distinct().ToList();

            // Leave taken is assigned to the cycle that contains the leave's StartDate.
            // TODO: confirm whether HrmLeaveApplicationEntry.LeaveTypeId stores LeaveTypeCode or AutoId.
            var approvedEntries = await entryRepo.All().AsNoTracking()
                .Where(x => empIds.Contains(x.EmployeeId) && x.HrapprovalStatus == ApprovedStatus)
                .Select(x => new { x.EmployeeId, x.LeaveTypeId, x.StartDate, x.NoOfDay })
                .ToListAsync();

            var leaveSummary = new List<LeaveSummaryRowDto>();

            foreach (var emp in employees)
            {
                var joining = emp.JoiningDate ?? asOf;
                var (cycleStart, cycleEnd) = GetCurrentCycle(joining, asOf);

                var availedByType = approvedEntries
                    .Where(x => x.EmployeeId == emp.EmpId && x.StartDate >= cycleStart && x.StartDate < cycleEnd)
                    .GroupBy(x => x.LeaveTypeId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.NoOfDay));

                var balances = leaveTypes.Select(lt =>
                {
                    var availed = availedByType.TryGetValue(lt.Code, out var d) ? d : 0;
                    return new LeaveTypeBalanceDto
                    {
                        LeaveTypeCode = lt.Code,
                        Granted = lt.GrantedDays,
                        Availed = availed,
                        Balance = lt.GrantedDays - availed
                    };
                }).ToList();

                leaveSummary.Add(new LeaveSummaryRowDto
                {
                    EmployeeId = emp.EmpId,
                    EmployeeName = emp.EmpName,
                    CompanyName = emp.CompanyName,
                    BranchName = emp.BranchName,
                    DepartmentName = emp.DepartmentName,
                    DesignationName = emp.DesignationName,
                    JoiningDate = joining.ToString("dd/MM/yyyy"),
                    CycleStart = cycleStart,
                    CycleEnd = cycleEnd,
                    LeaveBalances = balances
                });
            }

            var company = await companyRepo.All().AsNoTracking()
                .Select(c => new LookupDto { Code = c.CompanyCode, Name = c.CompanyName }).ToListAsync();

            var allBranch = await branchRepo.All().AsNoTracking()
                .Where(b => b.BranchCode != null && b.BranchName != null)
                .OrderBy(b => b.BranchCode)
                .Select(b => new LookupDto { Code = b.BranchCode, Name = b.BranchName })
                .Distinct().ToListAsync();

            var result = new LeaveSummaryFilterListViewModel
            {
                Companies = company,
                Branches = allBranch,

                Departments = employees.Where(x => x.DepartmentCode != null && x.DepartmentName != null)
                    .Select(x => new LookupDto { Code = x.DepartmentCode, Name = x.DepartmentName })
                    .Distinct().ToList(),

                Designations = employees.Where(x => x.DesignationCode != null && x.DesignationName != null)
                    .Select(x => new LookupDto { Code = x.DesignationCode, Name = x.DesignationName })
                    .Distinct().ToList(),

                Employees = employees.Where(x => x.EmpId != null && x.EmpName != null)
                    .Select(x => new LookupDto { Code = x.EmpId, Name = x.EmpName })
                    .Distinct().ToList(),

                LeaveTypes = leaveTypes,
                LeaveSummary = leaveSummary
            };

            return result;
        }

        public Task<PagedResultDto<GcItemDto>> GetEmployeesAsync(LeaveSummaryFilterViewModel req)
        {
            // TODO: confirm actual SP name for the generic employee-search dropdown (not OT-specific).
            return ExecAsync("dbo.sp_gc_employee_list_Leave", new
            {
                CompanyCodes = ToCsv(req.CompanyCodes),
                BranchCodes = ToCsv(req.BranchCodes),
                DepartmentCodes = ToCsv(req.DepartmentCodes),
                DesignationCodes = ToCsv(req.DesignationCodes),
                req.Search,
                req.Page,
                PageSize = 10
            });
        }

        private static string? ToCsv(List<string>? list)
            => (list == null || list.Count == 0) ? null : string.Join(",", list);

        private async Task<PagedResultDto<GcItemDto>> ExecAsync(string sp, object param)
        {
            using var con = new SqlConnection(_configuration);
            var rows = (await con.QueryAsync<GcSpRow>(sp, param, commandType: CommandType.StoredProcedure)).ToList();

            return new PagedResultDto<GcItemDto>
            {
                Items = rows.Select(x => new GcItemDto { Code = x.Code, Name = x.Name }).ToList(),
                More = rows.FirstOrDefault()?.More ?? false
            };
        }

        private class GcSpRow
        {
            public string? Code { get; set; }
            public string? Name { get; set; }
            public bool More { get; set; }
        }

        public async Task<bool> PagePermissionAsync(string accessCode)
        {
            // TODO: confirm the exact Title value stored in CoreAccessCode for this page.
            return await accessCodeRepo.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Leave Summary" && x.TitleCheck);
        }




       




        public async Task<byte[]> GenerateExcelReport(List<LeaveSummaryRowDto> data, List<LeaveTypeDto2> leaveTypes)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Leave Summary Report");

            int fixedCols = 6; // SL, EmpId, Name, Designation, Branch, Joining Date  (Department removed)
            int typeCount = leaveTypes.Count;
            int totalCols = fixedCols + (typeCount * 3);

            // ---- Column widths: fixed cols proportionate, type columns kept minimal/narrow ----
            double[] fixedWidthRatios = { 
                3, // SL,
                8, // EmpId, 
                24, // Name, 
                18, // Designation, 
                9, // Branch, 
                8  // JoinDate
            }; 
            const double fixedScale = 1.4;
            const double typeColWidth = 4.5; // narrow fixed width for G-/A-/B- columns

            for (int i = 0; i < fixedCols; i++)
                worksheet.Column(i + 1).Width = fixedWidthRatios[i] * fixedScale;
            for (int i = fixedCols; i < totalCols; i++)
                worksheet.Column(i + 1).Width = typeColWidth;

            var groupedData = data
                .OrderBy(x => x.DepartmentName)
                .ThenBy(x => x.EmployeeId)
                .GroupBy(x => x.DepartmentName ?? "No Department")
                .ToList();

            string companyName = data.Select(c => c.CompanyName).FirstOrDefault() ?? "";
            var grayColor = System.Drawing.Color.LightGray;

            void ApplyThinGrayBorder(ExcelRange range)
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Top.Color.SetColor(grayColor);
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Color.SetColor(grayColor);
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Color.SetColor(grayColor);
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Color.SetColor(grayColor);
            }

            //int row = 1;
            


            int row = 1;

            // Company & Report Title (full width, centered)
            worksheet.Cells[row, 1, row, totalCols].Merge = true;
            worksheet.Cells[row, 1].Value = companyName;
            worksheet.Cells[row, 1].Style.Font.Size = 16;
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            row++;

            worksheet.Cells[row, 1, row, totalCols].Merge = true;
            worksheet.Cells[row, 1].Value = "Leave Summary Report";
            worksheet.Cells[row, 1].Style.Font.Size = 13;
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            row += 2;


            foreach (var deptGroup in groupedData)
            {
                string deptName = deptGroup.Key;

               

                // Company & Report Title (full width, centered)
                //worksheet.Cells[row, 1, row, totalCols].Merge = true;
                //worksheet.Cells[row, 1].Value = companyName;
                //worksheet.Cells[row, 1].Style.Font.Size = 16;
                //worksheet.Cells[row, 1].Style.Font.Bold = true;
                //worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                //row++;

                //worksheet.Cells[row, 1, row, totalCols].Merge = true;
                //worksheet.Cells[row, 1].Value = "Leave Summary Report";
                //worksheet.Cells[row, 1].Style.Font.Size = 13;
                //worksheet.Cells[row, 1].Style.Font.Bold = true;
                //worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                //row += 2;

                // --- Department + Category Headers in one row ---
                worksheet.Cells[row, 1, row, 3].Merge = true;
                worksheet.Cells[row, 1].Value = $"Department: {deptName}";
                worksheet.Cells[row, 1].Style.Font.Size = 12;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;

                // Granted / Availed / Balanced একসাথে একই রোতে বসানো
                if (typeCount > 0)
                {
                    var grantedRange = worksheet.Cells[row, fixedCols + 1, row, fixedCols + typeCount];
                    grantedRange.Merge = true;
                    grantedRange.Value = "Granted Leave";
                    grantedRange.Style.Font.Bold = true;
                    grantedRange.Style.Font.Size = 9;
                    grantedRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ApplyThinGrayBorder(grantedRange);

                    var availedRange = worksheet.Cells[row, fixedCols + typeCount + 1, row, fixedCols + typeCount * 2];
                    availedRange.Merge = true;
                    availedRange.Value = "Availed Leave";
                    availedRange.Style.Font.Bold = true;
                    availedRange.Style.Font.Size = 9;
                    availedRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ApplyThinGrayBorder(availedRange);

                    var balancedRange = worksheet.Cells[row, fixedCols + typeCount * 2 + 1, row, fixedCols + typeCount * 3];
                    balancedRange.Merge = true;
                    balancedRange.Value = "Balanced Leave";
                    balancedRange.Style.Font.Bold = true;
                    balancedRange.Style.Font.Size = 9;
                    balancedRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    ApplyThinGrayBorder(balancedRange);
                }

                row++; // পরের লাইনে Fixed Headers যাবে



                

                // Column Headers — Department removed from this list
                string[] fixedHeaders = { "SL", "Employee ID", "Name", "Designation", "Branch", "Joining Date" };
                for (int i = 0; i < fixedHeaders.Length; i++)
                {
                    var cell = worksheet.Cells[row, i + 1];
                    cell.Value = fixedHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Size = 10;
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.WrapText = true;
                }

                int col = fixedCols + 1;
                foreach (var lt in leaveTypes) { worksheet.Cells[row, col++].Value = lt.Code; }
                foreach (var lt in leaveTypes) { worksheet.Cells[row, col++].Value = lt.Code; }
                foreach (var lt in leaveTypes) { worksheet.Cells[row, col++].Value = lt.Code; }

                var headerRowRange = worksheet.Cells[row, 1, row, totalCols];
                headerRowRange.Style.Font.Bold = true;
                headerRowRange.Style.Font.Size = 9;
                headerRowRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ApplyThinGrayBorder(headerRowRange);
                row++;

                // Data Rows — Department column removed
                int sl = 1;
                foreach (var emp in deptGroup)
                {
                    worksheet.Cells[row, 1].Value = sl++;
                    worksheet.Cells[row, 2].Value = emp.EmployeeId ?? "";
                    worksheet.Cells[row, 3].Value = emp.EmployeeName ?? "";
                    worksheet.Cells[row, 4].Value = emp.DesignationName ?? "";
                    worksheet.Cells[row, 5].Value = emp.BranchName ?? "";
                    worksheet.Cells[row, 6].Value = emp.JoiningDate ?? "";

                    int c = fixedCols + 1;
                    foreach (var lt in leaveTypes)
                        worksheet.Cells[row, c++].Value = emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == lt.Code)?.Granted ?? 0;
                    foreach (var lt in leaveTypes)
                        worksheet.Cells[row, c++].Value = emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == lt.Code)?.Availed ?? 0;
                    foreach (var lt in leaveTypes)
                        worksheet.Cells[row, c++].Value = emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == lt.Code)?.Balance ?? 0;

                    var dataRowRange = worksheet.Cells[row, 1, row, totalCols];
                    dataRowRange.Style.Font.Size = 9;
                    ApplyThinGrayBorder(dataRowRange);

                    worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    worksheet.Cells[row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    for (int cc = fixedCols + 1; cc <= totalCols; cc++)
                        worksheet.Cells[row, cc].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    row++;
                }

                row += 3; // Space between departments
            }
            // After finishing the foreach loop for departments
            // and before returning the package
            worksheet.Cells[worksheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            return package.GetAsByteArray();

            
        }

        public async Task<byte[]> GeneratePdfReport(List<LeaveSummaryRowDto> data, List<LeaveTypeDto2> leaveTypes, BaseViewModel model)
        {
            if (data == null || !data.Any())
                return Array.Empty<byte>();

            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            var pdf = new PdfDocument(writer);
            var document = new PdfDocument2(pdf, iText.Kernel.Geom.PageSize.A3.Rotate());

            var timesRegular = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            var timesBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);

            document.SetMargins(80, 36, 80, 36);

            int typeCount = leaveTypes.Count;
            int fixedColCount = 6;
            int totalCols = fixedColCount + (typeCount * 3);

            //float[] fixedWidths = { 4f, 8f, 18f, 15f, 12f, 8f };
            float[] fixedWidths = { 4f, 11f, 22f, 18f, 15f, 10f };

            float[] columnWidths = new float[totalCols];
            Array.Copy(fixedWidths, columnWidths, fixedColCount);

            float remaining = 100f - fixedWidths.Sum();
            float typeColWidth = typeCount > 0 ? remaining / (typeCount * 3) : 0;
            for (int i = fixedColCount; i < totalCols; i++)
                columnWidths[i] = typeColWidth;

            var groupedData = data
                .OrderBy(x => x.DepartmentName)
                .ThenBy(x => x.EmployeeId)
                .GroupBy(x => x.DepartmentName ?? "No Department")
                .ToList();

            string companyName = data.Select(c => c.CompanyName).FirstOrDefault() ?? "Data Path";

            foreach (var deptGroup in groupedData)
            {
                string deptName = deptGroup.Key;

                // New page for each new department
                //if (pdf.GetNumberOfPages() > 0)
                //    document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

                // Add a small gap between departments instead of a page break
                document.Add(new iText.Layout.Element.Paragraph("\n").SetMarginTop(20));




                // === Table ===
                var table = new iText.Layout.Element.Table(UnitValue.CreatePercentArray(columnWidths));
                table.SetWidth(UnitValue.CreatePercentValue(100));



               

                if (typeCount > 0)
                {
                    table.AddHeaderCell(new Cell(1, 4)
                        .Add(new iText.Layout.Element.Paragraph($"Department: {deptName}").SetFont(timesBold))
                        .SetFontSize(13)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT)
                       .SetBorder(iText.Layout.Borders.Border.NO_BORDER)); // thin border

                    table.AddHeaderCell(new Cell(1, fixedColCount - 4)
                        .Add(new iText.Layout.Element.Paragraph(""))
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER));

                    table.AddHeaderCell(new Cell(1, typeCount)
                        .Add(new iText.Layout.Element.Paragraph("Granted Leave").SetFont(timesBold))
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                        .SetFontSize(11)
                        .SetBorder(new iText.Layout.Borders.SolidBorder(
                            iText.Kernel.Colors.ColorConstants.LIGHT_GRAY, 0.25f))); // thin border

                    table.AddHeaderCell(new Cell(1, typeCount)
                        .Add(new iText.Layout.Element.Paragraph("Availed Leave").SetFont(timesBold))
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                        .SetFontSize(11)
                        .SetBorder(new iText.Layout.Borders.SolidBorder(
                            iText.Kernel.Colors.ColorConstants.LIGHT_GRAY, 0.25f))); // thin border

                    table.AddHeaderCell(new Cell(1, typeCount)
                        .Add(new iText.Layout.Element.Paragraph("Balanced Leave").SetFont(timesBold))
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                        .SetFontSize(11)
                        .SetBorder(new iText.Layout.Borders.SolidBorder(
                            iText.Kernel.Colors.ColorConstants.LIGHT_GRAY, 0.25f))); // thin border
                }



                string[] fixedHeaders = { "SN", "Employee ID", "Name", "Designation", "Branch", "Joining Date" };
                foreach (var h in fixedHeaders)
                {
                    table.AddHeaderCell(new Cell()
                        .Add(new iText.Layout.Element.Paragraph(h).SetFont(timesBold))
                        .SetFontSize(11)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                        .SetBorder(new iText.Layout.Borders.SolidBorder(
                            iText.Kernel.Colors.ColorConstants.LIGHT_GRAY, 0.25f))); // thin border
                }

                for (int b = 0; b < 3; b++)
                {
                    foreach (var lt in leaveTypes)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new iText.Layout.Element.Paragraph(lt.Code).SetFont(timesBold))
                            .SetFontSize(11)
                            .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                            .SetBorder(new iText.Layout.Borders.SolidBorder(
                                iText.Kernel.Colors.ColorConstants.LIGHT_GRAY, 0.25f))); // thin border
                    }
                }


                // Data Rows
                int sl = 1;
                foreach (var emp in deptGroup)
                {
                    var values = new List<string>
            {
                sl++.ToString(), emp.EmployeeId ?? "", emp.EmployeeName ?? "",
                emp.DesignationName ?? "",
                emp.BranchName ?? "", emp.JoiningDate ?? ""
            };

                    values.AddRange(leaveTypes.Select(t => (emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == t.Code)?.Granted ?? 0).ToString("G29")));
                    values.AddRange(leaveTypes.Select(t => (emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == t.Code)?.Availed ?? 0).ToString("G29")));
                    values.AddRange(leaveTypes.Select(t => (emp.LeaveBalances.FirstOrDefault(x => x.LeaveTypeCode == t.Code)?.Balance ?? 0).ToString("G29")));

                    foreach (var text in values)
                    {
                        var cell = new Cell().Add(new iText.Layout.Element.Paragraph(text).SetFont(timesRegular).SetFontSize(10))
                                             .SetBorder(new iText.Layout.Borders.SolidBorder(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY, 0.5f));

                        if (double.TryParse(text, out _))
                            cell.SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);

                        if (values.IndexOf(text) == 5) // ধরুন Joining Date 6th fixed column
                            cell.SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);

                        table.AddCell(cell);
                    }
                }

                document.Add(table);
            }

            // Close the main document
            document.Close();

            // ====================== ADD HEADER / FOOTER (Safe Method) ======================
            byte[] finalBytes;
            using (var ms = new MemoryStream(stream.ToArray()))
            using (var reader = new PdfReader(ms))
            using (var outputMs = new MemoryStream())
            using (var writer2 = new PdfWriter(outputMs))
           // using (var pdf2 = new PdfDocument(reader, writer2))
            {
                //var pdf2 = new PdfDocument(reader, writer2);
                var pdf2 = new PdfDocument(reader, writer2);
                var timesBold2 = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
                int totalPages = pdf2.GetNumberOfPages();

                for (int i = 1; i <= totalPages; i++)
                {
                    var page = pdf2.GetPage(i);
                    var ps = page.GetPageSize();
                    var canvas = new PdfCanvas(page);

                    // Logo
                    try
                    {
                        string logoPath = "wwwroot/images/DP_logo1.png";
                        var imgData = ImageDataFactory.Create(logoPath);
                        var logo = new iText.Layout.Element.Image(imgData);
                        logo.SetFixedPosition(i, 10, ps.GetTop() - 100);
                        logo.ScaleAbsolute(180, 120);
                        new iText.Layout.Canvas(canvas, ps).Add(logo);
                    }
                    catch { }

                    // Header Text
                    float cy = ps.GetTop() - 35;
                    float cx = ps.GetWidth() / 2;

                    // PdfCanvas থেকে Canvas বানান
                    iText.Layout.Canvas canvasWrapper = new iText.Layout.Canvas(canvas, ps);

                    // Company Name center এ
                    canvasWrapper.ShowTextAligned(
                        new iText.Layout.Element.Paragraph(companyName).SetFont(timesBold2).SetFontSize(20),
                        ps.GetWidth() / 2, cy,
                        iText.Layout.Properties.TextAlignment.CENTER);

                    // Leave Summary Report center এ
                    canvasWrapper.ShowTextAligned(
                        new iText.Layout.Element.Paragraph("Leave Summary Report").SetFont(timesBold2).SetFontSize(14),
                        ps.GetWidth() / 2, cy - 18,
                        iText.Layout.Properties.TextAlignment.CENTER);



                    


                    var legend = string.Join(", ",    leaveTypeRepo.All()        .Select(l => l.ShortName + " - " + l.Name)        .ToList()
);

                    var fFont = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                    float fSize = 9f;

                    float fy = ps.GetBottom() + 25;

                    // --- Legend above "Printed By" ---
                    float legendWidth = fFont.GetWidth("Status Legend: " + legend, fSize);
                    canvas.BeginText()
                          .SetFontAndSize(fFont, fSize)
                          .MoveText((ps.GetWidth() - legendWidth) / 2, fy + 12) // 12 units above Printed By
                          .ShowText("Status Legend: " + legend)
                          .EndText();

                    // --- Print Datetime (left) ---
                    string printTime = model.Ldate?.ToString("dd/MM/yyyy hh:mm:ss tt") ?? DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt");
                    canvas.BeginText().SetFontAndSize(fFont, fSize).MoveText(36, fy).ShowText($"Print Datetime: {printTime}").EndText();

                    // --- Printed By (center) ---
                    string printedBy = $"Printed By: {model.Luser ?? "System"}";
                    float uw = fFont.GetWidth(printedBy, fSize);
                    canvas.BeginText().SetFontAndSize(fFont, fSize).MoveText((ps.GetWidth() - uw) / 2, fy).ShowText(printedBy).EndText();

                    // --- Page info (right) ---
                    string pgText = $"Page {i} of {totalPages}";
                    float pw = fFont.GetWidth(pgText, fSize);
                    canvas.BeginText().SetFontAndSize(fFont, fSize).MoveText(ps.GetWidth() - 36 - pw, fy).ShowText(pgText).EndText();

                    canvas.Release();




                }

                pdf2.Close();
                finalBytes = outputMs.ToArray();
            }

            return finalBytes;
        }




    }
}
