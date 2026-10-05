using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GCTL.Core.Data;
using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
using GCTL.Data.Models;
using GCTL.Service.EmployeeOfficialInfo;
using GCTL.Service.HrmLeaveApplicationEntrys;
using GCTL.Service.LeaveTypes;

namespace GCTL.Service.EmailService
{
    public class LeaveEmailService : ILeaveEmailService
    {
        private readonly IEmployeeOfficialInfoService _employeeOfficialInfoService;
        private readonly IRepository<HrmAtdLeaveType> _leaveTypeRepository;
        private readonly IRepository<HrmLeaveApplicationEntry> _leaveEntryRepository;
        private readonly IRepository<HrmEmployee> _employeeRepository;

        public LeaveEmailService(IEmployeeOfficialInfoService employeeOfficialInfoService, IRepository<HrmAtdLeaveType> leaveTypeRepository, IRepository<HrmLeaveApplicationEntry> leaveEntryRepository, IRepository<HrmEmployee> employeeRepository)
        {
            _employeeOfficialInfoService = employeeOfficialInfoService;
            _leaveTypeRepository = leaveTypeRepository;
            _leaveEntryRepository = leaveEntryRepository;
            _employeeRepository = employeeRepository;
        }

        // ─── Shared inline style constants ────────────────────────────────────────
        private const string BodyStyle = "margin:0;padding:0;font-family:Arial,sans-serif;background-color:#f8f9fa;";
        private const string WrapTdStyle = "padding:20px;";
        private const string BoxStyle = "background:#ffffff;padding:20px;border:1px solid #ccc;";
        private const string ThStyle = "font-family:Arial,sans-serif;background-color:#f2f2f2;border:1px solid #ddd;padding:8px;text-align:left;font-weight:bold;";
        private const string TdStyle = "font-family:Arial,sans-serif;border:1px solid #ddd;padding:8px;";
        private const string ParaStyle = "font-family:Arial,sans-serif;margin:8px 0;color:#212529;";
        private const string TableStyle = "width:100%;border-collapse:collapse;margin:16px 0;";
        private const string BlueThStyle = "font-family:Arial,sans-serif;background-color:#007bff;color:#ffffff;border:1px solid #007bff;padding:8px;text-align:left;font-weight:bold;";
        private const string BtnStyle = "display:inline-block;padding:10px 24px;background-color:#007bff;color:#ffffff;text-decoration:none;font-family:Arial,sans-serif;font-size:14px;border-radius:4px;";

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private string WrapEmail(string innerHtml) => $@"
<!DOCTYPE html>
<html>
<body style=""{BodyStyle}"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
    <tr><td align=""center"" style=""{WrapTdStyle}"">
      <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""{BoxStyle}"">
        <tr><td>{innerHtml}</td></tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";

        private string DetailRow(string label, string value) =>
            $@"<tr>
              <th style=""{ThStyle}"">{label}</th>
              <td style=""{TdStyle}"">{value}</td>
            </tr>";

        private string BlueDetailRow(string label, string value) =>
            $@"<tr>
              <td style=""{TdStyle}"">{label}</td>
              <td style=""{TdStyle}"">{value}</td>
            </tr>";

        // ─── Generate Leave Request Email (Employee copy) ──────────────────────
        public string GenerateLeaveRequestEmail(LeaveApplicationViewModel leaveApplication)
        {
            string formattedFromDate = leaveApplication.FromDate?.ToString("yyyy-MM-dd") ?? "N/A";
            string formattedToDate = leaveApplication.ToDate?.ToString("yyyy-MM-dd") ?? "N/A";
            string formattedSelectedDates = leaveApplication.SelectedDates != null && leaveApplication.SelectedDates.Any()
                ? string.Join(", ", leaveApplication.SelectedDates.Select(d => d.ToString("yyyy-MM-dd")))
                : "N/A";

            string inner = $@"
              <h1 style=""color:#5a5a5a;font-family:Arial,sans-serif;margin:0 0 16px 0;"">Leave Request Submitted</h1>
              <p style=""{ParaStyle}"">Dear {leaveApplication.EmployeeName} ({leaveApplication.EmployeeId}),</p>
              <p style=""{ParaStyle}"">We have received your leave request with the following details:</p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#ddd;"">
                {DetailRow("Leave ID", leaveApplication.EntryId ?? "N/A")}
                {DetailRow("Leave Format", leaveApplication.LeaveFormat ?? "N/A")}
                {DetailRow("Leave Type", leaveApplication.LeaveType ?? "N/A")}
                {DetailRow("From Date", formattedFromDate)}
                {DetailRow("To Date", formattedToDate)}
                {DetailRow("Selected Dates", formattedSelectedDates)}
                {DetailRow("Reason", leaveApplication.Reason ?? "N/A")}
              </table>
              <p style=""{ParaStyle}"">Thank you,<br/>The HR Team</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Leave Approval Email (Employee copy) ─────────────────────
        public string GenerateLeaveApprovalEmail(LeaveEntryGetViewModel leaveApplication)
        {
            string formattedFromDate = leaveApplication.StartDate.ToString("yyyy-MM-dd");
            string formattedToDate = leaveApplication.EndDate.ToString("yyyy-MM-dd");

            var leaveTypeName = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId)?.Name ?? "N/A";

            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
                : "N/A";

            var emp = _employeeRepository.GetAll()
                .FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);

            string inner = $@"
              <h1 style=""color:#28a745;font-family:Arial,sans-serif;margin:0 0 16px 0;"">Leave Application Approved</h1>
              <p style=""{ParaStyle}"">Dear {emp?.FirstName} {emp?.LastName} ({emp?.EmployeeId}),</p>
              <p style=""{ParaStyle}"">Your leave application has been approved by management.</p>
              <p style=""{ParaStyle}"">Details as follows:</p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#ddd;"">
                {DetailRow("Approved By", "Khandaker Fazle Rabbi")}
                {DetailRow("Leave Type", leaveTypeName)}
                {DetailRow("Number Of Days", leaveApplication.NoOfDay.ToString())}
                {DetailRow("Details Of Days", formattedSelectedDates)}
                {DetailRow("Comments", leaveApplication.HRApprovalRemarks ?? "N/A")}
              </table>
              <p style=""{ParaStyle}"">Regards,<br/>The HR Team</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Leave Approval Office Notification (Supervisor copy) ─────
        public string GenerateLeaveApprovalOfcNotify(LeaveEntryGetViewModel leaveApplication, HrmEmployeeOfficialInfo supOfc)
        {
            var mailTo = _employeeRepository.GetAll()
                .FirstOrDefault(e => e.EmployeeId == supOfc.EmployeeId);

            var leaveTypeName = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId)?.Name ?? "N/A";

            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
                : "N/A";

