-- =========================================================================================
-- FILE: Shift_MasterDetails_Setup_And_Procedures.sql
-- DESCRIPTION: Complete Database Setup for Shift Setup (Master-Details Architecture),
--              Covering Indexes, Views, and Shift-Integrated Stored Procedures.
-- CREATED: 2026-10-31
-- INCLUDES:
--   1. Table: [dbo].[HRM_ATD_ShiftDetails] (Child table for multiple timing schedules)
--   2. Indexes: Performance & Covering Indexes on ShiftDetails & Attendance Tables
--   3. View: [dbo].[vw_HRM_ATD_Shift_MasterDetails]
--   4. Stored Procedure: [dbo].[SP_HrmAtdShift_DashboardCount]
--   5. Stored Procedure: [dbo].[SP_HrmAtdShift_GetList]
--   6. Stored Procedure: [dbo].[SP_HrmAtdShiftDetails_GetList]
--   7. Stored Procedure: [dbo].[usp_JobCardReport]
--   8. Stored Procedure: [dbo].[usp_JobCardBulkReport]
--   9. Stored Procedure: [dbo].[usp_MonthlyAttendanceReport]
-- =========================================================================================

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================================================
-- 1. TABLE: [dbo].[HRM_ATD_ShiftDetails]
-- =========================================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.HRM_ATD_ShiftDetails'))
BEGIN
    CREATE TABLE [dbo].[HRM_ATD_ShiftDetails] (
        [AutoId]           DECIMAL(18, 0) IDENTITY(1,1) NOT NULL,
        [ShiftCode]        NVARCHAR(50)   NOT NULL,
        [ShiftStartTime]   DATETIME       NOT NULL,
        [ShiftEndTime]     DATETIME       NOT NULL,
        [LateTime]         DATETIME       NOT NULL,
        [AbsentTime]       DATETIME       NOT NULL,
        [LunchOutTime]     DATETIME       NOT NULL,
        [LunchInTime]      DATETIME       NOT NULL,
        [LunchBreakHour]   DECIMAL(18, 2) NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_LunchBreakHour] DEFAULT ((0.00)),
        [EarlyLeaveTime]   NVARCHAR(50)   NULL,
        [IsCrossMidnight]  BIT            NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_IsCrossMidnight] DEFAULT ((0)),
        [CompanyIds]       NVARCHAR(MAX)  NULL,
        [BranchIds]        NVARCHAR(MAX)  NULL,
        [DepartmentIds]    NVARCHAR(MAX)  NULL,
        [EmployeeIds]      NVARCHAR(MAX)  NULL,
        [OvertimeRuleID]   NVARCHAR(50)   NULL,
        [OTGraceTime]      INT            NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_OTGraceTime] DEFAULT ((60)),
        [MaxOTPerDay]      DECIMAL(18, 2) NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_MaxOTPerDay] DEFAULT ((4.00)),
        [MinOTMinutes]     INT            NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_MinOTMinutes] DEFAULT ((50)),
        [WeeklyOffOTRule]  NVARCHAR(100)  NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_WeeklyOffOTRule] DEFAULT ('Not Applicable'),
        [HolidayOTRule]    NVARCHAR(100)  NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_HolidayOTRule] DEFAULT ('Not Applicable'),
        [WEF]              DATETIME       NOT NULL,
        [Remarks]          NVARCHAR(250)  NULL,
        [IsActive]         BIT            NULL CONSTRAINT [DF_HRM_ATD_ShiftDetails_IsActive] DEFAULT ((1)),
        [LUser]            NVARCHAR(50)   NULL,
        [LDate]            SMALLDATETIME  NULL,
        [LIP]              NVARCHAR(50)   NULL,
        [LMAC]             NVARCHAR(50)   NULL,
        [ModifyDate]       SMALLDATETIME  NULL,
        CONSTRAINT [PK_HRM_ATD_ShiftDetails] PRIMARY KEY CLUSTERED ([AutoId] ASC)
    );

    IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.HRM_ATD_Shift'))
    BEGIN
        ALTER TABLE [dbo].[HRM_ATD_ShiftDetails]  WITH CHECK 
        ADD CONSTRAINT [FK_HRM_ATD_ShiftDetails_HRM_ATD_Shift] FOREIGN KEY([ShiftCode])
        REFERENCES [dbo].[HRM_ATD_Shift] ([ShiftCode])
        ON UPDATE CASCADE
        ON DELETE CASCADE;
    END
END;
GO

