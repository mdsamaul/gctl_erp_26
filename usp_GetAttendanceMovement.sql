SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_GetAttendanceMovement]
    @CompanyCode      VARCHAR(50)  = '',
    @BranchCode       VARCHAR(50)  = '',
    @DepartmentCode   VARCHAR(50)  = '',
    @FromDate         DATE         = NULL,
    @Page             INT          = 1,
    @PageSize         INT          = 10,
    @Search           VARCHAR(100) = '',
    @LoginEmployeeId  NVARCHAR(50) = '',   
    @AccessCodeId     NVARCHAR(50) = ''    
AS
BEGIN
    SET NOCOUNT ON;

    SET @CompanyCode      = LTRIM(RTRIM(ISNULL(@CompanyCode, '')));
    SET @BranchCode       = LTRIM(RTRIM(ISNULL(@BranchCode, '')));
    SET @DepartmentCode   = LTRIM(RTRIM(ISNULL(@DepartmentCode, '')));
    SET @Search           = LTRIM(RTRIM(ISNULL(@Search, '')));
    SET @LoginEmployeeId  = LTRIM(RTRIM(ISNULL(@LoginEmployeeId, '')));
    SET @AccessCodeId     = LTRIM(RTRIM(ISNULL(@AccessCodeId, '')));

    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId = '0001'            THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003') THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'            THEN 1 ELSE 0 END;

    IF @AccessCodeId = '' OR @LoginEmployeeId = ''
        SET @IsAdmin = 1;

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
        SELECT
            0   AS TotalEmployees, 0   AS PresentCount,
            0   AS LateCount,      0   AS OnLeaveCount,
            0   AS AbsentCount,    0.0 AS PresentPct,
            0.0 AS LatePct,        0.0 AS OnLeavePct,
            0.0 AS AbsentPct,      NULL AS DataDate
        WHERE 1 = 0;

        SELECT
            NULL AS EmployeeId,    NULL AS [Name],
            NULL AS Designation,   NULL AS CheckIn,
            NULL AS CheckOut,      NULL AS [Status],
            NULL AS Movement,      NULL AS Remarks,
            NULL AS Photo,         NULL AS ImgType,
            0    AS RowNum,        0    AS TotalCount,
            NULL AS DataDate,      0    AS LateByMinutes
        WHERE 1 = 0;
        RETURN;
    END

    CREATE TABLE #ActiveEmployees (
        EmployeeId   VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        ShiftCode    VARCHAR(50) COLLATE DATABASE_DEFAULT,
        ShiftStart   TIME(0),
        LateTime     TIME(0),
        AbsentTime   TIME(0),
        ShiftEndTime TIME(0)
    );

    INSERT INTO #ActiveEmployees (EmployeeId, ShiftCode, ShiftStart, LateTime, AbsentTime, ShiftEndTime)
    SELECT
        e.EmployeeId,
        oi.ShiftCode,
        CONVERT(TIME(0), s.ShiftStartTime),
        CONVERT(TIME(0), s.LateTime),
        CONVERT(TIME(0), s.AbsentTime),
        CONVERT(TIME(0), s.ShiftEndTime)
    FROM HRM_Employee              e
    INNER JOIN HRM_EmployeeOfficialInfo oi ON oi.EmployeeId = e.EmployeeId
    LEFT  JOIN HRM_ATD_Shift         s  ON s.ShiftCode    = oi.ShiftCode AND oi.ShiftCode <> ''
    WHERE oi.EmployeeStatus = '01'
      AND (@CompanyCode     = '' OR oi.CompanyCode     = @CompanyCode)
      AND (@BranchCode      = '' OR oi.BranchCode      = @BranchCode)
      AND (@DepartmentCode  = '' OR oi.DepartmentCode  = @DepartmentCode)
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

    CREATE TABLE #AllPunches (
        EmployeeId VARCHAR(50)   COLLATE DATABASE_DEFAULT NOT NULL,
        PunchTime  DATETIME      NOT NULL,
        Source     VARCHAR(4)    COLLATE DATABASE_DEFAULT,
        Remarks    NVARCHAR(500) COLLATE DATABASE_DEFAULT
    );

    INSERT INTO #AllPunches (EmployeeId, PunchTime, Source, Remarks)
    SELECT
        md.FingerPrintId,
        md.[Time],
        CASE
            WHEN (md.[Latitude ] IS NOT NULL AND LTRIM(RTRIM(md.[Latitude ])) <> '')
              OR (md.Longitude   IS NOT NULL AND LTRIM(RTRIM(md.Longitude  )) <> '')
            THEN 'Apps'
            ELSE NULL
        END,
        NULL
    FROM HRM_ATD_MachineData md
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = md.FingerPrintId
    WHERE md.[Date] >= @FromDate AND md.[Date] < @NextDate

    UNION ALL

    SELECT
        man.EmployeeId,
        man.[Time],
        'ME',
        NULLIF(LTRIM(RTRIM(man.Remarks)), '')
    FROM HRM_ATD_Manual man
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = man.EmployeeId
    WHERE man.[Date] >= @FromDate AND man.[Date] < @NextDate;

    CREATE CLUSTERED INDEX IX_AllPunches_Emp_Time ON #AllPunches (EmployeeId, PunchTime);

    CREATE TABLE #EmpFirstLast (
        EmployeeId    VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        FirstPunchDT  DATETIME,
        LatestPunchDT DATETIME
    );

    INSERT INTO #EmpFirstLast (EmployeeId, FirstPunchDT, LatestPunchDT)
    SELECT EmployeeId, MIN(PunchTime), MAX(PunchTime)
    FROM #AllPunches
    GROUP BY EmployeeId;

    CREATE TABLE #CheckOutCandidate (
        EmployeeId  VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL,
        PunchTime   DATETIME,
        Source      VARCHAR(4)  COLLATE DATABASE_DEFAULT,
        rn          INT
    );

    INSERT INTO #CheckOutCandidate (EmployeeId, PunchTime, Source, rn)
    SELECT ap.EmployeeId, ap.PunchTime, ap.Source,
           ROW_NUMBER() OVER (PARTITION BY ap.EmployeeId ORDER BY ap.PunchTime ASC)
    FROM #AllPunches ap
    INNER JOIN #ActiveEmployees ae ON ae.EmployeeId = ap.EmployeeId
    WHERE ae.ShiftEndTime IS NOT NULL
      AND CONVERT(TIME(0), ap.PunchTime) > ae.ShiftEndTime;

    CREATE UNIQUE CLUSTERED INDEX IX_CheckOutCandidate_Emp_Rn ON #CheckOutCandidate (EmployeeId, rn);

    CREATE TABLE #Punch (
        EmployeeId      VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        FirstPunch      TIME(0),
        CheckIn         TIME(0),
        CheckInSource   VARCHAR(4) COLLATE DATABASE_DEFAULT,
        CheckOut        TIME(0),
        CheckOutSource  VARCHAR(4) COLLATE DATABASE_DEFAULT,
        FirstPunchDT    DATETIME,
        LatestPunchDT   DATETIME
    );

    ;WITH FirstPunchRanked AS (
        SELECT EmployeeId, PunchTime, Source,
               ROW_NUMBER() OVER (PARTITION BY EmployeeId ORDER BY PunchTime ASC) AS rn
        FROM #AllPunches
    )
    INSERT INTO #Punch (EmployeeId, FirstPunch, CheckIn, CheckInSource,
                          CheckOut, CheckOutSource, FirstPunchDT, LatestPunchDT)
    SELECT
        fl.EmployeeId,
        CONVERT(TIME(0), fl.FirstPunchDT),
        CONVERT(TIME(0), fl.FirstPunchDT),
        fp.Source,
        CONVERT(TIME(0), co.PunchTime),
        co.Source,
        fl.FirstPunchDT,
        fl.LatestPunchDT
    FROM #EmpFirstLast fl
    LEFT JOIN FirstPunchRanked fp ON fp.EmployeeId = fl.EmployeeId AND fp.rn = 1
    LEFT JOIN #CheckOutCandidate co ON co.EmployeeId = fl.EmployeeId AND co.rn = 1;

    CREATE TABLE #Leave (EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    INSERT INTO #Leave (EmployeeId)
    SELECT DISTINCT ae.EmployeeId
    FROM #ActiveEmployees ae
    WHERE NOT EXISTS (SELECT 1 FROM #Punch p WHERE p.EmployeeId = ae.EmployeeId)
      AND (
          EXISTS (
              SELECT 1
              FROM HRM_LeaveApplicationEntry la
              WHERE la.EmployeeId             = ae.EmployeeId
                AND CONVERT(DATE, la.StartDate) <= @FromDate
                AND CONVERT(DATE, la.EndDate)   >= @FromDate
                AND (
                      la.IsApproved        IN ('Y', 'Approved')
                   OR la.HODApprovalStatus IN ('Y', 'Approved')
                   OR la.HRApprovalStatus  IN ('Y', 'Approved')
                )
          )
          OR
          EXISTS (
              SELECT 1
              FROM HRM_LeaveApplicationDays  ld
              JOIN HRM_LeaveApplicationEntry la ON la.LeaveAppEntryId = ld.LeaveAppEntryId
              WHERE la.EmployeeId            = ae.EmployeeId
                AND CONVERT(DATE, ld.[days]) = @FromDate
                AND (
                      la.IsApproved        IN ('Y', 'Approved')
                   OR la.HODApprovalStatus IN ('Y', 'Approved')
                   OR la.HRApprovalStatus  IN ('Y', 'Approved')
                )
          )
      );

    CREATE TABLE #EmployeeStatus (
        EmployeeId    VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        [Status]      VARCHAR(20) COLLATE DATABASE_DEFAULT,
        StatusOrder   TINYINT,
        LateByMinutes INT
    );

    INSERT INTO #EmployeeStatus (EmployeeId, [Status], StatusOrder, LateByMinutes)
    SELECT
        ae.EmployeeId,
        CASE
            WHEN p.EmployeeId IS NOT NULL
             AND ae.LateTime   IS NOT NULL
             AND ae.AbsentTime IS NOT NULL THEN
                CASE
                    WHEN p.FirstPunch < ae.LateTime                                   THEN 'Present'
                    WHEN p.FirstPunch >= ae.LateTime AND p.FirstPunch < ae.AbsentTime THEN 'Late'
                    ELSE 'Absent'
                END
            WHEN p.EmployeeId IS NOT NULL AND ae.LateTime IS NULL THEN 'Present'
            WHEN l.EmployeeId IS NOT NULL                         THEN 'On Leave'
            ELSE 'Absent'
        END,
        CASE
            WHEN p.EmployeeId IS NOT NULL
             AND ae.LateTime   IS NOT NULL
             AND ae.AbsentTime IS NOT NULL THEN
                CASE
                    WHEN p.FirstPunch < ae.LateTime                                   THEN 1
                    WHEN p.FirstPunch >= ae.LateTime AND p.FirstPunch < ae.AbsentTime THEN 2
                    ELSE 4
                END
            WHEN p.EmployeeId IS NOT NULL AND ae.LateTime IS NULL THEN 1
            WHEN l.EmployeeId IS NOT NULL                         THEN 3
            ELSE 4
        END,
        CASE
            WHEN p.EmployeeId IS NOT NULL
             AND ae.LateTime   IS NOT NULL
             AND ae.AbsentTime IS NOT NULL
             AND p.FirstPunch >= ae.LateTime
             AND p.FirstPunch  < ae.AbsentTime
             THEN ISNULL(DATEDIFF(MINUTE, ae.ShiftStart, p.FirstPunch), 0)
            ELSE 0
        END
    FROM #ActiveEmployees ae
    LEFT JOIN #Punch p ON p.EmployeeId = ae.EmployeeId
    LEFT JOIN #Leave l ON l.EmployeeId = ae.EmployeeId;

    DECLARE @Total   INT = 0,
            @Present INT = 0,
            @Late    INT = 0,
            @OnLeave INT = 0,
            @Absent  INT = 0;

    SELECT
        @Total   = COUNT(1),
        @Present = COUNT(CASE WHEN [Status] = 'Present'  THEN 1 END),
        @Late    = COUNT(CASE WHEN [Status] = 'Late'     THEN 1 END),
        @OnLeave = COUNT(CASE WHEN [Status] = 'On Leave' THEN 1 END),
        @Absent  = COUNT(CASE WHEN [Status] = 'Absent'   THEN 1 END)
    FROM #EmployeeStatus;

    SELECT
        @Total   AS TotalEmployees,
        @Present AS PresentCount,
        @Late    AS LateCount,
        @OnLeave AS OnLeaveCount,
        @Absent  AS AbsentCount,
        CASE WHEN @Total=0 THEN 0.0 ELSE CONVERT(DECIMAL(5,1), 100.0 * @Present / @Total) END AS PresentPct,
        CASE WHEN @Total=0 THEN 0.0 ELSE CONVERT(DECIMAL(5,1), 100.0 * @Late    / @Total) END AS LatePct,
        CASE WHEN @Total=0 THEN 0.0 ELSE CONVERT(DECIMAL(5,1), 100.0 * @OnLeave / @Total) END AS OnLeavePct,
        CASE WHEN @Total=0 THEN 0.0 ELSE CONVERT(DECIMAL(5,1), 100.0 * @Absent  / @Total) END AS AbsentPct,
        @FromDate AS DataDate;

    CREATE TABLE #PagedRows (
        RowNum         INT PRIMARY KEY,
        TotalCount     INT,
        EmployeeId     VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        [Name]         NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        Designation    NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        CheckIn        VARCHAR(20)   COLLATE DATABASE_DEFAULT,
        CheckOut       VARCHAR(20)   COLLATE DATABASE_DEFAULT,
        [Status]       VARCHAR(20)   COLLATE DATABASE_DEFAULT,
        LateByMinutes  INT,
        Photo          VARBINARY(MAX),
        ImgType        VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        FirstPunchDT   DATETIME,
        ShiftEndTime   TIME(0)
    );

    ;WITH EmpData AS (
        SELECT
            ae.EmployeeId,
            ISNULL(NULLIF(LTRIM(RTRIM(
                ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,'')
            )),''), ae.EmployeeId)                        AS [Name],
            ISNULL(NULLIF(d.DesignationName, ''), ISNULL(NULLIF(oi.DesignationCode, ''), '—')) AS Designation,

            CASE WHEN p.CheckIn IS NULL THEN NULL ELSE
                RIGHT('0'+CAST(CASE WHEN DATEPART(HOUR,p.CheckIn)%12=0 THEN 12
                                    ELSE DATEPART(HOUR,p.CheckIn)%12 END AS VARCHAR(2)),2)+':'+
                RIGHT('0'+CAST(DATEPART(MINUTE,p.CheckIn) AS VARCHAR(2)),2)+' '+
                IIF(DATEPART(HOUR,p.CheckIn)>=12,'PM','AM') +
                CASE WHEN p.CheckInSource IS NOT NULL AND p.CheckInSource <> '' THEN ' [' + p.CheckInSource + ']' ELSE '' END
            END AS CheckIn,

            CASE WHEN p.CheckOut IS NULL THEN NULL ELSE
                RIGHT('0'+CAST(CASE WHEN DATEPART(HOUR,p.CheckOut)%12=0 THEN 12
                                    ELSE DATEPART(HOUR,p.CheckOut)%12 END AS VARCHAR(2)),2)+':'+
                RIGHT('0'+CAST(DATEPART(MINUTE,p.CheckOut) AS VARCHAR(2)),2)+' '+
                IIF(DATEPART(HOUR,p.CheckOut)>=12,'PM','AM') +
                CASE WHEN p.CheckOutSource IS NOT NULL AND p.CheckOutSource <> '' THEN ' [' + p.CheckOutSource + ']' ELSE '' END
            END AS CheckOut,

            es.[Status],
            es.LateByMinutes,
            (SELECT TOP 1 Photo    FROM HRM_EmployeePhoto ep WHERE ep.EmployeeID=ae.EmployeeId) AS Photo,
            (SELECT TOP 1 ImgType FROM HRM_EmployeePhoto ep WHERE ep.EmployeeID=ae.EmployeeId) AS ImgType,
            es.StatusOrder,
            p.LatestPunchDT,
            p.FirstPunchDT,
            ae.ShiftEndTime
        FROM #ActiveEmployees         ae
        INNER JOIN #EmployeeStatus     es  ON es.EmployeeId      = ae.EmployeeId
        LEFT  JOIN HRM_Employee        e   ON e.EmployeeId       = ae.EmployeeId
        LEFT  JOIN HRM_EmployeeOfficialInfo oi ON oi.EmployeeId = ae.EmployeeId
        LEFT  JOIN HRM_Def_Designation d   ON d.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
        LEFT  JOIN #Punch              p   ON p.EmployeeId       = ae.EmployeeId
        WHERE @Search = ''
           OR ae.EmployeeId LIKE '%'+@Search+'%'
           OR ISNULL(NULLIF(LTRIM(RTRIM(
                ISNULL(e.FirstName,'')+' '+ISNULL(e.LastName,'')
              )),''), ae.EmployeeId) LIKE '%'+@Search+'%'
    ),
    PagedRanked AS (
        SELECT *,
            ROW_NUMBER() OVER (
                ORDER BY
                    CASE WHEN StatusOrder IN (1, 2) THEN 0 ELSE StatusOrder END ASC,
                    ISNULL(LatestPunchDT, '1900-01-01') DESC,
                    [Name] ASC
            ) AS RowNum,
            COUNT(*) OVER () AS TotalCount
        FROM EmpData
    )
    INSERT INTO #PagedRows (
        RowNum, TotalCount, EmployeeId, [Name], Designation,
        CheckIn, CheckOut, [Status], LateByMinutes, Photo, ImgType,
        FirstPunchDT, ShiftEndTime
    )
    SELECT
        RowNum, TotalCount, EmployeeId, [Name], Designation,
        CheckIn, CheckOut, [Status], LateByMinutes, Photo, ImgType,
        FirstPunchDT, ShiftEndTime
    FROM PagedRanked
    WHERE RowNum BETWEEN (@Page - 1) * @PageSize + 1 AND @Page * @PageSize;

    SELECT
        pr.EmployeeId,
        pr.[Name],
        pr.Designation,
        ISNULL(pr.CheckIn, '') AS CheckIn,
        ISNULL(pr.CheckOut, '') AS CheckOut,
        pr.[Status],
        ISNULL(m.MovementText, '') AS Movement,
        ISNULL(r.RemarksText, '')  AS Remarks,
        pr.LateByMinutes,
        pr.Photo,
        ISNULL(pr.ImgType, '') AS ImgType,
        pr.RowNum,
        pr.TotalCount,
        @FromDate AS DataDate
    FROM #PagedRows pr
    OUTER APPLY (
        SELECT STUFF((
            SELECT ', ' +
                RIGHT('0' + CAST(
                    CASE WHEN DATEPART(HOUR, CONVERT(TIME, x.PunchTime)) % 12 = 0
                         THEN 12
                         ELSE DATEPART(HOUR, CONVERT(TIME, x.PunchTime)) % 12
                    END AS VARCHAR(2)), 2) + ':' +
                RIGHT('0' + CAST(DATEPART(MINUTE, CONVERT(TIME, x.PunchTime)) AS VARCHAR(2)), 2) + ' ' +
                CASE WHEN DATEPART(HOUR, CONVERT(TIME, x.PunchTime)) >= 12 THEN 'PM' ELSE 'AM' END
                + CASE WHEN x.Source IS NOT NULL AND x.Source <> '' THEN ' [' + x.Source + ']' ELSE '' END
            FROM #AllPunches x
            WHERE x.EmployeeId = pr.EmployeeId
              AND pr.FirstPunchDT IS NOT NULL
              AND x.PunchTime > pr.FirstPunchDT
              AND (pr.ShiftEndTime IS NULL OR CONVERT(TIME(0), x.PunchTime) <= pr.ShiftEndTime)
            ORDER BY x.PunchTime ASC
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    ) m (MovementText)
    OUTER APPLY (
        SELECT STUFF((
            SELECT DISTINCT ', ' + x2.Remarks
            FROM #AllPunches x2
            WHERE x2.EmployeeId = pr.EmployeeId
              AND x2.Remarks IS NOT NULL
              AND x2.Remarks <> ''
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    ) r (RemarksText)
    ORDER BY pr.RowNum
    OPTION (RECOMPILE);

    DROP TABLE #AllPunches;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #EmpFirstLast;
    DROP TABLE #CheckOutCandidate;
    DROP TABLE #Punch;
    DROP TABLE #Leave;
    DROP TABLE #EmployeeStatus;
    DROP TABLE #PagedRows;
END;
GO