            var emp = _employeeRepository.GetAll()
                .FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);

            string inner = $@"
              <h1 style=""color:#28a745;font-family:Arial,sans-serif;margin:0 0 16px 0;"">Leave Application Approved</h1>
              <p style=""{ParaStyle}"">Dear {mailTo?.FirstName} {mailTo?.LastName} ({mailTo?.EmployeeId}),</p>
              <p style=""{ParaStyle}"">Leave application of {emp?.FirstName} {emp?.LastName} ({emp?.EmployeeId}) has been approved by management.</p>
              <p style=""{ParaStyle}"">Details as follows:</p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#ddd;"">
                {DetailRow("Approved By", "Khandaker Fazle Rabbi")}
                {DetailRow("Leave Type", leaveTypeName)}
                {DetailRow("Number Of Days", leaveApplication.NoOfDay.ToString())}
                {DetailRow("Details Of Days", formattedSelectedDates)}
                {DetailRow("Comments", leaveApplication.HRApprovalRemarks ?? "N/A")}
              </table>
              <p style=""{ParaStyle}"">Regards,<br/>The HR Team</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Leave Cancel Email (Employee copy) ───────────────────────
        public string GenerateLeaveCancelEmail(LeaveEntryGetViewModel leaveApplication, string cancelledBy)
        {
            var leaveTypeName = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId)?.Name ?? "N/A";

            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
                : "N/A";

