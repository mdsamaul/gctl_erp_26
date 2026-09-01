using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.SalaryInformationReport
{
    public class SalaryInformationReportIncentivesDto
    {
        public int SL { get; set; }
        public string IdNo { get; set; }
        public string PayId { get; set; }
        public string NameOfTheEmployee { get; set; }
        public string Status { get; set; }
        public string Department { get; set; }
        public string DateOfHire { get; set; }
        public string Dot { get; set; }
        public string Dbbl { get; set; }
        public string BankAccountNo { get; set; }
        public decimal? Salary { get; set; }
        public string Designation { get; set; }
        public decimal? Incentives { get; set; }
        public string Eligibility { get; set; }
        public string Notes { get; set; }
    }
}
