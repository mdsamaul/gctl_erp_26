SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_Attendance_Absent]
    @CompanyCode      VARCHAR(50)   = '',
    @BranchCode       VARCHAR(50)   = '',
    @BranchCodes      NVARCHAR(MAX) = '',  
    @DepartmentCode   VARCHAR(50)   = '',
    @DepartmentCodes  NVARCHAR(MAX) = '',  
    @EmployeeId       VARCHAR(50)   = '',
    @EmployeeIds      NVARCHAR(MAX) = '',  
    @FromDate         DATE          = NULL,
    @ReportType       NVARCHAR(20)  = 'Absent',
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
    SET @ReportType       = ISNULL(NULLIF(LTRIM(RTRIM(@ReportType)), ''), 'Absent');
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
               '' AS DepartmentName, '' AS [Status]
        WHERE 1 = 0;

        SELECT @FromDate AS DataDate, '' AS CompanyName, 'Absent' AS ReportType
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
        AbsentTime     TIME(0)
    );

    INSERT INTO #ActiveEmployees (
        EmployeeId, EmployeeName, Designation, DepartmentCode,
        DepartmentName, AbsentTime
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
        CASE WHEN s.AbsentTime IS NOT NULL AND s.AbsentTime <> '1900-01-01' THEN CONVERT(TIME(0), s.AbsentTime) ELSE NULL END
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


    CREATE TABLE #FirstPunch (
        EmployeeId VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY,
        FirstPunch TIME(0)     NOT NULL
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
    INSERT INTO #FirstPunch (EmployeeId, FirstPunch)
    SELECT EmployeeId, MIN(PunchTime)
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

    SELECT
        ROW_NUMBER() OVER (PARTITION BY ae.DepartmentName ORDER BY ae.EmployeeName) AS SN,
        ISNULL(ae.EmployeeId, '')     AS EmployeeId,
        ISNULL(ae.EmployeeName, '')   AS EmployeeName,
        ISNULL(ae.Designation, '')    AS Designation,
        ISNULL(ae.DepartmentName, '') AS DepartmentName,
        'Absent'                      AS [Status]
    FROM #ActiveEmployees ae
    LEFT JOIN #FirstPunch fp ON fp.EmployeeId = ae.EmployeeId
    LEFT JOIN #OnLeave    lv ON lv.EmployeeId = ae.EmployeeId
    WHERE lv.EmployeeId IS NULL  -- Exclude employees who are on approved leave
      AND (
            fp.EmployeeId IS NULL -- Never punched
            OR (ae.AbsentTime IS NOT NULL AND fp.FirstPunch >= ae.AbsentTime) -- Punched after absent cutoff
          )
    ORDER BY ae.DepartmentName, ae.EmployeeName
    OPTION (RECOMPILE);

    SELECT
        @FromDate AS DataDate,
        ISNULL((SELECT TOP 1 CompanyName FROM Core_Company WHERE (@CompanyCode <> '' AND CompanyCode = @CompanyCode)), 'Data Path') AS CompanyName,
        'Absent' AS ReportType;

    -- Cleanup
    DROP TABLE #BranchFilter;
    DROP TABLE #DeptFilter;
    DROP TABLE #EmpFilter;
    DROP TABLE #ActiveEmployees;
    DROP TABLE #FirstPunch;
    DROP TABLE #OnLeave;
END;
GO