            var emp = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);
            var cancelByEmp = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == cancelledBy);

            string remarks = "Leave Cancelled";
            if (leaveApplication.HRApprovalRemarks != null)
                remarks = leaveApplication.HRApprovalRemarks;
            else if (leaveApplication.HODApprovalRemarks != null)
                remarks = leaveApplication.HODApprovalRemarks;
            else if (leaveApplication.ConfirmationRemarks != null)
                remarks = leaveApplication.ConfirmationRemarks;

            string inner = $@"
              <h1 style=""color:#dc3545;font-family:Arial,sans-serif;margin:0 0 16px 0;"">Leave Application Cancelled</h1>
              <p style=""{ParaStyle}"">Dear {emp?.FirstName} {emp?.LastName},</p>
              <p style=""{ParaStyle}"">Your leave application has been cancelled by management.</p>
              <p style=""{ParaStyle}"">Details as follows:</p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#ddd;"">
                {DetailRow("Cancelled By", $"{cancelByEmp?.FirstName} {cancelByEmp?.LastName}")}
                {DetailRow("Leave Type", leaveTypeName)}
                {DetailRow("Number Of Days", leaveApplication.NoOfDay.ToString())}
                {DetailRow("Details Of Days", formattedSelectedDates)}
                {DetailRow("Comments", remarks)}
              </table>
              <p style=""{ParaStyle}"">Regards,<br/>The HR Team</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Leave Cancel Office Notification (Supervisor copy) ───────
        public string GenerateLeaveCancelOfcNotify(LeaveEntryGetViewModel leaveApplication, HrmEmployeeOfficialInfo supOfc)
        {
            var mailTo = _employeeRepository.GetAll()
                .FirstOrDefault(e => e.EmployeeId == supOfc.EmployeeId);

            var leaveTypeName = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId)?.Name ?? "N/A";

            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
                : "N/A";

            var emp = _employeeRepository.GetAll()
                .FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);

            string inner = $@"
              <h1 style=""color:#dc3545;font-family:Arial,sans-serif;margin:0 0 16px 0;"">Leave Application Cancelled</h1>
              <p style=""{ParaStyle}"">Dear {mailTo?.FirstName} {mailTo?.LastName} ({mailTo?.EmployeeId}),</p>
              <p style=""{ParaStyle}"">Leave application of {emp?.FirstName} {emp?.LastName} ({emp?.EmployeeId}) has been cancelled by management.</p>
              <p style=""{ParaStyle}"">Details as follows:</p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#ddd;"">
                {DetailRow("Cancelled By", "Khandaker Fazle Rabbi")}
                {DetailRow("Leave Type", leaveTypeName)}
                {DetailRow("Number Of Days", leaveApplication.NoOfDay.ToString())}
                {DetailRow("Details Of Days", formattedSelectedDates)}
                {DetailRow("Comments", leaveApplication.HRApprovalRemarks ?? "N/A")}
              </table>
              <p style=""{ParaStyle}"">Regards,<br/>The HR Team</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Leave Rejection Email (Employee copy) ────────────────────
        public async Task<string> GenerateLeaveRejectionEmail(LeaveEntryGetViewModel leaveApplication, string empName, string reMark)
        {
            string formattedFromDate = leaveApplication.StartDate.ToString("yyyy-MM-dd");
            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("yyyy-MM-dd")))
                : "N/A";

            var leaveTypeDetails = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId);

            string inner = $@"
              <h1 style=""color:#dc3545;font-family:Arial,sans-serif;margin:0 0 16px 0;"">Leave Request Rejected</h1>
              <p style=""{ParaStyle}"">Dear {empName} ({leaveApplication.EmployeeId}),</p>
              <p style=""{ParaStyle}"">We regret to inform you that your leave request has been rejected with the following details:</p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#ddd;"">
                {DetailRow("Leave ID", leaveApplication.LeaveAppEntryId ?? "N/A")}
                {DetailRow("Leave Format", leaveApplication.ApplyLeaveFormat ?? "N/A")}
                {DetailRow("Leave Type", leaveTypeDetails?.Name ?? "N/A")}
                {DetailRow("Leave Start Date", formattedFromDate)}
                {DetailRow("Leave Dates", formattedSelectedDates)}
                {DetailRow("Remarks", reMark ?? "N/A")}
              </table>
              <p style=""{ParaStyle}"">Please contact your manager for further clarification.<br/>The HR Team</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Notify HR / Supervisor Email (Approval request) ─────────
        public async Task<string> GenerateNotifyHrEmail(LeaveApplicationViewModel leaveApplication, LeaveBalanceViewModel leaveBalDetails,
                                                        bool isFirstNotify, HrmEmployeeOfficialInfo resEmp)
        {
            string days = leaveApplication.SelectedDates != null && leaveApplication.SelectedDates.Count > 1 ? "days" : "day";
            string formattedSelectedDates = leaveApplication.SelectedDates != null && leaveApplication.SelectedDates.Any()
                ? string.Join(", ", leaveApplication.SelectedDates.Select(d => d.ToString("yyyy-MM-dd")))
                : "N/A";
            int apvStage = 1;

            var leaveTypeDetails = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveType);

            var empInfo = await _employeeOfficialInfoService.GetEmployeeDetailsByCode(leaveApplication.EmployeeId);

            string mailto = empInfo.SuporVisorName1;
            string approvalLink = $"https://localhost:44370/?returnUrl=%2FLeaveApplicationEntry%2FApproval%3FleaveId%3D{leaveApplication.EntryId}%26empId%3D{resEmp.EmployeeId}%26ApvStage%3D{apvStage}";

            string inner = $@"
              <h1 style=""color:#343a40;font-family:Arial,sans-serif;margin:0 0 16px 0;text-align:center;"">Leave Application</h1>
              <p style=""{ParaStyle}"">Dear {mailto},</p>
              <p style=""{ParaStyle}"">{empInfo.FullName} has applied for leave for {leaveApplication.TotalDays} {days}.</p>

              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#dee2e6;"">
                <tr>
                  <th style=""{BlueThStyle}"">Detail</th>
                  <th style=""{BlueThStyle}"">Information</th>
                </tr>
                {BlueDetailRow("Employee Name", empInfo.FullName)}
                {BlueDetailRow("Designation", empInfo.DesignationName)}
                {BlueDetailRow("Department", empInfo.DepartmentName)}
                {BlueDetailRow("Leave Type", leaveTypeDetails?.Name ?? "N/A")}
                {BlueDetailRow("No. of Days", leaveApplication.TotalDays.ToString())}
                {BlueDetailRow("Date", formattedSelectedDates)}
                {BlueDetailRow("Reason", leaveApplication.Reason ?? "N/A")}
              </table>

              <h2 style=""color:#343a40;font-family:Arial,sans-serif;margin:16px 0 8px 0;"">Applicant Leave Status</h2>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#dee2e6;"">
                <tr>
                  <th style=""{BlueThStyle}"">Leave Type</th>
                  <th style=""{BlueThStyle}"">Yearly Entitled</th>
                  <th style=""{BlueThStyle}"">Taken</th>
                  <th style=""{BlueThStyle}"">Balance</th>
                </tr>
                <tr>
                  <td style=""{TdStyle}text-align:center;"">{leaveTypeDetails?.ShortName ?? "N/A"}</td>
                  <td style=""{TdStyle}text-align:center;"">{leaveTypeDetails?.NoOfDay ?? 0}</td>
                  <td style=""{TdStyle}text-align:center;"">{leaveBalDetails.TotalLeaveTaken}</td>
                  <td style=""{TdStyle}text-align:center;"">{leaveBalDetails.RemainingLeave}</td>
                </tr>
              </table>

              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
                <tr>
                  <td align=""center"">
                    <a href=""{approvalLink}"" style=""{BtnStyle}"">Click here to process the request</a>
                  </td>
                </tr>
              </table>
              <p style=""{ParaStyle}"">Regards,<br/>{empInfo.SuporVisorName1}</p>";

            return WrapEmail(inner);
        }

        // ─── Generate Second Notify HR Email (HOD → HR forwarding) ────────────
        public async Task<string> GenerateSecondNotifyHrEmail(LeaveEntryGetViewModel leaveApplication, LeaveBalanceViewModel leaveBalDetails,
                                                              bool isSecondNotify, HrmEmployeeOfficialInfo resEmp)
        {
            string days = leaveApplication.Days != null && leaveApplication.Days.Count > 1 ? "days" : "day";
            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("yyyy-MM-dd")))
                : "N/A";

            var leaveTypeDetails = _leaveTypeRepository.GetAll()
                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId);

            var empInfo = await _employeeOfficialInfoService.GetEmployeeDetailsByCode(leaveApplication.EmployeeId);

            int apvStage = isSecondNotify ? 2 : 3;
            var empId = resEmp.EmployeeId;
            var mailto = isSecondNotify ? empInfo.HodName1 : "HR Manager";
            var mailFrom = isSecondNotify ? empInfo.SuporVisorName1 : empInfo.HodName1;

            string approvalLink = $"https://localhost:44370/?returnUrl=%2FLeaveApplicationEntry%2FApproval%3FleaveId%3D{leaveApplication.LeaveAppEntryId}%26empId%3D{empId}%26ApvStage%3D{apvStage}";

            string inner = $@"
              <h1 style=""color:#343a40;font-family:Arial,sans-serif;margin:0 0 16px 0;text-align:center;"">Leave Application Review</h1>
              <p style=""{ParaStyle}"">Dear {mailto},</p>
              <p style=""{ParaStyle}"">Please review the leave request submitted by <strong>{empInfo.FullName}</strong> and take necessary action.</p>

              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#dee2e6;"">
                <tr>
                  <th style=""{BlueThStyle}"">Detail</th>
                  <th style=""{BlueThStyle}"">Information</th>
                </tr>
                {BlueDetailRow("Employee Name", empInfo.FullName)}
                {BlueDetailRow("Designation", empInfo.DesignationName)}
                {BlueDetailRow("Department", empInfo.DepartmentName)}
                {BlueDetailRow("Leave Type", leaveTypeDetails?.Name ?? "N/A")}
                {BlueDetailRow("No. of Days", leaveApplication.NoOfDay.ToString())}
                {BlueDetailRow("Date", formattedSelectedDates)}
                {BlueDetailRow("Reason", leaveApplication.Reason ?? "N/A")}
                {BlueDetailRow("Remarks", leaveApplication.ConfirmationRemarks ?? "N/A")}
              </table>

              <h2 style=""color:#343a40;font-family:Arial,sans-serif;margin:16px 0 8px 0;"">Leave Balance &amp; Status</h2>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""1"" style=""{TableStyle}border-color:#dee2e6;"">
                <tr>
                  <th style=""{BlueThStyle}"">Leave Type</th>
                  <th style=""{BlueThStyle}"">Yearly Entitled</th>
                  <th style=""{BlueThStyle}"">Taken</th>
                  <th style=""{BlueThStyle}"">Balance</th>
                </tr>
                <tr>
                  <td style=""{TdStyle}text-align:center;"">{leaveTypeDetails?.ShortName ?? "N/A"}</td>
                  <td style=""{TdStyle}text-align:center;"">{leaveTypeDetails?.NoOfDay ?? 0}</td>
                  <td style=""{TdStyle}text-align:center;"">{leaveBalDetails.TotalLeaveTaken}</td>
                  <td style=""{TdStyle}text-align:center;"">{leaveBalDetails.RemainingLeave}</td>
                </tr>
              </table>

              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
                <tr>
                  <td align=""center"">
                    <a href=""{approvalLink}"" style=""{BtnStyle}"">Click here to process the request</a>
                  </td>
                </tr>
              </table>
              <p style=""{ParaStyle}"">Regards,<br/>{mailFrom}</p>";

            return WrapEmail(inner);
        }

        public Task<string> HrEmailForWard(LeaveApplicationViewModel leaveApplication, string leaveStatus)
        {
            throw new NotImplementedException();
        }
    }
}