-- =========================================================================================
-- 2. COVERING INDEXES FOR FAST SHIFT & ATTENDANCE LOOKUP
-- =========================================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_ShiftDetails') AND name = 'IX_HRM_ATD_ShiftDetails_ShiftCode_Active')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HRM_ATD_ShiftDetails_ShiftCode_Active]
    ON [dbo].[HRM_ATD_ShiftDetails] ([ShiftCode] ASC, [IsActive] DESC, [AutoId] ASC)
    INCLUDE ([ShiftStartTime], [ShiftEndTime], [LateTime], [AbsentTime]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_MachineData') AND name = 'IX_ATD_MachineData_FP_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ATD_MachineData_FP_Date]
    ON [dbo].[HRM_ATD_MachineData] ([FingerPrintID] ASC, [Date] ASC)
    INCLUDE ([Time]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_MachineData') AND name = 'IX_HRM_ATD_MachineData_Date_FP_Time')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HRM_ATD_MachineData_Date_FP_Time]
    ON [dbo].[HRM_ATD_MachineData] ([Date] ASC, [FingerPrintID] ASC)
    INCLUDE ([Time]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_Manual') AND name = 'IX_ATD_Manual_Emp_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ATD_Manual_Emp_Date]
    ON [dbo].[HRM_ATD_Manual] ([EmployeeId] ASC, [Date] ASC)
    INCLUDE ([Time], [Remarks]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_EmployeeOfficialInfo') AND name = 'IX_EmpOfficialInfo_Filter')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EmpOfficialInfo_Filter]
    ON [dbo].[HRM_EmployeeOfficialInfo] ([EmployeeID] ASC)
    INCLUDE ([AttendenceID], [EmployeeStatus], [DesignationCode], [DepartmentCode], [CompanyCode], [BranchCode], [ShiftCode], [ReportingTo], [JoiningDate], [HOD]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_EmployeeWeekendDeclaration') AND name = 'IX_EmpWeekendDecl_Emp_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EmpWeekendDecl_Emp_Date]
    ON [dbo].[HRM_EmployeeWeekendDeclaration] ([EmployeeID] ASC, [Date] ASC);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_RosterScheduleEntry') AND name = 'IX_RosterSchedEntry_Emp_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RosterSchedEntry_Emp_Date]
    ON [dbo].[HRM_RosterScheduleEntry] ([EmployeeID] ASC, [Date] ASC)
    INCLUDE ([ShiftCode], [Weekend]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_Holiday') AND name = 'IX_ATD_Holiday_From_To')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ATD_Holiday_From_To]
    ON [dbo].[HRM_ATD_Holiday] ([FromDate] ASC, [ToDate] ASC)
    INCLUDE ([HolidayName]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_LeaveApplicationEntry') AND name = 'IX_LeaveAppEntry_Emp_Start_End')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaveAppEntry_Emp_Start_End]
    ON [dbo].[HRM_LeaveApplicationEntry] ([EmployeeID] ASC, [StartDate] ASC, [EndDate] ASC)
    INCLUDE ([LeaveTypeId], [Reason], [IsApproved], [HODApprovalStatus], [HRApprovalStatus]);
END;
GO

-- =========================================================================================
-- 3. VIEW: [dbo].[vw_HRM_ATD_Shift_MasterDetails]
-- =========================================================================================
CREATE OR ALTER VIEW [dbo].[vw_HRM_ATD_Shift_MasterDetails]
AS
SELECT
    sh.autoId,
    sh.ShiftTypeID,
    sh.ShiftCode,
    sh.ShiftName,
    sh.ShiftShortName,
    sh.Remarks,
    sh.IsActive,
    sd.ShiftStartTime,
    sd.ShiftEndTime,
    sd.LateTime,
    sd.AbsentTime,
    sd.LunchOutTime,
    sd.LunchInTime,
    sd.LunchBreakHour,
    sd.WEF
FROM [dbo].[HRM_ATD_Shift] sh WITH (NOLOCK)
OUTER APPLY (
    SELECT TOP 1
        d.ShiftStartTime,
        d.ShiftEndTime,
        d.LateTime,
        d.AbsentTime,
        d.LunchOutTime,
        d.LunchInTime,
        d.LunchBreakHour,
        d.WEF
    FROM [dbo].[HRM_ATD_ShiftDetails] d WITH (NOLOCK)
    WHERE d.ShiftCode = sh.ShiftCode
    ORDER BY ISNULL(d.IsActive, 1) DESC, d.AutoId ASC
) sd;
GO

-- =========================================================================================
-- 4. PROCEDURE: [dbo].[SP_HrmAtdShift_DashboardCount]
-- =========================================================================================
CREATE OR ALTER PROCEDURE [dbo].[SP_HrmAtdShift_DashboardCount]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        COUNT(*) AS TotalShifts,
        COUNT(CASE WHEN st.ShiftTypeName LIKE '%Day%' OR s.ShiftTypeID = '01' OR s.ShiftTypeID = '1' THEN 1 END) AS DayShifts,
        COUNT(CASE WHEN st.ShiftTypeName LIKE '%Night%' OR s.ShiftTypeID = '02' OR s.ShiftTypeID = '2' THEN 1 END) AS NightShifts,
        COUNT(CASE WHEN ISNULL(dAgg.HasOT, 0) = 1 THEN 1 END) AS WithOvertimeRules,
        COUNT(CASE WHEN ISNULL(dAgg.HasCross, 0) = 1 THEN 1 END) AS CrossMidnight,
        COUNT(CASE WHEN ISNULL(s.IsActive, 1) = 1 THEN 1 END) AS Active,
        COUNT(CASE WHEN ISNULL(s.IsActive, 1) = 0 THEN 1 END) AS Inactive
    FROM [dbo].[HRM_ATD_Shift] s WITH (NOLOCK)
    LEFT JOIN [dbo].[HRM_Def_ShiftType] st WITH (NOLOCK) ON s.ShiftTypeID = st.ShiftTypeId
    OUTER APPLY (
        SELECT 
            MAX(CASE WHEN ISNULL(d.OvertimeRuleID, '') <> '' THEN 1 ELSE 0 END) AS HasOT,
            MAX(CASE WHEN ISNULL(d.IsCrossMidnight, 0) = 1 THEN 1 ELSE 0 END) AS HasCross
        FROM [dbo].[HRM_ATD_ShiftDetails] d WITH (NOLOCK)
        WHERE d.ShiftCode = s.ShiftCode
    ) dAgg;
END;
GO

-- =========================================================================================
-- 5. PROCEDURE: [dbo].[SP_HrmAtdShift_GetList]
-- =========================================================================================
CREATE OR ALTER PROCEDURE [dbo].[SP_HrmAtdShift_GetList]
    @SearchText   VARCHAR(250) = '',
    @ShiftType    VARCHAR(50)  = '',
    @CompanyId    VARCHAR(50)  = '',
    @BranchId     VARCHAR(50)  = '',
    @DepartmentId VARCHAR(50)  = '',
    @Status       VARCHAR(20)  = '',
    @Offset       INT          = 0,
    @PageSize     INT          = 100000
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        s.autoId AS AutoId,
        s.ShiftCode,
        s.ShiftName,
        s.ShiftShortName AS ShiftShortName,
        s.ShiftShortName AS Description,
        s.ShiftTypeID AS ShiftTypeId,
        ISNULL(st.ShiftTypeName, '') AS ShiftTypeName,
        ISNULL(s.Remarks, '') AS Remarks,
        ISNULL(s.IsActive, 1) AS IsActive,
        CASE WHEN ISNULL(s.IsActive, 1) = 1 THEN 'Active' ELSE 'Inactive' END AS IsActiveStatus,
        s.LUser AS Luser,
        s.LDate AS Ldate,
        s.LIP AS Lip,
        s.LMAC AS Lmac,
        s.ModifyDate
    FROM [dbo].[HRM_ATD_Shift] s WITH (NOLOCK)
    LEFT JOIN [dbo].[HRM_Def_ShiftType] st WITH (NOLOCK) ON s.ShiftTypeID = st.ShiftTypeId
    WHERE 
        (@SearchText = '' OR s.ShiftName LIKE '%' + @SearchText + '%' OR s.ShiftCode LIKE '%' + @SearchText + '%' OR s.ShiftShortName LIKE '%' + @SearchText + '%')
        AND (@ShiftType = '' OR ISNULL(s.ShiftTypeID, '') = @ShiftType)
        AND (
            @CompanyId = '' 
            OR EXISTS (
                SELECT 1 FROM [dbo].[HRM_ATD_ShiftDetails] d WITH (NOLOCK)
                WHERE d.ShiftCode = s.ShiftCode AND ISNULL(d.CompanyIds, '') LIKE '%' + @CompanyId + '%'
            )
        )
        AND (
            @BranchId = '' 
            OR EXISTS (
                SELECT 1 FROM [dbo].[HRM_ATD_ShiftDetails] d WITH (NOLOCK)
                WHERE d.ShiftCode = s.ShiftCode AND ISNULL(d.BranchIds, '') LIKE '%' + @BranchId + '%'
            )
        )
        AND (
            @DepartmentId = '' 
            OR EXISTS (
                SELECT 1 FROM [dbo].[HRM_ATD_ShiftDetails] d WITH (NOLOCK)
                WHERE d.ShiftCode = s.ShiftCode AND ISNULL(d.DepartmentIds, '') LIKE '%' + @DepartmentId + '%'
            )
        )
        AND (
            @Status = '' 
            OR (@Status = 'Active' AND ISNULL(s.IsActive, 1) = 1) 
            OR (@Status = 'Inactive' AND ISNULL(s.IsActive, 1) = 0)
        )
    ORDER BY s.autoId DESC;
END;
GO

-- =========================================================================================
-- 6. PROCEDURE: [dbo].[SP_HrmAtdShiftDetails_GetList]
-- =========================================================================================
CREATE OR ALTER PROCEDURE [dbo].[SP_HrmAtdShiftDetails_GetList]
    @ShiftCode NVARCHAR(50) = ''
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        d.AutoId,
        d.ShiftCode,
        d.ShiftStartTime,
        d.ShiftEndTime,
        d.LateTime,
        d.AbsentTime,
        d.LunchOutTime,
        d.LunchInTime,
        ISNULL(d.LunchBreakHour, 0.00) AS LunchBreakHour,
        ISNULL(d.EarlyLeaveTime, '') AS EarlyLeaveTime,
        ISNULL(d.IsCrossMidnight, 0) AS IsCrossMidnight,
        ISNULL(d.CompanyIds, '') AS CompanyIds,
        ISNULL(d.BranchIds, '') AS BranchIds,
        ISNULL(d.DepartmentIds, '') AS DepartmentIds,
        ISNULL(d.EmployeeIds, '') AS EmployeeIds,
        ISNULL(d.OvertimeRuleID, '') AS OvertimeRuleID,
        ISNULL(ot.OvertimeRuleName, '') AS OvertimeRuleName,
        ISNULL(d.OTGraceTime, 60) AS OTGraceTime,
        ISNULL(d.MaxOTPerDay, 4.00) AS MaxOTPerDay,
        ISNULL(d.MinOTMinutes, 50) AS MinOTMinutes,
        ISNULL(d.WeeklyOffOTRule, 'Not Applicable') AS WeeklyOffOTRule,
        ISNULL(d.HolidayOTRule, 'Not Applicable') AS HolidayOTRule,
        d.WEF AS Wef,
        ISNULL(d.Remarks, '') AS Remarks,
        ISNULL(d.IsActive, 1) AS IsActive,
        CASE WHEN ISNULL(d.IsActive, 1) = 1 THEN 'Active' ELSE 'Inactive' END AS IsActiveStatus
    FROM [dbo].[HRM_ATD_ShiftDetails] d WITH (NOLOCK)
    LEFT JOIN [dbo].[HRM_Def_OvertimeRule] ot WITH (NOLOCK) ON d.OvertimeRuleID = ot.OvertimeRuleID
    WHERE (@ShiftCode = '' OR d.ShiftCode = @ShiftCode)
    ORDER BY d.AutoId ASC;
END;
GO

-- =========================================================================================
-- 7. PROCEDURE: [dbo].[usp_JobCardReport]
-- =========================================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =====================================================================
-- PROPER COVERING INDEXES FOR INSTANT JOB CARD REPORT EXECUTION
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_ShiftDetails') AND name = 'IX_HRM_ATD_ShiftDetails_ShiftCode_Active')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HRM_ATD_ShiftDetails_ShiftCode_Active]
    ON [dbo].[HRM_ATD_ShiftDetails] ([ShiftCode] ASC, [IsActive] DESC, [AutoId] ASC)
    INCLUDE ([ShiftStartTime], [ShiftEndTime], [LateTime], [AbsentTime]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_MachineData') AND name = 'IX_ATD_MachineData_FP_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ATD_MachineData_FP_Date]
    ON [dbo].[HRM_ATD_MachineData] ([FingerPrintID] ASC, [Date] ASC)
    INCLUDE ([Time]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_MachineData') AND name = 'IX_HRM_ATD_MachineData_Date_FP_Time')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_HRM_ATD_MachineData_Date_FP_Time]
    ON [dbo].[HRM_ATD_MachineData] ([Date] ASC, [FingerPrintID] ASC)
    INCLUDE ([Time]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_Manual') AND name = 'IX_ATD_Manual_Emp_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ATD_Manual_Emp_Date]
    ON [dbo].[HRM_ATD_Manual] ([EmployeeId] ASC, [Date] ASC)
    INCLUDE ([Time], [Remarks]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_EmployeeOfficialInfo') AND name = 'IX_EmpOfficialInfo_Filter')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EmpOfficialInfo_Filter]
    ON [dbo].[HRM_EmployeeOfficialInfo] ([EmployeeID] ASC)
    INCLUDE ([AttendenceID], [EmployeeStatus], [DesignationCode], [DepartmentCode], [CompanyCode], [BranchCode], [ShiftCode], [ReportingTo], [JoiningDate], [HOD]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_EmployeeWeekendDeclaration') AND name = 'IX_EmpWeekendDecl_Emp_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EmpWeekendDecl_Emp_Date]
    ON [dbo].[HRM_EmployeeWeekendDeclaration] ([EmployeeID] ASC, [Date] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_RosterScheduleEntry') AND name = 'IX_RosterSchedEntry_Emp_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RosterSchedEntry_Emp_Date]
    ON [dbo].[HRM_RosterScheduleEntry] ([EmployeeID] ASC, [Date] ASC)
    INCLUDE ([ShiftCode], [Weekend]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_ATD_Holiday') AND name = 'IX_ATD_Holiday_From_To')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ATD_Holiday_From_To]
    ON [dbo].[HRM_ATD_Holiday] ([FromDate] ASC, [ToDate] ASC)
    INCLUDE ([HolidayName]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.HRM_LeaveApplicationEntry') AND name = 'IX_LeaveAppEntry_Emp_Start_End')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaveAppEntry_Emp_Start_End]
    ON [dbo].[HRM_LeaveApplicationEntry] ([EmployeeID] ASC, [StartDate] ASC, [EndDate] ASC)
    INCLUDE ([LeaveTypeId], [Reason], [IsApproved], [HODApprovalStatus], [HRApprovalStatus]);
END
GO

-- =====================================================================
-- ULTRA-FAST STORED PROCEDURE: dbo.usp_JobCardReport
-- =====================================================================
IF OBJECT_ID('dbo.usp_JobCardReport', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[usp_JobCardReport] AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE [dbo].[usp_JobCardReport]
    @CompanyCode      VARCHAR(50)   = '',
    @BranchCodes      NVARCHAR(MAX) = '',
    @DepartmentCodes  NVARCHAR(MAX) = '',
    @DesignationCodes NVARCHAR(MAX) = '',
    @EmployeeIds      NVARCHAR(MAX) = '',
    @FromDate         DATE          = NULL,
    @ToDate           DATE          = NULL,
    @Month            INT           = NULL,
    @Year             INT           = NULL,
    @LoginEmployeeId  NVARCHAR(50)  = '',
    @AccessCodeId     NVARCHAR(50)  = ''
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    -- Clean parameters
    SET @CompanyCode      = LTRIM(RTRIM(ISNULL(@CompanyCode, '')));
    SET @BranchCodes      = LTRIM(RTRIM(ISNULL(@BranchCodes, '')));
    SET @DepartmentCodes  = LTRIM(RTRIM(ISNULL(@DepartmentCodes, '')));
    SET @DesignationCodes = LTRIM(RTRIM(ISNULL(@DesignationCodes, '')));
    SET @EmployeeIds      = LTRIM(RTRIM(ISNULL(@EmployeeIds, '')));
    SET @LoginEmployeeId  = LTRIM(RTRIM(ISNULL(@LoginEmployeeId, '')));
    SET @AccessCodeId     = LTRIM(RTRIM(ISNULL(@AccessCodeId, '')));

    -- Date handling: By Month or By Date
    IF (@FromDate IS NULL OR @FromDate = '1900-01-01') AND (@Month IS NOT NULL AND @Year IS NOT NULL AND @Month BETWEEN 1 AND 12 AND @Year > 1900)
    BEGIN
        SET @FromDate = DATEFROMPARTS(@Year, @Month, 1);
        SET @ToDate   = EOMONTH(@FromDate);
    END
    ELSE
    BEGIN
        IF @FromDate IS NULL OR @FromDate = '1900-01-01'
            SET @FromDate = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

        IF @ToDate IS NULL OR @ToDate = '1900-01-01'
            SET @ToDate = CONVERT(DATE, GETDATE());
    END

    IF @ToDate < @FromDate
    BEGIN
        DECLARE @TempDate DATE = @FromDate;
        SET @FromDate = @ToDate;
        SET @ToDate = @TempDate;
    END

    DECLARE @NextToDate DATE = DATEADD(DAY, 1, @ToDate);
    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @NextToDateTime DATETIME = CAST(@NextToDate AS DATETIME);

    -- Role-based access control
    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId NOT IN ('0002','0003','0005') THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003')           THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'                     THEN 1 ELSE 0 END;

    IF @AccessCodeId = '' OR @LoginEmployeeId = ''
        SET @IsAdmin = 1;

    -- Fast indexed filter tables matching NVARCHAR(50)
    CREATE TABLE #CompFilter   (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #BranchFilter (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DeptFilter   (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DesigFilter  (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #EmpFilter    (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    IF @CompanyCode <> ''
    BEGIN
        INSERT INTO #CompFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @CompanyCode)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @BranchCodes <> ''
    BEGIN
        INSERT INTO #BranchFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @BranchCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @DepartmentCodes <> ''
    BEGIN
        INSERT INTO #DeptFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @DepartmentCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @DesignationCodes <> ''
    BEGIN
        INSERT INTO #DesigFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @DesignationCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @EmployeeIds <> ''
    BEGIN
        INSERT INTO #EmpFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @EmployeeIds)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    -- Pre-compute Shift Lookup once (only 2-5 rows) for O(1) join speed
    CREATE TABLE #ShiftLookup (
        ShiftCode    NVARCHAR(50)  COLLATE DATABASE_DEFAULT PRIMARY KEY,
        ShiftDisplay NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftStart   TIME(0),
        ShiftEnd     TIME(0),
        LateCutoff   TIME(0)
    );

    INSERT INTO #ShiftLookup (ShiftCode, ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff)
    SELECT
        sh.ShiftCode,
        CASE
            WHEN sd.ShiftStartTime IS NOT NULL AND sd.ShiftEndTime IS NOT NULL THEN
                (CASE
                    WHEN LEN(ISNULL(sh.ShiftName,'')) BETWEEN 1 AND 5 THEN sh.ShiftName
                    WHEN LEN(ISNULL(sh.ShiftShortName,'')) BETWEEN 1 AND 5 THEN sh.ShiftShortName
                    ELSE ISNULL(NULLIF(sh.ShiftName,''), 'G')
                 END) + '(' +
                SUBSTRING(CONVERT(VARCHAR(8), sd.ShiftStartTime, 108), 1, 5) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), sd.ShiftEndTime, 108), 1, 5) + ')'
            ELSE 'Day(09:00-18:00)'
        END,
        ISNULL(CAST(sd.ShiftStartTime AS TIME(0)), CAST('09:00:00' AS TIME(0))),
        ISNULL(CAST(sd.ShiftEndTime AS TIME(0)), CAST('18:00:00' AS TIME(0))),
        ISNULL(CAST(sd.LateTime AS TIME(0)), CAST('09:15:00' AS TIME(0)))
    FROM dbo.HRM_ATD_Shift sh WITH (NOLOCK)
    OUTER APPLY (
        SELECT TOP 1 d.ShiftStartTime, d.ShiftEndTime, d.LateTime
        FROM dbo.HRM_ATD_ShiftDetails d WITH (NOLOCK)
        WHERE d.ShiftCode = sh.ShiftCode
        ORDER BY ISNULL(d.IsActive, 1) DESC, d.AutoId ASC
    ) sd
    WHERE sh.ShiftCode IS NOT NULL AND sh.ShiftCode <> '';

    -- Active Employees (Target group filtered early for maximum performance)
    CREATE TABLE #ActiveEmployees (
        EmployeeId      NVARCHAR(50)   COLLATE DATABASE_DEFAULT PRIMARY KEY,
        AttendenceId    NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        EmployeeName    NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        DesignationCode NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        Designation     NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        DepartmentCode  NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        DepartmentName  NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        CompanyCode     NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        CompanyName     NVARCHAR(300)  COLLATE DATABASE_DEFAULT,
        ShiftDisplay    NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        ShiftStart      TIME(0),
        ShiftEnd        TIME(0),
        LateCutoff      TIME(0)
    );

    INSERT INTO #ActiveEmployees (
        EmployeeId, AttendenceId, EmployeeName,
        DesignationCode, Designation,
        DepartmentCode, DepartmentName,
        CompanyCode, CompanyName,
        ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff
    )
    SELECT
        e.EmployeeId,
        ISNULL(LTRIM(RTRIM(oi.AttendenceId)), ''),
        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))), ''), ISNULL(e.EmployeeId, '')),
        ISNULL(oi.DesignationCode, ''),
        ISNULL(NULLIF(desig.DesignationName, ''), ISNULL(NULLIF(oi.DesignationCode, ''), '-')),
        ISNULL(oi.DepartmentCode, ''),
        ISNULL(NULLIF(dept.DepartmentName, ''), ISNULL(NULLIF(oi.DepartmentCode, ''), 'General Department')),
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'DataPath Ltd.'),
        ISNULL(s.ShiftDisplay, 'Day(09:00-18:00)'),
        ISNULL(s.ShiftStart, CAST('09:00:00' AS TIME(0))),
        ISNULL(s.ShiftEnd, CAST('18:00:00' AS TIME(0))),
        ISNULL(s.LateCutoff, CAST('09:15:00' AS TIME(0)))
    FROM HRM_Employee e WITH (NOLOCK)
    INNER JOIN HRM_EmployeeOfficialInfo oi WITH (NOLOCK) ON oi.EmployeeId = e.EmployeeId
    LEFT  JOIN Core_Company comp WITH (NOLOCK) ON comp.CompanyCode = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT  JOIN HRM_Def_Department dept WITH (NOLOCK) ON dept.DepartmentCode = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation desig WITH (NOLOCK) ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT  JOIN #ShiftLookup s ON s.ShiftCode = oi.ShiftCode AND oi.ShiftCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (NOT EXISTS (SELECT 1 FROM #CompFilter)   OR oi.CompanyCode IN (SELECT Code FROM #CompFilter))
      AND (NOT EXISTS (SELECT 1 FROM #BranchFilter) OR oi.BranchCode IN (SELECT Code FROM #BranchFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DeptFilter)   OR oi.DepartmentCode IN (SELECT Code FROM #DeptFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DesigFilter)  OR oi.DesignationCode IN (SELECT Code FROM #DesigFilter))
      AND (NOT EXISTS (SELECT 1 FROM #EmpFilter)    OR e.EmployeeId IN (SELECT Code FROM #EmpFilter))
      AND (
            @IsAdmin = 1
            OR (
                 @IsTeamLeader = 1
                 AND (
                       e.EmployeeId   = @LoginEmployeeId
                    OR (oi.ReportingTo <> '' AND oi.ReportingTo = @LoginEmployeeId)
                    OR (oi.HOD <> '' AND oi.HOD = @LoginEmployeeId)
                 )
               )
            OR (@IsUser = 1 AND e.EmployeeId = @LoginEmployeeId)
          )
      AND (oi.JoiningDate IS NULL OR oi.JoiningDate = '1900-01-01' OR CONVERT(DATE, oi.JoiningDate) <= @ToDate);

    CREATE NONCLUSTERED INDEX IX_ActiveEmp_Dept_Emp ON #ActiveEmployees (DepartmentName, EmployeeId);

    -- Fingerprints mapping for 100% direct indexed equality joins to MachineData
    CREATE TABLE #EmpFingerprints (
        FingerPrintId NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        EmployeeId    NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (FingerPrintId, EmployeeId)
    );

    INSERT INTO #EmpFingerprints (FingerPrintId, EmployeeId)
    SELECT DISTINCT EmployeeId, EmployeeId FROM #ActiveEmployees WHERE EmployeeId <> ''
    UNION
    SELECT DISTINCT AttendenceId, EmployeeId FROM #ActiveEmployees WHERE AttendenceId <> '';

    -- Fast Date Series
    CREATE TABLE #Dates ([Date] DATE PRIMARY KEY);

    ;WITH DateSeries AS (
        SELECT @FromDate AS [Date]
        UNION ALL
        SELECT DATEADD(DAY, 1, [Date])
        FROM DateSeries
        WHERE [Date] < @ToDate
    )
    INSERT INTO #Dates ([Date])
    SELECT [Date]
    FROM DateSeries
    OPTION (MAXRECURSION 0);

    -- Punches (Machine + Manual unified with SARGable index seek)
    CREATE TABLE #Punches (
        EmployeeId    NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        PunchDate     DATE,
        FirstPunch    DATETIME,
        LastPunch     DATETIME,
        ManualRemarks NVARCHAR(500) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, PunchDate)
    );

    ;WITH RawMachine AS (
        SELECT 
            ef.EmployeeId,
            CONVERT(DATE, md.[Date]) AS PunchDate,
            MIN(md.[Time]) AS FirstPunch,
            MAX(md.[Time]) AS LastPunch
        FROM HRM_ATD_MachineData md WITH (NOLOCK)
        INNER JOIN #EmpFingerprints ef ON md.FingerPrintId = ef.FingerPrintId
        WHERE md.[Date] >= @FromDateTime AND md.[Date] < @NextToDateTime
          AND md.[Time] IS NOT NULL AND md.[Time] <> '1900-01-01'
        GROUP BY ef.EmployeeId, CONVERT(DATE, md.[Date])
    ),
    RawManual AS (
        SELECT 
            ae.EmployeeId,
            CONVERT(DATE, m.[Date]) AS PunchDate,
            MIN(m.[Time]) AS FirstPunch,
            MAX(m.[Time]) AS LastPunch,
            MAX(ISNULL(m.Remarks, '')) AS ManualRemarks
        FROM HRM_ATD_Manual m WITH (NOLOCK)
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = m.EmployeeId
        WHERE m.[Date] >= @FromDateTime AND m.[Date] < @NextToDateTime
          AND m.[Time] IS NOT NULL AND m.[Time] <> '1900-01-01'
        GROUP BY ae.EmployeeId, CONVERT(DATE, m.[Date])
    ),
    CombinedPunches AS (
        SELECT
            COALESCE(rm.EmployeeId, rman.EmployeeId) AS EmployeeId,
            COALESCE(rm.PunchDate, rman.PunchDate)   AS PunchDate,
            CASE
                WHEN rm.FirstPunch IS NULL THEN rman.FirstPunch
                WHEN rman.FirstPunch IS NULL THEN rm.FirstPunch
                WHEN rm.FirstPunch <= rman.FirstPunch THEN rm.FirstPunch
                ELSE rman.FirstPunch
            END AS FirstPunch,
            CASE
                WHEN rm.LastPunch IS NULL THEN rman.LastPunch
                WHEN rman.LastPunch IS NULL THEN rm.LastPunch
                WHEN rm.LastPunch >= rman.LastPunch THEN rm.LastPunch
                ELSE rman.LastPunch
            END AS LastPunch,
            rman.ManualRemarks
        FROM RawMachine rm
        FULL OUTER JOIN RawManual rman 
            ON rm.EmployeeId = rman.EmployeeId AND rm.PunchDate = rman.PunchDate
    )
    INSERT INTO #Punches (EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks)
    SELECT EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks
    FROM CombinedPunches;

    -- Leaves (filtered to target date range)
    CREATE TABLE #EmpLeaves (
        EmployeeId NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        LeaveDate  DATE,
        LeaveCode  NVARCHAR(20)  COLLATE DATABASE_DEFAULT,
        Reason     NVARCHAR(500) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, LeaveDate)
    );

    ;WITH ApprovedLeaves AS (
        SELECT 
            la.EmployeeId,
            d.[Date] AS LeaveDate,
            ISNULL(NULLIF(lt.ShortName, ''), 'L') AS LeaveCode,
            ISNULL(NULLIF(LTRIM(RTRIM(la.Reason)), ''), '') AS Reason
        FROM HRM_LeaveApplicationEntry la WITH (NOLOCK)
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = la.EmployeeId
        INNER JOIN #Dates d ON d.[Date] >= CONVERT(DATE, la.StartDate) AND d.[Date] <= CONVERT(DATE, la.EndDate)
        LEFT  JOIN HRM_ATD_LeaveType lt WITH (NOLOCK) ON lt.LeaveTypeCode = la.LeaveTypeId
        WHERE la.StartDate < @NextToDateTime AND la.EndDate >= @FromDateTime
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    INSERT INTO #EmpLeaves (EmployeeId, LeaveDate, LeaveCode, Reason)
    SELECT EmployeeId, LeaveDate, MAX(LeaveCode), MAX(Reason)
    FROM ApprovedLeaves
    GROUP BY EmployeeId, LeaveDate;

    -- Holidays (only within target date range)
    CREATE TABLE #Holidays (
        HolidayDate DATE PRIMARY KEY,
        HolidayName NVARCHAR(200) COLLATE DATABASE_DEFAULT
    );

    INSERT INTO #Holidays (HolidayDate, HolidayName)
    SELECT v.HolidayDate, v.HolidayName
    FROM (VALUES 
        (CAST('2026-01-01' AS DATE), N'New Year''s Day'),
        (CAST('2026-02-21' AS DATE), N'International Mother Language Day'),
        (CAST('2026-03-19' AS DATE), N'Holiday'),
        (CAST('2026-03-20' AS DATE), N'Holiday'),
        (CAST('2026-03-26' AS DATE), N'Independence Day'),
        (CAST('2026-04-14' AS DATE), N'Bengali New Year'),
        (CAST('2026-05-01' AS DATE), N'May Day'),
        (CAST('2026-12-16' AS DATE), N'Victory Day'),
        (CAST('2026-12-25' AS DATE), N'Christmas Day')
    ) v(HolidayDate, HolidayName)
    WHERE v.HolidayDate >= @FromDate AND v.HolidayDate <= @ToDate;

    INSERT INTO #Holidays (HolidayDate, HolidayName)
    SELECT d.[Date], MAX(ISNULL(NULLIF(h.HolidayName, ''), 'Holiday'))
    FROM #Dates d
    INNER JOIN HRM_ATD_Holiday h WITH (NOLOCK) ON d.[Date] >= CONVERT(DATE, h.FromDate) AND d.[Date] <= CONVERT(DATE, h.ToDate)
    WHERE h.FromDate < @NextToDateTime AND h.ToDate >= @FromDateTime
      AND NOT EXISTS (SELECT 1 FROM #Holidays WHERE HolidayDate = d.[Date])
    GROUP BY d.[Date];

    -- Weekends (filtered to target date range)
    CREATE TABLE #EmpWeekends (
        EmployeeId  NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        WeekendDate DATE,
        PRIMARY KEY (EmployeeId, WeekendDate)
    );

    INSERT INTO #EmpWeekends (EmployeeId, WeekendDate)
    SELECT DISTINCT ewd.EmployeeId, CONVERT(DATE, ewd.[Date])
    FROM HRM_EmployeeWeekendDeclaration ewd WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = ewd.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, ewd.[Date])
    WHERE ewd.[Date] >= @FromDateTime AND ewd.[Date] < @NextToDateTime;

    -- Roster Overrides (filtered to target date range)
    CREATE TABLE #EmpRosters (
        EmployeeId   NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        RosterDate   DATE,
        ShiftDisplay NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftStart   TIME(0),
        ShiftEnd     TIME(0),
        LateCutoff   TIME(0),
        IsWeekend    BIT,
        PRIMARY KEY (EmployeeId, RosterDate)
    );

    INSERT INTO #EmpRosters (EmployeeId, RosterDate, ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff, IsWeekend)
    SELECT 
        rse.EmployeeId,
        CONVERT(DATE, rse.[Date]),
        ISNULL(s.ShiftDisplay, 'Day(09:00-18:00)'),
        ISNULL(s.ShiftStart, CAST('09:00:00' AS TIME(0))),
        ISNULL(s.ShiftEnd, CAST('18:00:00' AS TIME(0))),
        ISNULL(s.LateCutoff, CAST('09:15:00' AS TIME(0))),
        CASE WHEN rse.Weekend = 'Y' OR rse.Weekend = '1' THEN 1 ELSE 0 END
    FROM HRM_RosterScheduleEntry rse WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = rse.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, rse.[Date])
    LEFT JOIN #ShiftLookup s ON s.ShiftCode = rse.ShiftCode
    WHERE rse.[Date] >= @FromDateTime AND rse.[Date] < @NextToDateTime;

    -- Final calculation CTE
    ;WITH BaseRows AS (
        SELECT
            ae.DepartmentCode,
            ae.DepartmentName,
            ae.DesignationCode,
            ae.Designation,
            ae.EmployeeId,
            ae.EmployeeName,
            ae.CompanyName,
            d.[Date],
            DATENAME(WEEKDAY, d.[Date]) AS [Day],
            COALESCE(ros.ShiftDisplay, ae.ShiftDisplay) AS ShiftDisplay,
            COALESCE(ros.ShiftStart, ae.ShiftStart) AS ShiftStart,
            COALESCE(ros.ShiftEnd, ae.ShiftEnd) AS ShiftEnd,
            COALESCE(ros.LateCutoff, ae.LateCutoff) AS LateCutoff,
            p.FirstPunch,
            p.LastPunch,
            p.ManualRemarks,
            lv.LeaveCode,
            lv.Reason AS LeaveReason,
            h.HolidayName,
            CASE
                WHEN ros.IsWeekend = 1 THEN 1
                WHEN ew.WeekendDate IS NOT NULL THEN 1
                WHEN DATENAME(WEEKDAY, d.[Date]) IN ('Friday', 'Saturday') THEN 1
                ELSE 0
            END AS IsWeekend
        FROM #ActiveEmployees ae
        CROSS JOIN #Dates d
        LEFT JOIN #Punches p ON p.EmployeeId = ae.EmployeeId AND p.PunchDate = d.[Date]
        LEFT JOIN #EmpLeaves lv ON lv.EmployeeId = ae.EmployeeId AND lv.LeaveDate = d.[Date]
        LEFT JOIN #Holidays h ON h.HolidayDate = d.[Date]
        LEFT JOIN #EmpWeekends ew ON ew.EmployeeId = ae.EmployeeId AND ew.WeekendDate = d.[Date]
        LEFT JOIN #EmpRosters ros ON ros.EmployeeId = ae.EmployeeId AND ros.RosterDate = d.[Date]
    ),
    SecCalculated AS (
        SELECT
            DepartmentCode,
            DepartmentName,
            DesignationCode,
            Designation,
            EmployeeId,
            EmployeeName,
            CompanyName,
            [Date],
            CONVERT(VARCHAR(10), [Date], 103) AS DateDisplay, -- dd/MM/yyyy
            [Day],
            ShiftDisplay,
            ShiftStart,
            ShiftEnd,
            LateCutoff,
            FirstPunch,
            LastPunch,
            ManualRemarks,
            LeaveCode,
            LeaveReason,
            HolidayName,
            IsWeekend,
            CAST(FirstPunch AS TIME(0)) AS FirstPunchTime,
            CAST(LastPunch AS TIME(0))  AS LastPunchTime,
            CASE
                WHEN IsWeekend = 1 OR HolidayName IS NOT NULL OR LeaveCode IS NOT NULL THEN 0
                WHEN FirstPunch IS NOT NULL AND CAST(FirstPunch AS TIME(0)) > LateCutoff
                THEN DATEDIFF(SECOND, LateCutoff, CAST(FirstPunch AS TIME(0)))
                ELSE 0
            END AS LateSec,
            CASE
                WHEN IsWeekend = 1 OR HolidayName IS NOT NULL OR LeaveCode IS NOT NULL THEN 0
                WHEN LastPunch IS NOT NULL AND CAST(LastPunch AS TIME(0)) <> CAST(FirstPunch AS TIME(0))
                 AND CAST(LastPunch AS TIME(0)) < ShiftEnd
                THEN DATEDIFF(SECOND, CAST(LastPunch AS TIME(0)), ShiftEnd)
                ELSE 0
            END AS EarlyOutSec,
            CASE
                WHEN FirstPunch IS NOT NULL AND LastPunch IS NOT NULL AND CAST(LastPunch AS TIME(0)) <> CAST(FirstPunch AS TIME(0))
                THEN DATEDIFF(SECOND, FirstPunch, LastPunch)
                ELSE 0
            END AS WorkSec
        FROM BaseRows
    )
    SELECT
        DepartmentCode,
        DepartmentName,
        DesignationCode,
        Designation,
        EmployeeId,
        EmployeeName,
        CompanyName,
        [Date],
        DateDisplay,
        [Day],
        CASE WHEN FirstPunch IS NOT NULL THEN ShiftDisplay ELSE '' END AS Shift,
        CASE 
            WHEN FirstPunch IS NOT NULL THEN 
                LTRIM(RIGHT(CONVERT(VARCHAR(30), FirstPunch, 100), 7))
            ELSE ''
        END AS InTime,
        CASE
            WHEN LateSec > 0 THEN
                RIGHT('00' + CAST(LateSec / 3600 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST((LateSec % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST(LateSec % 60 AS VARCHAR(2)), 2)
            ELSE ''
        END AS Late,
        CASE
            WHEN LastPunch IS NOT NULL AND LastPunchTime <> FirstPunchTime THEN 
                LTRIM(RIGHT(CONVERT(VARCHAR(30), LastPunch, 100), 7))
            ELSE ''
        END AS OutTime,
        CASE
            WHEN EarlyOutSec > 0 THEN
                RIGHT('00' + CAST(EarlyOutSec / 3600 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST((EarlyOutSec % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST(EarlyOutSec % 60 AS VARCHAR(2)), 2)
            ELSE ''
        END AS EarlyOut,
        CASE
            WHEN WorkSec > 0 THEN
                CAST(ROUND(CAST(WorkSec AS FLOAT) / 3600.0, 2) AS VARCHAR(10))
            ELSE ''
        END AS WorkHours,
        CASE
            WHEN LeaveCode IS NOT NULL THEN LeaveCode
            WHEN HolidayName IS NOT NULL THEN 'H'
            WHEN IsWeekend = 1 THEN 'W'
            WHEN FirstPunch IS NOT NULL THEN
                CASE WHEN FirstPunchTime > LateCutoff THEN 'L' ELSE 'P' END
            ELSE 'A'
        END AS [Status],
        COALESCE(NULLIF(LeaveReason, ''), NULLIF(ManualRemarks, ''), '') AS Remarks
    FROM SecCalculated
    ORDER BY DepartmentName, EmployeeId, [Date]
    OPTION (RECOMPILE);

    -- Header Result Set (2nd result set)
    SELECT
        ISNULL((SELECT TOP 1 CompanyName FROM #ActiveEmployees), 'DataPath Ltd.') AS CompanyName,
        CONVERT(VARCHAR(10), @FromDate, 103) AS FromDate,
        CONVERT(VARCHAR(10), @ToDate, 103)   AS ToDate,
        'Date: ' + CONVERT(VARCHAR(10), @FromDate, 103) + ' - ' + CONVERT(VARCHAR(10), @ToDate, 103) AS DateRangeDisplay;

    -- Cleanup
    DROP TABLE #CompFilter;
    DROP TABLE #BranchFilter;
    DROP TABLE #DeptFilter;
    DROP TABLE #DesigFilter;
    DROP TABLE #EmpFilter;
    DROP TABLE #ShiftLookup;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #EmpFingerprints;
    DROP TABLE #Dates;
    DROP TABLE #Punches;
    DROP TABLE #EmpLeaves;
    DROP TABLE #Holidays;
    DROP TABLE #EmpWeekends;
    DROP TABLE #EmpRosters;
END;
GO

GO

-- =========================================================================================
-- 8. PROCEDURE: [dbo].[usp_JobCardBulkReport]
-- =========================================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.usp_JobCardBulkReport', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[usp_JobCardBulkReport] AS SET NOCOUNT ON;');
GO

ALTER PROCEDURE [dbo].[usp_JobCardBulkReport]
    @CompanyCode      VARCHAR(50)   = '',
    @BranchCodes      NVARCHAR(MAX) = '',
    @DepartmentCodes  NVARCHAR(MAX) = '',
    @DesignationCodes NVARCHAR(MAX) = '',
    @EmployeeIds      NVARCHAR(MAX) = '',
    @FromDate         DATE          = NULL,
    @ToDate           DATE          = NULL,
    @Month            INT           = NULL,
    @Year             INT           = NULL,
    @LoginEmployeeId  NVARCHAR(50)  = '',
    @AccessCodeId     NVARCHAR(50)  = ''
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    -- Clean parameters
    SET @CompanyCode      = LTRIM(RTRIM(ISNULL(@CompanyCode, '')));
    SET @BranchCodes      = LTRIM(RTRIM(ISNULL(@BranchCodes, '')));
    SET @DepartmentCodes  = LTRIM(RTRIM(ISNULL(@DepartmentCodes, '')));
    SET @DesignationCodes = LTRIM(RTRIM(ISNULL(@DesignationCodes, '')));
    SET @EmployeeIds      = LTRIM(RTRIM(ISNULL(@EmployeeIds, '')));
    SET @LoginEmployeeId  = LTRIM(RTRIM(ISNULL(@LoginEmployeeId, '')));
    SET @AccessCodeId     = LTRIM(RTRIM(ISNULL(@AccessCodeId, '')));

    -- Date handling: By Month or By Date
    IF (@FromDate IS NULL OR @FromDate = '1900-01-01') AND (@Month IS NOT NULL AND @Year IS NOT NULL AND @Month BETWEEN 1 AND 12 AND @Year > 1900)
    BEGIN
        SET @FromDate = DATEFROMPARTS(@Year, @Month, 1);
        SET @ToDate   = EOMONTH(@FromDate);
    END
    ELSE
    BEGIN
        IF @FromDate IS NULL OR @FromDate = '1900-01-01'
            SET @FromDate = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

        IF @ToDate IS NULL OR @ToDate = '1900-01-01'
            SET @ToDate = CONVERT(DATE, GETDATE());
    END

    IF @ToDate < @FromDate
    BEGIN
        DECLARE @TempDate DATE = @FromDate;
        SET @FromDate = @ToDate;
        SET @ToDate = @TempDate;
    END

    DECLARE @NextToDate DATE = DATEADD(DAY, 1, @ToDate);
    DECLARE @FromDateTime DATETIME = CAST(@FromDate AS DATETIME);
    DECLARE @NextToDateTime DATETIME = CAST(@NextToDate AS DATETIME);

    -- Role-based access control
    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId NOT IN ('0002','0003','0005') THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003')           THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'                     THEN 1 ELSE 0 END;

    IF @AccessCodeId = '' OR @LoginEmployeeId = ''
        SET @IsAdmin = 1;

    -- Fast indexed filter tables matching NVARCHAR(50)
    CREATE TABLE #CompFilter   (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #BranchFilter (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DeptFilter   (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DesigFilter  (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #EmpFilter    (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    IF @CompanyCode <> ''
    BEGIN
        INSERT INTO #CompFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @CompanyCode)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @BranchCodes <> ''
    BEGIN
        INSERT INTO #BranchFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @BranchCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @DepartmentCodes <> ''
    BEGIN
        INSERT INTO #DeptFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @DepartmentCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @DesignationCodes <> ''
    BEGIN
        INSERT INTO #DesigFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @DesignationCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @EmployeeIds <> ''
    BEGIN
        INSERT INTO #EmpFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @EmployeeIds)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    -- Pre-compute Shift Lookup once (only 2-5 rows) for O(1) join speed
    CREATE TABLE #ShiftLookup (
        ShiftCode    NVARCHAR(50)  COLLATE DATABASE_DEFAULT PRIMARY KEY,
        ShiftDisplay NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftStart   TIME(0),
        ShiftEnd     TIME(0),
        LateCutoff   TIME(0)
    );

    INSERT INTO #ShiftLookup (ShiftCode, ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff)
    SELECT
        sh.ShiftCode,
        CASE
            WHEN sd.ShiftStartTime IS NOT NULL AND sd.ShiftEndTime IS NOT NULL THEN
                (CASE
                    WHEN LEN(ISNULL(sh.ShiftName,'')) BETWEEN 1 AND 5 THEN sh.ShiftName
                    WHEN LEN(ISNULL(sh.ShiftShortName,'')) BETWEEN 1 AND 5 THEN sh.ShiftShortName
                    ELSE ISNULL(NULLIF(sh.ShiftName,''), 'G')
                 END) + '(' +
                SUBSTRING(CONVERT(VARCHAR(8), sd.ShiftStartTime, 108), 1, 5) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), sd.ShiftEndTime, 108), 1, 5) + ')'
            ELSE 'Day(09:00-18:00)'
        END,
        ISNULL(CAST(sd.ShiftStartTime AS TIME(0)), CAST('09:00:00' AS TIME(0))),
        ISNULL(CAST(sd.ShiftEndTime AS TIME(0)), CAST('18:00:00' AS TIME(0))),
        ISNULL(CAST(sd.LateTime AS TIME(0)), CAST('09:15:00' AS TIME(0)))
    FROM dbo.HRM_ATD_Shift sh WITH (NOLOCK)
    OUTER APPLY (
        SELECT TOP 1 d.ShiftStartTime, d.ShiftEndTime, d.LateTime
        FROM dbo.HRM_ATD_ShiftDetails d WITH (NOLOCK)
        WHERE d.ShiftCode = sh.ShiftCode
        ORDER BY ISNULL(d.IsActive, 1) DESC, d.AutoId ASC
    ) sd
    WHERE sh.ShiftCode IS NOT NULL AND sh.ShiftCode <> '';

    -- Active Employees (Target group filtered early for maximum performance)
    CREATE TABLE #ActiveEmployees (
        EmployeeId      NVARCHAR(50)   COLLATE DATABASE_DEFAULT PRIMARY KEY,
        AttendenceId    NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        EmployeeName    NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        DesignationCode NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        Designation     NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        DepartmentCode  NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        DepartmentName  NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        CompanyCode     NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        CompanyName     NVARCHAR(300)  COLLATE DATABASE_DEFAULT,
        ShiftDisplay    NVARCHAR(200)  COLLATE DATABASE_DEFAULT,
        ShiftStart      TIME(0),
        ShiftEnd        TIME(0),
        LateCutoff      TIME(0)
    );

    INSERT INTO #ActiveEmployees (
        EmployeeId, AttendenceId, EmployeeName,
        DesignationCode, Designation,
        DepartmentCode, DepartmentName,
        CompanyCode, CompanyName,
        ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff
    )
    SELECT
        e.EmployeeId,
        ISNULL(LTRIM(RTRIM(oi.AttendenceId)), ''),
        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))), ''), ISNULL(e.EmployeeId, '')),
        ISNULL(oi.DesignationCode, ''),
        ISNULL(NULLIF(desig.DesignationName, ''), ISNULL(NULLIF(oi.DesignationCode, ''), '-')),
        ISNULL(oi.DepartmentCode, ''),
        ISNULL(NULLIF(dept.DepartmentName, ''), ISNULL(NULLIF(oi.DepartmentCode, ''), 'General Department')),
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'DataPath Ltd.'),
        ISNULL(s.ShiftDisplay, 'Day(09:00-18:00)'),
        ISNULL(s.ShiftStart, CAST('09:00:00' AS TIME(0))),
        ISNULL(s.ShiftEnd, CAST('18:00:00' AS TIME(0))),
        ISNULL(s.LateCutoff, CAST('09:15:00' AS TIME(0)))
    FROM HRM_Employee e WITH (NOLOCK)
    INNER JOIN HRM_EmployeeOfficialInfo oi WITH (NOLOCK) ON oi.EmployeeId = e.EmployeeId
    LEFT  JOIN Core_Company comp WITH (NOLOCK) ON comp.CompanyCode = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT  JOIN HRM_Def_Department dept WITH (NOLOCK) ON dept.DepartmentCode = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation desig WITH (NOLOCK) ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT  JOIN #ShiftLookup s ON s.ShiftCode = oi.ShiftCode AND oi.ShiftCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (NOT EXISTS (SELECT 1 FROM #CompFilter)   OR oi.CompanyCode IN (SELECT Code FROM #CompFilter))
      AND (NOT EXISTS (SELECT 1 FROM #BranchFilter) OR oi.BranchCode IN (SELECT Code FROM #BranchFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DeptFilter)   OR oi.DepartmentCode IN (SELECT Code FROM #DeptFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DesigFilter)  OR oi.DesignationCode IN (SELECT Code FROM #DesigFilter))
      AND (NOT EXISTS (SELECT 1 FROM #EmpFilter)    OR e.EmployeeId IN (SELECT Code FROM #EmpFilter))
      AND (
            @IsAdmin = 1
            OR (
                 @IsTeamLeader = 1
                 AND (
                       e.EmployeeId   = @LoginEmployeeId
                    OR (oi.ReportingTo <> '' AND oi.ReportingTo = @LoginEmployeeId)
                    OR (oi.HOD <> '' AND oi.HOD = @LoginEmployeeId)
                 )
               )
            OR (@IsUser = 1 AND e.EmployeeId = @LoginEmployeeId)
          )
      AND (oi.JoiningDate IS NULL OR oi.JoiningDate = '1900-01-01' OR CONVERT(DATE, oi.JoiningDate) <= @ToDate);

    CREATE NONCLUSTERED INDEX IX_ActiveEmp_Dept_Emp ON #ActiveEmployees (DepartmentName, EmployeeId);

    -- Fingerprints mapping for 100% direct indexed equality joins to MachineData
    CREATE TABLE #EmpFingerprints (
        FingerPrintId NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        EmployeeId    NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (FingerPrintId, EmployeeId)
    );

    INSERT INTO #EmpFingerprints (FingerPrintId, EmployeeId)
    SELECT DISTINCT EmployeeId, EmployeeId FROM #ActiveEmployees WHERE EmployeeId <> ''
    UNION
    SELECT DISTINCT AttendenceId, EmployeeId FROM #ActiveEmployees WHERE AttendenceId <> '';

    -- Fast Date Series
    CREATE TABLE #Dates ([Date] DATE PRIMARY KEY);

    ;WITH DateSeries AS (
        SELECT @FromDate AS [Date]
        UNION ALL
        SELECT DATEADD(DAY, 1, [Date])
        FROM DateSeries
        WHERE [Date] < @ToDate
    )
    INSERT INTO #Dates ([Date])
    SELECT [Date]
    FROM DateSeries
    OPTION (MAXRECURSION 0);

    -- Punches (Machine + Manual unified with SARGable index seek)
    CREATE TABLE #Punches (
        EmployeeId    NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        PunchDate     DATE,
        FirstPunch    DATETIME,
        LastPunch     DATETIME,
        ManualRemarks NVARCHAR(500) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, PunchDate)
    );

    ;WITH RawMachine AS (
        SELECT 
            ef.EmployeeId,
            CONVERT(DATE, md.[Date]) AS PunchDate,
            MIN(md.[Time]) AS FirstPunch,
            MAX(md.[Time]) AS LastPunch
        FROM HRM_ATD_MachineData md WITH (NOLOCK)
        INNER JOIN #EmpFingerprints ef ON md.FingerPrintId = ef.FingerPrintId
        WHERE md.[Date] >= @FromDateTime AND md.[Date] < @NextToDateTime
          AND md.[Time] IS NOT NULL AND md.[Time] <> '1900-01-01'
        GROUP BY ef.EmployeeId, CONVERT(DATE, md.[Date])
    ),
    RawManual AS (
        SELECT 
            ae.EmployeeId,
            CONVERT(DATE, m.[Date]) AS PunchDate,
            MIN(m.[Time]) AS FirstPunch,
            MAX(m.[Time]) AS LastPunch,
            MAX(ISNULL(m.Remarks, '')) AS ManualRemarks
        FROM HRM_ATD_Manual m WITH (NOLOCK)
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = m.EmployeeId
        WHERE m.[Date] >= @FromDateTime AND m.[Date] < @NextToDateTime
          AND m.[Time] IS NOT NULL AND m.[Time] <> '1900-01-01'
        GROUP BY ae.EmployeeId, CONVERT(DATE, m.[Date])
    ),
    CombinedPunches AS (
        SELECT
            COALESCE(rm.EmployeeId, rman.EmployeeId) AS EmployeeId,
            COALESCE(rm.PunchDate, rman.PunchDate)   AS PunchDate,
            CASE
                WHEN rm.FirstPunch IS NULL THEN rman.FirstPunch
                WHEN rman.FirstPunch IS NULL THEN rm.FirstPunch
                WHEN rm.FirstPunch <= rman.FirstPunch THEN rm.FirstPunch
                ELSE rman.FirstPunch
            END AS FirstPunch,
            CASE
                WHEN rm.LastPunch IS NULL THEN rman.LastPunch
                WHEN rman.LastPunch IS NULL THEN rm.LastPunch
                WHEN rm.LastPunch >= rman.LastPunch THEN rm.LastPunch
                ELSE rman.LastPunch
            END AS LastPunch,
            rman.ManualRemarks
        FROM RawMachine rm
        FULL OUTER JOIN RawManual rman 
            ON rm.EmployeeId = rman.EmployeeId AND rm.PunchDate = rman.PunchDate
    )
    INSERT INTO #Punches (EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks)
    SELECT EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks
    FROM CombinedPunches;

    -- Leaves (filtered to target date range)
    CREATE TABLE #EmpLeaves (
        EmployeeId NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        LeaveDate  DATE,
        LeaveCode  NVARCHAR(20)  COLLATE DATABASE_DEFAULT,
        Reason     NVARCHAR(500) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, LeaveDate)
    );

    ;WITH ApprovedLeaves AS (
        SELECT 
            la.EmployeeId,
            d.[Date] AS LeaveDate,
            ISNULL(NULLIF(lt.ShortName, ''), 'L') AS LeaveCode,
            ISNULL(NULLIF(LTRIM(RTRIM(la.Reason)), ''), '') AS Reason
        FROM HRM_LeaveApplicationEntry la WITH (NOLOCK)
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = la.EmployeeId
        INNER JOIN #Dates d ON d.[Date] >= CONVERT(DATE, la.StartDate) AND d.[Date] <= CONVERT(DATE, la.EndDate)
        LEFT  JOIN HRM_ATD_LeaveType lt WITH (NOLOCK) ON lt.LeaveTypeCode = la.LeaveTypeId
        WHERE la.StartDate < @NextToDateTime AND la.EndDate >= @FromDateTime
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    INSERT INTO #EmpLeaves (EmployeeId, LeaveDate, LeaveCode, Reason)
    SELECT EmployeeId, LeaveDate, MAX(LeaveCode), MAX(Reason)
    FROM ApprovedLeaves
    GROUP BY EmployeeId, LeaveDate;

    -- Holidays (only within target date range)
    CREATE TABLE #Holidays (
        HolidayDate DATE PRIMARY KEY,
        HolidayName NVARCHAR(200) COLLATE DATABASE_DEFAULT
    );

    INSERT INTO #Holidays (HolidayDate, HolidayName)
    SELECT v.HolidayDate, v.HolidayName
    FROM (VALUES 
        (CAST('2026-01-01' AS DATE), N'New Year''s Day'),
        (CAST('2026-02-21' AS DATE), N'International Mother Language Day'),
        (CAST('2026-03-19' AS DATE), N'Holiday'),
        (CAST('2026-03-20' AS DATE), N'Holiday'),
        (CAST('2026-03-26' AS DATE), N'Independence Day'),
        (CAST('2026-04-14' AS DATE), N'Bengali New Year'),
        (CAST('2026-05-01' AS DATE), N'May Day'),
        (CAST('2026-12-16' AS DATE), N'Victory Day'),
        (CAST('2026-12-25' AS DATE), N'Christmas Day')
    ) v(HolidayDate, HolidayName)
    WHERE v.HolidayDate >= @FromDate AND v.HolidayDate <= @ToDate;

    INSERT INTO #Holidays (HolidayDate, HolidayName)
    SELECT d.[Date], MAX(ISNULL(NULLIF(h.HolidayName, ''), 'Holiday'))
    FROM #Dates d
    INNER JOIN HRM_ATD_Holiday h WITH (NOLOCK) ON d.[Date] >= CONVERT(DATE, h.FromDate) AND d.[Date] <= CONVERT(DATE, h.ToDate)
    WHERE h.FromDate < @NextToDateTime AND h.ToDate >= @FromDateTime
      AND NOT EXISTS (SELECT 1 FROM #Holidays WHERE HolidayDate = d.[Date])
    GROUP BY d.[Date];

    -- Weekends (filtered to target date range)
    CREATE TABLE #EmpWeekends (
        EmployeeId  NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        WeekendDate DATE,
        PRIMARY KEY (EmployeeId, WeekendDate)
    );

    INSERT INTO #EmpWeekends (EmployeeId, WeekendDate)
    SELECT DISTINCT ewd.EmployeeId, CONVERT(DATE, ewd.[Date])
    FROM HRM_EmployeeWeekendDeclaration ewd WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = ewd.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, ewd.[Date])
    WHERE ewd.[Date] >= @FromDateTime AND ewd.[Date] < @NextToDateTime;

    -- Roster Overrides (filtered to target date range)
    CREATE TABLE #EmpRosters (
        EmployeeId   NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        RosterDate   DATE,
        ShiftDisplay NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftStart   TIME(0),
        ShiftEnd     TIME(0),
        LateCutoff   TIME(0),
        IsWeekend    BIT,
        PRIMARY KEY (EmployeeId, RosterDate)
    );

    INSERT INTO #EmpRosters (EmployeeId, RosterDate, ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff, IsWeekend)
    SELECT 
        rse.EmployeeId,
        CONVERT(DATE, rse.[Date]),
        ISNULL(s.ShiftDisplay, 'Day(09:00-18:00)'),
        ISNULL(s.ShiftStart, CAST('09:00:00' AS TIME(0))),
        ISNULL(s.ShiftEnd, CAST('18:00:00' AS TIME(0))),
        ISNULL(s.LateCutoff, CAST('09:15:00' AS TIME(0))),
        CASE WHEN rse.Weekend = 'Y' OR rse.Weekend = '1' THEN 1 ELSE 0 END
    FROM HRM_RosterScheduleEntry rse WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = rse.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, rse.[Date])
    LEFT JOIN #ShiftLookup s ON s.ShiftCode = rse.ShiftCode
    WHERE rse.[Date] >= @FromDateTime AND rse.[Date] < @NextToDateTime;

    -- Final calculation CTE
    ;WITH BaseRows AS (
        SELECT
            ae.DepartmentCode,
            ae.DepartmentName,
            ae.DesignationCode,
            ae.Designation,
            ae.EmployeeId,
            ae.EmployeeName,
            ae.CompanyName,
            d.[Date],
            DATENAME(WEEKDAY, d.[Date]) AS [Day],
            COALESCE(ros.ShiftDisplay, ae.ShiftDisplay) AS ShiftDisplay,
            COALESCE(ros.ShiftStart, ae.ShiftStart) AS ShiftStart,
            COALESCE(ros.ShiftEnd, ae.ShiftEnd) AS ShiftEnd,
            COALESCE(ros.LateCutoff, ae.LateCutoff) AS LateCutoff,
            p.FirstPunch,
            p.LastPunch,
            p.ManualRemarks,
            lv.LeaveCode,
            lv.Reason AS LeaveReason,
            h.HolidayName,
            CASE
                WHEN ros.IsWeekend = 1 THEN 1
                WHEN ew.WeekendDate IS NOT NULL THEN 1
                WHEN DATENAME(WEEKDAY, d.[Date]) IN ('Friday', 'Saturday') THEN 1
                ELSE 0
            END AS IsWeekend
        FROM #ActiveEmployees ae
        CROSS JOIN #Dates d
        LEFT JOIN #Punches p ON p.EmployeeId = ae.EmployeeId AND p.PunchDate = d.[Date]
        LEFT JOIN #EmpLeaves lv ON lv.EmployeeId = ae.EmployeeId AND lv.LeaveDate = d.[Date]
        LEFT JOIN #Holidays h ON h.HolidayDate = d.[Date]
        LEFT JOIN #EmpWeekends ew ON ew.EmployeeId = ae.EmployeeId AND ew.WeekendDate = d.[Date]
        LEFT JOIN #EmpRosters ros ON ros.EmployeeId = ae.EmployeeId AND ros.RosterDate = d.[Date]
    ),
    SecCalculated AS (
        SELECT
            DepartmentCode,
            DepartmentName,
            DesignationCode,
            Designation,
            EmployeeId,
            EmployeeName,
            CompanyName,
            [Date],
            CONVERT(VARCHAR(10), [Date], 103) AS DateDisplay, -- dd/MM/yyyy
            [Day],
            ShiftDisplay,
            ShiftStart,
            ShiftEnd,
            LateCutoff,
            FirstPunch,
            LastPunch,
            ManualRemarks,
            LeaveCode,
            LeaveReason,
            HolidayName,
            IsWeekend,
            CAST(FirstPunch AS TIME(0)) AS FirstPunchTime,
            CAST(LastPunch AS TIME(0))  AS LastPunchTime,
            CASE
                WHEN IsWeekend = 1 OR HolidayName IS NOT NULL OR LeaveCode IS NOT NULL THEN 0
                WHEN FirstPunch IS NOT NULL AND CAST(FirstPunch AS TIME(0)) > LateCutoff
                THEN DATEDIFF(SECOND, LateCutoff, CAST(FirstPunch AS TIME(0)))
                ELSE 0
            END AS LateSec,
            CASE
                WHEN IsWeekend = 1 OR HolidayName IS NOT NULL OR LeaveCode IS NOT NULL THEN 0
                WHEN LastPunch IS NOT NULL AND CAST(LastPunch AS TIME(0)) <> CAST(FirstPunch AS TIME(0))
                 AND CAST(LastPunch AS TIME(0)) < ShiftEnd
                THEN DATEDIFF(SECOND, CAST(LastPunch AS TIME(0)), ShiftEnd)
                ELSE 0
            END AS EarlyOutSec,
            CASE
                WHEN FirstPunch IS NOT NULL AND LastPunch IS NOT NULL AND CAST(LastPunch AS TIME(0)) <> CAST(FirstPunch AS TIME(0))
                THEN DATEDIFF(SECOND, FirstPunch, LastPunch)
                ELSE 0
            END AS WorkSec
        FROM BaseRows
    )
    SELECT
        DepartmentCode,
        DepartmentName,
        DesignationCode,
        Designation,
        EmployeeId,
        EmployeeName,
        CompanyName,
        [Date],
        DateDisplay,
        [Day],
        CASE WHEN FirstPunch IS NOT NULL THEN ShiftDisplay ELSE '' END AS Shift,
        CASE 
            WHEN FirstPunch IS NOT NULL THEN 
                LTRIM(RIGHT(CONVERT(VARCHAR(30), FirstPunch, 100), 7))
            ELSE ''
        END AS InTime,
        CASE
            WHEN LateSec > 0 THEN
                RIGHT('00' + CAST(LateSec / 3600 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST((LateSec % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST(LateSec % 60 AS VARCHAR(2)), 2)
            ELSE ''
        END AS Late,
        CASE
            WHEN LastPunch IS NOT NULL AND LastPunchTime <> FirstPunchTime THEN 
                LTRIM(RIGHT(CONVERT(VARCHAR(30), LastPunch, 100), 7))
            ELSE ''
        END AS OutTime,
        CASE
            WHEN EarlyOutSec > 0 THEN
                RIGHT('00' + CAST(EarlyOutSec / 3600 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST((EarlyOutSec % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                RIGHT('00' + CAST(EarlyOutSec % 60 AS VARCHAR(2)), 2)
            ELSE ''
        END AS EarlyOut,
        CASE
            WHEN WorkSec > 0 THEN
                CAST(ROUND(CAST(WorkSec AS FLOAT) / 3600.0, 2) AS VARCHAR(10))
            ELSE ''
        END AS WorkHours,
        CASE
            WHEN LeaveCode IS NOT NULL THEN LeaveCode
            WHEN HolidayName IS NOT NULL THEN 'H'
            WHEN IsWeekend = 1 THEN 'W'
            WHEN FirstPunch IS NOT NULL THEN
                CASE WHEN FirstPunchTime > LateCutoff THEN 'L' ELSE 'P' END
            ELSE 'A'
        END AS [Status],
        COALESCE(NULLIF(LeaveReason, ''), NULLIF(ManualRemarks, ''), '') AS Remarks
    FROM SecCalculated
    ORDER BY DepartmentName, EmployeeId, [Date]
    OPTION (RECOMPILE);

    -- Header Result Set (2nd result set)
    SELECT
        ISNULL((SELECT TOP 1 CompanyName FROM #ActiveEmployees), 'DataPath Ltd.') AS CompanyName,
        CONVERT(VARCHAR(10), @FromDate, 103) AS FromDate,
        CONVERT(VARCHAR(10), @ToDate, 103)   AS ToDate,
        'Date: ' + CONVERT(VARCHAR(10), @FromDate, 103) + ' - ' + CONVERT(VARCHAR(10), @ToDate, 103) AS DateRangeDisplay;

    -- Cleanup
    DROP TABLE #CompFilter;
    DROP TABLE #BranchFilter;
    DROP TABLE #DeptFilter;
    DROP TABLE #DesigFilter;
    DROP TABLE #EmpFilter;
    DROP TABLE #ShiftLookup;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #EmpFingerprints;
    DROP TABLE #Dates;
    DROP TABLE #Punches;
    DROP TABLE #EmpLeaves;
    DROP TABLE #Holidays;
    DROP TABLE #EmpWeekends;
    DROP TABLE #EmpRosters;
END;
GO

GO

-- =========================================================================================
-- 9. PROCEDURE: [dbo].[usp_MonthlyAttendanceReport]
-- =========================================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_MonthlyAttendanceReport]
    @CompanyCode           VARCHAR(50)   = '',
    @BranchCodes           NVARCHAR(MAX) = '',
    @DepartmentCodes       NVARCHAR(MAX) = '',
    @DesignationCodes      NVARCHAR(MAX) = '',
    @EmploymentNatureCodes NVARCHAR(MAX) = '',
    @EmployeeIds           NVARCHAR(MAX) = '',
    @FromDate              VARCHAR(50)   = '',
    @ToDate                VARCHAR(50)   = '',
    @Month                 INT           = NULL,
    @Year                  INT           = NULL,
    @LoginEmployeeId       NVARCHAR(50)  = '',
    @AccessCodeId          NVARCHAR(50)  = ''
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
    SET DATEFORMAT dmy;

    -- Clean parameters
    SET @CompanyCode           = LTRIM(RTRIM(ISNULL(@CompanyCode, '')));
    SET @BranchCodes           = LTRIM(RTRIM(ISNULL(@BranchCodes, '')));
    SET @DepartmentCodes       = LTRIM(RTRIM(ISNULL(@DepartmentCodes, '')));
    SET @DesignationCodes      = LTRIM(RTRIM(ISNULL(@DesignationCodes, '')));
    SET @EmploymentNatureCodes = LTRIM(RTRIM(ISNULL(@EmploymentNatureCodes, '')));
    SET @EmployeeIds           = LTRIM(RTRIM(ISNULL(@EmployeeIds, '')));
    SET @FromDate              = LTRIM(RTRIM(ISNULL(@FromDate, '')));
    SET @ToDate                = LTRIM(RTRIM(ISNULL(@ToDate, '')));
    SET @LoginEmployeeId       = LTRIM(RTRIM(ISNULL(@LoginEmployeeId, '')));
    SET @AccessCodeId          = LTRIM(RTRIM(ISNULL(@AccessCodeId, '')));

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ SAFE DATE PARSING (Supports dd/MM/yyyy, yyyy-MM-dd, etc.) â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    DECLARE @RealFromDate DATE = NULL;
    DECLARE @RealToDate   DATE = NULL;

    IF @FromDate <> '' AND @FromDate <> '1900-01-01'
    BEGIN
        IF @FromDate LIKE '[0-9][0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9]'
            OR @FromDate LIKE '[0-9]/[0-9]/[0-9][0-9][0-9][0-9]'
            OR @FromDate LIKE '[0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9]'
            OR @FromDate LIKE '[0-9][0-9]/[0-9]/[0-9][0-9][0-9][0-9]'
        BEGIN
            SET @RealFromDate = CONVERT(DATE, @FromDate, 103);
        END
        ELSE IF @FromDate LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]%'
        BEGIN
            SET @RealFromDate = CONVERT(DATE, SUBSTRING(@FromDate, 1, 10), 120);
        END
        ELSE IF ISDATE(@FromDate) = 1
        BEGIN
            SET @RealFromDate = CONVERT(DATE, @FromDate);
        END
    END;

    IF @ToDate <> '' AND @ToDate <> '1900-01-01'
    BEGIN
        IF @ToDate LIKE '[0-9][0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9]'
            OR @ToDate LIKE '[0-9]/[0-9]/[0-9][0-9][0-9][0-9]'
            OR @ToDate LIKE '[0-9]/[0-9][0-9]/[0-9][0-9][0-9][0-9]'
            OR @ToDate LIKE '[0-9][0-9]/[0-9]/[0-9][0-9][0-9][0-9]'
        BEGIN
            SET @RealToDate = CONVERT(DATE, @ToDate, 103);
        END
        ELSE IF @ToDate LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]%'
        BEGIN
            SET @RealToDate = CONVERT(DATE, SUBSTRING(@ToDate, 1, 10), 120);
        END
        ELSE IF ISDATE(@ToDate) = 1
        BEGIN
            SET @RealToDate = CONVERT(DATE, @ToDate);
        END
    END;

    -- Date handling: By Month or By Date
    IF (@RealFromDate IS NULL) AND (@Month IS NOT NULL AND @Year IS NOT NULL AND @Month BETWEEN 1 AND 12 AND @Year > 1900)
    BEGIN
        SET @RealFromDate = DATEFROMPARTS(@Year, @Month, 1);
        SET @RealToDate   = EOMONTH(@RealFromDate);
    END
    ELSE
    BEGIN
        IF @RealFromDate IS NULL
            SET @RealFromDate = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

        IF @RealToDate IS NULL
            SET @RealToDate = EOMONTH(@RealFromDate);
    END

    -- Always ensure full month coverage for monthly attendance report (1st of month to EOMONTH)
    IF @RealFromDate IS NOT NULL
    BEGIN
        SET @RealFromDate = DATEFROMPARTS(YEAR(@RealFromDate), MONTH(@RealFromDate), 1);
        SET @RealToDate   = EOMONTH(@RealFromDate);
    END;

    DECLARE @NextToDate DATE = DATEADD(DAY, 1, @RealToDate);
    DECLARE @FromDateTime DATETIME = CAST(@RealFromDate AS DATETIME);
    DECLARE @NextToDateTime DATETIME = CAST(@NextToDate AS DATETIME);
    DECLARE @TodayDate DATE = CAST(GETDATE() AS DATE);

    -- Role-based access control
    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId NOT IN ('0002','0003','0005') THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003')           THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'                     THEN 1 ELSE 0 END;

    IF @AccessCodeId = '' OR @LoginEmployeeId = ''
        SET @IsAdmin = 1;

    -- Fast indexed filter tables matching DB Collation
    CREATE TABLE #CompFilter      (Code NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY);
    CREATE TABLE #BranchFilter    (Code NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY);
    CREATE TABLE #DeptFilter      (Code NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY);
    CREATE TABLE #DesigFilter     (Code NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY);
    CREATE TABLE #EmpNatureFilter (Code NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY);
    CREATE TABLE #EmpFilter       (Code NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY);

    IF @CompanyCode <> ''
    BEGIN
        INSERT INTO #CompFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @CompanyCode)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @BranchCodes <> ''
    BEGIN
        INSERT INTO #BranchFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @BranchCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @DepartmentCodes <> ''
    BEGIN
        INSERT INTO #DeptFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @DepartmentCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @DesignationCodes <> ''
    BEGIN
        INSERT INTO #DesigFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @DesignationCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @EmploymentNatureCodes <> ''
    BEGIN
        INSERT INTO #EmpNatureFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @EmploymentNatureCodes)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    IF @EmployeeIds <> ''
    BEGIN
        INSERT INTO #EmpFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @EmployeeIds)
        WHERE LTRIM(RTRIM([Value])) <> '';
    END

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ PRE-COMPUTED SHIFT LOOKUP (Master HRM_ATD_Shift + Details) â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #ShiftLookup (
        ShiftCode    NVARCHAR(50)  COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY,
        ShiftDisplay NVARCHAR(200) COLLATE SQL_Latin1_General_CP1_CI_AS,
        ShiftStart   TIME(0),
        ShiftEnd     TIME(0),
        LateCutoff   TIME(0)
    );

    ;WITH RankedShifts AS (
        SELECT
            s.ShiftCode,
            s.ShiftName,
            sd.ShiftStartTime,
            sd.ShiftEndTime,
            sd.LateTime,
            ROW_NUMBER() OVER (
                PARTITION BY s.ShiftCode
                ORDER BY ISNULL(sd.IsActive, 1) DESC, sd.AutoId ASC
            ) AS rn
        FROM HRM_ATD_Shift s WITH (NOLOCK)
        LEFT JOIN HRM_ATD_ShiftDetails sd WITH (NOLOCK) ON sd.ShiftCode = s.ShiftCode
        WHERE s.ShiftCode IS NOT NULL AND s.ShiftCode <> ''
    )
    INSERT INTO #ShiftLookup (ShiftCode, ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff)
    SELECT
        ShiftCode,
        CASE
            WHEN ShiftStartTime IS NOT NULL AND ShiftEndTime IS NOT NULL THEN
                ISNULL(NULLIF(ShiftName, ''), 'G') + '(' +
                SUBSTRING(CONVERT(VARCHAR(8), ShiftStartTime, 108), 1, 5) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), ShiftEndTime, 108), 1, 5) + ')'
            ELSE 'Day(09:00-18:00)'
        END,
        ISNULL(CAST(ShiftStartTime AS TIME(0)), CAST('09:00:00' AS TIME(0))),
        ISNULL(CAST(ShiftEndTime AS TIME(0)), CAST('18:00:00' AS TIME(0))),
        ISNULL(CAST(LateTime AS TIME(0)), CAST('09:15:00' AS TIME(0)))
    FROM RankedShifts
    WHERE rn = 1;

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ ACTIVE EMPLOYEES (Covering Index Seek) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #ActiveEmployees (
        EmployeeId         NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY,
        AttendenceId       NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        EmployeeName       NVARCHAR(200)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        DesignationCode    NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        Designation        NVARCHAR(200)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        DepartmentCode     NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        DepartmentName     NVARCHAR(200)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        CompanyCode        NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        CompanyName        NVARCHAR(300)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        EmploymentNatureId NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        ShiftDisplay       NVARCHAR(200)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        ShiftStart         TIME(0),
        ShiftEnd           TIME(0),
        LateCutoff         TIME(0)
    );

    INSERT INTO #ActiveEmployees (
        EmployeeId, AttendenceId, EmployeeName,
        DesignationCode, Designation,
        DepartmentCode, DepartmentName,
        CompanyCode, CompanyName, EmploymentNatureId,
        ShiftDisplay, ShiftStart, ShiftEnd, LateCutoff
    )
    SELECT
        e.EmployeeId,
        ISNULL(LTRIM(RTRIM(oi.AttendenceId)), ''),
        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))), ''), ISNULL(e.EmployeeId, '')),
        ISNULL(oi.DesignationCode, ''),
        ISNULL(NULLIF(desig.DesignationName, ''), ISNULL(NULLIF(oi.DesignationCode, ''), 'â€”')),
        ISNULL(oi.DepartmentCode, ''),
        ISNULL(NULLIF(dept.DepartmentName, ''), ISNULL(NULLIF(oi.DepartmentCode, ''), 'General Department')),
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'DataPath Ltd.'),
        ISNULL(oi.EmploymentNatureId, ''),
        ISNULL(sl.ShiftDisplay, 'Day(09:00-18:00)'),
        ISNULL(sl.ShiftStart, CAST('09:00:00' AS TIME(0))),
        ISNULL(sl.ShiftEnd, CAST('18:00:00' AS TIME(0))),
        ISNULL(sl.LateCutoff, CAST('09:15:00' AS TIME(0)))
    FROM HRM_EmployeeOfficialInfo oi WITH (NOLOCK)
    INNER JOIN HRM_Employee e WITH (NOLOCK) ON e.EmployeeId = oi.EmployeeId
    LEFT  JOIN Core_Company comp WITH (NOLOCK) ON comp.CompanyCode = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT  JOIN HRM_Def_Department dept WITH (NOLOCK) ON dept.DepartmentCode = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation desig WITH (NOLOCK) ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT  JOIN #ShiftLookup sl ON sl.ShiftCode = oi.ShiftCode AND oi.ShiftCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (NOT EXISTS (SELECT 1 FROM #CompFilter)      OR oi.CompanyCode IN (SELECT Code FROM #CompFilter))
      AND (NOT EXISTS (SELECT 1 FROM #BranchFilter)    OR oi.BranchCode IN (SELECT Code FROM #BranchFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DeptFilter)      OR oi.DepartmentCode IN (SELECT Code FROM #DeptFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DesigFilter)     OR oi.DesignationCode IN (SELECT Code FROM #DesigFilter))
      AND (NOT EXISTS (SELECT 1 FROM #EmpNatureFilter) OR oi.EmploymentNatureId IN (SELECT Code FROM #EmpNatureFilter))
      AND (NOT EXISTS (SELECT 1 FROM #EmpFilter)       OR e.EmployeeId IN (SELECT Code FROM #EmpFilter))
      AND (
            @IsAdmin = 1
            OR (
                 @IsTeamLeader = 1
                 AND (
                       e.EmployeeId   = @LoginEmployeeId
                    OR (oi.ReportingTo <> '' AND oi.ReportingTo = @LoginEmployeeId)
                    OR (oi.HOD <> '' AND oi.HOD = @LoginEmployeeId)
                 )
               )
            OR (@IsUser = 1 AND e.EmployeeId = @LoginEmployeeId)
          )
      AND (oi.JoiningDate IS NULL OR oi.JoiningDate = '1900-01-01' OR CONVERT(DATE, oi.JoiningDate) <= @RealToDate);

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ FINGERPRINTS MAPPING (Exact Match to MachineData Collation) â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #EmpFingerprints (
        FingerPrintId VARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY,
        EmployeeId    NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS
    );

    INSERT INTO #EmpFingerprints (FingerPrintId, EmployeeId)
    SELECT CAST(EmployeeId AS VARCHAR(100)), EmployeeId FROM #ActiveEmployees WHERE EmployeeId <> ''
    UNION
    SELECT CAST(AttendenceId AS VARCHAR(100)), EmployeeId FROM #ActiveEmployees WHERE AttendenceId <> '';

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ FAST DATE SERIES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #Dates (
        [Date]    DATE PRIMARY KEY,
        DayNum    INT,
        IsFriSat  BIT
    );

    ;WITH DateSeries AS (
        SELECT @RealFromDate AS [Date]
        UNION ALL
        SELECT DATEADD(DAY, 1, [Date])
        FROM DateSeries
        WHERE [Date] < @RealToDate
    )
    INSERT INTO #Dates ([Date], DayNum, IsFriSat)
    SELECT
        [Date],
        DAY([Date]),
        CASE WHEN DATENAME(WEEKDAY, [Date]) IN ('Friday', 'Saturday') THEN 1 ELSE 0 END
    FROM DateSeries
    OPTION (MAXRECURSION 0);

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ PUNCHES (Machine Data & Manual Data Aggregated in Milliseconds) 
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #Punches (
        EmployeeId NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS,
        PunchDate  DATE,
        FirstPunch DATETIME,
        PRIMARY KEY (EmployeeId, PunchDate)
    );

    INSERT INTO #Punches (EmployeeId, PunchDate, FirstPunch)
    SELECT
        fp.EmployeeId,
        CAST(md.[Date] AS DATE) AS PunchDate,
        MIN(md.[Time]) AS FirstPunch
    FROM HRM_ATD_MachineData md WITH (NOLOCK)
    INNER JOIN #EmpFingerprints fp ON fp.FingerPrintId = md.FingerPrintID
    WHERE md.[Date] >= @FromDateTime AND md.[Date] < @NextToDateTime
      AND md.[Time] IS NOT NULL AND md.[Time] <> '1900-01-01'
    GROUP BY fp.EmployeeId, CAST(md.[Date] AS DATE);

    ;WITH ManualAgg AS (
        SELECT 
            m.EmployeeId,
            CAST(m.[Date] AS DATE) AS PunchDate,
            MIN(m.[Time]) AS FirstPunch
        FROM HRM_ATD_Manual m WITH (NOLOCK)
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = m.EmployeeId
        WHERE m.[Date] >= @FromDateTime AND m.[Date] < @NextToDateTime
          AND m.[Time] IS NOT NULL AND m.[Time] <> '1900-01-01'
        GROUP BY m.EmployeeId, CAST(m.[Date] AS DATE)
    )
    MERGE #Punches AS tgt
    USING ManualAgg AS src
       ON (tgt.EmployeeId = src.EmployeeId AND tgt.PunchDate = src.PunchDate)
    WHEN MATCHED THEN
        UPDATE SET 
            tgt.FirstPunch = CASE WHEN src.FirstPunch < tgt.FirstPunch THEN src.FirstPunch ELSE tgt.FirstPunch END
    WHEN NOT MATCHED THEN
        INSERT (EmployeeId, PunchDate, FirstPunch)
        VALUES (src.EmployeeId, src.PunchDate, src.FirstPunch);

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ LEAVES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #EmpLeaves (
        EmployeeId NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        LeaveDate  DATE,
        LeaveCode  NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        PRIMARY KEY (EmployeeId, LeaveDate)
    );

    ;WITH ApprovedLeaves AS (
        SELECT 
            la.EmployeeId,
            d.[Date] AS LeaveDate,
            ISNULL(NULLIF(lt.ShortName, ''), 'L') AS LeaveCode
        FROM HRM_LeaveApplicationEntry la WITH (NOLOCK)
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = la.EmployeeId
        INNER JOIN #Dates d ON d.[Date] >= CONVERT(DATE, la.StartDate) AND d.[Date] <= CONVERT(DATE, la.EndDate)
        LEFT  JOIN HRM_ATD_LeaveType lt WITH (NOLOCK) ON lt.LeaveTypeCode = la.LeaveTypeId
        WHERE la.StartDate < @NextToDateTime AND la.EndDate >= @FromDateTime
          AND (la.IsApproved NOT IN ('Canceled', 'Rejected'))
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    INSERT INTO #EmpLeaves (EmployeeId, LeaveDate, LeaveCode)
    SELECT EmployeeId, LeaveDate, MAX(LeaveCode)
    FROM ApprovedLeaves
    GROUP BY EmployeeId, LeaveDate;

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ HOLIDAYS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #Holidays (
        HolidayDate DATE PRIMARY KEY
    );

    INSERT INTO #Holidays (HolidayDate)
    VALUES 
        ('2026-01-01'), ('2026-02-21'), ('2026-03-19'), ('2026-03-20'),
        ('2026-03-26'), ('2026-04-14'), ('2026-05-01'), ('2026-12-16'), ('2026-12-25');

    INSERT INTO #Holidays (HolidayDate)
    SELECT DISTINCT d.[Date]
    FROM #Dates d
    INNER JOIN HRM_ATD_Holiday h WITH (NOLOCK) ON d.[Date] >= CONVERT(DATE, h.FromDate) AND d.[Date] <= CONVERT(DATE, h.ToDate)
    WHERE h.FromDate < @NextToDate AND h.ToDate >= @RealFromDate
      AND NOT EXISTS (SELECT 1 FROM #Holidays WHERE HolidayDate = d.[Date]);

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ WEEKENDS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #EmpWeekends (
        EmployeeId  NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS,
        WeekendDate DATE,
        PRIMARY KEY (EmployeeId, WeekendDate)
    );

    INSERT INTO #EmpWeekends (EmployeeId, WeekendDate)
    SELECT DISTINCT ewd.EmployeeId, CONVERT(DATE, ewd.[Date])
    FROM HRM_EmployeeWeekendDeclaration ewd WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = ewd.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, ewd.[Date])
    WHERE ewd.[Date] >= @FromDateTime AND ewd.[Date] < @NextToDateTime;

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ ROSTER OVERRIDES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    CREATE TABLE #EmpRosters (
        EmployeeId   NVARCHAR(50)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        RosterDate   DATE,
        LateCutoff   TIME(0),
        IsWeekend    BIT,
        PRIMARY KEY (EmployeeId, RosterDate)
    );

    INSERT INTO #EmpRosters (EmployeeId, RosterDate, LateCutoff, IsWeekend)
    SELECT 
        rse.EmployeeId,
        CONVERT(DATE, rse.[Date]),
        sl.LateCutoff,
        CASE WHEN rse.Weekend = 'Y' OR rse.Weekend = '1' THEN 1 ELSE 0 END
    FROM HRM_RosterScheduleEntry rse WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = rse.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, rse.[Date])
    LEFT JOIN #ShiftLookup sl ON sl.ShiftCode = rse.ShiftCode
    WHERE rse.[Date] >= @FromDateTime AND rse.[Date] < @NextToDateTime;

    CREATE NONCLUSTERED INDEX IX_Dept_Emp ON #ActiveEmployees (DepartmentName, EmployeeId);

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ FAST ATTENDANCE MATRIX (Direct Instant Output) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    SELECT
        ae.DepartmentCode,
        ae.DepartmentName,
        ae.DesignationCode,
        ae.Designation,
        ae.EmployeeId,
        ae.EmployeeName,
        ae.CompanyName,
        d.[Date],
        CONVERT(VARCHAR(10), d.[Date], 103) AS DateDisplay,
        CASE DATENAME(WEEKDAY, d.[Date])
            WHEN 'Monday'    THEN 'Mo'
            WHEN 'Tuesday'   THEN 'Tu'
            WHEN 'Wednesday' THEN 'We'
            WHEN 'Thursday'  THEN 'Th'
            WHEN 'Friday'    THEN 'Fr'
            WHEN 'Saturday'  THEN 'Sa'
            WHEN 'Sunday'    THEN 'Su'
            ELSE SUBSTRING(DATENAME(WEEKDAY, d.[Date]), 1, 2)
        END AS [Day],
        DAY(d.[Date]) AS DayNumber,
        '' AS Shift,
        '' AS InTime,
        '' AS Late,
        '' AS OutTime,
        '' AS WorkHours,
        CASE
            WHEN d.[Date] > @TodayDate THEN ''
            WHEN lv.LeaveCode IS NOT NULL THEN lv.LeaveCode
            WHEN h.HolidayDate IS NOT NULL THEN 'H'
            WHEN ros.IsWeekend = 1 OR ew.WeekendDate IS NOT NULL OR d.IsFriSat = 1 THEN 'W'
            WHEN p.FirstPunch IS NOT NULL THEN
                CASE WHEN CAST(p.FirstPunch AS TIME(0)) > COALESCE(ros.LateCutoff, ae.LateCutoff) THEN 'L' ELSE 'P' END
            ELSE 'A'
        END AS [Status],
        '' AS Remarks
    FROM #ActiveEmployees ae
    CROSS JOIN #Dates d
    LEFT JOIN #Punches p ON p.EmployeeId = ae.EmployeeId AND p.PunchDate = d.[Date]
    LEFT JOIN #EmpLeaves lv ON lv.EmployeeId = ae.EmployeeId AND lv.LeaveDate = d.[Date]
    LEFT JOIN #Holidays h ON h.HolidayDate = d.[Date]
    LEFT JOIN #EmpWeekends ew ON ew.EmployeeId = ae.EmployeeId AND ew.WeekendDate = d.[Date]
    LEFT JOIN #EmpRosters ros ON ros.EmployeeId = ae.EmployeeId AND ros.RosterDate = d.[Date]
    ORDER BY ae.DepartmentName, ae.EmployeeId, d.[Date]
    OPTION (RECOMPILE);

    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    -- â”€â”€ HEADER RESULT SET (2nd Result Set) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    -- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    DECLARE @SubTitleText NVARCHAR(200);
    IF @Month IS NOT NULL AND @Year IS NOT NULL AND @Month BETWEEN 1 AND 12
    BEGIN
        SET @SubTitleText = DATENAME(MONTH, DATEFROMPARTS(@Year, @Month, 1)) + ',' + CAST(@Year AS VARCHAR(4));
    END
    ELSE
    BEGIN
        SET @SubTitleText = CONVERT(VARCHAR(10), @RealFromDate, 103) + ' - ' + CONVERT(VARCHAR(10), @RealToDate, 103);
    END

    SELECT
        ISNULL((SELECT TOP 1 CompanyName FROM #ActiveEmployees), 'DataPath Ltd.') AS CompanyName,
        'Monthly Attendance Details Report' AS ReportTitle,
        @SubTitleText AS Subtitle,
        CONVERT(VARCHAR(10), @RealFromDate, 103) AS FromDate,
        CONVERT(VARCHAR(10), @RealToDate, 103)   AS ToDate,
        'Date: ' + CONVERT(VARCHAR(10), @RealFromDate, 103) + ' - ' + CONVERT(VARCHAR(10), @RealToDate, 103) AS DateRangeDisplay;

    -- Cleanup
    DROP TABLE #CompFilter;
    DROP TABLE #BranchFilter;
    DROP TABLE #DeptFilter;
    DROP TABLE #DesigFilter;
    DROP TABLE #EmpNatureFilter;
    DROP TABLE #EmpFilter;
    DROP TABLE #ShiftLookup;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #EmpFingerprints;
    DROP TABLE #Dates;
    DROP TABLE #Punches;
    DROP TABLE #EmpLeaves;
    DROP TABLE #Holidays;
    DROP TABLE #EmpWeekends;
    DROP TABLE #EmpRosters;
END;
GO

GO

