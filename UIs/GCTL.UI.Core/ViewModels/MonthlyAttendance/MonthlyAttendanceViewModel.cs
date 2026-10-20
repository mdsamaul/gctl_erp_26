// ═══════════════════════════════════════════════════════════════════
// File: GCTL.UI.Core/ViewModels/MonthlyAttendance/MonthlyAttendanceViewModel.cs
// ═══════════════════════════════════════════════════════════════════
using GCTL.Core.ViewModels;
using GCTL.Core.ViewModels.MonthlyAttendance;

namespace GCTL.UI.Core.ViewModels.MonthlyAttendance
{
    public class MonthlyAttendanceViewModel : BaseViewModel
    {
        public MonthlyAttendanceFilterDto Setup { get; set; } = new MonthlyAttendanceFilterDto();
    }
}