//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using DocumentFormat.OpenXml.Bibliography;
//using DocumentFormat.OpenXml.Spreadsheet;
//using DocumentFormat.OpenXml.Wordprocessing;
//using GCTL.Core.Data;
//using GCTL.Core.ViewModels.HrmLeaveApplicationEntry;
//using GCTL.Data.Models;
//using GCTL.Service.EmployeeOfficialInfo;
//using GCTL.Service.HrmLeaveApplicationEntrys;
//using GCTL.Service.LeaveTypes;
//using Org.BouncyCastle.Pqc.Crypto.Lms;

//namespace GCTL.Service.EmailService
//{
//    public class LeaveEmailService : ILeaveEmailService
//    {

//        //private readonly IHrmLeaveApplicationEntry _leaveApplicationEntry;
//        private readonly IEmployeeOfficialInfoService _employeeOfficialInfoService;
//        private readonly IRepository<HrmAtdLeaveType> _leaveTypeRepository;
//        private readonly IRepository<HrmLeaveApplicationEntry> _leaveEntryRepository;
//        private readonly IRepository<HrmEmployee> _employeeRepository;


//        //public LeaveEmailService( IHrmLeaveApplicationEntry leaveApplicationEntry, IEmployeeOfficialInfoService employeeOfficialInfoService, IRepository<HrmAtdLeaveType> leaveTypeRepository)
//        //{

//        //    _leaveApplicationEntry = leaveApplicationEntry;
//        //    _employeeOfficialInfoService = employeeOfficialInfoService;
//        //    _leaveTypeRepository = leaveTypeRepository;
//        //}

//        public LeaveEmailService(IEmployeeOfficialInfoService employeeOfficialInfoService, IRepository<HrmAtdLeaveType> leaveTypeRepository, IRepository<HrmLeaveApplicationEntry> leaveEntryRepository, IRepository<HrmEmployee> employeeRepository)
//        {
//            _employeeOfficialInfoService = employeeOfficialInfoService;
//            _leaveTypeRepository = leaveTypeRepository;
//            _leaveEntryRepository = leaveEntryRepository;
//            _employeeRepository = employeeRepository;
//        }


//        // Generate Leave Request Email
//        public string GenerateLeaveRequestEmail(LeaveApplicationViewModel leaveApplication)
//        {
//            // Format dates
//            string formattedFromDate = leaveApplication.FromDate?.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedToDate = leaveApplication.ToDate?.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedSelectedDates = leaveApplication.SelectedDates != null && leaveApplication.SelectedDates.Any()
//                ? string.Join(", ", leaveApplication.SelectedDates.Select(d => d.ToString("yyyy-MM-dd")))
//                : "N/A";

//            return $@"
//        <!DOCTYPE html>
//        <html>
//        <head>
//            <style>
//                body {{ font-family: Arial, sans-serif; }}
//                .container {{ background: #f9f9f9; padding: 20px; border-radius: 8px; }}
//                h1 {{ color: #5a5a5a; }}
//                table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
//                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
//                th {{ background-color: #f2f2f2; }}
//            </style>
//        </head>
//        <body>
//            <div class='container'>
//                <h1>Leave Request Submitted</h1>
//                <p>Dear {leaveApplication.EmployeeName}  ({leaveApplication.EmployeeId}),</p>
//                <p>We have received your leave request with the following details:</p>
//                <table>
//                    <tr>
//                        <th>Leave ID</th>
//                        <td>{leaveApplication.EntryId ?? "N/A"}</td>
//                    </tr>
//                    <tr>
//                        <th>Leave Format</th>
//                        <td>{leaveApplication.LeaveFormat ?? "N/A"}</td>
//                    </tr>
//                    <tr>
//                        <th>Leave Type</th>
//                        <td>{leaveApplication.LeaveType ?? "N/A"}</td>
//                    </tr>
//                    <tr>
//                        <th>From Date</th>
//                        <td>{formattedFromDate}</td>
//                    </tr>
//                    <tr>
//                        <th>To Date</th>
//                        <td>{formattedToDate}</td>
//                    </tr>
//                    <tr>
//                        <th>Selected Dates</th>
//                        <td>{formattedSelectedDates}</td>
//                    </tr>
//                    <tr>
//                        <th>Reason</th>
//                        <td>{leaveApplication.Reason ?? "N/A"}</td>
//                    </tr>
//                </table>
//                <p>Thank you,</p>
//                <p>The HR Team</p>
//            </div>
//        </body>
//        </html>";
//        }

//        // Generate Leave Approval Email
//        public string GenerateLeaveApprovalEmail(LeaveEntryGetViewModel leaveApplication)
//        {
//            string formattedFromDate = leaveApplication.StartDate.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedToDate = leaveApplication.EndDate.ToString("yyyy-MM-dd") ?? "N/A";

//            var leaveTypeName = _leaveTypeRepository.GetAll().FirstOrDefault(e=>e.LeaveTypeCode == leaveApplication.LeaveTypeId).Name;

//            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
//               ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
//               : "N/A";

//            var emp = _employeeRepository.GetAll().FirstOrDefault(e=>e.EmployeeId == leaveApplication.EmployeeId);


