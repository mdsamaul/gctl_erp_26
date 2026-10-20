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

    -- ═══════════════════════════════════════════════════════════════════
    -- ── SAFE DATE PARSING (Supports dd/MM/yyyy, yyyy-MM-dd, etc.) ─────
    -- ═══════════════════════════════════════════════════════════════════
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
        IF @RealToDate IS NULL OR @RealToDate < EOMONTH(@RealFromDate)
            SET @RealToDate = EOMONTH(ISNULL(@RealToDate, @RealFromDate));
        ELSE
            SET @RealToDate = EOMONTH(@RealToDate);
    END;

    IF @RealToDate < @RealFromDate
    BEGIN
        DECLARE @TempDate DATE = @RealFromDate;
        SET @RealFromDate = @RealToDate;
        SET @RealToDate   = @TempDate;
    END

    DECLARE @NextToDate DATE = DATEADD(DAY, 1, @RealToDate);
    DECLARE @FromDateTime DATETIME = CAST(@RealFromDate AS DATETIME);
    DECLARE @NextToDateTime DATETIME = CAST(@NextToDate AS DATETIME);

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

    -- ═══════════════════════════════════════════════════════════════════
    -- ── ACTIVE EMPLOYEES (Covering Index Seek) ─────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
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
        ISNULL(NULLIF(desig.DesignationName, ''), ISNULL(NULLIF(oi.DesignationCode, ''), '—')),
        ISNULL(oi.DepartmentCode, ''),
        ISNULL(NULLIF(dept.DepartmentName, ''), ISNULL(NULLIF(oi.DepartmentCode, ''), 'General Department')),
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'DataPath Ltd.'),
        ISNULL(oi.EmploymentNatureId, ''),
        CASE
            WHEN s.ShiftCode IS NOT NULL AND s.ShiftStartTime IS NOT NULL AND s.ShiftEndTime IS NOT NULL THEN
                ISNULL(NULLIF(s.ShiftName,''), 'G') + '(' + 
                SUBSTRING(CONVERT(VARCHAR(8), s.ShiftStartTime, 108), 1, 5) + '-' +
                SUBSTRING(CONVERT(VARCHAR(8), s.ShiftEndTime, 108), 1, 5) + ')'
            ELSE 'Day(09:00-18:00)'
        END,
        ISNULL(CAST(s.ShiftStartTime AS TIME(0)), CAST('09:00:00' AS TIME(0))),
        ISNULL(CAST(s.ShiftEndTime AS TIME(0)), CAST('18:00:00' AS TIME(0))),
        ISNULL(CAST(s.LateTime AS TIME(0)), CAST('09:15:00' AS TIME(0)))
    FROM HRM_EmployeeOfficialInfo oi WITH (NOLOCK)
    INNER JOIN HRM_Employee e WITH (NOLOCK) ON e.EmployeeId = oi.EmployeeId
    LEFT  JOIN Core_Company comp WITH (NOLOCK) ON comp.CompanyCode = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT  JOIN HRM_Def_Department dept WITH (NOLOCK) ON dept.DepartmentCode = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation desig WITH (NOLOCK) ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT  JOIN HRM_ATD_Shift s WITH (NOLOCK) ON s.ShiftCode = oi.ShiftCode AND oi.ShiftCode <> ''
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

    -- ═══════════════════════════════════════════════════════════════════
    -- ── FINGERPRINTS MAPPING (Exact Match to MachineData Collation) ───
    -- ═══════════════════════════════════════════════════════════════════
    CREATE TABLE #EmpFingerprints (
        FingerPrintId VARCHAR(100) COLLATE SQL_Latin1_General_CP1_CI_AS PRIMARY KEY,
        EmployeeId    NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS
    );

    INSERT INTO #EmpFingerprints (FingerPrintId, EmployeeId)
    SELECT CAST(EmployeeId AS VARCHAR(100)), EmployeeId FROM #ActiveEmployees WHERE EmployeeId <> ''
    UNION
    SELECT CAST(AttendenceId AS VARCHAR(100)), EmployeeId FROM #ActiveEmployees WHERE AttendenceId <> '';

    -- ═══════════════════════════════════════════════════════════════════
    -- ── FAST DATE SERIES ──────────────────────────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
    CREATE TABLE #Dates ([Date] DATE PRIMARY KEY);

    ;WITH DateSeries AS (
        SELECT @RealFromDate AS [Date]
        UNION ALL
        SELECT DATEADD(DAY, 1, [Date])
        FROM DateSeries
        WHERE [Date] < @RealToDate
    )
    INSERT INTO #Dates ([Date])
    SELECT [Date]
    FROM DateSeries
    OPTION (MAXRECURSION 0);

    -- ═══════════════════════════════════════════════════════════════════
    -- ── PUNCHES (Machine Data & Manual Data Aggregated in Milliseconds) 
    -- ═══════════════════════════════════════════════════════════════════
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

    -- ═══════════════════════════════════════════════════════════════════
    -- ── LEAVES ────────────────────────────────────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
    CREATE TABLE #EmpLeaves (
        EmployeeId NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        LeaveDate  DATE,
        LeaveCode  NVARCHAR(50)   COLLATE SQL_Latin1_General_CP1_CI_AS,
        Reason     NVARCHAR(1000) COLLATE SQL_Latin1_General_CP1_CI_AS,
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
          AND (la.IsApproved NOT IN ('Canceled', 'Rejected'))
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    INSERT INTO #EmpLeaves (EmployeeId, LeaveDate, LeaveCode, Reason)
    SELECT EmployeeId, LeaveDate, MAX(LeaveCode), MAX(Reason)
    FROM ApprovedLeaves
    GROUP BY EmployeeId, LeaveDate;

    -- ═══════════════════════════════════════════════════════════════════
    -- ── HOLIDAYS ──────────────────────────────────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
    CREATE TABLE #Holidays (
        HolidayDate DATE PRIMARY KEY,
        HolidayName NVARCHAR(200) COLLATE SQL_Latin1_General_CP1_CI_AS
    );

    INSERT INTO #Holidays (HolidayDate, HolidayName)
    VALUES 
        ('2026-01-01', 'New Year''s Day'),
        ('2026-02-21', 'International Mother Language Day'),
        ('2026-03-19', 'Holiday'),
        ('2026-03-20', 'Holiday'),
        ('2026-03-26', 'Independence Day'),
        ('2026-04-14', 'Bengali New Year'),
        ('2026-05-01', 'May Day'),
        ('2026-12-16', 'Victory Day'),
        ('2026-12-25', 'Christmas Day');

    INSERT INTO #Holidays (HolidayDate, HolidayName)
    SELECT DISTINCT d.[Date], ISNULL(NULLIF(h.HolidayName, ''), 'Holiday')
    FROM #Dates d
    INNER JOIN HRM_ATD_Holiday h WITH (NOLOCK) ON d.[Date] >= CONVERT(DATE, h.FromDate) AND d.[Date] <= CONVERT(DATE, h.ToDate)
    WHERE h.FromDate < @NextToDate AND h.ToDate >= @RealFromDate
      AND NOT EXISTS (SELECT 1 FROM #Holidays WHERE HolidayDate = d.[Date]);

    -- ═══════════════════════════════════════════════════════════════════
    -- ── WEEKENDS ──────────────────────────────────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
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

    -- ═══════════════════════════════════════════════════════════════════
    -- ── ROSTER OVERRIDES ──────────────────────────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
    CREATE TABLE #EmpRosters (
        EmployeeId   NVARCHAR(50)  COLLATE SQL_Latin1_General_CP1_CI_AS,
        RosterDate   DATE,
        ShiftDisplay NVARCHAR(200) COLLATE SQL_Latin1_General_CP1_CI_AS,
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
        ISNULL(NULLIF(s.ShiftName,''), 'G') + '(' + 
        SUBSTRING(CONVERT(VARCHAR(8), s.ShiftStartTime, 108), 1, 5) + '-' +
        SUBSTRING(CONVERT(VARCHAR(8), s.ShiftEndTime, 108), 1, 5) + ')',
        CAST(s.ShiftStartTime AS TIME(0)),
        CAST(s.ShiftEndTime AS TIME(0)),
        CAST(s.LateTime AS TIME(0)),
        CASE WHEN rse.Weekend = 'Y' OR rse.Weekend = '1' THEN 1 ELSE 0 END
    FROM HRM_RosterScheduleEntry rse WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = rse.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, rse.[Date])
    LEFT JOIN HRM_ATD_Shift s WITH (NOLOCK) ON s.ShiftCode = rse.ShiftCode
    WHERE rse.[Date] >= @FromDateTime AND rse.[Date] < @NextToDateTime;

    CREATE NONCLUSTERED INDEX IX_Dept_Emp ON #ActiveEmployees (DepartmentName, EmployeeId);

    -- ═══════════════════════════════════════════════════════════════════
    -- ── FAST ATTENDANCE MATRIX (Direct Instant Output) ────────────────
    -- ═══════════════════════════════════════════════════════════════════
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
            WHEN d.[Date] > CAST(GETDATE() AS DATE) THEN ''
            WHEN lv.LeaveCode IS NOT NULL THEN lv.LeaveCode
            WHEN h.HolidayDate IS NOT NULL THEN 'H'
            WHEN ros.IsWeekend = 1 OR ew.WeekendDate IS NOT NULL OR DATENAME(WEEKDAY, d.[Date]) IN ('Friday', 'Saturday') THEN 'W'
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

    -- ═══════════════════════════════════════════════════════════════════
    -- ── HEADER RESULT SET (2nd Result Set) ────────────────────────────
    -- ═══════════════════════════════════════════════════════════════════
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
