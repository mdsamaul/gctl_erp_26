using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml.Style;

namespace GCTL.Core.ViewModels.HrmLeaveApplicationEntry
{
    public class ColumnSetting
    {
        public double Width { get; set; }
        public ExcelHorizontalAlignment Alignment { get; set; }
        public bool WrapText { get; set; }
    }
}
