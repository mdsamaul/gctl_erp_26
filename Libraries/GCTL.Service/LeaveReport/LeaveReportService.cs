using Dapper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Elements.Table;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace GCTL.Service.LeaveReport
{
    public class LeaveReportService : ILeaveReportService
    {
        private readonly GCTL_ERP_DB_DatapathContext _context;
        private readonly IConfiguration _configuration;
        private readonly IRepository<CoreAccessCode> _accessCodeRepo;
        private readonly IRepository<CoreBranch> _branchRepo;
        private readonly IRepository<HrmDefDepartment> _deptRepo;
        private readonly IRepository<HrmEmployee> _empRepo;
        private readonly IRepository<HrmEmployeeOfficialInfo> _empOfficialRepo;
        private readonly IRepository<HrmLeaveApplicationDays> _leaveDayRepo;

        public LeaveReportService(
            GCTL_ERP_DB_DatapathContext context,
            IConfiguration configuration,
            IRepository<CoreAccessCode> accessCodeRepo,
            IRepository<CoreBranch> branchRepo,
            IRepository<HrmDefDepartment> deptRepo,
            IRepository<HrmEmployee> empRepo,
            IRepository<HrmEmployeeOfficialInfo> empOfficialRepo,
            IRepository<HrmLeaveApplicationDays> leaveDayRepo)
        {
            _context = context;
            _configuration = configuration;
            _accessCodeRepo = accessCodeRepo;
            _branchRepo = branchRepo;
            _deptRepo = deptRepo;
            _empRepo = empRepo;
            _empOfficialRepo = empOfficialRepo;
            _leaveDayRepo = leaveDayRepo;
        }

        // ─── PERMISSIONS ─────────────────────────────────────────────────────────

        public Task<bool> HasPagePermissionAsync(string accessCode, string title) =>
            _accessCodeRepo.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == title && x.TitleCheck);

        public Task<bool> HasPrintPermissionAsync(string accessCode, string title) =>
            _accessCodeRepo.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == title && x.CheckPrint);

        // ─── GET REPORT (EF Core — optimized) ────────────────────────────────────
        // Fix: filter in DB, fetch only matching leave days (not all rows)

        public async Task<List<LeaveApplicartionGridVM>> GetReportAsync(LeaveReportViewModel model)
        {
            // Build base query with all filters applied at DB level
            var query = from l in _context.HrmLeaveApplicationEntry
                        join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId into ej
                        from e in ej.DefaultIfEmpty()
                        join hod in _context.HrmEmployee on l.Hod equals hod.EmployeeId into hodj
                        from hod in hodj.DefaultIfEmpty()
                        join type in _context.HrmAtdLeaveType on l.LeaveTypeId equals type.LeaveTypeCode into tj
                        from type in tj.DefaultIfEmpty()
                        join sup in _context.HrmEmployee on l.BossEmpAutoId equals sup.EmployeeId into sj
                        from sup in sj.DefaultIfEmpty()
                        join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId into oej
                        from oe in oej.DefaultIfEmpty()
                        join dep in _context.HrmDefDepartment on oe.DepartmentCode equals dep.DepartmentCode into dj
                        from dep in dj.DefaultIfEmpty()
                        join desig in _context.HrmDefDesignation on oe.DesignationCode equals desig.DesignationCode into dgj
                        from desig in dgj.DefaultIfEmpty()
                        where l.HrapprovalStatus == "Approved"
                        select new
                        {
                            l,
                            e,
                            hod,
                            type,
                            sup,
                            DepartmentName = dep.DepartmentName,
                            DesignationName = desig.DesignationName,
                            BranchCode = oe.BranchCode,
                            DepartmentCode = oe.DepartmentCode
                        };

            // All filters applied at DB level — no load-all-then-filter
            if (!string.IsNullOrEmpty(model.Company))
                query = query.Where(x => x.l.CompanyCode == model.Company);

            if (!string.IsNullOrEmpty(model.Branch))
                query = query.Where(x => x.BranchCode == model.Branch);

            if (!string.IsNullOrEmpty(model.Department))
                query = query.Where(x => x.DepartmentCode == model.Department);

            if (!string.IsNullOrEmpty(model.Employee))
                query = query.Where(x => x.l.EmployeeId == model.Employee);

            if (model.DateFrom != DateTime.MinValue)
                query = query.Where(x => x.l.StartDate >= model.DateFrom);

            if (model.DateTo != DateTime.MinValue)
                query = query.Where(x => x.l.EndDate <= model.DateTo);

            if (!string.IsNullOrEmpty(model.LeaveFormat))
                query = query.Where(x => x.l.ApplyLeaveFormat == model.LeaveFormat);

            if (!string.IsNullOrEmpty(model.LeaveStatus))
                query = query.Where(x => x.l.HrapprovalStatus == model.LeaveStatus);

            var leaveEntries = await query.ToListAsync();

            if (!leaveEntries.Any())
                return new List<LeaveApplicartionGridVM>();

            // Fetch only leave days for matching entries — not the entire table
            var entryIds = leaveEntries.Select(x => x.l.LeaveAppEntryId).Distinct().ToList();

            var groupedLeaveDays = await _context.HrmLeaveApplicationDays
                .Where(d => entryIds.Contains(d.LeaveAppEntryId))
                .GroupBy(d => d.LeaveAppEntryId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(d => d.Days).ToList());

            return leaveEntries.Select(data => new LeaveApplicartionGridVM
            {
                EmployeeID = data.e?.EmployeeId,
                EmployeeFirstName = (data.e?.FirstName ?? "") + " " + (data.e?.LastName ?? ""),
                DepartmentName = data.DepartmentName,
                DesignationName = data.DesignationName,
                LeaveAppEntryCode = data.l.AutoId,
                LeaveAppEntryId = data.l.LeaveAppEntryId,
                LeaveTypeId = data.type?.ShortName,
                StartDate = data.l.StartDate,
                EndDate = data.l.EndDate,
                NoOfDay = data.l.NoOfDay,
                ModifyDate = data.l.ModifyDate,
                ConfirmationRemarks = data.l.ConfirmationRemarks,
                HODApprovalStatus = data.l.HodapprovalStatus,
                HRApprovalStatus = data.l.HrapprovalStatus,
                HRApprovalRemarks = data.l.HrapprovalRemarks,
                SickLeaveFilePath = data.l.SickLeaveFilePath,
                ApplyLeaveFormat = data.l.ApplyLeaveFormat,
                HODFirstName = (data.hod?.FirstName ?? "") + " " + (data.hod?.LastName ?? ""),
                SupervisorFirstName = (data.sup?.FirstName ?? "") + " " + (data.sup?.LastName ?? ""),
                Reason = data.l.Reason,
                ShortLeaveFrom = data.l.ShortLeaveFrom,
                ShortLeaveTo = data.l.ShortLeaveTo,
                ShortLeaveTime = data.l.ShortLeaveTime,
                IsApproved = data.l.IsApproved,
                FirstOrSecondHalf = data.l.FirstOrSecondHalf == "1" ? "First Half"
                                    : data.l.FirstOrSecondHalf == "2" ? "Second Half" : null,
                Days = groupedLeaveDays.TryGetValue(data.l.LeaveAppEntryId, out var days)
                       ? days : new List<DateTime>()
            }).ToList();
        }


        //------------------------------####


        public async Task<List<LeaveApplicartionGridVM>> GetLeaveReportArrayAsync(LeaveReportArrayViewModel model)
        {
            var company = model.Company?.ToList();
            var branch = model.Branch?.ToList();
            var department = model.Department?.ToList();
            var employee = model.Employee?.ToList();
            var leaveFormat = model.LeaveFormat?.ToList();
            var leaveStatus = model.LeaveStatus?.ToList();
            DateTime? dateFrom = model.DateFrom == default ? null : model.DateFrom;
            DateTime? dateTo = model.DateTo == default ? null : model.DateTo;

            bool hasCompany = company != null && company.Count > 0;
            bool hasBranch = branch != null && branch.Count > 0;
            bool hasDepartment = department != null && department.Count > 0;
            bool hasEmployee = employee != null && employee.Count > 0;
            bool hasLeaveFormat = leaveFormat != null && leaveFormat.Count > 0;
            bool hasLeaveStatus = leaveStatus != null && leaveStatus.Count > 0;

            var baseQuery =
                from l in _context.HrmLeaveApplicationEntry.AsNoTracking()
                join e in _context.HrmEmployee on l.EmployeeId equals e.EmployeeId
                join oe in _context.HrmEmployeeOfficialInfo on l.EmployeeId equals oe.EmployeeId
                join hod0 in _context.HrmEmployee on l.Hod equals hod0.EmployeeId into hodJoin
                from hod in hodJoin.DefaultIfEmpty()
                join sup0 in _context.HrmEmployee on l.BossEmpAutoId equals sup0.EmployeeId into supJoin
                from sup in supJoin.DefaultIfEmpty()
                join type0 in _context.HrmAtdLeaveType on l.LeaveTypeId equals type0.LeaveTypeCode into typeJoin
                from type in typeJoin.DefaultIfEmpty()
                join dep0 in _context.HrmDefDepartment on oe.DepartmentCode equals dep0.DepartmentCode into depJoin
                from dep in depJoin.DefaultIfEmpty()
                join desig0 in _context.HrmDefDesignation on oe.DesignationCode equals desig0.DesignationCode into desigJoin
                from desig in desigJoin.DefaultIfEmpty()
                join comp0 in _context.CoreCompany on l.CompanyCode equals comp0.CompanyCode into compJoin
                from comp in compJoin.DefaultIfEmpty()
                where
                    (!hasLeaveStatus || leaveStatus.Contains(l.HrapprovalStatus)) &&
                    (dateFrom == null || l.StartDate >= dateFrom) &&
                    (dateTo == null || l.EndDate <= dateTo) &&
                    (!hasCompany || company.Contains(l.CompanyCode)) &&
                    (!hasBranch || branch.Contains(oe.BranchCode)) &&
                    (!hasDepartment || department.Contains(oe.DepartmentCode)) &&
                    (!hasEmployee || employee.Contains(l.EmployeeId)) &&
                    (!hasLeaveFormat || leaveFormat.Contains(l.ApplyLeaveFormat))
                select new { l, e, hod, sup, type, dep, desig, comp };

            var grouped = await baseQuery
                .GroupBy(x => new
                {
                    x.l.AutoId,
                    x.l.LeaveAppEntryId,
                    x.e.EmployeeId,
                    x.e.FirstName,
                    x.e.LastName,
                    DesignationName = x.desig.DesignationName,
                    LeaveTypeShortName = x.type.ShortName,
                    x.l.StartDate,
                    x.l.EndDate,
                    x.l.NoOfDay,
                    x.l.ModifyDate,
                    x.l.ConfirmationRemarks,
                    x.l.HodapprovalStatus,
                    x.l.HrapprovalRemarks,
                    x.l.SickLeaveFilePath,
                    x.l.HrapprovalStatus,
                    x.l.ApplyLeaveFormat,
                    HODFirstName = x.hod.FirstName + " " + x.hod.LastName,
                    SupervisorFirstName = x.sup.FirstName + " " + x.sup.LastName,
                    x.l.Reason,
                    DepartmentName = x.dep.DepartmentName,
                    x.l.ShortLeaveFrom,
                    x.l.ShortLeaveTo,
                    x.l.ShortLeaveTime,
                    x.l.IsApproved,
                    x.l.FirstOrSecondHalf
                })
                .Select(g => new
                {
                    g.Key,
                    CountTotal = g.Count()
                })
                .OrderBy(r => r.Key.DepartmentName)
                .ToListAsync();

            // Final shaping into the grid VM (in-memory, so string formatting/ternaries are safe here)
            var result = grouped.Select(r => new LeaveApplicartionGridVM
            {
                EmployeeID = r.Key.EmployeeId,
                LeaveAppEntryCode = r.Key.AutoId,
                LeaveAppEntryId = r.Key.LeaveAppEntryId,
                LeaveTypeId = r.Key.LeaveTypeShortName,
                StartDate = r.Key.StartDate,
                EndDate = r.Key.EndDate,
                NoOfDay = r.Key.NoOfDay,
                ModifyDate = r.Key.ModifyDate,
                ConfirmationRemarks = r.Key.ConfirmationRemarks,
                HODApprovalStatus = r.Key.HodapprovalStatus,
                HODApprovalRemarks = r.Key.HrapprovalRemarks, // SP had no separate HOD remarks column; reusing as in original
                SickLeaveFilePath = r.Key.SickLeaveFilePath,
                HRApprovalStatus = r.Key.HrapprovalStatus,
                HRApprovalRemarks = r.Key.HrapprovalRemarks,
                ApplyLeaveFormat = r.Key.ApplyLeaveFormat,
                HODFirstName = r.Key.HODFirstName,
                SupervisorFirstName = r.Key.SupervisorFirstName,
                EmployeeFirstName = r.Key.FirstName + " " + r.Key.LastName,
                Reason = r.Key.Reason,
                DepartmentName = r.Key.DepartmentName,
                DesignationName = r.Key.DesignationName,
                ShortLeaveFrom = r.Key.ShortLeaveFrom,
                ShortLeaveTo = r.Key.ShortLeaveTo,
                ShortLeaveTime = r.Key.ShortLeaveTime,
                IsApproved = r.Key.IsApproved,
                FirstOrSecondHalf =
                    r.Key.FirstOrSecondHalf == "1" ? "First Half" :
                    r.Key.FirstOrSecondHalf == "2" ? "Second Half" :
                    null,
                ShortLeaveFromStr = r.Key.ShortLeaveFrom?.ToString("dd/MM/yyyy"),
                ShortLeaveToStr = r.Key.ShortLeaveTo?.ToString("dd/MM/yyyy"),
                ShortLeaveTimeStr = r.Key.ShortLeaveTime?.ToString("HH:mm"),
                CountTotal = r.CountTotal,
                Days = null,    // not derivable from this aggregation; populate separately if needed
                DaysStr = null
            }).ToList();

            return result;
        }


        // ─── GET REPORT ARRAY (Stored Proc — safe parameterization) ──────────────
        // ─── GET REPORT ARRAY ────────────────────────────────────────────────────────

        public async Task<Dictionary<string, CompanyLeaveDataVM>> GetReportArrayAsync(LeaveReportArrayViewModel model)
        {
            var connStr = _configuration.GetConnectionString("ApplicationDbConnection");
            await using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();

            var parameters = new DynamicParameters();
            parameters.Add("@DateFrom", model.DateFrom, DbType.Date);
            parameters.Add("@DateTo", model.DateTo, DbType.Date);
            parameters.Add("@Company", model.Company != null ? string.Join(", ", model.Company) : (object)DBNull.Value, DbType.String);
            parameters.Add("@Branch", model.Branch != null ? string.Join(", ", model.Branch) : (object)DBNull.Value, DbType.String);
            parameters.Add("@Department", model.Department != null ? string.Join(", ", model.Department) : (object)DBNull.Value, DbType.String);
            parameters.Add("@Employee", model.Employee != null ? string.Join(", ", model.Employee) : (object)DBNull.Value, DbType.String);
            parameters.Add("@LeaveFormat", model.LeaveFormat != null ? string.Join(", ", model.LeaveFormat) : (object)DBNull.Value, DbType.String);
            parameters.Add("@LeaveStatus", model.LeaveStatus != null ? string.Join(", ", model.LeaveStatus) : (object)DBNull.Value, DbType.String);

            string query = BuildQueryString(parameters);
            var results = (await connection.QueryAsync<LeaveDetailVM>(query, parameters)).ToList();

            if (!results.Any())
                return new Dictionary<string, CompanyLeaveDataVM>();

            // Fetch leave days only for returned entries (single query, not N+1)
            var entryIds = results
                .Where(r => (double)r.NoOfDay > 0.5)
                .Select(r => r.LeaveAppEntryId)
                .Distinct()
                .ToList();

            Dictionary<string, List<string>> leaveDaysMap = new();

            if (entryIds.Any())
            {
                leaveDaysMap = _leaveDayRepo
                    .FindBy(d => entryIds.Contains(d.LeaveAppEntryId))
                    .ToList()
                    .GroupBy(d => d.LeaveAppEntryId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(d => d.Days.ToString("dd/MM/yyyy")).ToList());
            }

            // Group into Company → Department hierarchy
            var grouped = new Dictionary<string, CompanyLeaveDataVM>();

            foreach (var item in results)
            {
                if ((double)item.NoOfDay > 0.5)
                    item.DaysStr = leaveDaysMap.TryGetValue(item.LeaveAppEntryId, out var d) ? d : new List<string>();

                var compKey = string.IsNullOrWhiteSpace(item.CompanyName) ? "Unknown Company" : item.CompanyName;
                if (!grouped.ContainsKey(compKey))
                    grouped[compKey] = new CompanyLeaveDataVM { CompanyCode = compKey, DepartmentData = new() };

                var deptKey = string.IsNullOrWhiteSpace(item.DepartmentName) ? "Unknown Department" : item.DepartmentName;
                if (!grouped[compKey].DepartmentData.ContainsKey(deptKey))
                    grouped[compKey].DepartmentData[deptKey] = new DepartmentLeaveDataVM { DepartmentName = deptKey, LeaveDetails = new() };

                grouped[compKey].DepartmentData[deptKey].LeaveDetails.Add(item);
            }

            return grouped;
        }

        // ─── DROPDOWNS ────────────────────────────────────────────────────────────
        // Cleaned up null-list guards, consolidated into helper

        public async Task<List<CoreBranch>> GetBranchesByMultiCompAsync(List<string> companyCodes, bool isAll)
        {
            if (isAll)
                return (await _branchRepo.AllAsync()).ToList();

            return (await _branchRepo.FindByAsync(e => companyCodes.Contains(e.CompanyCode))).ToList();
        }

        public async Task<List<HrmDefDepartment>> GetDeptByMultiCompBranchAsync(
            List<string> companyCodes, List<string> branchCodes, bool isAll)
        {
            if (isAll)
                return (await _deptRepo.AllAsync()).ToList();

            companyCodes = Sanitize(companyCodes);
            branchCodes = Sanitize(branchCodes);

            int c = companyCodes.Count, b = branchCodes.Count;

            var query = from off in _empOfficialRepo.All().AsNoTracking()
                        join dep in _deptRepo.All().AsNoTracking()
                            on off.DepartmentCode equals dep.DepartmentCode into dj
                        from dep in dj.DefaultIfEmpty()
                        select new { off, dep };

            if (c > 0 && b > 0) query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode));
            else if (c > 0) query = query.Where(q => companyCodes.Contains(q.off.CompanyCode));
            else if (b > 0) query = query.Where(q => branchCodes.Contains(q.off.BranchCode));

            return await query.Select(q => new HrmDefDepartment
            {
                DepartmentCode = q.dep.DepartmentCode,
                DepartmentName = q.dep.DepartmentName,
                DepartmentShortName = q.dep.DepartmentShortName
            }).Distinct().ToListAsync();
        }

        public async Task<List<EmpInfoViewModel>> GetEmpByMultiCompBranchDeptAsync(
            List<string> companyCodes, List<string> branchCodes, List<string> departmentCodes, bool isAll)
        {
            if (isAll)
            {
                return await (from e in _empRepo.All().AsNoTracking()
                              select new EmpInfoViewModel
                              {
                                  EmployeeId = e.EmployeeId,
                                  EmployeeFirstName = e.FirstName + " " + e.LastName
                              }).ToListAsync();
            }

            companyCodes = Sanitize(companyCodes);
            branchCodes = Sanitize(branchCodes);
            departmentCodes = Sanitize(departmentCodes);

            int c = companyCodes.Count, b = branchCodes.Count, d = departmentCodes.Count;

            var query = from off in _empOfficialRepo.All().AsNoTracking()
                        join emp in _empRepo.All().AsNoTracking()
                            on off.EmployeeId equals emp.EmployeeId into ej
                        from emp in ej.DefaultIfEmpty()
                        select new { off, emp };

            if (c > 0 && b > 0 && d > 0) query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode) && departmentCodes.Contains(q.off.DepartmentCode));
            else if (c > 0 && b > 0) query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && branchCodes.Contains(q.off.BranchCode));
            else if (c > 0 && d > 0) query = query.Where(q => companyCodes.Contains(q.off.CompanyCode) && departmentCodes.Contains(q.off.DepartmentCode));
            else if (b > 0 && d > 0) query = query.Where(q => branchCodes.Contains(q.off.BranchCode) && departmentCodes.Contains(q.off.DepartmentCode));
            else if (c > 0) query = query.Where(q => companyCodes.Contains(q.off.CompanyCode));
            else if (b > 0) query = query.Where(q => branchCodes.Contains(q.off.BranchCode));
            else if (d > 0) query = query.Where(q => departmentCodes.Contains(q.off.DepartmentCode));

            return await query.Select(q => new EmpInfoViewModel
            {
                EmployeeId = q.emp.EmployeeId,
                EmployeeFirstName = q.emp.FirstName + " " + q.emp.LastName
            }).ToListAsync();
        }

        // ─── PDF ─────────────────────────────────────────────────────────────────
        // Moved from HrmLeaveApplicationEntryService unchanged — generation logic same

        public byte[] GeneratePdfReportArray(Dictionary<string, CompanyLeaveDataVM> data, string sdate, string edate)
        {
            if (data == null || !data.Any())
                throw new ArgumentException("No data to generate PDF.");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    //page.Size(432, 279, Unit.Millimetre);
                    page.Size(632, 279, Unit.Millimetre);
                    page.Content().Element(content =>
                    {
                        content.Column(column =>
                        {
                            foreach (var company in data)
                            {
                                column.Item().Element(c => ComposeHeader(c, company.Key, sdate, edate));

                                foreach (var deptEntry in company.Value.DepartmentData)
                                {
                                    column.Item().PaddingTop(10).BorderBottom(1).Padding(5)
                                        .Text($"Department: {deptEntry.Key}").FontSize(14).Bold();

                                    column.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(cols => DefineColumns(cols));
                                        table.Header(h => AddTableHeader(h));

                                        

                                        foreach (var app in ConvertLeaveDetails(deptEntry.Value.LeaveDetails))
                                            AddTableRow(table, app);
                                    });

                                    bool isLastDept = deptEntry.Key == company.Value.DepartmentData.Keys.Last();
                                    bool isLastComp = company.Key == data.Keys.Last();
                                    //if (!(isLastComp && isLastDept))
                                    //    column.Item().PageBreak();
                                }
                                if (company.Key != data.Keys.Last())
                                    column.Item().PageBreak();
                            }
                        });
                    });
                    page.Footer().Element(ComposeFooter);
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }

        public byte[] GeneratePdfReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    //page.Size(432, 279, Unit.Millimetre);
                    page.Size(632, 279, Unit.Millimetre);
                    page.Header().Element(c => ComposeHeader(c, companyName, sDate, eDate));
                    page.Content().Element(c => ComposeContent(c, data));
                    page.Footer().Element(ComposeFooter);
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }


        public byte[] GenerateExcelReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Leave Report");

            SetColumnWidths(ws);
            int row = ComposeExcelHeader(ws, companyName, sDate, eDate, 1);

            foreach (var dept in data)
                row = ComposeDeptData(ws, dept, row);

            ComposeExcelFooter(ws, row + 1);
            ApplyGeneralStyling(ws, row);

            return package.GetAsByteArray();
        }


        #region ─── EXCEL ────────────────────────────────────────────────────────────────

        public byte[] GenerateExcelReportArray(Dictionary<string, CompanyLeaveDataVM> data, string sdate, string edate)
        {
            if (data == null || !data.Any()) throw new ArgumentException("No data.");
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();

            foreach (var company in data)
            {
                string wsName = SanitizeSheetName($"{company.Key}_LeaveReport");
                var ws = package.Workbook.Worksheets.Add(wsName);

                var headers = GetReportHeaders();
                WriteCompanyHeader(ws, company.Key, sdate, edate, headers.Length);

                SetFixedColumnWidths(ws);

                int row = 3;

                foreach (var deptEntry in company.Value.DepartmentData)
                {
                    WriteDeptHeader(ws, deptEntry.Key, ref row, headers.Length);
                    row++;
                    WriteColumnHeaders(ws, headers, row);
                    int headerRow = row;   // ✅ now scoped per department
                    row++;

                    foreach (var app in ConvertLeaveDetails(deptEntry.Value.LeaveDetails))
                    {
                        WriteDataRow(ws, app, row);
                        row++;
                    }

                    int lastRow = row - 1;
                    int[] centerCols = { 1, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,  17, 19, 21, 22 };

                    foreach (int col in centerCols)
                    {
                        using (var range = ws.Cells[headerRow, col, lastRow, col])
                        {
                            range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                        }
                    }
                }

                WriteFooter(ws, row);
                ApplyPrintSettings(ws);

                //ws.Cells[ws.Dimension.Address].AutoFitColumns(5, 20);
            }

            return package.GetAsByteArray();
        }


        // ─── EXCEL HELPERS ────────────────────────────────────────────────────────

        private string[] GetReportHeaders() => new[]
        {
            "Employee ID","Name","Designation","Leave ID","Leave Format","Approval Status",
            "Leave Type","From Date","To","Date(s)","No. of Day(s)","1st/2nd Half",
            "From","To","Time","Reason","IS App. Status","IS App. Remarks",
            "HOD App. Status","HOD App. Remarks","HR App. Status","HR App. Remarks"
        };

        private void SetFixedColumnWidths(ExcelWorksheet ws)
        {
            // 22 columns matching GetReportHeaders()
            double[] widths = {
                        12, // Employee ID
                        35, // Name
                        24, // Designation
                        7,  // Leave ID
                        12, // Leave Format
                        10, // Approval Status
                        6, // Leave Type
                        12, // From Date
                        12, // To Date
                        12, // Date(s)
                        7, // No. of Day(s)
                        9,  // 1st/2nd Half
                        6, // From
                        6, // To
                        6, // Time
                        24, // Reason
                        10, // IS App. Status
                        14, // IS App. Remarks
                        10, // HOD App. Status
                        14, // HOD App. Remarks
                        10, // HR App. Status
                        14  // HR App. Remarks
                    };

            for (int i = 0; i < widths.Length; i++)
                ws.Column(i + 1).Width = widths[i];
        }


        private void WriteCompanyHeader(ExcelWorksheet ws, string companyCode, string sdate, string edate, int colCount)
        {
            ws.Cells[1, 1, 1, colCount].Merge = true;
            ws.Cells[1, 1].Value = $"Company: {companyCode}";
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1, 2, colCount].Merge = true;
            ws.Cells[2, 1].Value = $"Period: {sdate} to {edate}";
            ws.Cells[2, 1].Style.Font.Bold = true;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        

        private void WriteDeptHeader(ExcelWorksheet ws, string deptName, ref int row, int colCount)
        {
            // Add gap row
            row++;
            //ws.Row(row).Height = 5;

           
            var cell = ws.Cells[row, 1, row, colCount];
            cell.Merge = true;
            cell.Value = $"Department: {deptName}";
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.White);

            // ✅ Force left alignment in merged cell
            cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

            // ✅ Add indent so text doesn’t stick to the edge
            cell.Style.Indent = 1;
        }



        private void WriteColumnHeaders(ExcelWorksheet ws, string[] headers, int row)
        {
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[row, i + 1].Value = headers[i];

            var range = ws.Cells[row, 1, row, headers.Length];
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.WrapText = true;
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.Row(row).Height = 30; // room for 2-line wrapped headers
        }




        //private void WriteColumnHeaders(ExcelWorksheet ws, string[] headers, int row)
        //{
        //    for (int i = 0; i < headers.Length; i++)
        //        ws.Cells[row, i + 1].Value = headers[i];

        //    var range = ws.Cells[row, 1, row, headers.Length];
        //    range.Style.Font.Bold = true;
        //    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        //    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        //    range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        //    range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        //}

        private void WriteDataRow(ExcelWorksheet ws, LeaveApplicartionGridVM app, int row)
        {
            string startStr = app.StartDate != DateTime.MinValue ? app.StartDate.ToString("dd/MM/yyyy") : "";
            string endStr = app.EndDate != DateTime.MinValue ? app.EndDate.ToString("dd/MM/yyyy") : "";
            string datesStr = app.Days?.Any() == true ? string.Join(", ", app.Days.Select(d => d.ToString("dd/MM/yyyy"))) : "";
            string from = "", to = "", time = "";
            if (app.ApplyLeaveFormat == "shortLeave") { from = app.ShortLeaveFromStr; to = app.ShortLeaveToStr; time = app.ShortLeaveTimeStr; }

            object[] values = {
                app.EmployeeID, app.EmployeeFirstName, app.DesignationName, app.LeaveAppEntryId,
                app.ApplyLeaveFormat, app.HODApprovalStatus, app.LeaveTypeId,
                startStr, endStr, datesStr, app.NoOfDay.ToString(), app.FirstOrSecondHalf,
                from, to, time, app.Reason, app.IsApproved, app.ConfirmationRemarks,
                app.HODApprovalStatus, app.HODApprovalRemarks, app.HRApprovalStatus, app.HRApprovalRemarks
            };

            for (int i = 0; i < values.Length; i++)
                ws.Cells[row, i + 1].Value = values[i] ?? "";

            var dataRange = ws.Cells[row, 1, row, values.Length];
            dataRange.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            dataRange.Style.WrapText = true;
            dataRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        private void WriteFooter(ExcelWorksheet ws, int row)
        {
            ws.Cells[row, 1].Value = "Report Generated On: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            ws.Cells[row, 1, row, 22].Merge = true;
            ws.Cells[row, 1].Style.Font.Italic = true;
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        private void ApplyPrintSettings(ExcelWorksheet ws)
        {
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
        }

        // Existing single-sheet Excel helpers (GetReport flow) kept as-is
        private void SetColumnWidths(ExcelWorksheet ws)
        {
            int[] widths = { 12, 15, 15, 5, 12, 12, 10, 12, 12, 10, 10, 10, 10, 10, 10, 20, 12, 15, 15, 15, 12, 15 };
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];
        }

        private int ComposeExcelHeader(ExcelWorksheet ws, string companyName, string sDate, string eDate, int startRow)
        {
            ws.Cells[startRow, 1, startRow, 22].Merge = true;
            ws.Cells[startRow, 1].Value = companyName;
            ws.Cells[startRow, 1].Style.Font.Size = 20; ws.Cells[startRow, 1].Style.Font.Bold = true;
            ws.Cells[startRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[startRow + 1, 1, startRow + 1, 22].Merge = true;
            ws.Cells[startRow + 1, 1].Value = "Leave Application Report";
            ws.Cells[startRow + 1, 1].Style.Font.Bold = true; ws.Cells[startRow + 1, 1].Style.Font.UnderLine = true;
            ws.Cells[startRow + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[startRow + 2, 1, startRow + 2, 22].Merge = true;
            ws.Cells[startRow + 2, 1].Value = $"Date: {sDate} - {eDate}";
            ws.Cells[startRow + 2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            return startRow + 4;
        }

        private int ComposeDeptData(ExcelWorksheet ws, LeaveReportGridVM dept, int startRow)
        {
            ws.Cells[startRow, 1, startRow + 1, 22].Merge = true;
            ws.Cells[startRow, 1].Value = $"Department Name : {dept.Department}";
            ws.Cells[startRow, 1].Style.Font.Bold = true;
            ws.Cells[startRow, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[startRow, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 240, 240));
            startRow += 2;
            startRow = ComposeTableHeaders(ws, startRow);

            foreach (var item in dept.Data)
            {
                WriteDataRow(ws, item, startRow);
                ws.Row(startRow).Height = 30;
                startRow++;
            }

            return startRow;
        }

        private int ComposeTableHeaders(ExcelWorksheet ws, int startRow)
        {
            string[] fixed12 = { "Emp. ID", "Name", "Designation", "Leave ID", "Leave Format", "Approval Status", "Leave Type", "From Date", "To Date", "Date(s)", "No. of Day(s)", "1st/2nd Half" };
            for (int i = 0; i < fixed12.Length; i++)
            {
                ws.Cells[startRow, i + 1, startRow + 1, i + 1].Merge = true;
                ws.Cells[startRow, i + 1].Value = fixed12[i];
                ws.Cells[startRow, i + 1].Style.WrapText = true;
            }

            ws.Cells[startRow, 13, startRow, 15].Merge = true;
            ws.Cells[startRow, 13].Value = "Short Leave";
            ws.Cells[startRow + 1, 13].Value = "From";
            ws.Cells[startRow + 1, 14].Value = "To";
            ws.Cells[startRow + 1, 15].Value = "Time";

            string[] rest = { "Reason", "IS App. Status", "IS App. Remarks", "HOD App. Status", "HOD App. Remarks", "HR App. Status", "HR App. Remarks" };
            for (int i = 0; i < rest.Length; i++)
            {
                ws.Cells[startRow, i + 16, startRow + 1, i + 16].Merge = true;
                ws.Cells[startRow, i + 16].Value = rest[i];
                ws.Cells[startRow, i + 16].Style.WrapText = true;
            }

            var hRange = ws.Cells[startRow, 1, startRow + 1, 22];
            hRange.Style.Font.Bold = true;
            hRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            hRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            hRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            hRange.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            hRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            hRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            hRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            hRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            ws.Row(startRow).Height = 30;
            ws.Row(startRow + 1).Height = 30;

            return startRow + 2;
        }

        private void ComposeExcelFooter(ExcelWorksheet ws, int row)
        {
            ws.Cells[row, 1].Value = $"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cells[row, 21, row, 22].Merge = true;
            ws.Cells[row, 21].Value = "Page 1 of 1";
            ws.Cells[row, 21].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        }

        private void ApplyGeneralStyling(ExcelWorksheet ws, int lastRow)
        {
            var range = ws.Cells[1, 1, lastRow, 22];
            range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Right.Style = ExcelBorderStyle.Thin;

            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.Orientation = eOrientation.Landscape;
        }

        #endregion

        // ─── WORD ─────────────────────────────────────────────────────────────────

        public byte[] GenerateWordReport(List<LeaveReportGridVM> data, string companyName, string sDate, string eDate)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Word.Document();
                var body = mainPart.Document.AppendChild(new Word.Body());

                AddWordHeader(body, companyName, sDate, eDate);

                foreach (var dept in data)
                {
                    body.AppendChild(new Word.Paragraph(new Word.Run(new Word.Text($"Department: {dept.Department}")))
                    {
                        ParagraphProperties = new Word.ParagraphProperties(new Word.SpacingBetweenLines { Before = "400" })
                    });

                    var table = new Word.Table();
                    table.AppendChild(new Word.TableProperties(new Word.TableBorders(
                        new Word.TopBorder { Val = Word.BorderValues.Single },
                        new Word.BottomBorder { Val = Word.BorderValues.Single },
                        new Word.LeftBorder { Val = Word.BorderValues.Single },
                        new Word.RightBorder { Val = Word.BorderValues.Single },
                        new Word.InsideHorizontalBorder { Val = Word.BorderValues.Single },
                        new Word.InsideVerticalBorder { Val = Word.BorderValues.Single })));

                    AddWordTableHeader(table);
                    foreach (var app in dept.Data) AddWordTableRow(table, app);

                    body.AppendChild(table);
                    body.AppendChild(new Word.Paragraph(new Word.Run()));
                }

                AddWordFooter(body);
            }

            return ms.ToArray();
        }

        // ─── PRIVATE HELPERS ──────────────────────────────────────────────────────

        private static List<string> Sanitize(List<string> list) =>
            list?.Where(x => !string.IsNullOrEmpty(x)).ToList() ?? new List<string>();

        private static string SanitizeSheetName(string name) =>
            name.Replace("/", "_").Substring(0, Math.Min(31, name.Length));

        private List<LeaveApplicartionGridVM> ConvertLeaveDetails(List<LeaveDetailVM> details)
        {
            var result = new List<LeaveApplicartionGridVM>();

            foreach (var leave in details)
            {
                string fromStr = "", toStr = "", timeStr = "";
                if (leave.ApplyLeaveFormat == "shortLeave" && leave.ShortLeaveFrom.HasValue && leave.ShortLeaveTo.HasValue)
                {
                    fromStr = leave.ShortLeaveFrom.Value.ToString("HH:mm");
                    toStr = leave.ShortLeaveTo.Value.ToString("HH:mm");
                    var diff = leave.ShortLeaveTo.Value.TimeOfDay - leave.ShortLeaveFrom.Value.TimeOfDay;
                    timeStr = diff.ToString(@"hh\:mm");
                }

                var app = new LeaveApplicartionGridVM
                {
                    EmployeeID = leave.EmployeeId,
                    EmployeeFirstName = leave.EmployeeFirstName,
                    DesignationName = leave.DesignationName,
                    LeaveAppEntryId = leave.LeaveAppEntryId,
                    ApplyLeaveFormat = leave.ApplyLeaveFormat,
                    HODApprovalStatus = leave.HODApprovalStatus,
                    LeaveTypeId = leave.LeaveTypeId?.ToString() ?? "",
                    StartDate = leave.StartDate ?? DateTime.MinValue,
                    EndDate = leave.EndDate ?? DateTime.MinValue,
                    NoOfDay = leave.NoOfDay,
                    FirstOrSecondHalf = leave.FirstOrSecondHalf ?? "",
                    ShortLeaveFromStr = fromStr,
                    ShortLeaveToStr = toStr,
                    ShortLeaveTimeStr = timeStr,
                    Reason = leave.Reason,
                    IsApproved = leave.IsApproved ?? "",
                    ConfirmationRemarks = leave.ConfirmationRemarks,
                    HODApprovalRemarks = "",
                    HRApprovalStatus = leave.HRApprovalStatus,
                    HRApprovalRemarks = leave.HRApprovalRemarks,
                    Days = (double)leave.NoOfDay > 0.5
                        ? _leaveDayRepo.FindBy(d => d.LeaveAppEntryId == leave.LeaveAppEntryId)
                                       .Select(d => d.Days).ToList()
                        : new List<DateTime>()
                };

                result.Add(app);
            }

            return result;
        }

        // ─── PDF HELPERS ──────────────────────────────────────────────────────────


        private void DefineColumns(TableColumnsDefinitionDescriptor cols)
        {
            float[] sizes = {
                    2, // Emp.ID
                    4, // Name
                    4, // Designation
                    1.5f, // LeaveID
                    1.8f, // Format
                    1.5f, // ApprovalStatus
                    1, // LeaveType
                    1.8f, // FromDate
                    1.8f, // ToDate
                    1.8f, // Date(s)
                    1, // NoOfDay
                    1, // Half
                    1, // From
                    1, // To
                    1, // Time
                    4, // Reason
                    1.5f, // ISStatus
                    3, // ISRemarks
                    1.5f, // HODStatus
                    3, // HODRemarks
                    1.5f, // HRStatus
                    3  // HRRemarks
                };

            foreach (var s in sizes)
                cols.RelativeColumn(s);
        }


        private void ComposeHeader(IContainer container, string companyName, string sDate, string eDate)
        {
            container.Row(row => row.RelativeItem().Column(col =>
            {
                col.Item().Text(companyName).FontSize(20).Bold().AlignCenter();
                col.Item().Text("Leave Application Report").FontSize(12).Bold().Underline().AlignCenter();
                col.Item().Text(" ");
                col.Item().Text($"Date: {sDate} - {eDate}").FontSize(10).AlignCenter();
            }));
        }

        private void ComposeFooter(IContainer content)
        {
            content.Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}");
                row.RelativeItem().AlignRight().Text(t => { t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
            });
        }

        private void ComposeContent(IContainer container, List<LeaveReportGridVM> data)
        {
            container.Column(col =>
            {
                foreach (var dept in data)
                {
                    col.Item().PaddingTop(10).BorderBottom(1).Padding(5)
                        .Text($"Department: {dept.Department}").FontSize(16).Bold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols => DefineColumns(cols));
                        table.Header(h => AddTableHeader(h));

                        

                        foreach (var app in dept.Data) AddTableRow(table, app);
                    });
                }
            });
        }

        private void AddTableHeader(TableCellDescriptor header)
        {
            var style = TextStyle.Default.FontSize(10).Bold();

            foreach (var text in new[] { "Emp. ID","Name","Designation","Leave ID","Leave\r\nFormat",
        "Approval\r\nStatus","Leave\r\nType","From\r\nDate","To\r\nDate","Date(s)",
        "No.\r\nof\r\nDay(s)","1st/\r\n2nd\r\nHalf" })
            {
                header.Cell().RowSpan(2).Border(0.25f).BorderColor(Colors.Black)
                    .Padding(5).AlignCenter().AlignMiddle().Text(text).Style(style);
            }

            header.Cell().ColumnSpan(3).Border(0.25f).BorderColor(Colors.Black)
                .Padding(5).AlignCenter().AlignMiddle().Text("Short Leave").Style(style);

            foreach (var text in new[] { "Reason","IS App.\r\nStatus","IS App. Remarks",
        "HOD App.\r\nStatus","HOD App. Remarks","HR App. Status","HR App. Remarks" })
            {
                header.Cell().RowSpan(2).Border(0.25f).BorderColor(Colors.Black)
                    .Padding(5).AlignCenter().AlignMiddle().Text(text).Style(style);
            }

            foreach (var text in new[] { "From", "To", "Time" })
            {
                header.Cell().Border(0.25f).BorderColor(Colors.Black)
                    .Padding(5).AlignCenter().AlignMiddle().Text(text).Style(style);
            }
        }

        


        private void AddTableRow(TableDescriptor table, LeaveApplicartionGridVM app)
        {
            string a = "", b = "", c = "";
            if (app.ApplyLeaveFormat == "shortLeave")
            {
                a = app.ShortLeaveFromStr ?? (app.ShortLeaveFrom.HasValue ? app.ShortLeaveFrom.Value.ToString("HH:mm") : "");
                b = app.ShortLeaveToStr ?? (app.ShortLeaveTo.HasValue ? app.ShortLeaveTo.Value.ToString("HH:mm") : "");
                c = app.ShortLeaveTimeStr ?? (app.ShortLeaveTime != null ? app.ShortLeaveTime.ToString() : "");
            }

            string startStr = app.StartDate != DateTime.MinValue ? app.StartDate.ToString("dd/MM/yyyy") : "";
            string endStr = app.EndDate != DateTime.MinValue ? app.EndDate.ToString("dd/MM/yyyy") : "";
            string datesStr = app.Days?.Any() == true ? string.Join(", ", app.Days.Select(d => d.ToString("dd/MM/yyyy"))) : "";

            var cellStyle = TextStyle.Default.FontSize(10);

            // (text, isLeftAligned)
            var cells = new (string text, bool left)[]
            {
                (app.EmployeeID ?? "", false),
                (app.EmployeeFirstName ?? "", true),   // Name -> left
                (app.DesignationName ?? "", true),     // Designation -> left
                (app.LeaveAppEntryId ?? "", false),
                (app.ApplyLeaveFormat ?? "", false),
                (app.HODApprovalStatus ?? "", false),
                (app.LeaveTypeId ?? "", false),
                (startStr, false),
                (endStr, false),
                (datesStr, false),
                (app.NoOfDay.ToString(), false),
                (app.FirstOrSecondHalf ?? "", false),
                (a, false), (b, false), (c, false),
                (app.Reason ?? "", true),              // Reason -> left
                (app.IsApproved ?? "", false),
                (app.ConfirmationRemarks ?? "", true), // Remarks -> left
                (app.HODApprovalStatus ?? "", false),
                (app.HODApprovalRemarks ?? "", true),  // Remarks -> left
                (app.HRApprovalStatus ?? "", false),
                (app.HRApprovalRemarks ?? "", true)    // Remarks -> left
            };

            foreach (var (text, left) in cells)
            {
                var cell = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(5).AlignMiddle();
                if (left) cell.AlignLeft().Text(text).Style(cellStyle);
                else cell.AlignCenter().Text(text).Style(cellStyle);
            }
        }


        
        // ─── WORD HELPERS ─────────────────────────────────────────────────────────

        private void AddWordHeader(Word.Body body, string companyName, string sDate, string eDate)
        {
            foreach (var text in new[] { companyName, "Leave Application Report", $"Date: {sDate} - {eDate}" })
            {
                body.AppendChild(new Word.Paragraph(new Word.Run(new Word.Text(text)))
                {
                    ParagraphProperties = new Word.ParagraphProperties(
                        new Word.Justification { Val = Word.JustificationValues.Center })
                });
            }
        }

        private void AddWordTableHeader(Word.Table table)
        {
            var row = new Word.TableRow();
            foreach (var h in GetReportHeaders())
            {
                var cell = new Word.TableCell(new Word.Paragraph(new Word.Run(new Word.Text(h))));
                cell.TableCellProperties = new Word.TableCellProperties(
                    new Word.TableCellWidth { Type = Word.TableWidthUnitValues.Auto });
                row.AppendChild(cell);
            }
            table.AppendChild(row);
        }

        private void AddWordTableRow(Word.Table table, LeaveApplicartionGridVM app)
        {
            string a = "", b = "", c = "";
            if (app.ApplyLeaveFormat == "shortLeave")
            {
                a = app.ShortLeaveFrom.HasValue ? app.ShortLeaveFrom.Value.ToString("HH:mm") : "";
                b = app.ShortLeaveTo.HasValue ? app.ShortLeaveTo.Value.ToString("HH:mm") : "";
                c = app.ShortLeaveTime != null ? app.ShortLeaveTime.ToString() : "";
            }

            var row = new Word.TableRow();
            foreach (var val in new[]
            {
                app.EmployeeID, app.EmployeeFirstName, app.DesignationName, app.LeaveAppEntryId,
                app.ApplyLeaveFormat, app.HODApprovalStatus, app.LeaveTypeId,
                app.StartDate.ToString("dd/MM/yyyy"), app.EndDate.ToString("dd/MM/yyyy"),
                app.Days?.Any() == true ? string.Join(", ", app.Days.Select(d => d.ToString("dd/MM/yyyy"))) : "",
                app.NoOfDay.ToString(), app.FirstOrSecondHalf, a, b, c,
                app.Reason, app.IsApproved, app.ConfirmationRemarks,
                app.HODApprovalStatus, app.HODApprovalRemarks, app.HRApprovalStatus, app.HRApprovalRemarks
            })
            {
                var cell = new Word.TableCell(new Word.Paragraph(new Word.Run(new Word.Text(val ?? ""))));
                cell.TableCellProperties = new Word.TableCellProperties(
                    new Word.TableCellWidth { Type = Word.TableWidthUnitValues.Auto });
                row.AppendChild(cell);
            }

            table.AppendChild(row);
        }

        private void AddWordFooter(Word.Body body)
        {
            body.AppendChild(new Word.Paragraph(
                new Word.Run(new Word.Text($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}"))));
        }

        // ─── STORED PROC HELPER ───────────────────────────────────────────────────

        private string BuildQueryString(DynamicParameters parameters)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("EXEC GetLeaveReport100 ");

            foreach (var param in parameters.ParameterNames)
            {
                var value = parameters.Get<object>(param);
                if (value == null || value == DBNull.Value)
                    sb.Append($"@{param} = NULL, ");
                else
                    sb.Append($"@{param} = '{value}', ");
            }

            if (sb.Length > 0) sb.Length -= 2;

            return sb.ToString();
        }
    }






    










}