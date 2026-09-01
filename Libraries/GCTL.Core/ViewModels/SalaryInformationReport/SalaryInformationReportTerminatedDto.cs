using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.SalaryInformationReport
{
    public class SalaryInformationReportTerminatedDto
    {
        public int SL { get; set; }
        public string IdNo { get; set; }
        public string PayId { get; set; }
        public string NameOfTheEmployee { get; set; }
        public string Department { get; set; }
        public string Status { get; set; }
        public string DateOfHire { get; set; }
        public string Dot { get; set; }
        public string Dbbl { get; set; }
        public string Ucbl { get; set; }
        public string Duration { get; set; }
        public decimal? PfEligiblity { get; set; }
        public int? Days { get; set; }
        public string Note { get; set; }
    }
}
