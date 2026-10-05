
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_HRM_EmployeeOfficialInfo_EmpStatus_Dept' 
      AND object_id = OBJECT_ID('HRM_EmployeeOfficialInfo')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_HRM_EmployeeOfficialInfo_EmpStatus_Dept
    ON [dbo].[HRM_EmployeeOfficialInfo] ([EmployeeStatus], [CompanyCode], [DepartmentCode], [BranchCode])
    INCLUDE ([EmployeeID], [DesignationCode], [ShiftCode]);
    PRINT 'Created IX_HRM_EmployeeOfficialInfo_EmpStatus_Dept';
END
ELSE
BEGIN
    PRINT 'IX_HRM_EmployeeOfficialInfo_EmpStatus_Dept already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_HRM_EmployeeOfficialInfo_EmployeeID' 
      AND object_id = OBJECT_ID('HRM_EmployeeOfficialInfo')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_HRM_EmployeeOfficialInfo_EmployeeID
    ON [dbo].[HRM_EmployeeOfficialInfo] ([EmployeeID])
    INCLUDE ([CompanyCode], [BranchCode], [DepartmentCode], [DesignationCode], [ShiftCode], [EmployeeStatus]);
    PRINT 'Created IX_HRM_EmployeeOfficialInfo_EmployeeID';
END
ELSE
BEGIN
    PRINT 'IX_HRM_EmployeeOfficialInfo_EmployeeID already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_HRM_ATD_Manual_Date_Emp_Time' 
      AND object_id = OBJECT_ID('HRM_ATD_Manual')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_HRM_ATD_Manual_Date_Emp_Time
    ON [dbo].[HRM_ATD_Manual] ([Date], [EmployeeId])
    INCLUDE ([Time]);
    PRINT 'Created IX_HRM_ATD_Manual_Date_Emp_Time';
END
ELSE
BEGIN
    PRINT 'IX_HRM_ATD_Manual_Date_Emp_Time already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_HRM_ATD_MachineData_Date_FP_Time' 
      AND object_id = OBJECT_ID('HRM_ATD_MachineData')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_HRM_ATD_MachineData_Date_FP_Time
    ON [dbo].[HRM_ATD_MachineData] ([Date], [FingerPrintID])
    INCLUDE ([Time]);
    PRINT 'Created IX_HRM_ATD_MachineData_Date_FP_Time';
END
ELSE
BEGIN
    PRINT 'IX_HRM_ATD_MachineData_Date_FP_Time already exists';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_HRM_LeaveApp_Dates_Status' 
      AND object_id = OBJECT_ID('HRM_LeaveApplicationEntry')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_HRM_LeaveApp_Dates_Status
    ON [dbo].[HRM_LeaveApplicationEntry] ([EmployeeId], [StartDate], [EndDate])
    INCLUDE ([IsApproved], [HODApprovalStatus], [HRApprovalStatus]);
    PRINT 'Created IX_HRM_LeaveApp_Dates_Status';
END
ELSE
BEGIN
    PRINT 'IX_HRM_LeaveApp_Dates_Status already exists';
END
GO
