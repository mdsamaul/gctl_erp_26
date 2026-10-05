SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_Attendance_InOut]
    @CompanyCode      VARCHAR(50)   = '',
    @BranchCode       VARCHAR(50)   = '',
    @BranchCodes      NVARCHAR(MAX) = '',  
    @DepartmentCode   VARCHAR(50)   = '',
    @DepartmentCodes  NVARCHAR(MAX) = '',  
    @EmployeeId       VARCHAR(50)   = '',
    @EmployeeIds      NVARCHAR(MAX) = '',  
    @FromDate         DATE          = NULL,
    @ReportType       NVARCHAR(20)  = 'InOut',  -- 'InOut', 'MissingCheckOut', 'EarlyLeave'
    @LoginEmployeeId  NVARCHAR(50)  = '',   
    @AccessCodeId     NVARCHAR(50)  = ''    
AS
BEGIN
    SET NOCOUNT ON;

    SET @CompanyCode      = LTRIM(RTRIM(ISNULL(@CompanyCode, '')));
    SET @BranchCode       = LTRIM(RTRIM(ISNULL(@BranchCode, '')));
    SET @BranchCodes      = LTRIM(RTRIM(ISNULL(@BranchCodes, '')));
    SET @DepartmentCode   = LTRIM(RTRIM(ISNULL(@DepartmentCode, '')));
    SET @DepartmentCodes  = LTRIM(RTRIM(ISNULL(@DepartmentCodes, '')));
    SET @EmployeeId       = LTRIM(RTRIM(ISNULL(@EmployeeId, '')));
    SET @EmployeeIds      = LTRIM(RTRIM(ISNULL(@EmployeeIds, '')));
    SET @ReportType       = ISNULL(NULLIF(LTRIM(RTRIM(@ReportType)), ''), 'InOut');
    SET @LoginEmployeeId  = LTRIM(RTRIM(ISNULL(@LoginEmployeeId, '')));
    SET @AccessCodeId     = LTRIM(RTRIM(ISNULL(@AccessCodeId, '')));

    IF @BranchCode <> '' AND @BranchCodes = ''
        SET @BranchCodes = @BranchCode;
    IF @DepartmentCode <> '' AND @DepartmentCodes = ''
        SET @DepartmentCodes = @DepartmentCode;
    IF @EmployeeId <> '' AND @EmployeeIds = ''
        SET @EmployeeIds = @EmployeeId;

    IF @FromDate IS NULL OR @FromDate = '1900-01-01'
        SET @FromDate = CONVERT(DATE, GETDATE());

    DECLARE @NextDate DATE = DATEADD(DAY, 1, @FromDate);

    IF NOT EXISTS (
        SELECT 1 FROM HRM_ATD_MachineData WHERE [Date] >= @FromDate AND [Date] < @NextDate
        UNION ALL
        SELECT 1 FROM HRM_ATD_Manual      WHERE [Date] >= @FromDate AND [Date] < @NextDate
    )
    BEGIN
        SELECT @FromDate = MAX(d) FROM (
            SELECT MAX(CONVERT(DATE, [Date])) AS d FROM HRM_ATD_MachineData
            WHERE [Date] < @FromDate AND [Date] <> '1900-01-01'
            UNION ALL
            SELECT MAX(CONVERT(DATE, [Date])) AS d FROM HRM_ATD_Manual
            WHERE [Date] < @FromDate AND [Date] <> '1900-01-01'
        ) sub;

        IF @FromDate IS NOT NULL AND @FromDate <> '1900-01-01'
            SET @NextDate = DATEADD(DAY, 1, @FromDate);
    END

    IF @FromDate IS NULL OR @FromDate = '1900-01-01'
    BEGIN
        SELECT 0 AS SN, '' AS EmployeeId, '' AS EmployeeName, '' AS Designation,
               '' AS DepartmentName, '' AS ShiftName, '' AS InTime,
               '' AS LateDisplay, '' AS OutTime, '00:00:00' AS EarlyOut,
               '00:00:00' AS WorkHours, 0.00 AS OTHours, '' AS [Status], '' AS Remarks
        WHERE 1 = 0;

        SELECT @FromDate AS DataDate, '' AS CompanyName, @ReportType AS ReportType
        WHERE 1 = 0;
        RETURN;
    END

    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId = '0001'            THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003') THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'            THEN 1 ELSE 0 END;

    IF @AccessCodeId = '' OR @LoginEmployeeId = ''
        SET @IsAdmin = 1;

    CREATE TABLE #BranchFilter (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #DeptFilter   (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    CREATE TABLE #EmpFilter    (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    IF @BranchCodes <> ''
    BEGIN
        DECLARE @BranchXml XML = CAST('<r><v>' + REPLACE(@BranchCodes, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO #BranchFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @BranchXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @DepartmentCodes <> ''
    BEGIN
        DECLARE @DeptXml XML = CAST('<r><v>' + REPLACE(@DepartmentCodes, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO #DeptFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @DeptXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @EmployeeIds <> ''
    BEGIN
        DECLARE @EmpXml XML = CAST('<r><v>' + REPLACE(@EmployeeIds, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO #EmpFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @EmpXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    CREATE TABLE #ActiveEmployees (
        EmployeeId     VARCHAR(50)   COLLATE DATABASE_DEFAULT PRIMARY KEY,
        EmployeeName   NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        Designation    NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        DepartmentCode VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        DepartmentName NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftName      NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        ShiftStart     TIME(0),
        LateTime       TIME(0),
        AbsentTime     TIME(0),
        ShiftEndTime   TIME(0)
    );

    INSERT INTO #ActiveEmployees (
        EmployeeId, EmployeeName, Designation, DepartmentCode,
        DepartmentName, ShiftName, ShiftStart, LateTime, AbsentTime, ShiftEndTime
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
        CASE 
            WHEN s.ShiftCode IS NOT NULL AND s.ShiftCode <> ''
             AND s.ShiftStartTime IS NOT NULL AND s.ShiftStartTime <> '1900-01-01'
             AND s.ShiftEndTime IS NOT NULL AND s.ShiftEndTime <> '1900-01-01'
            THEN s.ShiftCode + '(' +
                 LTRIM(RIGHT(CONVERT(VARCHAR(20), CONVERT(DATETIME, s.ShiftStartTime), 100), 7)) +
                 '-' +
                 LTRIM(RIGHT(CONVERT(VARCHAR(20), CONVERT(DATETIME, s.ShiftEndTime), 100), 7)) +
                 ')'
            WHEN s.ShiftCode IS NOT NULL AND s.ShiftCode <> '' THEN s.ShiftCode
            ELSE ''
        END,
        CASE WHEN s.ShiftStartTime IS NOT NULL AND s.ShiftStartTime <> '1900-01-01' THEN CONVERT(TIME(0), s.ShiftStartTime) ELSE NULL END,
        CASE WHEN s.LateTime IS NOT NULL AND s.LateTime <> '1900-01-01' THEN CONVERT(TIME(0), s.LateTime) ELSE NULL END,
        CASE WHEN s.AbsentTime IS NOT NULL AND s.AbsentTime <> '1900-01-01' THEN CONVERT(TIME(0), s.AbsentTime) ELSE NULL END,
        CASE WHEN s.ShiftEndTime IS NOT NULL AND s.ShiftEndTime <> '1900-01-01' THEN CONVERT(TIME(0), s.ShiftEndTime) ELSE NULL END
    FROM HRM_Employee             e
    INNER JOIN HRM_EmployeeOfficialInfo oi    ON oi.EmployeeId         = e.EmployeeId
    LEFT  JOIN Core_Branch          b     ON b.BranchCode          = oi.BranchCode AND oi.BranchCode <> ''
    LEFT  JOIN HRM_Def_Department   dept  ON dept.DepartmentCode   = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT  JOIN HRM_Def_Designation  desig ON desig.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT  JOIN HRM_ATD_Shift        s     ON s.ShiftCode           = oi.ShiftCode AND oi.ShiftCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (@CompanyCode = '' OR oi.CompanyCode = @CompanyCode)
      AND (NOT EXISTS (SELECT 1 FROM #BranchFilter) OR (oi.BranchCode <> '' AND oi.BranchCode IN (SELECT Code FROM #BranchFilter)))
      AND (NOT EXISTS (SELECT 1 FROM #DeptFilter)   OR (oi.DepartmentCode <> '' AND oi.DepartmentCode IN (SELECT Code FROM #DeptFilter)))
      AND (NOT EXISTS (SELECT 1 FROM #EmpFilter)    OR (e.EmployeeId <> '' AND e.EmployeeId IN (SELECT Code FROM #EmpFilter)))
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
      AND (oi.JoiningDate IS NULL OR oi.JoiningDate = '1900-01-01' OR CONVERT(DATE, oi.JoiningDate) <= @FromDate);

    CREATE TABLE #PunchSummary (
        EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        FirstPunch TIME(0)     NULL,
        LastPunch  TIME(0)     NULL,
        PunchCount INT         NOT NULL
    );

    ;WITH AllPunches AS (
        SELECT md.FingerPrintId AS EmployeeId, CONVERT(TIME(0), md.[Time]) AS PunchTime
        FROM HRM_ATD_MachineData md
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = md.FingerPrintId
        WHERE md.[Date] >= @FromDate AND md.[Date] < @NextDate
          AND md.FingerPrintId <> ''
          AND md.[Time] IS NOT NULL AND md.[Time] <> '1900-01-01'

        UNION ALL

        SELECT man.EmployeeId, CONVERT(TIME(0), man.[Time]) AS PunchTime
        FROM HRM_ATD_Manual man
        INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = man.EmployeeId
        WHERE man.[Date] >= @FromDate AND man.[Date] < @NextDate
          AND man.EmployeeId <> ''
          AND man.[Time] IS NOT NULL AND man.[Time] <> '1900-01-01'
    )
    INSERT INTO #PunchSummary (EmployeeId, FirstPunch, LastPunch, PunchCount)
    SELECT
        EmployeeId,
        MIN(PunchTime) AS FirstPunch,
        MAX(PunchTime) AS LastPunch,
        COUNT(*)       AS PunchCount
    FROM AllPunches
    GROUP BY EmployeeId;

    CREATE TABLE #OnLeave (EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    INSERT INTO #OnLeave (EmployeeId)
    SELECT DISTINCT ae.EmployeeId
    FROM #ActiveEmployees ae
    WHERE EXISTS (
        SELECT 1 FROM HRM_LeaveApplicationEntry la
        WHERE la.EmployeeId = ae.EmployeeId
          AND la.StartDate < @NextDate
          AND la.EndDate >= @FromDate
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    )
    OR EXISTS (
        SELECT 1 FROM HRM_LeaveApplicationDays ld
        JOIN HRM_LeaveApplicationEntry la ON la.LeaveAppEntryId = ld.LeaveAppEntryId
        WHERE la.EmployeeId = ae.EmployeeId
          AND ld.[days] >= @FromDate AND ld.[days] < @NextDate
          AND (la.IsApproved IN ('Y','Approved') OR la.HODApprovalStatus IN ('Y','Approved') OR la.HRApprovalStatus IN ('Y','Approved'))
    );

    ;WITH InOutData AS (
        SELECT
            ae.EmployeeId,
            ae.EmployeeName,
            ae.Designation,
            ae.DepartmentName,
            ae.ShiftName,
            ae.ShiftEndTime,
            ps.FirstPunch,
            ps.LastPunch,
            ps.PunchCount,
            CASE
                WHEN ps.EmployeeId IS NOT NULL AND ae.LateTime IS NOT NULL AND ps.FirstPunch >= ae.LateTime
                    THEN DATEDIFF(MINUTE, ae.LateTime, ps.FirstPunch)
                ELSE 0
            END AS LateMinutes,
            CASE
                WHEN ps.EmployeeId IS NOT NULL
                  AND ae.LateTime   IS NOT NULL
                  AND ae.AbsentTime IS NOT NULL
                    THEN CASE
                        WHEN ps.FirstPunch < ae.LateTime                            THEN 'Present'
                        WHEN ps.FirstPunch >= ae.LateTime AND ps.FirstPunch < ae.AbsentTime THEN 'Late'
                        ELSE 'Absent'
                    END
                WHEN ps.EmployeeId IS NOT NULL AND ae.LateTime IS NULL THEN 'Present'
                WHEN lv.EmployeeId IS NOT NULL                         THEN 'On Leave'
                ELSE 'Absent'
            END AS RawStatus
        FROM #ActiveEmployees ae
        LEFT JOIN #PunchSummary ps ON ps.EmployeeId = ae.EmployeeId
        LEFT JOIN #OnLeave      lv ON lv.EmployeeId = ae.EmployeeId
    ),
    CalculatedRows AS (
        SELECT
            EmployeeId,
            EmployeeName,
            Designation,
            DepartmentName,
            ShiftName,
            FirstPunch,
            LastPunch,
            ShiftEndTime,
            RawStatus,
            ISNULL(CONVERT(VARCHAR(8), FirstPunch, 108), '') AS InTime,
            CASE
                WHEN LateMinutes >= 60 THEN CAST(LateMinutes / 60 AS VARCHAR(5)) + ' Hrs. ' + CAST(LateMinutes % 60 AS VARCHAR(5)) + ' Min.'
                WHEN LateMinutes > 0  THEN CAST(LateMinutes AS VARCHAR(5)) + ' Min.'
                ELSE ''
            END AS LateDisplay,
            CASE
                WHEN LastPunch IS NOT NULL AND LastPunch <> FirstPunch
                    THEN CONVERT(VARCHAR(8), LastPunch, 108)
                ELSE ''
            END AS OutTime,
            CASE
                WHEN LastPunch IS NOT NULL AND LastPunch <> FirstPunch
                   AND ShiftEndTime IS NOT NULL AND LastPunch < ShiftEndTime
                    THEN CONVERT(VARCHAR(8), DATEADD(SECOND, DATEDIFF(SECOND, LastPunch, ShiftEndTime), CAST('00:00:00' AS TIME)), 108)
                ELSE '00:00:00'
            END AS EarlyOut,
            CASE
                WHEN FirstPunch IS NOT NULL AND LastPunch IS NOT NULL AND LastPunch <> FirstPunch
                    THEN CONVERT(VARCHAR(8), DATEADD(SECOND, DATEDIFF(SECOND, FirstPunch, LastPunch), CAST('00:00:00' AS TIME)), 108)
                ELSE '00:00:00'
            END AS WorkHours,
            CASE
                WHEN LastPunch IS NOT NULL AND LastPunch <> FirstPunch
                   AND ShiftEndTime IS NOT NULL AND LastPunch > ShiftEndTime
                    THEN CAST(CAST(DATEDIFF(MINUTE, ShiftEndTime, LastPunch) AS FLOAT) / 60.0 AS DECIMAL(5,2))
                ELSE 0.00
            END AS OTHours,
            CASE
                WHEN FirstPunch IS NOT NULL AND (LastPunch IS NULL OR LastPunch = FirstPunch) THEN 'MCO'
                ELSE RawStatus
            END AS [Status],
            '' AS Remarks
        FROM InOutData
    )
    SELECT
        ROW_NUMBER() OVER (PARTITION BY DepartmentName ORDER BY EmployeeName) AS SN,
        ISNULL(EmployeeId, '')     AS EmployeeId,
        ISNULL(EmployeeName, '')   AS EmployeeName,
        ISNULL(Designation, '')    AS Designation,
        ISNULL(DepartmentName, '') AS DepartmentName,
        ISNULL(ShiftName, '')      AS ShiftName,
        ISNULL(InTime, '')         AS InTime,
        ISNULL(LateDisplay, '')    AS LateDisplay,
        ISNULL(OutTime, '')        AS OutTime,
        ISNULL(EarlyOut, '00:00:00')   AS EarlyOut,
        ISNULL(WorkHours, '00:00:00')  AS WorkHours,
        ISNULL(OTHours, 0.00)        AS OTHours,
        ISNULL([Status], '')       AS [Status],
        ISNULL(Remarks, '')        AS Remarks
    FROM CalculatedRows
    WHERE (
            @ReportType = 'InOut'
           )
           OR (
            @ReportType = 'MissingCheckOut'
            AND FirstPunch IS NOT NULL
            AND (LastPunch IS NULL OR LastPunch = FirstPunch)
           )
           OR (
            @ReportType = 'EarlyLeave'
            AND FirstPunch IS NOT NULL
            AND LastPunch IS NOT NULL
            AND FirstPunch <> LastPunch
            AND ShiftEndTime IS NOT NULL
            AND LastPunch < ShiftEndTime
           )
    ORDER BY DepartmentName, EmployeeName
    OPTION (RECOMPILE);

    SELECT
        @FromDate   AS DataDate,
        ISNULL((SELECT TOP 1 CompanyName FROM Core_Company WHERE (@CompanyCode <> '' AND CompanyCode = @CompanyCode)), 'Data Path') AS CompanyName,
        @ReportType AS ReportType;

    DROP TABLE #BranchFilter;
    DROP TABLE #DeptFilter;
    DROP TABLE #EmpFilter;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #PunchSummary;
    DROP TABLE #OnLeave;
END;
GO