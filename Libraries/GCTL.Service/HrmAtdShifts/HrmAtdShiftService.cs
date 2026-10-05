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
        private readonly IRepository<HrmDefShiftType> shiftService;
        private readonly IRepository<CoreAccessCode> accessCodeRepository;
        private readonly IConfiguration _configuration;

        public HrmAtdShiftService(IRepository<HrmAtdShift> hRmAtdShiftRepository, IRepository<HrmDefShiftType> shiftService, IRepository<CoreAccessCode> accessCodeRepository, IConfiguration configuration)
    : base(hRmAtdShiftRepository)
        {
            this.hRmAtdShiftRepository = hRmAtdShiftRepository;
            this.shiftService = shiftService;
            this.accessCodeRepository = accessCodeRepository;
            this._configuration = configuration;
        }


        public async Task<HrmAtdShiftSetupViewModel> GetByIdAsync(string code)
        {
            var entity = await hRmAtdShiftRepository.GetByIdAsync(code);
            if (entity == null)
            {
                return null;
            }
            return new HrmAtdShiftSetupViewModel

            {
                AutoId = entity.AutoId, //id
                ShiftCode = entity.ShiftCode,
                ShiftName = entity.ShiftName,
                ShiftShortName = entity.ShiftShortName,
                Remarks = entity.Remarks,
                //Description = entity.Description,
                ShiftStartTime = entity.ShiftStartTime,
                ShiftEndTime = entity.ShiftEndTime,
                AbsentTime = entity.AbsentTime,
                LateTime = entity.LateTime,
                ShiftTypeId = entity.ShiftTypeId,
                Wef = entity.Wef,
                Ldate = entity.Ldate,
                ModifyDate = entity.ModifyDate,
                Luser = entity.Luser,
                Lip = entity.Lip,
                Lmac = entity.Lmac,
                LunchInTime = entity.LunchInTime,
                LunchOutTime = entity.LunchOutTime,
                LunchBreakHour = entity.LunchBreakHour,
                EarlyLeaveTime = entity.EarlyLeaveTime,
                IsCrossMidnight = entity.IsCrossMidnight ?? false,
                CompanyIds = entity.CompanyIds,
                BranchIds = entity.BranchIds,
                DepartmentIds = entity.DepartmentIds,
                EmployeeIds = entity.EmployeeIds,
                CompanyIdsList = !string.IsNullOrEmpty(entity.CompanyIds) ? entity.CompanyIds.Split(',').ToList() : new List<string>(),
                BranchIdsList = !string.IsNullOrEmpty(entity.BranchIds) ? entity.BranchIds.Split(',').ToList() : new List<string>(),
                DepartmentIdsList = !string.IsNullOrEmpty(entity.DepartmentIds) ? entity.DepartmentIds.Split(',').ToList() : new List<string>(),
                EmployeeIdsList = !string.IsNullOrEmpty(entity.EmployeeIds) ? entity.EmployeeIds.Split(',').ToList() : new List<string>(),
                OvertimeRuleID = entity.OvertimeRuleId,
                IsActive = entity.IsActive ?? false,
                IsActiveStatus = (entity.IsActive == true) ? "Active" : "Inactive"
            };
        }
        public async Task<List<HrmAtdShiftSetupViewModel>> GetAllAsync()
        {
            var entity = await hRmAtdShiftRepository.GetAllAsync();
            return entity.Select(entityVM => new HrmAtdShiftSetupViewModel
            {
                AutoId = entityVM.AutoId,
                ShiftCode = entityVM.ShiftCode,
                ShiftTypeId = entityVM.ShiftTypeId,
                ShiftTypeName = shiftService.All().Where(w => w.ShiftTypeId == entityVM.ShiftTypeId).Select(x => x.ShiftTypeName).FirstOrDefault() ?? "",
                ShiftName = entityVM.ShiftName,
                ShiftShortName = entityVM.ShiftShortName,
                Remarks = entityVM.Remarks,
                Description = entityVM.ShiftShortName,
                ShiftStartTime = entityVM.ShiftStartTime,
                ShiftEndTime = entityVM.ShiftEndTime,
                AbsentTime = entityVM.AbsentTime,
                LateTime = entityVM.LateTime,
                Ldate = entityVM.Ldate,
                ModifyDate = entityVM.ModifyDate,
                Wef = entityVM.Wef,
                Luser = entityVM.Luser,
                Lip = entityVM.Lip,
                Lmac = entityVM.Lmac,
                LunchInTime = entityVM.LunchInTime,
                LunchOutTime = entityVM.LunchOutTime,
                LunchBreakHour = entityVM.LunchBreakHour,
                EarlyLeaveTime = entityVM.EarlyLeaveTime,
                IsCrossMidnight = entityVM.IsCrossMidnight ?? false,
                CompanyIds = entityVM.CompanyIds,
                BranchIds = entityVM.BranchIds,
                DepartmentIds = entityVM.DepartmentIds,
                EmployeeIds = entityVM.EmployeeIds,
                OvertimeRuleID = entityVM.OvertimeRuleId,
                IsActive = entityVM.IsActive ?? false,
                IsActiveStatus = (entityVM.IsActive == true) ? "Active" : "Inactive"

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

            var result = await connection.QueryAsync<HrmAtdShiftSetupViewModel>("SP_HrmAtdShift_GetList", parameters, commandType: CommandType.StoredProcedure);
            return result.ToList();
        }


        public async Task<bool> SaveAsync(HrmAtdShiftSetupViewModel entityVM)
        {
            await hRmAtdShiftRepository.BeginTransactionAsync();
            try
            {
                // Check if shift with same name or shift code already exists
                //string nextShiftCode = await GenerateNextCode();

                //var existingShift = await hRmAtdShiftRepository
                //    .FindByAsync(x => x.ShiftCode == nextShiftCode);

                //if (existingShift != null)
                //{
                //    // ShiftCode already used
                //    throw new InvalidOperationException($"ShiftCode '{nextShiftCode}' already exists.");
                //}

                HrmAtdShift entity = new HrmAtdShift
                {
                    ShiftCode = await GenerateNextCode(),
                    ShiftName = entityVM.ShiftName,
                    ShiftShortName = entityVM.Description ?? string.Empty,
                    Remarks = entityVM.Remarks ?? string.Empty,
                    ShiftStartTime = entityVM.ShiftStartTime,
                    ShiftEndTime = entityVM.ShiftEndTime,
                    AbsentTime = entityVM.AbsentTime,
                    LateTime = entityVM.LateTime,
                    Luser = entityVM.Luser ?? string.Empty,
                    Lip = entityVM.Lip ?? string.Empty,
                    Lmac = entityVM.Lmac ?? string.Empty,
                    Wef = entityVM.Wef,
                    ShiftTypeId = entityVM.ShiftTypeId ?? string.Empty,
                    Ldate = DateTime.Now,
                    LunchInTime = entityVM.LunchInTime,
                    LunchOutTime = entityVM.LunchOutTime,
                    LunchBreakHour = entityVM.LunchBreakHour,
                    EarlyLeaveTime = entityVM.EarlyLeaveTime ?? "",
                    IsCrossMidnight = entityVM.IsCrossMidnight,
                    CompanyIds = entityVM.CompanyIdsList != null ? string.Join(",", entityVM.CompanyIdsList) : "",
                    BranchIds = entityVM.BranchIdsList != null ? string.Join(",", entityVM.BranchIdsList) : "",
                    DepartmentIds = entityVM.DepartmentIdsList != null ? string.Join(",", entityVM.DepartmentIdsList) : "",
                    EmployeeIds = entityVM.EmployeeIdsList != null ? string.Join(",", entityVM.EmployeeIdsList) : "",
                    OvertimeRuleId = entityVM.OvertimeRuleID ?? "",
                    IsActive = entityVM.IsActiveStatus == "Active"
                };




                await hRmAtdShiftRepository.AddAsync(entity);
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

                entity.ShiftCode = entityVM.ShiftCode;
                entity.ShiftName = entityVM.ShiftName;
                entity.ShiftShortName = entityVM.ShiftShortName ?? string.Empty;
                entity.ShiftShortName = entityVM.Description ?? string.Empty;
                entity.Remarks = entityVM.Remarks ?? string.Empty;
                entity.ShiftStartTime = entityVM.ShiftStartTime;
                entity.ShiftEndTime = entityVM.ShiftEndTime;
                entity.AbsentTime = entityVM.AbsentTime;
                entity.LateTime = entityVM.LateTime;
                entity.Luser = entityVM.Luser ?? string.Empty;
                entity.Lip = entityVM.Lip ?? string.Empty;
                entity.Lmac = entityVM.Lmac ?? string.Empty;
                entity.Wef = entityVM.Wef;
                entity.ShiftTypeId = entityVM.ShiftTypeId ?? string.Empty;
                entity.ModifyDate = DateTime.Now;
                entity.LunchInTime = entityVM.LunchInTime;
                entity.LunchOutTime = entityVM.LunchOutTime;
                entity.LunchBreakHour = entityVM.LunchBreakHour;
                entity.EarlyLeaveTime = entityVM.EarlyLeaveTime ?? "";
                entity.IsCrossMidnight = entityVM.IsCrossMidnight;
                entity.CompanyIds = entityVM.CompanyIdsList != null ? string.Join(",", entityVM.CompanyIdsList) : "";
                entity.BranchIds = entityVM.BranchIdsList != null ? string.Join(",", entityVM.BranchIdsList) : "";
                entity.DepartmentIds = entityVM.DepartmentIdsList != null ? string.Join(",", entityVM.DepartmentIdsList) : "";
                entity.EmployeeIds = entityVM.EmployeeIdsList != null ? string.Join(",", entityVM.EmployeeIdsList) : "";
                entity.OvertimeRuleId = entityVM.OvertimeRuleID ?? "";
                entity.IsActive = entityVM.IsActiveStatus == "Active";
                await hRmAtdShiftRepository.UpdateAsync(entity);
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


        public bool DeleteLeaveType(string id)
        {
            var entity = GetLeaveType(id);
            if (entity != null)
            {
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
            var lastCode = leaveTypeCode.Max(b => b.ShiftCode);
            int nextCode = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                int lastNumber = int.Parse(lastCode.TrimStart('0'));
                lastNumber++;
                nextCode = lastNumber;
            }
            return nextCode.ToString();

        }

        //public async Task<string> GenerateNextCode()
        //{

        //    var customers = await hRmAtdShiftRepository.GetAllAsync();
        //    var lastCode = customers.Max(b => b.ShiftCode);
        //    string nextCode = "SC01";
        //    if (!string.IsNullOrEmpty(lastCode))
        //    {
        //        int lastNumber = int.Parse(lastCode.Substring(2));
        //        lastNumber++;
        //        nextCode = $"SC{lastNumber:D2}";
        //    }

        //    return nextCode;
        //}

        #region Duplicate Check 
        public async Task<bool> IsExistByCodeAsync(string code)
        {
            return await hRmAtdShiftRepository.All().AnyAsync(x => x.ShiftCode == code);
        }

        public async Task<bool> IsExistAsync(string name)
        {
            return await hRmAtdShiftRepository.All().AnyAsync(x => x.ShiftName == name);
        }

        public async Task<bool> IsExistAsync(string name, string typeCode)
        {
            return await hRmAtdShiftRepository.All().AnyAsync(x => x.ShiftName == name && x.ShiftCode != typeCode);
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