//            return $@"
//                     <!DOCTYPE html>
//                        <html>
//                        <head>
//                            <meta charset=""UTF-8"">
//                            <title>Leave Application Approved</title>
//                            <style>
//                                body {{
//                                    font-family: Arial, sans-serif;
//                                }}
//                                .container {{
//                                    width: 600px;
//                                    margin: auto;
//                                    border: 1px solid #ccc;
//                                    padding: 20px;
//                                }}
//                                .header {{
//                                    font-size: 18px;
//                                    font-weight: bold;
//                                }}
//                                .details-table {{
//                                    width: 100%;
//                                    border-collapse: collapse;
//                                    margin-top: 10px;
//                                }}
//                                .details-table th, .details-table td {{
//                                    border: 1px solid #000;
//                                    padding: 8px;
//                                    text-align: left;
//                                }}
//                                .details-table th {{
//                                    background-color: #f2f2f2;
//                                }}
//                            </style>
//                        </head>
//                        <body>
//                            <div class=""container"">

//                                <p>Dear {emp.FirstName +" "+ emp.LastName } ({emp.EmployeeId}),</p>
//                                <p>Your leave application has been approved by management.</p>
//                                <p>Details as follows:</p>
//                                <table class=""details-table"">
//                                    <tr>
//                                        <th>Approved By</th>
//                                        <td>Khandaker Fazle Rabbi</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Leave Type</th>
//                                        <td>{leaveTypeName}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Number Of Days</th>
//                                        <td>{leaveApplication.NoOfDay}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Details Of Days</th>
//                                        <td>{formattedSelectedDates}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Comments</th>
//                                        <td>{leaveApplication.HRApprovalRemarks}</td>
//                                    </tr>
//                                </table>
//                            </div>
//                        </body>
//                        </html>
//                    ";



//        }


//        public string GenerateLeaveApprovalOfcNotify(LeaveEntryGetViewModel leaveApplication, HrmEmployeeOfficialInfo supOfc)
//        {
//            //string formattedFromDate = leaveApplication.StartDate?.ToString("yyyy-MM-dd") ?? "N/A";
//            //string formattedToDate = leaveApplication.EndDate?.ToString("yyyy-MM-dd") ?? "N/A";

//            var mailTo = _employeeRepository.GetAll().FirstOrDefault(e=>e.EmployeeId == supOfc.EmployeeId);

//            var leaveTypeName = _leaveTypeRepository.GetAll().FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId).Name;

//            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
//               ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
//               : "N/A";

//            var emp = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);


//            return $@"
//                     <!DOCTYPE html>
//                        <html>
//                        <head>
//                            <meta charset=""UTF-8"">
//                            <title>Leave Application Approved</title>
//                            <style>
//                                body {{
//                                    font-family: Arial, sans-serif;
//                                }}
//                                .container {{
//                                    width: 600px;
//                                    margin: auto;
//                                    border: 1px solid #ccc;
//                                    padding: 20px;
//                                }}
//                                .header {{
//                                    font-size: 18px;
//                                    font-weight: bold;
//                                }}
//                                .details-table {{
//                                    width: 100%;
//                                    border-collapse: collapse;
//                                    margin-top: 10px;
//                                }}
//                                .details-table th, .details-table td {{
//                                    border: 1px solid #000;
//                                    padding: 8px;
//                                    text-align: left;
//                                }}
//                                .details-table th {{
//                                    background-color: #f2f2f2;
//                                }}
//                            </style>
//                        </head>
//                        <body>
//                            <div class=""container"">

//                                <p>Dear {mailTo.FirstName + " " + mailTo.LastName} ({mailTo.EmployeeId}),</p>
//                                <p>Leave application of {emp.FirstName + " " + emp.LastName} ({emp.EmployeeId}) approved by management.</p>
//                                <p>Details as follows:</p>
//                                <table class=""details-table"">
//                                    <tr>
//                                        <th>Approved By</th>
//                                        <td>Khandaker Fazle Rabbi</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Leave Type</th>
//                                        <td>{leaveTypeName}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Number Of Days</th>
//                                        <td>{leaveApplication.NoOfDay}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Details Of Days</th>
//                                        <td>{formattedSelectedDates}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Comments</th>
//                                        <td>{leaveApplication.HRApprovalRemarks}</td>
//                                    </tr>
//                                </table>
//                            </div>
//                        </body>
//                        </html>
//                    ";



//        }


//        public string GenerateLeaveCancelEmail(LeaveEntryGetViewModel leaveApplication, string cancelledBy)
//        {
//            string formattedFromDate = leaveApplication.StartDate.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedToDate = leaveApplication.EndDate.ToString("yyyy-MM-dd") ?? "N/A";

//            var leaveTypeName = _leaveTypeRepository.GetAll().FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId).Name;

//            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
//               ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
//               : "N/A";

//            var emp = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);
//            var cancelBYemp = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == cancelledBy);

//            var remarks = "Leave Cancelled";

//            if (leaveApplication.HRApprovalRemarks != null)
//            {
//                remarks = leaveApplication.HRApprovalRemarks;
//            }
//            else if (leaveApplication.HRApprovalRemarks == null && leaveApplication.HODApprovalRemarks != null)
//            {
//                remarks = leaveApplication.HODApprovalRemarks;
//            }
//            else if (leaveApplication.HRApprovalRemarks == null && leaveApplication.HODApprovalRemarks == null && leaveApplication.ConfirmationRemarks != null)
//            {
//                remarks = leaveApplication.ConfirmationRemarks;
//            }

//            return $@"
//                     <!DOCTYPE html>
//                        <html>
//                        <head>
//                            <meta charset=""UTF-8"">
//                            <title>Leave Application Cancelled</title>
//                            <style>
//                                body {{
//                                    font-family: Arial, sans-serif;
//                                }}
//                                .container {{
//                                    width: 600px;
//                                    margin: auto;
//                                    border: 1px solid #ccc;
//                                    padding: 20px;
//                                }}
//                                .header {{
//                                    font-size: 18px;
//                                    font-weight: bold;
//                                }}
//                                .details-table {{
//                                    width: 100%;
//                                    border-collapse: collapse;
//                                    margin-top: 10px;
//                                }}
//                                .details-table th, .details-table td {{
//                                    border: 1px solid #000;
//                                    padding: 8px;
//                                    text-align: left;
//                                }}
//                                .details-table th {{
//                                    background-color: #f2f2f2;
//                                }}
//                            </style>
//                        </head>
//                        <body>
//                            <div class=""container"">

