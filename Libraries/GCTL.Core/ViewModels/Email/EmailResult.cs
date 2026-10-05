using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCTL.Core.ViewModels.Email
{
    public class EmailResult
    {
        public string Email { get; set; }
        public bool isSuccess { get; set; }
        public string Error { get; set; }
    }
}
