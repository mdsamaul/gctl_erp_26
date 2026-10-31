using GCTL.Core.Data;
using GCTL.Core.ViewModels.Common;
using GCTL.Core.ViewModels.HrmAtdShifts;
using GCTL.Data.Models;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace GCTL.Service.HrmAtdShifts
{
    public class HrmAtdShiftService : AppService<HrmAtdShift>, IHrmAtdShiftService
    {
        private readonly IRepository<HrmAtdShift> hRmAtdShiftRepository;
        private readonly IRepository<HrmAtdShiftDetails> shiftDetailsRepository;
        private readonly IRepository<HrmDefShiftType> shiftService;
        private readonly IRepository<CoreAccessCode> accessCodeRepository;
        private readonly IConfiguration _configuration;

        public HrmAtdShiftService(
            IRepository<HrmAtdShift> hRmAtdShiftRepository,
            IRepository<HrmAtdShiftDetails> shiftDetailsRepository,
            IRepository<HrmDefShiftType> shiftService,
            IRepository<CoreAccessCode> accessCodeRepository,
            IConfiguration configuration)
            : base(hRmAtdShiftRepository)
        {
            this.hRmAtdShiftRepository = hRmAtdShiftRepository;
            this.shiftDetailsRepository = shiftDetailsRepository;
            this.shiftService = shiftService;
            this.accessCodeRepository = accessCodeRepository;
            this._configuration = configuration;
        }

        public async Task<List<HrmAtdShiftDetailViewModel>> GetDetailsByShiftCodeAsync(string shiftCode = "")
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("ApplicationDbConnection"));
            var parameters = new DynamicParameters();
            parameters.Add("@ShiftCode", shiftCode ?? "");
            var details = await connection.QueryAsync<HrmAtdShiftDetailViewModel>(
                "SP_HrmAtdShiftDetails_GetList",
                parameters,
                commandType: CommandType.StoredProcedure);

            var list = details.ToList();
            foreach (var d in list)
            {
                d.CompanyIdsList = !string.IsNullOrEmpty(d.CompanyIds) ? d.CompanyIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
                d.BranchIdsList = !string.IsNullOrEmpty(d.BranchIds) ? d.BranchIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
                d.DepartmentIdsList = !string.IsNullOrEmpty(d.DepartmentIds) ? d.DepartmentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
                d.EmployeeIdsList = !string.IsNullOrEmpty(d.EmployeeIds) ? d.EmployeeIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
            }
            return list;
        }

        public async Task<HrmAtdShiftSetupViewModel> GetByIdAsync(string code)
        {
            var entity = await hRmAtdShiftRepository.GetByIdAsync(code);
            if (entity == null)
            {
                return null;
            }
            var details = await GetDetailsByShiftCodeAsync(code);
            var firstDetail = details.FirstOrDefault();
            return new HrmAtdShiftSetupViewModel
            {
                AutoId = entity.AutoId,
                ShiftCode = entity.ShiftCode,
                ShiftName = entity.ShiftName,
                ShiftShortName = entity.ShiftShortName,
                Description = entity.ShiftShortName,
                Remarks = entity.Remarks,
                ShiftTypeId = entity.ShiftTypeId,
                Ldate = entity.Ldate,
                ModifyDate = entity.ModifyDate,
                Luser = entity.Luser,
                Lip = entity.Lip,
                Lmac = entity.Lmac,
                IsActive = entity.IsActive ?? false,
                IsActiveStatus = (entity.IsActive == true) ? "Active" : "Inactive",
                ShiftStartTime = firstDetail != null ? firstDetail.ShiftStartTime : DateTime.MinValue,
                ShiftEndTime = firstDetail != null ? firstDetail.ShiftEndTime : DateTime.MinValue,
                AbsentTime = firstDetail != null ? firstDetail.AbsentTime : DateTime.MinValue,
                LateTime = firstDetail != null ? firstDetail.LateTime : DateTime.MinValue,
                Wef = firstDetail != null ? firstDetail.Wef : DateTime.Now,
                LunchInTime = firstDetail != null ? firstDetail.LunchInTime : DateTime.MinValue,
                LunchOutTime = firstDetail != null ? firstDetail.LunchOutTime : DateTime.MinValue,
                LunchBreakHour = firstDetail != null ? firstDetail.LunchBreakHour : 0m,
                EarlyLeaveTime = firstDetail?.EarlyLeaveTime ?? "",
                IsCrossMidnight = firstDetail?.IsCrossMidnight ?? false,
                CompanyIds = firstDetail?.CompanyIds ?? "",
                BranchIds = firstDetail?.BranchIds ?? "",
                DepartmentIds = firstDetail?.DepartmentIds ?? "",
                EmployeeIds = firstDetail?.EmployeeIds ?? "",
                CompanyIdsList = firstDetail?.CompanyIdsList ?? new List<string>(),
                BranchIdsList = firstDetail?.BranchIdsList ?? new List<string>(),
                DepartmentIdsList = firstDetail?.DepartmentIdsList ?? new List<string>(),
                EmployeeIdsList = firstDetail?.EmployeeIdsList ?? new List<string>(),
                OvertimeRuleID = firstDetail?.OvertimeRuleID ?? "",
                Details = details
            };
        }

        public async Task<List<HrmAtdShiftSetupViewModel>> GetAllAsync()
        {
            var entity = await hRmAtdShiftRepository.GetAllAsync();
            var allDetails = await GetDetailsByShiftCodeAsync("");
            return entity.Select(entityVM =>
            {
                var shiftDetails = allDetails.Where(d => d.ShiftCode == entityVM.ShiftCode).ToList();
                var firstDetail = shiftDetails.FirstOrDefault();
                return new HrmAtdShiftSetupViewModel
                {
                    AutoId = entityVM.AutoId,
                    ShiftCode = entityVM.ShiftCode,
                    ShiftTypeId = entityVM.ShiftTypeId,
                    ShiftTypeName = shiftService.All().Where(w => w.ShiftTypeId == entityVM.ShiftTypeId).Select(x => x.ShiftTypeName).FirstOrDefault() ?? "",
                    ShiftName = entityVM.ShiftName,
                    ShiftShortName = entityVM.ShiftShortName,
                    Remarks = entityVM.Remarks,
                    Description = entityVM.ShiftShortName,
                    Ldate = entityVM.Ldate,
                    ModifyDate = entityVM.ModifyDate,
                    Luser = entityVM.Luser,
                    Lip = entityVM.Lip,
                    Lmac = entityVM.Lmac,
                    IsActive = entityVM.IsActive ?? false,
                    IsActiveStatus = (entityVM.IsActive == true) ? "Active" : "Inactive",
                    ShiftStartTime = firstDetail != null ? firstDetail.ShiftStartTime : DateTime.MinValue,
                    ShiftEndTime = firstDetail != null ? firstDetail.ShiftEndTime : DateTime.MinValue,
                    AbsentTime = firstDetail != null ? firstDetail.AbsentTime : DateTime.MinValue,
                    LateTime = firstDetail != null ? firstDetail.LateTime : DateTime.MinValue,
                    Wef = firstDetail != null ? firstDetail.Wef : DateTime.Now,
                    LunchInTime = firstDetail != null ? firstDetail.LunchInTime : DateTime.MinValue,
                    LunchOutTime = firstDetail != null ? firstDetail.LunchOutTime : DateTime.MinValue,
                    LunchBreakHour = firstDetail != null ? firstDetail.LunchBreakHour : 0m,
                    EarlyLeaveTime = firstDetail?.EarlyLeaveTime ?? "",
                    IsCrossMidnight = firstDetail?.IsCrossMidnight ?? false,
                    CompanyIds = firstDetail?.CompanyIds ?? "",
                    BranchIds = firstDetail?.BranchIds ?? "",
                    DepartmentIds = firstDetail?.DepartmentIds ?? "",
                    EmployeeIds = firstDetail?.EmployeeIds ?? "",
                    OvertimeRuleID = firstDetail?.OvertimeRuleID ?? "",
                    Details = shiftDetails
                };
            }).ToList();
        }

        public async Task<dynamic> GetDashboardCountAsync()
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("ApplicationDbConnection"));
            var result = await connection.QueryFirstOrDefaultAsync<dynamic>("SP_HrmAtdShift_DashboardCount", commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<List<HrmAtdShiftSetupViewModel>> GetGridDataAsync(string searchText, string shiftType, string companyId, string branchId, string departmentId, string status)
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("ApplicationDbConnection"));
            var parameters = new DynamicParameters();
            parameters.Add("@SearchText", searchText ?? "");
            parameters.Add("@ShiftType", shiftType ?? "");
            parameters.Add("@CompanyId", companyId ?? "");
            parameters.Add("@BranchId", branchId ?? "");
            parameters.Add("@DepartmentId", departmentId ?? "");
            parameters.Add("@Status", status ?? "");
            parameters.Add("@Offset", 0);
            parameters.Add("@PageSize", 100000);

            var result = (await connection.QueryAsync<HrmAtdShiftSetupViewModel>("SP_HrmAtdShift_GetList", parameters, commandType: CommandType.StoredProcedure)).ToList();
            var allDetails = await GetDetailsByShiftCodeAsync("");

            foreach (var item in result)
            {
                item.Details = allDetails.Where(d => d.ShiftCode == item.ShiftCode).ToList();
            }

            return result;
        }

        private DateTime EnsureValidSqlDateTime(DateTime dt, string fallbackTime = "09:00:00")
        {
            if (dt == DateTime.MinValue || dt.Year < 1753)
            {
                return DateTime.Parse($"1900-01-01 {fallbackTime}");
            }
            return dt;
        }

        public async Task<bool> SaveAsync(HrmAtdShiftSetupViewModel entityVM)
        {
            await hRmAtdShiftRepository.BeginTransactionAsync();
            try
            {
                string nextShiftCode = !string.IsNullOrEmpty(entityVM.ShiftCode) && !await IsExistByCodeAsync(entityVM.ShiftCode)
                    ? entityVM.ShiftCode.Trim()
                    : await GenerateNextCode();
                entityVM.ShiftCode = nextShiftCode;

                HrmAtdShift entity = new HrmAtdShift
                {
                    ShiftCode = nextShiftCode,
                    ShiftName = (entityVM.ShiftName ?? string.Empty).Trim(),
                    ShiftShortName = (entityVM.Description ?? entityVM.ShiftShortName ?? string.Empty).Trim(),
                    Remarks = (entityVM.Remarks ?? string.Empty).Trim(),
                    ShiftTypeId = (entityVM.ShiftTypeId ?? string.Empty).Trim(),
                    IsActive = string.IsNullOrEmpty(entityVM.IsActiveStatus) ? true : entityVM.IsActiveStatus == "Active",
                    Luser = entityVM.Luser ?? string.Empty,
                    Lip = entityVM.Lip ?? string.Empty,
                    Lmac = entityVM.Lmac ?? string.Empty,
                    Ldate = DateTime.Now
                };

                await hRmAtdShiftRepository.AddAsync(entity);

                if (entityVM.Details != null && entityVM.Details.Any())
                {
                    foreach (var d in entityVM.Details)
                    {
                        var detailEntity = new HrmAtdShiftDetails
                        {
                            ShiftCode = nextShiftCode,
                            ShiftStartTime = EnsureValidSqlDateTime(d.ShiftStartTime, "09:00:00"),
                            ShiftEndTime = EnsureValidSqlDateTime(d.ShiftEndTime, "18:00:00"),
                            LateTime = EnsureValidSqlDateTime(d.LateTime, "09:15:00"),
                            AbsentTime = EnsureValidSqlDateTime(d.AbsentTime, "11:00:00"),
                            LunchOutTime = EnsureValidSqlDateTime(d.LunchOutTime, "13:00:00"),
                            LunchInTime = EnsureValidSqlDateTime(d.LunchInTime, "14:00:00"),
                            LunchBreakHour = d.LunchBreakHour,
                            EarlyLeaveTime = d.EarlyLeaveTime ?? "",
                            IsCrossMidnight = d.IsCrossMidnight,
                            CompanyIds = d.CompanyIdsList != null && d.CompanyIdsList.Any() ? string.Join(",", d.CompanyIdsList) : (d.CompanyIds ?? ""),
                            BranchIds = d.BranchIdsList != null && d.BranchIdsList.Any() ? string.Join(",", d.BranchIdsList) : (d.BranchIds ?? ""),
                            DepartmentIds = d.DepartmentIdsList != null && d.DepartmentIdsList.Any() ? string.Join(",", d.DepartmentIdsList) : (d.DepartmentIds ?? ""),
                            EmployeeIds = d.EmployeeIdsList != null && d.EmployeeIdsList.Any() ? string.Join(",", d.EmployeeIdsList) : (d.EmployeeIds ?? ""),
                            OvertimeRuleId = d.OvertimeRuleID ?? "",
                            OtGraceTime = d.OTGraceTime,
                            MaxOtPerDay = d.MaxOTPerDay,
                            MinOtMinutes = d.MinOTMinutes,
                            WeeklyOffOtRule = d.WeeklyOffOTRule ?? "Not Applicable",
                            HolidayOtRule = d.HolidayOTRule ?? "Not Applicable",
                            Wef = EnsureValidSqlDateTime(d.Wef != DateTime.MinValue ? d.Wef : DateTime.Now, "00:00:00"),
                            Remarks = d.Remarks ?? "",
                            IsActive = string.IsNullOrEmpty(d.IsActiveStatus) ? d.IsActive : (d.IsActiveStatus == "Active"),
                            Luser = entityVM.Luser ?? string.Empty,
                            Lip = entityVM.Lip ?? string.Empty,
                            Lmac = entityVM.Lmac ?? string.Empty,
                            Ldate = DateTime.Now
                        };
                        await shiftDetailsRepository.AddAsync(detailEntity);
                    }
                }

                await hRmAtdShiftRepository.CommitTransactionAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message: {ex.Message}");
                await hRmAtdShiftRepository.RollbackTransactionAsync();
                return false;
            }
        }

        public async Task<bool> UpdateAsync(HrmAtdShiftSetupViewModel entityVM)
        {
            await hRmAtdShiftRepository.BeginTransactionAsync();
            try
            {
                var entity = await hRmAtdShiftRepository.GetByIdAsync(entityVM.ShiftCode);
                if (entity == null)
                {
                    await hRmAtdShiftRepository.RollbackTransactionAsync();
                    return false;
                }

                entity.ShiftName = (entityVM.ShiftName ?? string.Empty).Trim();
                entity.ShiftShortName = (entityVM.Description ?? entityVM.ShiftShortName ?? string.Empty).Trim();
                entity.Remarks = (entityVM.Remarks ?? string.Empty).Trim();
                entity.ShiftTypeId = (entityVM.ShiftTypeId ?? string.Empty).Trim();
                entity.IsActive = string.IsNullOrEmpty(entityVM.IsActiveStatus) ? true : entityVM.IsActiveStatus == "Active";
                entity.ModifyDate = DateTime.Now;
                entity.Luser = entityVM.Luser ?? string.Empty;
                entity.Lip = entityVM.Lip ?? string.Empty;
                entity.Lmac = entityVM.Lmac ?? string.Empty;

                await hRmAtdShiftRepository.UpdateAsync(entity);

                // Synchronize details table
                if (entityVM.Details != null)
                {
                    var existingDetails = await shiftDetailsRepository.All()
                        .Where(x => x.ShiftCode == entityVM.ShiftCode)
                        .ToListAsync();

                    var incomingAutoIds = entityVM.Details
                        .Where(d => d.AutoId > 0)
                        .Select(d => d.AutoId)
                        .ToHashSet();

                    // Delete removed detail rows
                    foreach (var oldItem in existingDetails.Where(e => !incomingAutoIds.Contains(e.AutoId)))
                    {
                        shiftDetailsRepository.Delete(oldItem);
                    }

                    // Update existing or Insert new detail rows
                    foreach (var d in entityVM.Details)
                    {
                        var existing = d.AutoId > 0 ? existingDetails.FirstOrDefault(e => e.AutoId == d.AutoId) : null;
                        if (existing != null)
                        {
                            existing.ShiftStartTime = EnsureValidSqlDateTime(d.ShiftStartTime, "09:00:00");
                            existing.ShiftEndTime = EnsureValidSqlDateTime(d.ShiftEndTime, "18:00:00");
                            existing.LateTime = EnsureValidSqlDateTime(d.LateTime, "09:15:00");
                            existing.AbsentTime = EnsureValidSqlDateTime(d.AbsentTime, "11:00:00");
                            existing.LunchOutTime = EnsureValidSqlDateTime(d.LunchOutTime, "13:00:00");
                            existing.LunchInTime = EnsureValidSqlDateTime(d.LunchInTime, "14:00:00");
                            existing.LunchBreakHour = d.LunchBreakHour;
                            existing.EarlyLeaveTime = d.EarlyLeaveTime ?? "";
                            existing.IsCrossMidnight = d.IsCrossMidnight;
                            existing.CompanyIds = d.CompanyIdsList != null && d.CompanyIdsList.Any() ? string.Join(",", d.CompanyIdsList) : (d.CompanyIds ?? "");
                            existing.BranchIds = d.BranchIdsList != null && d.BranchIdsList.Any() ? string.Join(",", d.BranchIdsList) : (d.BranchIds ?? "");
                            existing.DepartmentIds = d.DepartmentIdsList != null && d.DepartmentIdsList.Any() ? string.Join(",", d.DepartmentIdsList) : (d.DepartmentIds ?? "");
                            existing.EmployeeIds = d.EmployeeIdsList != null && d.EmployeeIdsList.Any() ? string.Join(",", d.EmployeeIdsList) : (d.EmployeeIds ?? "");
                            existing.OvertimeRuleId = d.OvertimeRuleID ?? "";
                            existing.OtGraceTime = d.OTGraceTime;
                            existing.MaxOtPerDay = d.MaxOTPerDay;
                            existing.MinOtMinutes = d.MinOTMinutes;
                            existing.WeeklyOffOtRule = d.WeeklyOffOTRule ?? "Not Applicable";
                            existing.HolidayOtRule = d.HolidayOTRule ?? "Not Applicable";
                            existing.Wef = EnsureValidSqlDateTime(d.Wef != DateTime.MinValue ? d.Wef : DateTime.Now, "00:00:00");
                            existing.Remarks = d.Remarks ?? "";
                            existing.IsActive = string.IsNullOrEmpty(d.IsActiveStatus) ? d.IsActive : (d.IsActiveStatus == "Active");
                            existing.ModifyDate = DateTime.Now;
                            existing.Luser = entityVM.Luser ?? string.Empty;
                            existing.Lip = entityVM.Lip ?? string.Empty;
                            existing.Lmac = entityVM.Lmac ?? string.Empty;
                            await shiftDetailsRepository.UpdateAsync(existing);
                        }
                        else
                        {
                            var detailEntity = new HrmAtdShiftDetails
                            {
                                ShiftCode = entityVM.ShiftCode,
                                ShiftStartTime = EnsureValidSqlDateTime(d.ShiftStartTime, "09:00:00"),
                                ShiftEndTime = EnsureValidSqlDateTime(d.ShiftEndTime, "18:00:00"),
                                LateTime = EnsureValidSqlDateTime(d.LateTime, "09:15:00"),
                                AbsentTime = EnsureValidSqlDateTime(d.AbsentTime, "11:00:00"),
                                LunchOutTime = EnsureValidSqlDateTime(d.LunchOutTime, "13:00:00"),
                                LunchInTime = EnsureValidSqlDateTime(d.LunchInTime, "14:00:00"),
                                LunchBreakHour = d.LunchBreakHour,
                                EarlyLeaveTime = d.EarlyLeaveTime ?? "",
                                IsCrossMidnight = d.IsCrossMidnight,
                                CompanyIds = d.CompanyIdsList != null && d.CompanyIdsList.Any() ? string.Join(",", d.CompanyIdsList) : (d.CompanyIds ?? ""),
                                BranchIds = d.BranchIdsList != null && d.BranchIdsList.Any() ? string.Join(",", d.BranchIdsList) : (d.BranchIds ?? ""),
                                DepartmentIds = d.DepartmentIdsList != null && d.DepartmentIdsList.Any() ? string.Join(",", d.DepartmentIdsList) : (d.DepartmentIds ?? ""),
                                EmployeeIds = d.EmployeeIdsList != null && d.EmployeeIdsList.Any() ? string.Join(",", d.EmployeeIdsList) : (d.EmployeeIds ?? ""),
                                OvertimeRuleId = d.OvertimeRuleID ?? "",
                                OtGraceTime = d.OTGraceTime,
                                MaxOtPerDay = d.MaxOTPerDay,
                                MinOtMinutes = d.MinOTMinutes,
                                WeeklyOffOtRule = d.WeeklyOffOTRule ?? "Not Applicable",
                                HolidayOtRule = d.HolidayOTRule ?? "Not Applicable",
                                Wef = EnsureValidSqlDateTime(d.Wef != DateTime.MinValue ? d.Wef : DateTime.Now, "00:00:00"),
                                Remarks = d.Remarks ?? "",
                                IsActive = string.IsNullOrEmpty(d.IsActiveStatus) ? d.IsActive : (d.IsActiveStatus == "Active"),
                                Luser = entityVM.Luser ?? string.Empty,
                                Lip = entityVM.Lip ?? string.Empty,
                                Lmac = entityVM.Lmac ?? string.Empty,
                                Ldate = DateTime.Now
                            };
                            await shiftDetailsRepository.AddAsync(detailEntity);
                        }
                    }
                }

                await hRmAtdShiftRepository.CommitTransactionAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred : {ex.Message}");
                await hRmAtdShiftRepository.RollbackTransactionAsync();
                return false;
            }
        }

        public async Task<bool> SaveDetailAsync(HrmAtdShiftDetailViewModel d)
        {
            try
            {
                if (d.AutoId > 0)
                {
                    var existing = await shiftDetailsRepository.All().FirstOrDefaultAsync(x => x.AutoId == d.AutoId);
                    if (existing == null) return false;

                    existing.ShiftStartTime = EnsureValidSqlDateTime(d.ShiftStartTime, "09:00:00");
                    existing.ShiftEndTime = EnsureValidSqlDateTime(d.ShiftEndTime, "18:00:00");
                    existing.LateTime = EnsureValidSqlDateTime(d.LateTime, "09:15:00");
                    existing.AbsentTime = EnsureValidSqlDateTime(d.AbsentTime, "11:00:00");
                    existing.LunchOutTime = EnsureValidSqlDateTime(d.LunchOutTime, "13:00:00");
                    existing.LunchInTime = EnsureValidSqlDateTime(d.LunchInTime, "14:00:00");
                    existing.LunchBreakHour = d.LunchBreakHour;
                    existing.EarlyLeaveTime = d.EarlyLeaveTime ?? "";
                    existing.IsCrossMidnight = d.IsCrossMidnight;
                    existing.CompanyIds = d.CompanyIdsList != null && d.CompanyIdsList.Any() ? string.Join(",", d.CompanyIdsList) : (d.CompanyIds ?? "");
                    existing.BranchIds = d.BranchIdsList != null && d.BranchIdsList.Any() ? string.Join(",", d.BranchIdsList) : (d.BranchIds ?? "");
                    existing.DepartmentIds = d.DepartmentIdsList != null && d.DepartmentIdsList.Any() ? string.Join(",", d.DepartmentIdsList) : (d.DepartmentIds ?? "");
                    existing.EmployeeIds = d.EmployeeIdsList != null && d.EmployeeIdsList.Any() ? string.Join(",", d.EmployeeIdsList) : (d.EmployeeIds ?? "");
                    existing.OvertimeRuleId = d.OvertimeRuleID ?? "";
                    existing.OtGraceTime = d.OTGraceTime;
                    existing.MaxOtPerDay = d.MaxOTPerDay;
                    existing.MinOtMinutes = d.MinOTMinutes;
                    existing.WeeklyOffOtRule = d.WeeklyOffOTRule ?? "Not Applicable";
                    existing.HolidayOtRule = d.HolidayOTRule ?? "Not Applicable";
                    existing.Wef = EnsureValidSqlDateTime(d.Wef != DateTime.MinValue ? d.Wef : DateTime.Now, "00:00:00");
                    existing.Remarks = d.Remarks ?? "";
                    existing.IsActive = string.IsNullOrEmpty(d.IsActiveStatus) ? d.IsActive : (d.IsActiveStatus == "Active");
                    existing.ModifyDate = DateTime.Now;
                    existing.Luser = d.Luser ?? string.Empty;
                    existing.Lip = d.Lip ?? string.Empty;
                    existing.Lmac = d.Lmac ?? string.Empty;
                    await shiftDetailsRepository.UpdateAsync(existing);
                }
                else
                {
                    var detailEntity = new HrmAtdShiftDetails
                    {
                        ShiftCode = d.ShiftCode,
                        ShiftStartTime = EnsureValidSqlDateTime(d.ShiftStartTime, "09:00:00"),
                        ShiftEndTime = EnsureValidSqlDateTime(d.ShiftEndTime, "18:00:00"),
                        LateTime = EnsureValidSqlDateTime(d.LateTime, "09:15:00"),
                        AbsentTime = EnsureValidSqlDateTime(d.AbsentTime, "11:00:00"),
                        LunchOutTime = EnsureValidSqlDateTime(d.LunchOutTime, "13:00:00"),
                        LunchInTime = EnsureValidSqlDateTime(d.LunchInTime, "14:00:00"),
                        LunchBreakHour = d.LunchBreakHour,
                        EarlyLeaveTime = d.EarlyLeaveTime ?? "",
                        IsCrossMidnight = d.IsCrossMidnight,
                        CompanyIds = d.CompanyIdsList != null && d.CompanyIdsList.Any() ? string.Join(",", d.CompanyIdsList) : (d.CompanyIds ?? ""),
                        BranchIds = d.BranchIdsList != null && d.BranchIdsList.Any() ? string.Join(",", d.BranchIdsList) : (d.BranchIds ?? ""),
                        DepartmentIds = d.DepartmentIdsList != null && d.DepartmentIdsList.Any() ? string.Join(",", d.DepartmentIdsList) : (d.DepartmentIds ?? ""),
                        EmployeeIds = d.EmployeeIdsList != null && d.EmployeeIdsList.Any() ? string.Join(",", d.EmployeeIdsList) : (d.EmployeeIds ?? ""),
                        OvertimeRuleId = d.OvertimeRuleID ?? "",
                        OtGraceTime = d.OTGraceTime,
                        MaxOtPerDay = d.MaxOTPerDay,
                        MinOtMinutes = d.MinOTMinutes,
                        WeeklyOffOtRule = d.WeeklyOffOTRule ?? "Not Applicable",
                        HolidayOtRule = d.HolidayOTRule ?? "Not Applicable",
                        Wef = EnsureValidSqlDateTime(d.Wef != DateTime.MinValue ? d.Wef : DateTime.Now, "00:00:00"),
                        Remarks = d.Remarks ?? "",
                        IsActive = string.IsNullOrEmpty(d.IsActiveStatus) ? d.IsActive : (d.IsActiveStatus == "Active"),
                        Luser = d.Luser ?? string.Empty,
                        Lip = d.Lip ?? string.Empty,
                        Lmac = d.Lmac ?? string.Empty,
                        Ldate = DateTime.Now
                    };
                    await shiftDetailsRepository.AddAsync(detailEntity);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving detail: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteDetailAsync(decimal detailAutoId)
        {
            var entity = await shiftDetailsRepository.All().FirstOrDefaultAsync(x => x.AutoId == detailAutoId);
            if (entity != null)
            {
                shiftDetailsRepository.Delete(entity);
                return true;
            }
            return false;
        }

        public bool DeleteLeaveType(string id)
        {
            var entity = GetLeaveType(id);
            if (entity != null)
            {
                var details = shiftDetailsRepository.All().Where(x => x.ShiftCode == id).ToList();
                foreach (var d in details)
                {
                    shiftDetailsRepository.Delete(d);
                }
                hRmAtdShiftRepository.Delete(entity);
                return true;
            }
            return false;
        }

        public HrmAtdShift GetLeaveType(string code)
        {
            return hRmAtdShiftRepository.GetById(code);
        }

        public async Task<IEnumerable<CommonSelectModel>> SelectionHrmAtdShiftAsync()
        {
            return await hRmAtdShiftRepository.All()
                      .Select(x => new CommonSelectModel
                      {
                          Code = x.ShiftCode,
                          Name = x.ShiftName
                      })
                      .ToListAsync();
        }

        public async Task<string> GenerateNextCode()
        {
            var leaveTypeCode = await hRmAtdShiftRepository.GetAllAsync();
            int maxNum = 0;
            foreach (var item in leaveTypeCode)
            {
                if (!string.IsNullOrEmpty(item.ShiftCode) && int.TryParse(item.ShiftCode.TrimStart('0'), out int parsed))
                {
                    if (parsed > maxNum) maxNum = parsed;
                }
            }
            return (maxNum + 1).ToString();
        }

        #region Duplicate Check 
        public async Task<bool> IsExistByCodeAsync(string code)
        {
            var c = (code ?? "").Trim();
            return await hRmAtdShiftRepository.All().AnyAsync(x => x.ShiftCode == c);
        }

        public async Task<bool> IsExistAsync(string name)
        {
            var n = (name ?? "").Trim().ToLower();
            return await hRmAtdShiftRepository.All().AnyAsync(x => x.ShiftName.ToLower() == n);
        }

        public async Task<bool> IsExistAsync(string name, string typeCode)
        {
            var n = (name ?? "").Trim().ToLower();
            var c = (typeCode ?? "").Trim();
            return await hRmAtdShiftRepository.All().AnyAsync(x => x.ShiftName.ToLower() == n && x.ShiftCode != c);
        }

        #endregion

        #region Permission all type
        public async Task<bool> PagePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Shift Information" && x.TitleCheck);
        }

        public async Task<bool> SavePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Shift Information" && x.CheckAdd);
        }

        public async Task<bool> UpdatePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Shift Information" && x.CheckEdit);
        }

        public async Task<bool> DeletePermissionAsync(string accessCode)
        {
            return await accessCodeRepository.All().AnyAsync(x => x.AccessCodeId == accessCode && x.Title == "Shift Information" && x.CheckDelete);
        }
        #endregion
    }
}