//                                <p>Dear {emp.FirstName +" " + emp.LastName},</p>
//                                <p>Your leave application has been Cancel by management.</p>
//                                <p>Details as follows:</p>
//                                <table class=""details-table"">
//                                    <tr>
//                                        <th>Cancelled By</th>
//                                        <td>{cancelBYemp.FirstName + " " + cancelBYemp.LastName}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Leave Type</th>
//                                        <td>{leaveTypeName}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Number Of Days</th>
//                                        <td>{leaveApplication.NoOfDay}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Details Of Days</th>
//                                        <td>{formattedSelectedDates}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Comments</th>
//                                        <td>{remarks}</td>
//                                    </tr>
//                                </table>
//                            </div>
//                        </body>
//                        </html>
//                    ";



//        }

//        public string GenerateLeaveCancelOfcNotify(LeaveEntryGetViewModel leaveApplication, HrmEmployeeOfficialInfo supOfc)
//        {
//            var mailTo = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == supOfc.EmployeeId);

//            var leaveTypeName = _leaveTypeRepository.GetAll().FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId).Name;

//            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
//               ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("dd-MM-yyyy")))
//               : "N/A";

//            var emp = _employeeRepository.GetAll().FirstOrDefault(e => e.EmployeeId == leaveApplication.EmployeeId);


//            return $@"
//                     <!DOCTYPE html>
//                        <html>
//                        <head>
//                            <meta charset=""UTF-8"">
//                            <title>Leave Application Cancelled</title>
//                            <style>
//                                body {{
//                                    font-family: Arial, sans-serif;
//                                }}
//                                .container {{
//                                    width: 600px;
//                                    margin: auto;
//                                    border: 1px solid #ccc;
//                                    padding: 20px;
//                                }}
//                                .header {{
//                                    font-size: 18px;
//                                    font-weight: bold;
//                                }}
//                                .details-table {{
//                                    width: 100%;
//                                    border-collapse: collapse;
//                                    margin-top: 10px;
//                                }}
//                                .details-table th, .details-table td {{
//                                    border: 1px solid #000;
//                                    padding: 8px;
//                                    text-align: left;
//                                }}
//                                .details-table th {{
//                                    background-color: #f2f2f2;
//                                }}
//                            </style>
//                        </head>
//                        <body>
//                            <div class=""container"">

//                                <p>Dear {mailTo.FirstName + " " + mailTo.LastName} ({mailTo.EmployeeId}),</p>
//                                <p>Leave application of {emp.FirstName + " " + emp.LastName} ({emp.EmployeeId}) cancel by management.</p>
//                                <p>Details as follows:</p>
//                                <table class=""details-table"">
//                                    <tr>
//                                        <th>Approved By</th>
//                                        <td>Khandaker Fazle Rabbi</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Leave Type</th>
//                                        <td>{leaveTypeName}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Number Of Days</th>
//                                        <td>{leaveApplication.NoOfDay}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Details Of Days</th>
//                                        <td>{formattedSelectedDates}</td>
//                                    </tr>
//                                    <tr>
//                                        <th>Comments</th>
//                                        <td>{leaveApplication.HRApprovalRemarks}</td>
//                                    </tr>
//                                </table>
//                            </div>
//                        </body>
//                        </html>
//                    ";
//        }

//        // Generate Leave Rejection Email
//        public async Task< string> GenerateLeaveRejectionEmail(LeaveEntryGetViewModel leaveApplication, string empName , string reMark)
//        {


//            string days = "day";
//            string formattedFromDate = leaveApplication.StartDate.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedToDate = leaveApplication.EndDate.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
//                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("yyyy-MM-dd")))
//                : "N/A";

//            var leaveTypeDetails =  _leaveTypeRepository.GetAll()
//                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId);

//            //var empInfo = await _employeeOfficialInfoService.GetEmployeeDetailsByCode(leaveApplication.EmployeeId);

//            //if (leaveApplication.Days != null && leaveApplication.Days.Count > 1)
//            //{
//            //    days = "days";
//            //}

//            //var mailto = empInfo.HodName1;
//            //var mailFrom = empInfo.SuporVisorName1;





//            return $@"
//        <!DOCTYPE html>
//        <html>
//        <head>
//            <style>
//                body {{ font-family: Arial, sans-serif; }}
//                .container {{ background: #f9f9f9; padding: 20px; border-radius: 8px; }}
//                h1 {{ color: #dc3545; }}
//                table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
//                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
//                th {{ background-color: #f2f2f2; }}
//            </style>
//        </head>
//        <body>
//            <div class='container'>
//                <h1>Leave Request Rejected</h1>
//                <p>Dear {empName}  ({leaveApplication.EmployeeId}),</p>
//                <p>We regret to inform you that your leave request has been rejected with the following details:</p>
//                <table>
//                    <tr>
//                        <th>Leave ID</th>
//                        <td>{leaveApplication.LeaveAppEntryId ?? "N/A"}</td>
//                    </tr>
//                    <tr>
//                        <th>Leave Format</th>
//                        <td>{leaveApplication.ApplyLeaveFormat ?? "N/A"}</td>
//                    </tr>
//                    <tr>
//                        <th>Leave Type</th>
//                        <td>{leaveTypeDetails.Name ?? "N/A"}</td>
//                    </tr>
//                    <tr>
//                        <th>Leave Start Date</th>
//                        <td>{formattedFromDate}</td>
//                    </tr>
//                     <tr>
//                        <th>Leave Dated</th>
//                        <td>{formattedSelectedDates}</td>
//                    </tr>
//                    <tr>
//                        <th>Remarks</th>
//                        <td>{reMark}</td>
//                    </tr>
//                </table>
//                <p>Please contact your manager for further clarification.</p>
//                <p>The HR Team</p>
//            </div>
//        </body>
//        </html>";
//        }



//        // Generate Notify HR Email
//        public async Task<string> GenerateNotifyHrEmail(LeaveApplicationViewModel leaveApplication, LeaveBalanceViewModel leaveBalDetails ,
//                                                        bool isFirstNotify, HrmEmployeeOfficialInfo resEmp)
//        {
//            string days = "day";
//            string formattedFromDate = leaveApplication.FromDate?.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedToDate = leaveApplication.ToDate?.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedSelectedDates = leaveApplication.SelectedDates != null && leaveApplication.SelectedDates.Any()
//                ? string.Join(", ", leaveApplication.SelectedDates.Select(d => d.ToString("yyyy-MM-dd")))
//                : "N/A";
//            int apvStage = 1;
//            var leaveTypeDetails = _leaveTypeRepository.GetAll()
//                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveType);

