using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;

namespace GCTL.Service.LeaveAppDay
{
    public class LeaveAppicationDayService : ILeaveApplicationDay
    {
        private readonly IRepository<HrmLeaveApplicationDays> _leaveDaysRepository;

        public LeaveAppicationDayService(IRepository<HrmLeaveApplicationDays> leaveDaysRepository)
        {
            _leaveDaysRepository = leaveDaysRepository;
        }

        public async Task<bool> DeleteAsync(string entryId)
        {
            try
            {

                var entry = await _leaveDaysRepository.FindByAsync(e => e.LeaveAppEntryId == entryId);

                if (entry != null)
                {
                    _leaveDaysRepository.Delete(entry);
                    return true;
                }


              

                return false;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<bool> SaveAsync(LeaveApplicationViewModel model)
        {
            try
            {
              
                HrmLeaveApplicationDays leaveDays = new HrmLeaveApplicationDays();

                foreach (var item in model.SelectedDates)
                {
                    leaveDays.AutoId = 0;
                    leaveDays.LeaveAppEntryId = model.EntryId;
                    var a = Convert.ToDateTime(item);
                    leaveDays.Days = a;

                    await _leaveDaysRepository.AddAsync(leaveDays);

                }

                await _leaveDaysRepository.CommitTransactionAsync();

                return true;
            }
            catch (Exception)
            {

                throw;
            }
            

        }

        public async Task<bool> UpdateAsync(LeaveApplicationViewModel model)
        {
            try
            {
                

                var entry = await _leaveDaysRepository.FindByAsync(e => e.LeaveAppEntryId == model.EntryId); 

                if (entry != null)
                {
                    _leaveDaysRepository.Delete(entry);
                }
                else
                {
                    return true;
                }


                HrmLeaveApplicationDays leaveDays = new HrmLeaveApplicationDays();

                foreach (var item in model.SelectedDates)
                {
                    leaveDays.AutoId = 0;
                    leaveDays.LeaveAppEntryId = model.EntryId;
                    var a = Convert.ToDateTime(item);
                    leaveDays.Days = a;

                    await _leaveDaysRepository.AddAsync(leaveDays);

                }

                await _leaveDaysRepository.CommitTransactionAsync();

                return true;


            }
            catch (Exception)
            {

                throw;
            }


        }
    }
}
