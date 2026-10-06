SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_JobCardReport]
    @CompanyCode      VARCHAR(50)   = '',
    @EmployeeIds      NVARCHAR(MAX) = '',
    @FromDate         DATE          = NULL,
    @ToDate           DATE          = NULL,
    @LoginEmployeeId  NVARCHAR(50)  = '',
    @AccessCodeId     NVARCHAR(50)  = ''
AS
BEGIN
    SET NOCOUNT ON;

    SET @CompanyCode      = LTRIM(RTRIM(ISNULL(@CompanyCode, '')));
    SET @EmployeeIds      = LTRIM(RTRIM(ISNULL(@EmployeeIds, '')));
    SET @LoginEmployeeId  = LTRIM(RTRIM(ISNULL(@LoginEmployeeId, '')));
    SET @AccessCodeId     = LTRIM(RTRIM(ISNULL(@AccessCodeId, '')));

    IF @FromDate IS NULL OR @FromDate = '1900-01-01'
        SET @FromDate = DATEFROMPARTS(YEAR(GETDATE()), 1, 1);

    IF @ToDate IS NULL OR @ToDate = '1900-01-01'
        SET @ToDate = CONVERT(DATE, GETDATE());

    IF @ToDate < @FromDate
    BEGIN
        DECLARE @TempDate DATE = @FromDate;
        SET @FromDate = @ToDate;
        SET @ToDate = @TempDate;
    END

    DECLARE @NextToDate DATE = DATEADD(DAY, 1, @ToDate);

    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId = '0001'            THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003') THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'            THEN 1 ELSE 0 END;

    IF @AccessCodeId = '' OR @LoginEmployeeId = ''
        SET @IsAdmin = 1;

    CREATE TABLE #EmpFilter (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    IF @EmployeeIds <> ''
    BEGIN
        DECLARE @EmpXml XML = CAST('<r><v>' + REPLACE(@EmployeeIds, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO #EmpFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @EmpXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    -- Active Employees
    CREATE TABLE #ActiveEmployees (
        EmployeeId     VARCHAR(50)   COLLATE DATABASE_DEFAULT PRIMARY KEY,
        EmployeeName   NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        Designation    NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        DepartmentCode VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        DepartmentName NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        CompanyCode    VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        CompanyName    NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftDisplay   NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftStart     TIME(0),
        ShiftEnd       TIME(0),
        LateCutoff     TIME(0)
    );

    INSERT INTO #ActiveEmployees (
        EmployeeId, EmployeeName, Designation, DepartmentCode,
        DepartmentName, CompanyCode, CompanyName, ShiftDisplay,
        ShiftStart, ShiftEnd, LateCutoff
    )
    SELECT
        e.EmployeeId,
        ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))), ''), ISNULL(e.EmployeeId, '')),
        CASE 
            WHEN desig.DesignationName IS NOT NULL AND desig.DesignationName <> '' THEN desig.DesignationName
            WHEN oi.DesignationCode IS NOT NULL AND oi.DesignationCode <> '' THEN oi.DesignationCode
            ELSE '—'
        END,
        ISNULL(oi.DepartmentCode, ''),
        CASE 
            WHEN dept.DepartmentName IS NOT NULL AND dept.DepartmentName <> '' THEN dept.DepartmentName
            WHEN oi.DepartmentCode IS NOT NULL AND oi.DepartmentCode <> '' THEN oi.DepartmentCode
            ELSE '—'
        END,
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'DataPath Ltd.'),
        'Day(09:00 AM-06:00 PM)',
        CAST('09:00:00' AS TIME(0)),
        CAST('18:00:00' AS TIME(0)),
        CAST('10:00:59' AS TIME(0))
    FROM HRM_Employee             e
    INNER JOIN HRM_EmployeeOfficialInfo oi    ON oi.EmployeeId         = e.EmployeeId
    LEFT  JOIN Core_Company         comp  ON comp.CompanyCode      = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT  JOIN HRM_Def_Department   dept  ON dept.DepartmentCode   = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation  desig ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (@CompanyCode = '' OR oi.CompanyCode = @CompanyCode)
      AND (NOT EXISTS (SELECT 1 FROM #EmpFilter) OR (e.EmployeeId <> '' AND e.EmployeeId IN (SELECT Code FROM #EmpFilter)))
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

    -- Date Series
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

    -- Punches
    CREATE TABLE #Punches (
        EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT,
        PunchDate  DATE,
        FirstPunch DATETIME,
        LastPunch  DATETIME,
        ManualRemarks NVARCHAR(500) COLLATE DATABASE_DEFAULT,
        PRIMARY KEY (EmployeeId, PunchDate)
    );

    ;WITH AllPunches AS (
        SELECT md.FingerPrintId AS EmployeeId, CONVERT(DATE, md.[Date]) AS PunchDate, md.[Time] AS PunchTime, CAST(NULL AS NVARCHAR(500)) AS Remarks
        FROM HRM_ATD_MachineData md
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = md.FingerPrintId
        WHERE md.[Date] >= @FromDate AND md.[Date] < @NextToDate
          AND md.FingerPrintId <> ''
          AND md.[Time] IS NOT NULL AND md.[Time] <> '1900-01-01'

        UNION ALL

        SELECT man.EmployeeId, CONVERT(DATE, man.[Date]) AS PunchDate, man.[Time] AS PunchTime, NULLIF(LTRIM(RTRIM(man.Remarks)), '') AS Remarks
        FROM HRM_ATD_Manual man
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = man.EmployeeId
        WHERE man.[Date] >= @FromDate AND man.[Date] < @NextToDate
          AND man.EmployeeId <> ''
          AND man.[Time] IS NOT NULL AND man.[Time] <> '1900-01-01'
    )
    INSERT INTO #Punches (EmployeeId, PunchDate, FirstPunch, LastPunch, ManualRemarks)
    SELECT
        EmployeeId,
        PunchDate,
        MIN(PunchTime) AS FirstPunch,
        MAX(PunchTime) AS LastPunch,
        MAX(Remarks)   AS ManualRemarks
    FROM AllPunches
    GROUP BY EmployeeId, PunchDate;

    -- Leaves
    CREATE TABLE #EmpLeaves (
        EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT,
        LeaveDate  DATE,
        LeaveCode  VARCHAR(20) COLLATE DATABASE_DEFAULT,
        Reason     NVARCHAR(500) COLLATE DATABASE_DEFAULT,
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
        WHERE (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    INSERT INTO #EmpLeaves (EmployeeId, LeaveDate, LeaveCode, Reason)
    SELECT EmployeeId, LeaveDate, MAX(LeaveCode), MAX(Reason)
    FROM ApprovedLeaves
    GROUP BY EmployeeId, LeaveDate;

    -- Holidays (National holidays in 2026 + any declared in HRM_ATD_Holiday)
    CREATE TABLE #Holidays (
        HolidayDate DATE PRIMARY KEY,
        HolidayName NVARCHAR(200) COLLATE DATABASE_DEFAULT
    );

    -- Insert standard/known holidays
    INSERT INTO #Holidays (HolidayDate, HolidayName)
    VALUES 
        ('2026-01-01', 'New Year''s Day'),
        ('2026-02-21', 'Shaheed Day & International Mother Language Day'),
        ('2026-03-19', 'Holiday'),
        ('2026-03-20', 'Holiday'),
        ('2026-03-26', 'Independence Day'),
        ('2026-04-14', 'Bengali New Year'),
        ('2026-05-01', 'May Day'),
        ('2026-12-16', 'Victory Day'),
        ('2026-12-25', 'Christmas Day');

    -- Insert any from HRM_ATD_Holiday
    INSERT INTO #Holidays (HolidayDate, HolidayName)
    SELECT DISTINCT d.[Date], ISNULL(NULLIF(h.HolidayName, ''), 'Holiday')
    FROM #Dates d
    INNER JOIN HRM_ATD_Holiday h ON d.[Date] >= CONVERT(DATE, h.FromDate) AND d.[Date] <= CONVERT(DATE, h.ToDate)
    WHERE NOT EXISTS (SELECT 1 FROM #Holidays WHERE HolidayDate = d.[Date]);

    -- Employee Specific Weekend Declarations
    CREATE TABLE #EmpWeekends (
        EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT,
        WeekendDate DATE,
        PRIMARY KEY (EmployeeId, WeekendDate)
    );

    INSERT INTO #EmpWeekends (EmployeeId, WeekendDate)
    SELECT DISTINCT ewd.EmployeeId, CONVERT(DATE, ewd.[Date])
    FROM HRM_EmployeeWeekendDeclaration ewd
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = ewd.EmployeeId
    INNER JOIN #Dates d ON d.[Date] = CONVERT(DATE, ewd.[Date]);

    -- Base calculation
    ;WITH BaseRows AS (
        SELECT
            ae.EmployeeId,
            ae.EmployeeName,
            ae.Designation,
            ae.DepartmentName,
            ae.CompanyName,
            d.[Date],
            DATENAME(WEEKDAY, d.[Date]) AS [Day],
            ae.ShiftDisplay,
            ae.ShiftStart,
            ae.ShiftEnd,
            ae.LateCutoff,
            p.FirstPunch,
            p.LastPunch,
            p.ManualRemarks,
            lv.LeaveCode,
            lv.Reason AS LeaveReason,
            h.HolidayName,
            CASE
                WHEN ew.WeekendDate IS NOT NULL THEN 1
                WHEN DATENAME(WEEKDAY, d.[Date]) IN ('Saturday', 'Sunday') THEN 1
                ELSE 0
            END AS IsWeekend
        FROM #ActiveEmployees ae
        CROSS JOIN #Dates d
        LEFT JOIN #Punches p ON p.EmployeeId = ae.EmployeeId AND p.PunchDate = d.[Date]
        LEFT JOIN #EmpLeaves lv ON lv.EmployeeId = ae.EmployeeId AND lv.LeaveDate = d.[Date]
        LEFT JOIN #Holidays h ON h.HolidayDate = d.[Date]
        LEFT JOIN #EmpWeekends ew ON ew.EmployeeId = ae.EmployeeId AND ew.WeekendDate = d.[Date]
    ),
    CalculatedRows AS (
        SELECT
            EmployeeId,
            EmployeeName,
            Designation,
            DepartmentName,
            CompanyName,
            [Date],
            CONVERT(VARCHAR(10), [Date], 105) AS DateDisplay, -- dd-MM-yyyy
            [Day],
            CASE
                WHEN FirstPunch IS NOT NULL THEN ShiftDisplay
                ELSE ''
            END AS Shift,
            CASE 
                WHEN FirstPunch IS NOT NULL THEN 
                    LTRIM(SUBSTRING(CONVERT(VARCHAR(30), FirstPunch, 109), 13, 8)) + ' ' + RIGHT(CONVERT(VARCHAR(30), FirstPunch, 109), 2)
                ELSE ''
            END AS InTime,
            CASE
                WHEN FirstPunch IS NOT NULL AND CONVERT(TIME(0), FirstPunch) > LateCutoff
                THEN
                    RIGHT('00' + CAST(DATEDIFF(SECOND, LateCutoff, CONVERT(TIME(0), FirstPunch)) / 3600 AS VARCHAR(2)), 2) + ':' +
                    RIGHT('00' + CAST((DATEDIFF(SECOND, LateCutoff, CONVERT(TIME(0), FirstPunch)) % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                    RIGHT('00' + CAST(DATEDIFF(SECOND, LateCutoff, CONVERT(TIME(0), FirstPunch)) % 60 AS VARCHAR(2)), 2)
                ELSE '00:00:00'
            END AS Late,
            CASE
                WHEN LastPunch IS NOT NULL AND CONVERT(TIME(0), LastPunch) <> CONVERT(TIME(0), FirstPunch)
                THEN 
                    LTRIM(SUBSTRING(CONVERT(VARCHAR(30), LastPunch, 109), 13, 8)) + ' ' + RIGHT(CONVERT(VARCHAR(30), LastPunch, 109), 2)
                ELSE ''
            END AS OutTime,
            CASE
                WHEN LastPunch IS NOT NULL AND CONVERT(TIME(0), LastPunch) <> CONVERT(TIME(0), FirstPunch)
                 AND CONVERT(TIME(0), LastPunch) < ShiftEnd
                THEN
                    RIGHT('00' + CAST(DATEDIFF(SECOND, CONVERT(TIME(0), LastPunch), ShiftEnd) / 3600 AS VARCHAR(2)), 2) + ':' +
                    RIGHT('00' + CAST((DATEDIFF(SECOND, CONVERT(TIME(0), LastPunch), ShiftEnd) % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                    RIGHT('00' + CAST(DATEDIFF(SECOND, CONVERT(TIME(0), LastPunch), ShiftEnd) % 60 AS VARCHAR(2)), 2)
                ELSE '00:00:00'
            END AS EarlyOut,
            CASE
                WHEN FirstPunch IS NOT NULL AND LastPunch IS NOT NULL AND CONVERT(TIME(0), LastPunch) <> CONVERT(TIME(0), FirstPunch)
                THEN
                    RIGHT('00' + CAST(DATEDIFF(SECOND, FirstPunch, LastPunch) / 3600 AS VARCHAR(2)), 2) + ':' +
                    RIGHT('00' + CAST((DATEDIFF(SECOND, FirstPunch, LastPunch) % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                    RIGHT('00' + CAST(DATEDIFF(SECOND, FirstPunch, LastPunch) % 60 AS VARCHAR(2)), 2)
                ELSE '00:00:00'
            END AS WorkHours,
            CASE
                WHEN FirstPunch IS NOT NULL THEN
                    CASE WHEN CONVERT(TIME(0), FirstPunch) > LateCutoff THEN 'L' ELSE 'P' END
                WHEN HolidayName IS NOT NULL THEN 'H'
                WHEN IsWeekend = 1 THEN 'W'
                WHEN LeaveCode IS NOT NULL THEN LeaveCode
                ELSE 'A'
            END AS [Status],
            CASE
                WHEN LeaveReason IS NOT NULL AND LeaveReason <> '' THEN LeaveReason
                WHEN ManualRemarks IS NOT NULL AND ManualRemarks <> '' THEN ManualRemarks
                ELSE ''
            END AS Remarks
        FROM BaseRows
    )
    SELECT
        EmployeeId,
        EmployeeName,
        Designation,
        DepartmentName,
        CompanyName,
        [Date],
        DateDisplay,
        [Day],
        Shift,
        InTime,
        Late,
        OutTime,
        EarlyOut,
        WorkHours,
        [Status],
        Remarks
    FROM CalculatedRows
    ORDER BY EmployeeId, [Date];

    -- Header Result Set
    SELECT
        ISNULL((SELECT TOP 1 CompanyName FROM #ActiveEmployees), 'DataPath Ltd.') AS CompanyName,
        CONVERT(VARCHAR(10), @FromDate, 103) AS FromDate, -- dd/MM/yyyy
        CONVERT(VARCHAR(10), @ToDate, 103)   AS ToDate,   -- dd/MM/yyyy
        'Date: ' + CONVERT(VARCHAR(10), @FromDate, 103) + ' - ' + CONVERT(VARCHAR(10), @ToDate, 103) AS DateRangeDisplay;

    DROP TABLE #EmpFilter;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #Dates;
    DROP TABLE #Punches;
    DROP TABLE #EmpLeaves;
    DROP TABLE #Holidays;
    DROP TABLE #EmpWeekends;
END;
GO