//            var empInfo = await _employeeOfficialInfoService.GetEmployeeDetailsByCode(leaveApplication.EmployeeId);

//            if (leaveApplication.SelectedDates != null && leaveApplication.SelectedDates.Count > 1)
//            {
//                days = "days";
//            }

//            return $@"
//            <!DOCTYPE html>
//            <html>

//            <head>
//                <title>Leave Application</title>
//                <style>
//                    body {{
//                        font-family: Arial, sans-serif;
//                        background-color: #f8f9fa;
//                        color: #212529;
//                        margin: 0;
//                        padding: 0;
//                        line-height: 1.6;
//                    }}

//                    h2, h1 {{
//                        color: #343a40;
//                        text-align: center;
//                        margin-bottom: 1rem;
//                    }}

//                    p {{
//                        font-size: 1rem;
//                        color: #495057;
//                        margin: 1rem auto;
//                        max-width: 800px;
//                    }}

//                    .table {{
//                        width: 45%;
//                        margin: 1.5rem auto;
//                        border-collapse: collapse;
//                        background-color: #ffffff;
//                        border: 1px solid #dee2e6;
//                        border-radius: 8px;
//                        overflow: hidden;
//                        box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
//                    }}

//                    .table th, .table td {{
//                        padding: 0.25rem;
//                        text-align: left;
//                        border: 1px solid #dee2e6;
//                    }}



//                    .table th {{
//                        background - color: #ebebeb;
//                        color: #303030;
//                        font-weight: bold;
//                    }}


//                    .table-striped tbody tr:nth-of-type(odd) {{
//                        background-color: #f8f9fa;
//                    }}

//                    .button {{
//                        display: inline-block;
//                        margin: 0.25rem auto;
//                        padding: 0.25rem 0.25rem;
//                        background-color: #ffffff;
//                        color: #ffffff;
//                        text-decoration: none;
//                        border-radius: 4px;
//                        font-size: 1rem;
//                        text-align: center;
//                        transition: background-color 0.3s ease;
//                    }}

//                    .button:hover {{
//                        background-color: #218838;
//                    }}

//                    .header {{
//                        margin: 2rem auto;
//                        text-align: center;
//                    }}

//                    @media (max-width: 768px) {{
//                        .table {{
//                            width: 100%;
//                        }}

//                        p {{
//                            padding: 0 .25rem;
//                        }}
//                    }}
//                </style>
//            </head>

//            <body>
//                <div class=""header"">
//                    <h1>Leave Application</h1>
//                </div>

//                <p>Dear {empInfo.SuporVisorName1}, </p>
//                <p>{empInfo.FullName} has applied for leave for {leaveApplication.TotalDays} {days}.</p>

//                <table class=""table table-bordered table-striped"">
//                    <thead>
//                        <tr>
//                            <th>Detail</th>
//                            <th>Information</th>
//                        </tr>
//                    </thead>
//                    <tbody>
//                        <tr>
//                            <td>Employee Name</td>
//                            <td>{empInfo.FullName}</td>
//                        </tr>
//                        <tr>
//                            <td>Designation</td>
//                            <td>{empInfo.DesignationName}</td>
//                        </tr>
//                        <tr>
//                            <td>Department</td>
//                            <td>{empInfo.DepartmentName}</td>
//                        </tr>
//                        <tr>
//                            <td>Leave Type</td>
//                            <td>{leaveTypeDetails?.Name ?? "N/A"}</td>
//                        </tr>
//                        <tr>
//                            <td>No. of Days</td>
//                            <td>{leaveApplication.TotalDays}</td>
//                        </tr>
//                        <tr>
//                            <td>Date</td>
//                            <td>{formattedSelectedDates}</td>
//                        </tr>
//                        <tr>
//                            <td>Reason</td>
//                            <td>{leaveApplication.Reason}</td>
//                        </tr>
//                    </tbody>
//                </table>

//                <h2>Applicant Leave Status</h2>
//                <table class=""table table-bordered table-striped"" style=""text-align: center;"">
//                    <thead>
//                        <tr>
//                            <th>Leave Type</th>
//                            <th>Yearly Leave Entitled</th>
//                            <th>Taken</th>
//                            <th>Balance</th>
//                        </tr>
//                    </thead>
//                    <tbody>
//                        <tr>
//                            <td style=""text-align: center;"">{leaveTypeDetails?.ShortName ?? "N/A"}</td>
//                            <td style=""text-align: center;"">{leaveTypeDetails?.NoOfDay ?? 0}</td>
//                            <td style=""text-align: center;"">{leaveBalDetails.TotalLeaveTaken}</td>
//                            <td style=""text-align: center;"">{leaveBalDetails.RemainingLeave}</td>
//                        </tr>
//                    </tbody>
//                </table>

//                <div style=""text-align: center;"">
//                    <a href=""https://localhost:44370/?returnUrl=%2FLeaveApplicationEntry%2FApproval%3FleaveId%3D{leaveApplication.EntryId}%26empId%3D{resEmp.EmployeeId}%26ApvStage%3D{apvStage}"" class=""button"">
//                        Click here to process the request
//                    </a>
//                </div>

//            </body>

//            </html>
//            ";
//        }

//        public Task<string> HrEmailForWard(LeaveApplicationViewModel leaveApplication, string leaveStatus)
//        {
//            throw new NotImplementedException();
//        }

//        public async Task<string> GenerateSecondNotifyHrEmail(LeaveEntryGetViewModel leaveApplication, LeaveBalanceViewModel leaveBalDetails, bool isSecondNotify, HrmEmployeeOfficialInfo resEmp)
//        {


//            string days = "day";
//            string formattedFromDate = leaveApplication.StartDate.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedToDate = leaveApplication.EndDate.ToString("yyyy-MM-dd") ?? "N/A";
//            string formattedSelectedDates = leaveApplication.Days != null && leaveApplication.Days.Any()
//                ? string.Join(", ", leaveApplication.Days.Select(d => d.ToString("yyyy-MM-dd")))
//                : "N/A";

//            var leaveTypeDetails = _leaveTypeRepository.GetAll()
//                .FirstOrDefault(e => e.LeaveTypeCode == leaveApplication.LeaveTypeId);

//            var empInfo = await _employeeOfficialInfoService.GetEmployeeDetailsByCode(leaveApplication.EmployeeId);

//            if (leaveApplication.Days != null && leaveApplication.Days.Count > 1)
//            {
//                days = "days";
//            }

