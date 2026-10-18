SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_JobCardBulkReport]
    @CompanyCode      VARCHAR(50)   = '',
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
    CREATE TABLE #CompFilter  (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DeptFilter  (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DesigFilter (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #EmpFilter   (Code NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    IF @CompanyCode <> ''
    BEGIN
        INSERT INTO #CompFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM([Value]))
        FROM dbo.STRING_SPLIT(',', @CompanyCode)
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
        ISNULL(NULLIF(desig.DesignationName, ''), ISNULL(NULLIF(oi.DesignationCode, ''), '—')),
        ISNULL(oi.DepartmentCode, ''),
        ISNULL(NULLIF(dept.DepartmentName, ''), ISNULL(NULLIF(oi.DepartmentCode, ''), 'General Department')),
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'DataPath Ltd.'),
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
    FROM HRM_Employee e
    INNER JOIN HRM_EmployeeOfficialInfo oi ON oi.EmployeeId = e.EmployeeId
    LEFT  JOIN Core_Company comp ON comp.CompanyCode = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT  JOIN HRM_Def_Department dept ON dept.DepartmentCode = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation desig ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT  JOIN HRM_ATD_Shift s ON s.ShiftCode = oi.ShiftCode AND oi.ShiftCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (NOT EXISTS (SELECT 1 FROM #CompFilter)  OR oi.CompanyCode IN (SELECT Code FROM #CompFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DeptFilter)  OR oi.DepartmentCode IN (SELECT Code FROM #DeptFilter))
      AND (NOT EXISTS (SELECT 1 FROM #DesigFilter) OR oi.DesignationCode IN (SELECT Code FROM #DesigFilter))
      AND (NOT EXISTS (SELECT 1 FROM #EmpFilter)   OR e.EmployeeId IN (SELECT Code FROM #EmpFilter))
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

    -- Fingerprints mapping for 100% direct indexed equality joins to MachineData
    CREATE TABLE #EmpFingerprints (
        FingerPrintId NVARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        EmployeeId    NVARCHAR(50) COLLATE DATABASE_DEFAULT
    );

    INSERT INTO #EmpFingerprints (FingerPrintId, EmployeeId)
    SELECT EmployeeId, EmployeeId FROM #ActiveEmployees WHERE EmployeeId <> ''
    UNION
    SELECT AttendenceId, EmployeeId FROM #ActiveEmployees WHERE AttendenceId <> '';

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

    -- Punches (Machine + Manual unified with direct index seek)
    CREATE TABLE #Punches (
        EmployeeId    NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        PunchDate     DATE,
        FirstPunch    DATETIME,
        LastPunch     DATETIME,
        ManualRemarks NVARCHAR(1000) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, PunchDate)
    );

    -- Step 1: Aggregate Machine Data using indexed join
    INSERT INTO #Punches (EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks)
    SELECT
        fp.EmployeeId,
        CONVERT(DATE, md.[Date]) AS PunchDate,
        MIN(md.[Time]) AS FirstPunch,
        MAX(md.[Time]) AS LastPunch,
        ''
    FROM HRM_ATD_MachineData md
    INNER JOIN #EmpFingerprints fp ON fp.FingerPrintId = md.FingerPrintID
    WHERE md.[Date] >= @FromDateTime AND md.[Date] < @NextToDateTime
      AND md.[Time] IS NOT NULL AND md.[Time] <> '1900-01-01'
    GROUP BY fp.EmployeeId, CONVERT(DATE, md.[Date]);

    -- Step 2: Merge Manual Entry Data
    ;WITH ManualAgg AS (
        SELECT 
            m.EmployeeId,
            CONVERT(DATE, m.[Date]) AS PunchDate,
            MIN(m.[Time]) AS FirstPunch,
            MAX(m.[Time]) AS LastPunch,
            MAX(ISNULL(m.Remarks, '')) AS ManualRemarks
        FROM HRM_ATD_Manual m
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = m.EmployeeId
        WHERE m.[Date] >= @FromDateTime AND m.[Date] < @NextToDateTime
          AND m.[Time] IS NOT NULL AND m.[Time] <> '1900-01-01'
        GROUP BY m.EmployeeId, CONVERT(DATE, m.[Date])
    )
    MERGE #Punches AS tgt
    USING ManualAgg AS src
       ON (tgt.EmployeeId = src.EmployeeId AND tgt.PunchDate = src.PunchDate)
    WHEN MATCHED THEN
        UPDATE SET 
            tgt.FirstPunch = CASE WHEN src.FirstPunch < tgt.FirstPunch THEN src.FirstPunch ELSE tgt.FirstPunch END,
            tgt.LastPunch  = CASE WHEN src.LastPunch > tgt.LastPunch THEN src.LastPunch ELSE tgt.LastPunch END,
            tgt.ManualRemarks = src.ManualRemarks
    WHEN NOT MATCHED THEN
        INSERT (EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks)
        VALUES (src.EmployeeId, src.PunchDate, src.FirstPunch, src.LastPunch, src.ManualRemarks);

    -- Leaves (filtered to target date range)
    CREATE TABLE #EmpLeaves (
        EmployeeId NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        LeaveDate  DATE,
        LeaveCode  NVARCHAR(50)   COLLATE DATABASE_DEFAULT,
        Reason     NVARCHAR(1000) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, LeaveDate)
    );

    ;WITH ApprovedLeaves AS (
        SELECT 
            la.EmployeeId,
            d.[Date] AS LeaveDate,
            ISNULL(NULLIF(lt.ShortName, ''), 'L') AS LeaveCode,
            ISNULL(NULLIF(LTRIM(RTRIM(la.Reason)), ''), '') AS Reason
        FROM HRM_LeaveApplicationEntry la
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = la.EmployeeId
        INNER JOIN #Dates d ON d.[Date] >= CONVERT(DATE, la.StartDate) AND d.[Date] <= CONVERT(DATE, la.EndDate)
        LEFT  JOIN HRM_ATD_LeaveType lt ON lt.LeaveTypeCode = la.LeaveTypeId
        WHERE la.StartDate < @NextToDateTime AND la.EndDate >= @FromDateTime
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    INSERT INTO #EmpLeaves (EmployeeId, LeaveDate, LeaveCode, Reason)
    SELECT EmployeeId, LeaveDate, MAX(LeaveCode), MAX(Reason)
    FROM ApprovedLeaves
    GROUP BY EmployeeId, LeaveDate;

    -- Holidays (filtered to target date range)
    CREATE TABLE #Holidays (
        HolidayDate DATE PRIMARY KEY,
        HolidayName NVARCHAR(200) COLLATE DATABASE_DEFAULT
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
    INNER JOIN HRM_ATD_Holiday h ON d.[Date] >= CONVERT(DATE, h.FromDate) AND d.[Date] <= CONVERT(DATE, h.ToDate)
    WHERE h.FromDate < @NextToDate AND h.ToDate >= @FromDate
      AND NOT EXISTS (SELECT 1 FROM #Holidays WHERE HolidayDate = d.[Date]);

    -- Weekends (filtered to target date range)
    CREATE TABLE #EmpWeekends (
        EmployeeId  NVARCHAR(50) COLLATE DATABASE_DEFAULT,
        WeekendDate DATE,
        PRIMARY KEY (EmployeeId, WeekendDate)
    );

    INSERT INTO #EmpWeekends (EmployeeId, WeekendDate)
    SELECT DISTINCT ewd.EmployeeId, CONVERT(DATE, ewd.[Date])
    FROM HRM_EmployeeWeekendDeclaration ewd
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
        ISNULL(NULLIF(s.ShiftName,''), 'G') + '(' + 
        SUBSTRING(CONVERT(VARCHAR(8), s.ShiftStartTime, 108), 1, 5) + '-' +
        SUBSTRING(CONVERT(VARCHAR(8), s.ShiftEndTime, 108), 1, 5) + ')',
        CAST(s.ShiftStartTime AS TIME(0)),
        CAST(s.ShiftEndTime AS TIME(0)),
        CAST(s.LateTime AS TIME(0)),
        CASE WHEN rse.Weekend = 'Y' OR rse.Weekend = '1' THEN 1 ELSE 0 END
    FROM HRM_RosterScheduleEntry rse
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = rse.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, rse.[Date])
    LEFT JOIN HRM_ATD_Shift s ON s.ShiftCode = rse.ShiftCode
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
            ELSE '00:00:00'
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
            ELSE '00:00:00'
        END AS EarlyOut,
        CASE
            WHEN WorkSec > 0 THEN
                CAST(ROUND(CAST(WorkSec AS FLOAT) / 3600.0, 2) AS VARCHAR(10))
            ELSE '0.00'
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
    -- Grouped by Department: one department finishes, then next department starts
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
    DROP TABLE #DeptFilter;
    DROP TABLE #DesigFilter;
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
