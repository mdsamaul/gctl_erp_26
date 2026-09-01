using Dapper;
using GCTL.Core.ViewModels.SalaryInformationReport;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace GCTL.Service.SalaryInformationReport
{
    public class SalaryInformationReportService : ISalaryInformationReportService
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _env;

        public SalaryInformationReportService(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("ApplicationDbConnection");
            _env = env;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private static string EmptyIfNull(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private DynamicParameters BuildParameters(SalaryInformationReportFilterDto filter)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@CompanyCode", EmptyIfNull(filter.CompanyCode));
            parameters.Add("@BranchCode", EmptyIfNull(filter.BranchCode));
            parameters.Add("@DepartmentCode", EmptyIfNull(filter.DepartmentCode));
            parameters.Add("@EmployeeID", EmptyIfNull(filter.EmployeeID));
            parameters.Add("@ModeOfPayment", EmptyIfNull(filter.ModeOfPayment));
            parameters.Add("@EmploymentNature", EmptyIfNull(filter.EmploymentNature));
            parameters.Add("@GenerateType", EmptyIfNull(filter.GenerateType));
            parameters.Add("@DateFrom", filter.DateFrom);
            parameters.Add("@DateTo", filter.DateTo);
            parameters.Add("@MonthName", EmptyIfNull(filter.MonthName));
            parameters.Add("@YearName", filter.YearName);
            parameters.Add("@AsOnDate", filter.AsOnDate);
            return parameters;
        }

        private static string BuildPeriodText(SalaryInformationReportFilterDto filter)
        {
            if (string.Equals(filter.GenerateType, "ByMonth", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(filter.MonthName) && filter.YearName.HasValue)
            {
                return $"For the month of {filter.MonthName}, {filter.YearName}";
            }
            if (string.Equals(filter.GenerateType, "ByDate", StringComparison.OrdinalIgnoreCase)
                && filter.DateFrom.HasValue && filter.DateTo.HasValue)
            {
                return $"For the period {filter.DateFrom:dd/MM/yyyy} to {filter.DateTo:dd/MM/yyyy}";
            }
            return $"Generated on {DateTime.Now:dd/MM/yyyy hh:mm tt}";
        }

        public async Task<List<SalaryInformationReportDto>> GetPayrollMasterFileAsync(SalaryInformationReportFilterDto filter)
        {
            try
            {
                using IDbConnection db = new SqlConnection(_connectionString);
                var rows = await db.QueryAsync(
                    "dbo.usp_GetPayrollMasterFile_General",
                    BuildParameters(filter),
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 120);
                return rows.Select(r => MapDynamicToDto((IDictionary<string, object>)r)).ToList();
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<byte[]> ExportToExcelAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_General");

            const int firstDataCol = 1;

            var headers = new[]
            {
        "SL", "ID NO.", "Pay ID", "DP User ID", "DBBL Employee Name", "UCBL Employee Name",
        "Status", "Department", "Designation", "DOH", "DOT", "Duration", "DBBL", "UCBL",
        "Salary", "Yearly Bonus Eligibility", "Gratuity Eligibility", "Eid Bonus Eligibility",
        "PF Eligible", "Gender", "Cell No.", "Special Notes", "Probation End Date"
    };

            double[] columnWidths =
            {
        6, 12, 8, 12, 28, 28, 10, 18, 18, 12, 12, 10, 14, 16,
        14, 19, 16, 16, 12, 10, 14, 20, 16
    };

            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);

            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[1, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            ws.Cells[2, 1].Value = "Payroll Master File - General";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[2, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[3, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string>
    {
        "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "Duration",
        "DBBL", "UCBL", "Yearly Bonus Eligibility", "Gratuity Eligibility",
        "Eid Bonus Eligibility", "PF Eligible", "Gender", "Cell No.",
        "Special Notes", "Probation End Date"
    };

            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;

                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.DpUserId;
                ws.Cells[r, c++].Value = row.DbblEmployeesName;
                ws.Cells[r, c++].Value = row.UcblEmployeesName;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.Designation;
                ws.Cells[r, c++].Value = row.Doh;
                ws.Cells[r, c++].Value = row.Dot;

                ws.Cells[r, c].Value = row.Duration;
                ws.Cells[r, c++].Style.Numberformat.Format = "0.00";

                ws.Cells[r, c++].Value = row.Dbbl;
                ws.Cells[r, c++].Value = row.Ucbl;

                ws.Cells[r, c].Value = row.Salary;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c++].Value = row.YearlyBonusEligibility;
                ws.Cells[r, c++].Value = row.GratuityEligibility;

                ws.Cells[r, c].Value = row.EidBonusEligibility;
                ws.Cells[r, c++].Style.Numberformat.Format = "0.00";

                ws.Cells[r, c].Value = row.PfEligiblity;
                ws.Cells[r, c++].Style.Numberformat.Format = "0.00";

                ws.Cells[r, c++].Value = row.Gender;
                ws.Cells[r, c++].Value = row.CellPhone;
                ws.Cells[r, c++].Value = row.SpecialNotes;
                ws.Cells[r, c++].Value = row.EndOfProbation;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);

                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            int totalRow = r;

            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 13].Merge = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int salaryColIndex = Array.IndexOf(headers, "Salary") + firstDataCol;
            if (data.Count > 0)
            {
                var salaryColLetter = ExcelCellAddress.GetColumnLetter(salaryColIndex);
                ws.Cells[totalRow, salaryColIndex].Formula =
                    $"SUM({salaryColLetter}{headerRow + 1}:{salaryColLetter}{r - 1})";
            }

            ws.Cells[totalRow, salaryColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, salaryColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, salaryColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A3;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }
        private static SalaryInformationReportDto MapDynamicToDto(IDictionary<string, object> r)
        {
            try
            {
                T Get<T>(string key)
                {
                    if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                        return default;
                    try
                    {
                        return (T)Convert.ChangeType(val, typeof(T));
                    }
                    catch
                    {
                        return default;
                    }
                }

                string GetStr(string key)
                {
                    if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                        return null;
                    return val.ToString()?.Trim();
                }

                // Safe decimal converter
                decimal? GetDec(string key)
                {
                    if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                        return null;

                    if (val is decimal d) return d;
                    if (val is double db) return (decimal)db;
                    if (val is float f) return (decimal)f;
                    if (val is int i) return i;
                    if (val is long l) return l;

                    var str = val.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(str))
                        return null;

                    if (decimal.TryParse(str, out var result))
                        return result;

                    return null; // invalid string হলে null রিটার্ন করবে, error দিবে না
                }

                return new SalaryInformationReportDto
                {
                    SL = Get<int>("SL."),
                    IdNo = GetStr("ID NO."),
                    PayId = GetStr("Pay ID"),
                    DpUserId = GetStr("DP User ID"),
                    DbblEmployeesName = GetStr("DBBL Employees Name"),
                    UcblEmployeesName = GetStr("UCBL Employees Name"),
                    Status = GetStr("Status"),
                    Department = GetStr("DEPARTMENT"),
                    Designation = GetStr("DESIGNATION"),
                    Doh = GetStr("DOH"),
                    Dot = GetStr("DOT"),

                    Duration = GetDec("Duration"),

                    Dbbl = GetStr("DBBL"),
                    Ucbl = GetStr("UCBL"),
                    Salary = GetDec("Salary"),
                    YearlyBonusEligibility = GetStr("Yearly Bonus Eligibility"),
                    GratuityEligibility = GetStr("Gratuity Eligibility"),
                    EidBonusEligibility = GetDec("Eid Bonus Eligibility"),
                    PfEligiblity = GetDec("PF Eligiblity"),
                    Gender = GetStr("Gender"),
                    CellPhone = GetStr("Cell Phone"),
                    SpecialNotes = GetStr("Special Notes"),
                    EndOfProbation = GetStr("End of Probation"),
                    ModeOfPayment = GetStr("Mode of Payment"),
                    EmploymentNature = GetStr("Employment Nature")
                };
            }
            catch (Exception)
            {

                throw;
            }
        }


        public async Task<List<SalaryInformationReportGratuityDto>> GetPayrollMasterFileGratuityAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_Gratuity",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToGratuityDto((IDictionary<string, object>)r)).ToList();
        }

        public async Task<byte[]> ExportToExcelGratuityAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileGratuityAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_Gratuity");

            const int firstDataCol = 1;
            var headers = new[]
            {
                "SL", "ID NO.", "Pay ID", "Name of the Employee", "Status", "Department",
                "DOH", "DOT", "Bank Account No", "Salary", "Tenure", "Gratuity", "Note"
            };

            double[] columnWidths = { 6, 12, 10, 28, 10, 18, 12, 12, 16, 12, 10, 12, 20 };
            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "Payroll Master File - Gratuity";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string> { "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "Bank Account No", "Tenure" };
            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.BankAccountNo;

                ws.Cells[r, c].Value = row.Salary ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c].Value = row.Tenure ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "0";

                decimal gratuityValue = 0;
                if (!string.IsNullOrWhiteSpace(row.Gratuity))
                    decimal.TryParse(row.Gratuity.Replace(",", ""), out gratuityValue);
                ws.Cells[r, c].Value = gratuityValue;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c++].Value = row.Note;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            int totalRow = r;
            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 8].Merge = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int salaryColIndex = Array.IndexOf(headers, "Salary") + firstDataCol;
            int gratuityColIndex = Array.IndexOf(headers, "Gratuity") + firstDataCol;

            if (data.Count > 0)
            {
                ws.Cells[totalRow, salaryColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(salaryColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(salaryColIndex)}{r - 1})";
                ws.Cells[totalRow, gratuityColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(gratuityColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(gratuityColIndex)}{r - 1})";
            }

            ws.Cells[totalRow, salaryColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, salaryColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, salaryColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.Cells[totalRow, gratuityColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, gratuityColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, gratuityColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
            {
                ws.Cells[totalRow, cc].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[totalRow, cc].Style.Font.Bold = true;
                ws.Cells[totalRow, cc].Style.Font.Name = "Times New Roman";
                ws.Cells[totalRow, cc].Style.Font.Size = 9;
            }

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }

        private static SalaryInformationReportGratuityDto MapDynamicToGratuityDto(IDictionary<string, object> r)
        {
            T Get<T>(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return default;
                return (T)Convert.ChangeType(val, typeof(T));
            }

            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToDecimal(val);
            }

            int? GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToInt32(val);
            }

            return new SalaryInformationReportGratuityDto
            {
                SL = Get<int>("SL."),
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Status = GetStr("Status"),
                Department = GetStr("DEPARTMENT"),
                DateOfHire = GetStr("DOH"),
                Dot = GetStr("DOT"),
                BankAccountNo = GetStr("Bank Account No"),
                Salary = GetDec("Salary"),
                Tenure = GetInt("Tenure"),
                Gratuity = GetStr("Gratuity"),
                Note = GetStr("Note")
            };
        }

        public async Task<List<SalaryInformationReportYearlyBonusDto>> GetPayrollMasterFileYearlyBonusAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_YearlyBonus",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToYearlyBonusDto((IDictionary<string, object>)r)).ToList();
        }

        public async Task<byte[]> ExportToExcelYearlyBonusAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileYearlyBonusAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_YearlyBonus");

            const int firstDataCol = 1;
            var headers = new[]
            {
                "SL", "ID NO.", "Pay ID", "Name of the Employee", "Status", "Department",
                "DOH", "DOT", "Bank Account No", "Salary", "Yearly Bonus", "Note"
            };

            double[] columnWidths = { 6, 12, 10, 28, 10, 18, 12, 12, 16, 12, 14, 20 };
            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "Yearly Bonus Report";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string> { "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "Bank Account No" };
            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.BankAccountNo;

                ws.Cells[r, c].Value = row.Salary ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c].Value = row.YearlyBonus ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c++].Value = row.Note;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            int totalRow = r;
            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 8].Merge = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int salaryColIndex = 10;
            int bonusColIndex = 11;

            if (data.Count > 0)
            {
                ws.Cells[totalRow, salaryColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(salaryColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(salaryColIndex)}{r - 1})";
                ws.Cells[totalRow, bonusColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(bonusColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(bonusColIndex)}{r - 1})";
            }

            ws.Cells[totalRow, salaryColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, salaryColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, salaryColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.Cells[totalRow, bonusColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, bonusColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, bonusColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
            {
                ws.Cells[totalRow, cc].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[totalRow, cc].Style.Font.Bold = true;
            }

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }

        private static SalaryInformationReportYearlyBonusDto MapDynamicToYearlyBonusDto(IDictionary<string, object> r)
        {
            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToDecimal(val);
            }

            int GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return 0;
                return Convert.ToInt32(val);
            }

            return new SalaryInformationReportYearlyBonusDto
            {
                SL = GetInt("SL."),
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Status = GetStr("Status"),
                Department = GetStr("DEPARTMENT"),
                DateOfHire = GetStr("DOH"),
                Dot = GetStr("DOT"),
                BankAccountNo = GetStr("Bank Account No"),
                Salary = GetDec("Salary"),
                YearlyBonus = GetDec("Yearly Bonus"),
                Note = GetStr("Note")
            };
        }

        public async Task<List<SalaryInformationReportExtraDayDto>> GetPayrollMasterFileExtraDayAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_ExtraDay",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToExtraDayDto((IDictionary<string, object>)r)).ToList();
        }

        public async Task<byte[]> ExportToExcelExtraDayAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileExtraDayAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_ExtraDay");

            const int firstDataCol = 1;
            var headers = new[]
            {
                "SL", "ID NO.", "Pay ID", "Name of the Employee", "Status", "Department",
                "DOH", "DOT", "Bank Account No", "Salary", "Days", "Extra Days Amount", "Note"
            };

            double[] columnWidths = { 6, 12, 10, 28, 10, 18, 12, 12, 16, 14, 10, 16, 20 };
            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "Extra Day Report";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string> { "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "Bank Account No", "Days" };
            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.BankAccountNo;

                ws.Cells[r, c].Value = row.Salary ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c].Value = row.Days ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "0";

                ws.Cells[r, c].Value = row.ExtraDaysAmount ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";

                ws.Cells[r, c++].Value = row.Note;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            int totalRow = r;
            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 8].Merge = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int salaryColIndex = 10;
            int daysColIndex = 11;
            int amountColIndex = 12;

            if (data.Count > 0)
            {
                ws.Cells[totalRow, salaryColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(salaryColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(salaryColIndex)}{r - 1})";
                ws.Cells[totalRow, daysColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(daysColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(daysColIndex)}{r - 1})";
                ws.Cells[totalRow, amountColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(amountColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(amountColIndex)}{r - 1})";
            }

            ws.Cells[totalRow, salaryColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, salaryColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, salaryColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.Cells[totalRow, daysColIndex].Style.Numberformat.Format = "0";
            ws.Cells[totalRow, daysColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, daysColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.Cells[totalRow, amountColIndex].Style.Numberformat.Format = "#,##0";
            ws.Cells[totalRow, amountColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, amountColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
            {
                ws.Cells[totalRow, cc].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[totalRow, cc].Style.Font.Bold = true;
            }

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }

        private static SalaryInformationReportExtraDayDto MapDynamicToExtraDayDto(IDictionary<string, object> r)
        {
            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToDecimal(val);
            }

            int? GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToInt32(val);
            }

            return new SalaryInformationReportExtraDayDto
            {
                SL = GetInt("SL.") ?? 0,
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Status = GetStr("Status"),
                Department = GetStr("DEPARTMENT"),
                DateOfHire = GetStr("DOH"),
                Dot = GetStr("DOT"),
                BankAccountNo = GetStr("Bank Account No"),
                Salary = GetDec("Salary"),
                Days = GetInt("Days"),
                ExtraDaysAmount = GetDec("Extra Days Amount"),
                Note = GetStr("Note")
            };
        }

        public async Task<List<SalaryInformationReportTerminatedDto>> GetPayrollMasterFileTerminatedAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_Terminated",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToTerminatedDto((IDictionary<string, object>)r)).ToList();
        }

        public async Task<byte[]> ExportToExcelTerminatedAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileTerminatedAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_Terminated");

            const int firstDataCol = 1;
            var headers = new[]
            {
                "SL", "ID NO.", "Pay ID", "Name of the Employee", "Department", "Status",
                "DOH", "DOT", "DBBL", "UCBL", "Duration", "PF Eligibility", "Days", "Note"
            };

            double[] columnWidths = { 6, 12, 10, 28, 18, 10, 14, 12, 14, 16, 20, 14, 10, 20 };
            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "Terminated Report";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string>
            {
                "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "DBBL", "UCBL", "Duration", "PF Eligibility", "Days"
            };
            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.Dbbl;
                ws.Cells[r, c++].Value = row.Ucbl;
                ws.Cells[r, c++].Value = row.Duration;

                ws.Cells[r, c].Value = row.PfEligiblity ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "0";

                ws.Cells[r, c].Value = row.Days ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "0";

                ws.Cells[r, c++].Value = row.Note;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }

        private static SalaryInformationReportTerminatedDto MapDynamicToTerminatedDto(IDictionary<string, object> r)
        {
            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToDecimal(val);
            }

            int? GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToInt32(val);
            }

            return new SalaryInformationReportTerminatedDto
            {
                SL = GetInt("SL.") ?? 0,
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Department = GetStr("DEPARTMENT"),
                Status = GetStr("Status"),
                DateOfHire = GetStr("Date of Hire"),
                Dot = GetStr("DOT"),
                Dbbl = GetStr("DBBL"),
                Ucbl = GetStr("UCBL"),
                Duration = GetStr("Duration"),
                PfEligiblity = GetDec("PF Eligiblity"),
                Days = GetInt("Days"),
                Note = GetStr("Note")
            };
        }

        public async Task<List<SalaryInformationReportIncentivesDto>> GetPayrollMasterFileIncentivesAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_Incentives",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToIncentivesDto((IDictionary<string, object>)r)).ToList();
        }

        private static SalaryInformationReportIncentivesDto MapDynamicToIncentivesDto(IDictionary<string, object> r)
        {
            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToDecimal(val);
            }

            int? GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return Convert.ToInt32(val);
            }

            return new SalaryInformationReportIncentivesDto
            {
                SL = GetInt("SL") ?? GetInt("SL.") ?? 0,
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Status = GetStr("Status"),
                Department = GetStr("DEPARTMENT"),
                DateOfHire = GetStr("Date of Hire"),
                Dot = GetStr("DOT"),
                Dbbl = GetStr("DBBL"),
                BankAccountNo = GetStr("Bank Account No"),
                Salary = GetDec("Salary"),
                Designation = GetStr("Designation"),
                Incentives = GetDec("Incentives"),
                Eligibility = GetStr("Eligibility"),
                Notes = GetStr("Notes")
            };
        }

        public async Task<byte[]> ExportToExcelIncentivesAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileIncentivesAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_Incentives");

            const int firstDataCol = 1;
            double[] columnWidths = { 6, 12, 8, 26, 10, 19, 12, 12, 16, 17, 10, 28, 10, 12, 20 };

            var headers = new[]
            {
                "SL", "ID No.", "Pay ID", "Name of the Employee", "Status", "Department",
                "DOH", "DOT", "DBBL", "Bank Account No", "Salary", "Designation",
                "Incentives", "Eligibility", "Notes"
            };

            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "Incentives Report";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                int col = firstDataCol + i;
                var cell = ws.Cells[headerRow, col];
                cell.Value = headers[i];
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            }

            var centerColumns = new HashSet<string>
            {
                "SL", "ID No.", "Pay ID", "Status", "DOH", "DOT", "DBBL", "Bank Account No", "Eligibility"
            };
            var rightColumns = new HashSet<string> { "Salary", "Incentives" };

            int r = headerRow + 1;
            decimal totalSalary = 0m;
            decimal totalIncentives = 0m;

            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.Dbbl;
                ws.Cells[r, c++].Value = row.BankAccountNo;

                decimal salary = row.Salary ?? 0m;
                ws.Cells[r, c].Value = salary;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";
                totalSalary += salary;

                ws.Cells[r, c++].Value = row.Designation;

                decimal incentive = row.Incentives ?? 0m;
                ws.Cells[r, c].Value = incentive;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0";
                totalIncentives += incentive;

                ws.Cells[r, c++].Value = row.Eligibility;
                ws.Cells[r, c++].Value = row.Notes;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var cell = ws.Cells[r, cc];
                    cell.Style.Font.Name = "Times New Roman";
                    cell.Style.Font.Size = 9;
                    cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                    string headerName = headers[cc - firstDataCol];
                    if (centerColumns.Contains(headerName))
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    else if (rightColumns.Contains(headerName))
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    else
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                }
                r++;
            }

            int totalRow = r;
            int salaryCol = firstDataCol + 10;
            int incentiveCol = firstDataCol + 12;

            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 9].Merge = true;
            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Name = "Times New Roman";
            ws.Cells[totalRow, firstDataCol].Style.Font.Size = 10;
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            ws.Cells[totalRow, salaryCol].Value = totalSalary;
            ws.Cells[totalRow, salaryCol].Style.Numberformat.Format = "#,##0";

            ws.Cells[totalRow, incentiveCol].Value = totalIncentives;
            ws.Cells[totalRow, incentiveCol].Style.Numberformat.Format = "#,##0";

            for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
            {
                var cell = ws.Cells[totalRow, cc];
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Bold = true;
                cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            }

            ws.Cells[totalRow, salaryCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, incentiveCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            ws.Row(totalRow).Height = 22;
            ws.Row(1).Height = 24;
            ws.Row(2).Height = 21;
            ws.Row(3).Height = 18;
            ws.Row(headerRow).Height = 30;
            ws.View.FreezePanes(headerRow + 1, firstDataCol);

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;
            ws.PrinterSettings.RepeatRows = new ExcelAddress(headerRow, 1, headerRow, totalCols);
            ws.PrinterSettings.PrintArea = ws.Cells[1, 1, totalRow, totalCols];

            return await package.GetAsByteArrayAsync();
        }

        public async Task<List<SalaryInformationReportNewHireDto>> GetPayrollMasterFileNewHireAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_NewHire",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToNewHireDto((IDictionary<string, object>)r)).ToList();
        }

        private static SalaryInformationReportNewHireDto MapDynamicToNewHireDto(IDictionary<string, object> r)
        {
            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString()?.Trim();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                if (val is decimal d) return d;
                if (val is double db) return (decimal)db;
                if (val is float f) return (decimal)f;
                if (val is int i) return i;
                if (val is long l) return l;

                var str = val.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (decimal.TryParse(str, out var result)) return result;
                return null;
            }

            int GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return 0;
                if (int.TryParse(val.ToString(), out var result))
                    return result;
                return 0;
            }

            return new SalaryInformationReportNewHireDto
            {
                SL = GetInt("SL."),
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Status = GetStr("Status"),
                Department = GetStr("DEPARTMENT"),
                DateOfHire = GetStr("Date of Hire"),
                Dot = GetStr("DOT"),
                BankAcNo = GetStr("DBBL"),
                BankAcNoUCBL = GetStr("Bank Account No"),
                GrossSalary = GetDec("Gross Salary"),
                DaysWorked = GetInt("Days Worked"),
                Payment = GetDec("Payment"),
                Note = GetStr("Note")
            };
        }

        public async Task<byte[]> ExportToExcelNewHireAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileNewHireAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_NewHire");

            const int firstDataCol = 1;
            var headers = new[]
            {
        "SL", "ID NO.", "Pay ID", "Name of the Employee", "Status", "Department",
        "DOH", "DOT", "DBBL", "Bank Account No", "Gross Salary", "Days Worked", "Payment", "Note"
    };

            double[] columnWidths = { 6, 12, 10, 28, 10, 18, 12, 12, 16, 16, 16, 12, 16, 16 };
            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "New Hire Report";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string> { "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "DBBL", "Bank Account No", "Days Worked", "Note" };
            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.BankAcNo;
                ws.Cells[r, c++].Value = row.BankAcNoUCBL;

                ws.Cells[r, c].Value = row.GrossSalary ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c].Value = row.DaysWorked;
                ws.Cells[r, c++].Style.Numberformat.Format = "0";

                ws.Cells[r, c].Value = row.Payment ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c++].Value = row.Note;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            int totalRow = r;
            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 9].Merge = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int grossSalaryColIndex = 11;
            int paymentColIndex = 13;

            if (data.Count > 0)
            {
                ws.Cells[totalRow, grossSalaryColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(grossSalaryColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(grossSalaryColIndex)}{r - 1})";
                ws.Cells[totalRow, paymentColIndex].Formula =
                    $"SUM({ExcelCellAddress.GetColumnLetter(paymentColIndex)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(paymentColIndex)}{r - 1})";
            }

            ws.Cells[totalRow, grossSalaryColIndex].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[totalRow, grossSalaryColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, grossSalaryColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            ws.Cells[totalRow, paymentColIndex].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[totalRow, paymentColIndex].Style.Font.Bold = true;
            ws.Cells[totalRow, paymentColIndex].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
            {
                ws.Cells[totalRow, cc].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[totalRow, cc].Style.Font.Bold = true;
                ws.Cells[totalRow, cc].Style.Font.Name = "Times New Roman";
                ws.Cells[totalRow, cc].Style.Font.Size = 9;
            }

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }
        public async Task<List<SalaryInformationReportWfhDto>> GetPayrollMasterFileWfhAsync(SalaryInformationReportFilterDto filter)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = await db.QueryAsync(
                "dbo.usp_GetPayrollMasterFile_WFH",
                BuildParameters(filter),
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);
            return rows.Select(r => MapDynamicToWfhDto((IDictionary<string, object>)r)).ToList();
        }

        private static SalaryInformationReportWfhDto MapDynamicToWfhDto(IDictionary<string, object> r)
        {
            string GetStr(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                return val.ToString()?.Trim();
            }

            decimal? GetDec(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return null;
                if (val is decimal d) return d;
                if (val is double db) return (decimal)db;
                if (val is float f) return (decimal)f;
                if (val is int i) return i;
                if (val is long l) return l;

                var str = val.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(str)) return null;
                if (decimal.TryParse(str, out var result)) return result;
                return null;
            }

            int GetInt(string key)
            {
                if (!r.TryGetValue(key, out var val) || val == null || val == DBNull.Value)
                    return 0;
                if (int.TryParse(val.ToString(), out var result))
                    return result;
                return 0;
            }

            return new SalaryInformationReportWfhDto
            {
                SL = GetInt("SL."),
                IdNo = GetStr("ID NO."),
                PayId = GetStr("Pay ID"),
                NameOfTheEmployee = GetStr("Name of the Employee"),
                Status = GetStr("Status"),
                Department = GetStr("DEPARTMENT"),
                DateOfHire = GetStr("Date of Hire"),
                Dot = GetStr("DOT"),
                BankAcNo = GetStr("DBBL"),
                BankAcNoUCBL = GetStr("Bank Account No"),
                GrossSalary = GetDec("Gross Salary"),
                Internet = GetDec("Internet"),
                Carrying = GetDec("Carrying"),
                Others = GetDec("Others"),
                Total = GetDec("Total"),
                Note = GetStr("Note")
            };
        }

        public async Task<byte[]> ExportToExcelWfhAsync(SalaryInformationReportFilterDto filter)
        {
            var data = await GetPayrollMasterFileWfhAsync(filter);
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PayrollMasterFile_WFH");

            const int firstDataCol = 1;
            var headers = new[]
            {
        "SL", "ID NO.", "Pay ID", "Name of the Employee", "Status", "Department",
        "DOH", "DOT", "DBBL", "Bank Account No", "Gross Salary",
        "Internet", "Carrying", "Others", "Total", "Note"
    };

            double[] columnWidths = { 6, 12, 10, 28, 10, 18, 12, 12, 16, 16, 15, 12, 12, 12, 15, 18 };
            int totalCols = headers.Length;
            int headerRow = 5;

            for (int i = 0; i < totalCols; i++)
                ws.Column(firstDataCol + i).Width = columnWidths[i];

            var logoPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "images", "DPL.jpeg");
            if (File.Exists(logoPath))
            {
                var picture = ws.Drawings.AddPicture("CompanyLogo", new FileInfo(logoPath));
                picture.SetSize(110, 40);
                picture.SetPosition(0, 10, 0, 15);
            }

            string periodText = BuildPeriodText(filter);
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[3, 1, 3, totalCols].Merge = true;

            ws.Cells[1, 1].Value = "DataPath Ltd.";
            ws.Cells[1, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = "WFH Report";
            ws.Cells[2, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[2, 1].Style.Font.Size = 13;
            ws.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[3, 1].Value = periodText;
            ws.Cells[3, 1].Style.Font.Name = "Times New Roman";
            ws.Cells[3, 1].Style.Font.Size = 10;
            ws.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, firstDataCol + i];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Name = "Times New Roman";
                cell.Style.Font.Size = 10;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            var centerAlignColumns = new HashSet<string> { "SL", "ID NO.", "Pay ID", "Status", "DOH", "DOT", "DBBL", "Bank Account No" };
            var colCenterFlags = new bool[headers.Length];
            for (int i = 0; i < headers.Length; i++)
                colCenterFlags[i] = centerAlignColumns.Contains(headers[i]);

            int r = headerRow + 1;
            foreach (var row in data)
            {
                int c = firstDataCol;
                ws.Cells[r, c++].Value = row.SL;
                ws.Cells[r, c++].Value = row.IdNo;
                ws.Cells[r, c++].Value = row.PayId;
                ws.Cells[r, c++].Value = row.NameOfTheEmployee;
                ws.Cells[r, c++].Value = row.Status;
                ws.Cells[r, c++].Value = row.Department;
                ws.Cells[r, c++].Value = row.DateOfHire;
                ws.Cells[r, c++].Value = row.Dot;
                ws.Cells[r, c++].Value = row.BankAcNo;
                ws.Cells[r, c++].Value = row.BankAcNoUCBL;

                ws.Cells[r, c].Value = row.GrossSalary ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c].Value = row.Internet ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c].Value = row.Carrying ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c].Value = row.Others ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c].Value = row.Total ?? 0;
                ws.Cells[r, c++].Style.Numberformat.Format = "#,##0.00";

                ws.Cells[r, c++].Value = row.Note;

                for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
                {
                    var dataCell = ws.Cells[r, cc];
                    dataCell.Style.Font.Name = "Times New Roman";
                    dataCell.Style.Font.Size = 9;
                    dataCell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    int headerIdx = cc - firstDataCol;
                    if (headerIdx >= 0 && headerIdx < colCenterFlags.Length && colCenterFlags[headerIdx])
                    {
                        dataCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    }
                }
                r++;
            }

            int totalRow = r;
            ws.Cells[totalRow, firstDataCol].Value = "Total";
            ws.Cells[totalRow, firstDataCol].Style.Font.Bold = true;
            ws.Cells[totalRow, firstDataCol, totalRow, firstDataCol + 9].Merge = true;
            ws.Cells[totalRow, firstDataCol].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[totalRow, firstDataCol].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells[totalRow, firstDataCol].Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int[] numericCols = { 11, 12, 13, 14, 15 }; // Gross Salary, Internet, Carrying, Others, Total
            foreach (var colIdx in numericCols)
            {
                if (data.Count > 0)
                {
                    ws.Cells[totalRow, colIdx].Formula =
                        $"SUM({ExcelCellAddress.GetColumnLetter(colIdx)}{headerRow + 1}:{ExcelCellAddress.GetColumnLetter(colIdx)}{r - 1})";
                }
                ws.Cells[totalRow, colIdx].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[totalRow, colIdx].Style.Font.Bold = true;
                ws.Cells[totalRow, colIdx].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            for (int cc = firstDataCol; cc < firstDataCol + totalCols; cc++)
            {
                ws.Cells[totalRow, cc].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[totalRow, cc].Style.Font.Bold = true;
                ws.Cells[totalRow, cc].Style.Font.Name = "Times New Roman";
                ws.Cells[totalRow, cc].Style.Font.Size = 9;
            }

            ws.PrinterSettings.Orientation = eOrientation.Landscape;
            ws.PrinterSettings.PaperSize = ePaperSize.A4;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.HorizontalCentered = true;
            ws.PrinterSettings.LeftMargin = 0.25m;
            ws.PrinterSettings.RightMargin = 0.25m;
            ws.PrinterSettings.TopMargin = 0.4m;
            ws.PrinterSettings.BottomMargin = 0.4m;

            return await package.GetAsByteArrayAsync();
        }
    }
}