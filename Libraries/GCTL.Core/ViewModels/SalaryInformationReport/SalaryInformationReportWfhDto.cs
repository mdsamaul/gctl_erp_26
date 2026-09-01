using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.SalaryInformationReport
{
    public class SalaryInformationReportWfhDto
    {
        public int SL { get; set; }
        public string IdNo { get; set; }
        public string PayId { get; set; }
        public string NameOfTheEmployee { get; set; }
        public string Status { get; set; }
        public string Department { get; set; }
        public string DateOfHire { get; set; }
        public string Dot { get; set; }
        public string BankAcNo { get; set; }
        public string BankAcNoUCBL { get; set; }
        public decimal? GrossSalary { get; set; }
        public decimal? Internet { get; set; }
        public decimal? Carrying { get; set; }
        public decimal? Others { get; set; }
        public decimal? Total { get; set; }
        public string Note { get; set; }
    }
}