//            int apvStage = 2;

//           // var empId = empInfo.HodID1;
//            var empId = resEmp.EmployeeId;

//            var mailto = empInfo.HodName1;
//            var mailFrom = empInfo.SuporVisorName1;

//            if (!isSecondNotify)
//            {
//                //TODO: 26-01-25 Add HR ID (REAL)
//                mailto = "HR Manager";
//                mailFrom = empInfo.HodName1;
//                //empId = "08060104001";
//                apvStage = 3;

//            }

//            return $@"

//                <!DOCTYPE html>
//                    <html>

//                    <head>
//                        <title>Leave Application - Review</title>
//                        <style>
//                            body {{
//                                font-family: Arial, sans-serif;
//                                background-color: #f8f9fa;
//                                color: #212529;
//                                margin: 0;
//                                padding: 0;
//                                line-height: 1.6;
//                            }}

//                            h1, h2 {{
//                                color: #343a40;
//                                text-align: center;
//                                margin-bottom: 1rem;
//                            }}

//                            p {{
//                                font-size: 1rem;
//                                color: #495057;
//                                margin: 1rem auto;
//                                max-width: 800px;
//                                padding: 0 .5rem;
//                            }}

//                            .table {{
//                                width: 45%;
//                                margin: 1.5rem auto;
//                                border-collapse: collapse;
//                                background-color: #ffffff;
//                                border: 1px solid #dee2e6;
//                                border-radius: 8px;
//                                overflow: hidden;
//                                box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
//                            }}

//                            .table th, .table td {{
//                                padding: 0.75rem;
//                                text-align: left;
//                                border: 1px solid #dee2e6;
//                            }}

//                            .table th {{
//                                background-color: #007bff;
//                                color: #ffffff;
//                                font-weight: bold;
//                            }}

//                            .table-striped tbody tr:nth-of-type(odd) {{
//                                background-color: #f8f9fa;
//                            }}

//                            .button {{
//                                display: inline-block;
//                                margin: 1rem auto;
//                                padding: 0.5rem 1.5rem;
//                                background-color: #ffffff;
//                                color: #ffffff;
//                                text-decoration: none;
//                                border-radius: 4px;
//                                font-size: 1rem;
//                                text-align: center;
//                                transition: background-color 0.3s ease;
//                            }}

//                            .button:hover {{
//                                background-color: #218838;
//                            }}

//                            .reject-button {{
//                                background-color: #dc3545;
//                                margin-left: 10px;
//                            }}

//                            .reject-button:hover {{
//                                background-color: #c82333;
//                            }}

//                            .action-buttons {{
//                                text-align: center;
//                                margin-top: 2rem;
//                            }}

//                            .note {{
//                                font-size: 0.9rem;
//                                color: #6c757d;
//                                text-align: center;
//                                margin-top: 1rem;
//                            }}

//                            .status {{
//                                text-align: center;
//                                margin: 2rem auto;
//                                padding: 1rem;
//                                background-color: #f8f9fa;
//                                border: 1px solid #dee2e6;
//                                border-radius: 8px;
//                            }}

//                            @media (max-width: 768px) {{
//                                .table {{
//                                    width: 45%;
//                                }}

//                                p {{
//                                    padding: 0 .25rem;
//                                }}
//                            }}
//                        </style>
//                    </head>

//                    <body>
//                        <div class=""header"">
//                            <h1>Leave Application Review</h1>
//                        </div>

//                        <p>Dear {mailto},</p>
//                        <p>Please review the leave request submitted by <strong>{empInfo.FullName}</strong> and take necessary action below.</p>

//                        <table class=""table table-bordered table-striped"" style=""text-align: center;"">
//                            <thead>
//                                <tr>
//                                    <th>Detail</th>
//                                    <th>Information</th>
//                                </tr>
//                            </thead>
//                            <tbody>
//                                <tr>
//                                    <td>Employee Name</td>
//                                    <td>{empInfo.FullName}</td>
//                                </tr>
//                                <tr>
//                                    <td>Designation</td>
//                                    <td>{empInfo.DesignationName}</td>
//                                </tr>
//                                <tr>
//                                    <td>Department</td>
//                                    <td>{empInfo.DepartmentName}</td>
//                                </tr>
//                                <tr>
//                                    <td>Leave Type</td>
//                                    <td>{leaveTypeDetails?.Name ?? "N/A"}</td>
//                                </tr>
//                                <tr>
//                                    <td>No. of Days</td>
//                                    <td>{leaveApplication.NoOfDay}</td>
//                                </tr>
//                                <tr>
//                                    <td>Date</td>
//                                    <td>{formattedSelectedDates}</td>
//                                </tr>
//                                <tr>
//                                    <td>Reason</td>
//                                    <td>{leaveApplication.Reason}</td>
//                                </tr>
//                                <tr>
//                                    <td>Remarks</td>
//                                    <td>{leaveApplication.ConfirmationRemarks}</td>
//                                </tr>
//                            </tbody>
//                        </table>

//                        <h2>Leave Balance & Status</h2>
//                        <table class=""table table-bordered table-striped"">
//                            <thead>
//                                <tr>
//                                    <th>Leave Type</th>
//                                    <th>Yearly Leave Entitled</th>
//                                    <th>Taken</th>
//                                    <th>Balance</th>
//                                </tr>
//                            </thead>
//                            <tbody>
//                                <tr>
//                                    <td style=""text-align: center;"">{leaveTypeDetails?.ShortName ?? "N/A"}</td>
//                                    <td style=""text-align: center;"">{leaveTypeDetails?.NoOfDay ?? 0}</td>
//                                    <td style=""text-align: center;"">{leaveBalDetails.TotalLeaveTaken}</td>
//                                    <td style=""text-align: center;"">{leaveBalDetails.RemainingLeave}</td>
//                                </tr>
//                            </tbody>
//                        </table>
//                        <div style=""text-align: center;"">

//                            <a href=""https://localhost:44370/?returnUrl=%2FLeaveApplicationEntry%2FApproval%3FleaveId%3D{leaveApplication.LeaveAppEntryId}%26empId%3D{empId}%26ApvStage%3D{apvStage}"" class=""button"">
//                                Click here to process the request</a>
//                        </div>

//                        <p>Regards</p>
//                        <p>{mailFrom}</p>
//                    </body>

//                    </html>


//                ";

//        }


//    }
//}
