SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =====================================================================
-- Master Procedure: [dbo].[GetLeaveReport100]
-- Purpose: Unified Single Stored Procedure for the entire Leave Module:
--          1. Leave Dashboard & Leave Summary Report (@ReportType = 'Dashboard')
--             - RS1: Summary Cards (TotalApplied, Approved, Canceled, Pending)
--             - RS2: Leave Types (Strict sequence: CL, SL, UL, ML, PL, MarL, HL, UmrL)
--             - RS3: Paged Employees with Granted, Availed, Balanced Days & Department/Branch/Company
--          2. Leave Detail & Application History Report (@ReportType = 'Report')
--             - Returns single detailed result set with HOD/HR status, date range, reason
-- =====================================================================

IF OBJECT_ID('[dbo].[usp_GetLeaveDashboard]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[usp_GetLeaveDashboard];
GO

CREATE OR ALTER PROCEDURE [dbo].[GetLeaveReport100]
    @DateFrom         DATETIME      = NULL,
    @DateTo           DATETIME      = NULL,
    @Company          VARCHAR(MAX)  = NULL,
    @CompanyCode      VARCHAR(50)   = NULL,
    @CompanyCodes     NVARCHAR(MAX) = NULL,
    @Branch           VARCHAR(MAX)  = NULL,
    @BranchCode       VARCHAR(50)   = NULL,
    @BranchCodes      NVARCHAR(MAX) = NULL,
    @Department       VARCHAR(MAX)  = NULL,
    @DepartmentCode   VARCHAR(50)   = NULL,
    @DepartmentCodes  NVARCHAR(MAX) = NULL,
    @Employee         VARCHAR(MAX)  = NULL,
    @EmployeeId       VARCHAR(50)   = NULL,
    @EmployeeIds      NVARCHAR(MAX) = NULL,
    @LeaveFormat      VARCHAR(MAX)  = NULL,
    @LeaveStatus      VARCHAR(MAX)  = NULL,
    @ReportFormat     VARCHAR(50)   = NULL,
    @Year             INT           = NULL,
    @Page             INT           = 1,
    @PageSize         INT           = 10,
    @Search           VARCHAR(100)  = NULL,
    @LoginEmployeeId  NVARCHAR(50)  = NULL,
    @AccessCodeId     NVARCHAR(50)  = NULL,
    @ReportType       VARCHAR(50)   = 'Report' -- 'Report' or 'Dashboard'
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    -- ════════════════════════════════════════════════════════════════
    -- 0. Input Sanitization & Normalization
    -- ════════════════════════════════════════════════════════════════
    DECLARE @CompanyStr    NVARCHAR(MAX) = COALESCE(NULLIF(LTRIM(RTRIM(@CompanyCodes)), ''), NULLIF(LTRIM(RTRIM(@Company)), ''), NULLIF(LTRIM(RTRIM(@CompanyCode)), ''));
    DECLARE @BranchStr     NVARCHAR(MAX) = COALESCE(NULLIF(LTRIM(RTRIM(@BranchCodes)), ''), NULLIF(LTRIM(RTRIM(@Branch)), ''), NULLIF(LTRIM(RTRIM(@BranchCode)), ''));
    DECLARE @DeptStr       NVARCHAR(MAX) = COALESCE(NULLIF(LTRIM(RTRIM(@DepartmentCodes)), ''), NULLIF(LTRIM(RTRIM(@Department)), ''), NULLIF(LTRIM(RTRIM(@DepartmentCode)), ''));
    DECLARE @EmpStr        NVARCHAR(MAX) = COALESCE(NULLIF(LTRIM(RTRIM(@EmployeeIds)), ''), NULLIF(LTRIM(RTRIM(@Employee)), ''), NULLIF(LTRIM(RTRIM(@EmployeeId)), ''));

    SET @LeaveFormat     = NULLIF(LTRIM(RTRIM(@LeaveFormat)), '');
    SET @LeaveStatus     = NULLIF(LTRIM(RTRIM(@LeaveStatus)), '');
    SET @ReportFormat    = NULLIF(LTRIM(RTRIM(@ReportFormat)), '');
    SET @Search          = NULLIF(LTRIM(RTRIM(@Search)), '');
    SET @LoginEmployeeId = NULLIF(LTRIM(RTRIM(@LoginEmployeeId)), '');
    SET @AccessCodeId    = NULLIF(LTRIM(RTRIM(@AccessCodeId)), '');
    SET @ReportType      = ISNULL(NULLIF(LTRIM(RTRIM(@ReportType)), ''), 'Report');

    IF @ReportFormat = 'Dashboard'
        SET @ReportType = 'Dashboard';

    -- Year and Date Normalization
    IF @Year IS NULL OR @Year <= 1900
    BEGIN
        IF @DateFrom IS NOT NULL AND @DateFrom <> '1900-01-01'
            SET @Year = YEAR(@DateFrom);
        ELSE
            SET @Year = YEAR(GETDATE());
    END

    IF @ReportType = 'Dashboard'
    BEGIN
        IF @DateFrom IS NULL OR @DateFrom = '1900-01-01'
            SET @DateFrom = DATEFROMPARTS(@Year, 1, 1);
        IF @DateTo IS NULL OR @DateTo = '1900-01-01'
            SET @DateTo = DATEFROMPARTS(@Year, 12, 31);
    END
    ELSE
    BEGIN
        IF @DateFrom = '1900-01-01' SET @DateFrom = NULL;
        IF @DateTo   = '1900-01-01' SET @DateTo   = NULL;
    END

    IF @Page IS NULL OR @Page < 1 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize < 1 SET @PageSize = 10;

    -- ════════════════════════════════════════════════════════════════
    -- 1. Table Variables for Filters (No Tempdb Catalog Locks)
    -- ════════════════════════════════════════════════════════════════
    DECLARE @CompanyFilter TABLE (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    DECLARE @BranchFilter  TABLE (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    DECLARE @DeptFilter    TABLE (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    DECLARE @EmpFilter     TABLE (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    DECLARE @FormatFilter  TABLE (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);
    DECLARE @StatusFilter  TABLE (Code VARCHAR(50) COLLATE DATABASE_DEFAULT PRIMARY KEY);

    IF @CompanyStr IS NOT NULL
    BEGIN
        DECLARE @CompanyXml XML = CAST('<r><v>' + REPLACE(@CompanyStr, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO @CompanyFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @CompanyXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @BranchStr IS NOT NULL
    BEGIN
        DECLARE @BranchXml XML = CAST('<r><v>' + REPLACE(@BranchStr, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO @BranchFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @BranchXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @DeptStr IS NOT NULL
    BEGIN
        DECLARE @DeptXml XML = CAST('<r><v>' + REPLACE(@DeptStr, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO @DeptFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @DeptXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @EmpStr IS NOT NULL
    BEGIN
        DECLARE @EmpXml XML = CAST('<r><v>' + REPLACE(@EmpStr, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO @EmpFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @EmpXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @LeaveFormat IS NOT NULL
    BEGIN
        DECLARE @FormatXml XML = CAST('<r><v>' + REPLACE(@LeaveFormat, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO @FormatFilter (Code)
        SELECT DISTINCT LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)')))
        FROM   @FormatXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    IF @LeaveStatus IS NOT NULL
    BEGIN
        DECLARE @StatusXml XML = CAST('<r><v>' + REPLACE(@LeaveStatus, ',', '</v><v>') + '</v></r>' AS XML);
        INSERT INTO @StatusFilter (Code)
        SELECT DISTINCT LOWER(LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))))
        FROM   @StatusXml.nodes('/r/v') AS x(v)
        WHERE  LTRIM(RTRIM(x.v.value('.', 'VARCHAR(50)'))) <> '';
    END

    -- ════════════════════════════════════════════════════════════════
    -- 2. Access Scope Flags
    -- ════════════════════════════════════════════════════════════════
    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId = '0001'           THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003') THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'           THEN 1 ELSE 0 END;

    IF @AccessCodeId IS NULL OR @LoginEmployeeId IS NULL
        SET @IsAdmin = 1;

    -- ════════════════════════════════════════════════════════════════
    -- 3. Filtered Active Employees
    -- ════════════════════════════════════════════════════════════════
    CREATE TABLE #ActiveEmployees (
        EmployeeId       VARCHAR(50)   COLLATE DATABASE_DEFAULT PRIMARY KEY CLUSTERED,
        EmployeeName     NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        Designation      NVARCHAR(100) COLLATE DATABASE_DEFAULT,
        DepartmentCode   VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        DepartmentName   NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        CompanyCode      VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        CompanyName      NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        BranchCode       VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        BranchName       NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        JoiningDate      DATE,
        AnniversaryStart DATE,
        AnniversaryEnd   DATE,
        RemainingMonths  INT
    );

    INSERT INTO #ActiveEmployees
    SELECT
        e.EmployeeId,
        ISNULL(NULLIF(LTRIM(RTRIM(
            ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,'')
        )),''), e.EmployeeId),
        CASE 
            WHEN d.DesignationName IS NOT NULL AND d.DesignationName <> '' THEN d.DesignationName
            WHEN oi.DesignationCode IS NOT NULL AND oi.DesignationCode <> '' THEN oi.DesignationCode
            ELSE '—'
        END,
        ISNULL(oi.DepartmentCode, ''),
        CASE 
            WHEN dep.DepartmentName IS NOT NULL AND dep.DepartmentName <> '' THEN dep.DepartmentName
            WHEN oi.DepartmentCode IS NOT NULL AND oi.DepartmentCode <> '' THEN oi.DepartmentCode
            ELSE '—'
        END,
        ISNULL(oi.CompanyCode, ''),
        ISNULL(comp.CompanyName, 'Data Path'),
        ISNULL(oi.BranchCode, ''),
        ISNULL(br.BranchName, 'Main Branch'),
        CASE WHEN oi.JoiningDate IS NOT NULL AND oi.JoiningDate <> '1900-01-01' THEN CONVERT(DATE, oi.JoiningDate) ELSE NULL END,
        CASE WHEN oi.JoiningDate IS NOT NULL AND oi.JoiningDate <> '1900-01-01'
             THEN DATEADD(YEAR, @Year - YEAR(CONVERT(DATE, oi.JoiningDate)), CONVERT(DATE, oi.JoiningDate))
             ELSE DATEFROMPARTS(@Year, 1, 1)
        END AS AnniversaryStart,
        CASE WHEN oi.JoiningDate IS NOT NULL AND oi.JoiningDate <> '1900-01-01'
             THEN DATEADD(DAY, -1, DATEADD(YEAR, @Year - YEAR(CONVERT(DATE, oi.JoiningDate)) + 1, CONVERT(DATE, oi.JoiningDate)))
             ELSE DATEFROMPARTS(@Year, 12, 31)
        END AS AnniversaryEnd,
        12 AS RemainingMonths
    FROM HRM_Employee              e    WITH (NOLOCK)
    JOIN  HRM_EmployeeOfficialInfo oi   WITH (NOLOCK) ON oi.EmployeeId      = e.EmployeeId
    LEFT JOIN HRM_Def_Department   dep  WITH (NOLOCK) ON dep.DepartmentCode = oi.DepartmentCode AND oi.DepartmentCode <> ''
    LEFT JOIN HRM_Def_Designation  d    WITH (NOLOCK) ON d.DesignationCode  = oi.DesignationCode AND oi.DesignationCode <> ''
    LEFT JOIN Core_Company         comp WITH (NOLOCK) ON comp.CompanyCode   = oi.CompanyCode AND oi.CompanyCode <> ''
    LEFT JOIN Core_Branch          br   WITH (NOLOCK) ON br.BranchCode       = oi.BranchCode AND oi.BranchCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (NOT EXISTS (SELECT 1 FROM @CompanyFilter) OR (oi.CompanyCode <> '' AND oi.CompanyCode IN (SELECT Code FROM @CompanyFilter)))
      AND (NOT EXISTS (SELECT 1 FROM @BranchFilter)  OR (oi.BranchCode <> '' AND oi.BranchCode IN (SELECT Code FROM @BranchFilter)))
      AND (NOT EXISTS (SELECT 1 FROM @DeptFilter)    OR (oi.DepartmentCode <> '' AND oi.DepartmentCode IN (SELECT Code FROM @DeptFilter)))
      AND (NOT EXISTS (SELECT 1 FROM @EmpFilter)     OR (e.EmployeeId <> '' AND e.EmployeeId IN (SELECT Code FROM @EmpFilter)))
      AND (
            @IsAdmin = 1
            OR (
                 @IsTeamLeader = 1
                 AND (
                       e.EmployeeId   = @LoginEmployeeId
                    OR (oi.ReportingTo <> '' AND oi.ReportingTo = @LoginEmployeeId)
                    OR (oi.HOD         <> '' AND oi.HOD         = @LoginEmployeeId)
                 )
               )
            OR (@IsUser = 1 AND e.EmployeeId = @LoginEmployeeId)
          )
      AND (
            @Search IS NULL
            OR e.EmployeeId LIKE '%' + @Search + '%'
            OR ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))),''), e.EmployeeId) LIKE '%' + @Search + '%'
      );

    -- ════════════════════════════════════════════════════════════════
    -- 4. Unified Filtered Leaves Table
    -- ════════════════════════════════════════════════════════════════
    CREATE TABLE #FilteredLeaves (
        LeaveAppEntryCode   VARCHAR(50)   COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY CLUSTERED,
        LeaveAppEntryId     VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        CompanyCode         VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        CompanyName         NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        DepartmentName      NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        DepartmentCode      VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        BranchCode          VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        EmployeeId          VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        EmployeeFirstName   NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        DesignationName     NVARCHAR(100) COLLATE DATABASE_DEFAULT,
        LeaveTypeId         VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        RawLeaveTypeCode    VARCHAR(50)   COLLATE DATABASE_DEFAULT,
        StartDate           DATETIME,
        EndDate             DATETIME,
        NoOfDay             DECIMAL(10,2),
        ModifyDate          DATETIME,
        ConfirmationRemarks NVARCHAR(MAX) COLLATE DATABASE_DEFAULT,
        HODApprovalStatus   NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        HRApprovalRemarks   NVARCHAR(MAX) COLLATE DATABASE_DEFAULT,
        SickLeaveFilePath   NVARCHAR(MAX) COLLATE DATABASE_DEFAULT,
        ApplyLeaveFormat    NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        HODFirstName        NVARCHAR(100) COLLATE DATABASE_DEFAULT,
        SupervisorFirstName NVARCHAR(100) COLLATE DATABASE_DEFAULT,
        Reason              NVARCHAR(MAX) COLLATE DATABASE_DEFAULT,
        ShortLeaveFrom      DATETIME,
        ShortLeaveTo        DATETIME,
        ShortLeaveTime      TIME(0),
        IsApproved          NVARCHAR(50)  COLLATE DATABASE_DEFAULT,
        FirstOrSecondHalf   VARCHAR(20)   COLLATE DATABASE_DEFAULT,
        NormalizedStatus    VARCHAR(20)   COLLATE DATABASE_DEFAULT
    );

    INSERT INTO #FilteredLeaves (
        LeaveAppEntryCode, LeaveAppEntryId, CompanyCode, CompanyName,
        DepartmentName, DepartmentCode, BranchCode, EmployeeId,
        EmployeeFirstName, DesignationName, LeaveTypeId, RawLeaveTypeCode,
        StartDate, EndDate, NoOfDay, ModifyDate, ConfirmationRemarks,
        HODApprovalStatus, HRApprovalRemarks, SickLeaveFilePath,
        ApplyLeaveFormat, HODFirstName, SupervisorFirstName, Reason,
        ShortLeaveFrom, ShortLeaveTo, ShortLeaveTime, IsApproved,
        FirstOrSecondHalf, NormalizedStatus
    )
    SELECT 
        CAST(l.AutoId AS VARCHAR(50)) AS LeaveAppEntryCode,
        l.LeaveAppEntryId,
        ae.CompanyCode,
        ae.CompanyName,
        ae.DepartmentName,
        ae.DepartmentCode,
        ae.BranchCode,
        ae.EmployeeId,
        ae.EmployeeName AS EmployeeFirstName,
        ae.Designation AS DesignationName,
        ISNULL(type.ShortName, ISNULL(l.LeaveTypeId, '')) AS LeaveTypeId,
        l.LeaveTypeId AS RawLeaveTypeCode,
        l.StartDate,
        l.EndDate,
        CAST(ISNULL(l.NoOfDay, 0) AS DECIMAL(10,2)) AS NoOfDay,
        l.ModifyDate,
        ISNULL(l.ConfirmationRemarks, '') AS ConfirmationRemarks,
        ISNULL(l.HodapprovalStatus, '') AS HODApprovalStatus,
        ISNULL(l.HrapprovalRemarks, '') AS HRApprovalRemarks,
        ISNULL(l.SickLeaveFilePath, '') AS SickLeaveFilePath,
        ISNULL(l.ApplyLeaveFormat, '') AS ApplyLeaveFormat,
        ISNULL(hod.FirstName, '') AS HODFirstName,
        ISNULL(sup.FirstName, '') AS SupervisorFirstName,
        ISNULL(l.Reason, '') AS Reason,
        l.ShortLeaveFrom,
        l.ShortLeaveTo,
        CASE 
            WHEN l.ShortLeaveTime IS NOT NULL AND l.ShortLeaveTime <> '1900-01-01' THEN CONVERT(TIME(0), l.ShortLeaveTime)
            ELSE NULL 
        END AS ShortLeaveTime,
        ISNULL(l.IsApproved, '') AS IsApproved,
        CASE
            WHEN l.FirstOrSecondHalf = '1' THEN 'First Half'
            WHEN l.FirstOrSecondHalf = '2' THEN 'Second Half'
            ELSE NULL
        END AS FirstOrSecondHalf,
        CASE 
            WHEN LOWER(ISNULL(l.IsApproved,'')) IN ('y','approved')
              OR LOWER(ISNULL(l.HodapprovalStatus,'')) IN ('y','approved')
              OR LOWER(ISNULL(l.HrapprovalStatus,'')) IN ('y','approved')
                THEN 'Approved'
            WHEN LOWER(ISNULL(l.IsApproved,'')) IN ('n','canceled','rejected')
              OR LOWER(ISNULL(l.HodapprovalStatus,'')) IN ('n','canceled','rejected')
              OR LOWER(ISNULL(l.HrapprovalStatus,'')) IN ('n','canceled','rejected')
                THEN 'Canceled'
            ELSE 'Pending'
        END AS NormalizedStatus
    FROM HRM_LeaveApplicationEntry l      WITH (NOLOCK)
    INNER JOIN #ActiveEmployees ae        ON ae.EmployeeId = l.EmployeeId
    LEFT  JOIN HRM_Employee hod           WITH (NOLOCK) ON l.Hod = hod.EmployeeId AND l.Hod <> ''
    LEFT  JOIN HRM_Employee sup           WITH (NOLOCK) ON l.BossEmpAutoId = sup.EmployeeId AND l.BossEmpAutoId <> ''
    LEFT  JOIN HRM_ATD_LeaveType type     WITH (NOLOCK) ON l.LeaveTypeId = type.LeaveTypeCode
    WHERE (
        @ReportType = 'Dashboard'
        AND l.StartDate <= @DateTo
        AND l.EndDate   >= @DateFrom
    ) OR (
        @ReportType = 'Report'
        AND (@DateFrom IS NULL OR l.StartDate >= @DateFrom)
        AND (@DateTo   IS NULL OR l.EndDate   <= @DateTo)
    )
    AND (NOT EXISTS (SELECT 1 FROM @FormatFilter) OR l.ApplyLeaveFormat IN (SELECT Code FROM @FormatFilter));

    -- ════════════════════════════════════════════════════════════════
    -- 5. Strict Sequence Leave Types Definition
    -- ════════════════════════════════════════════════════════════════
    CREATE TABLE #LeaveTypes (
        LeaveTypeCode VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY CLUSTERED,
        ShortName     VARCHAR(20) COLLATE DATABASE_DEFAULT,
        NoOfDay       DECIMAL(10,2),
        SortOrder     INT NOT NULL
    );

    INSERT INTO #LeaveTypes (LeaveTypeCode, ShortName, NoOfDay, SortOrder)
    SELECT
        LeaveTypeCode,
        ISNULL(NULLIF(LTRIM(RTRIM(ShortName)),''), LeaveTypeCode),
        ISNULL(NoOfDay, 0),
        CASE
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'CL'   OR LeaveTypeCode = '1' THEN 1
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'SL'   OR LeaveTypeCode = '2' THEN 2
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'UL'   OR LeaveTypeCode = '3' THEN 3
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'ML'   OR LeaveTypeCode = '4' THEN 4
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'PL'   OR LeaveTypeCode = '5' THEN 5
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'MARL' OR LeaveTypeCode = '6' THEN 6
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'HL'   OR LeaveTypeCode = '7' THEN 7
            WHEN UPPER(LTRIM(RTRIM(ISNULL(ShortName, '')))) = 'UMRL' OR LeaveTypeCode = '8' THEN 8
            ELSE 99
        END
    FROM HRM_ATD_LeaveType WITH (NOLOCK);

    -- ════════════════════════════════════════════════════════════════
    -- OUTPUT DISPATCHER: [DASHBOARD] vs [REPORT]
    -- ════════════════════════════════════════════════════════════════

    -- ─────────────────────────────────────────────────────────────────
    -- [BRANCH A] Dashboard & Leave Summary Report Mode: Returns 3 Result Sets
    -- ─────────────────────────────────────────────────────────────────
    IF @ReportType = 'Dashboard'
    BEGIN
        -- [RS1] Summary Cards
        SELECT
            COUNT(*) AS TotalApplied,
            ISNULL(SUM(CASE WHEN NormalizedStatus = 'Approved' THEN 1 ELSE 0 END), 0) AS Approved,
            ISNULL(SUM(CASE WHEN NormalizedStatus = 'Canceled' THEN 1 ELSE 0 END), 0) AS Canceled,
            ISNULL(SUM(CASE WHEN NormalizedStatus = 'Pending'  THEN 1 ELSE 0 END), 0) AS Pending
        FROM #FilteredLeaves;

        -- [RS2] Leave Types
        SELECT LeaveTypeCode, ShortName, NoOfDay
        FROM #LeaveTypes
        ORDER BY SortOrder, ShortName;

        -- [RS3] Paged Employees & Leave Balances
        CREATE TABLE #PagedEmployees (
            EmployeeId       VARCHAR(50)   COLLATE DATABASE_DEFAULT PRIMARY KEY CLUSTERED,
            EmployeeName     NVARCHAR(200) COLLATE DATABASE_DEFAULT,
            Designation      NVARCHAR(100) COLLATE DATABASE_DEFAULT,
            DepartmentName   NVARCHAR(200) COLLATE DATABASE_DEFAULT,
            BranchName       NVARCHAR(200) COLLATE DATABASE_DEFAULT,
            CompanyName      NVARCHAR(200) COLLATE DATABASE_DEFAULT,
            JoiningDate      DATE,
            AnniversaryStart DATE,
            AnniversaryEnd   DATE,
            RemainingMonths  INT,
            RowNum           INT,
            TotalCount       INT
        );

        ;WITH EmpPaged AS (
            SELECT
                EmployeeId, EmployeeName, Designation, DepartmentName, BranchName, CompanyName, JoiningDate,
                AnniversaryStart, AnniversaryEnd, RemainingMonths,
                ROW_NUMBER() OVER (ORDER BY EmployeeName ASC) AS RowNum,
                COUNT(*)     OVER ()                          AS TotalCount
            FROM #ActiveEmployees
        )
        INSERT INTO #PagedEmployees
        SELECT *
        FROM EmpPaged
        WHERE RowNum BETWEEN (@Page - 1) * @PageSize + 1 AND @Page * @PageSize;

        -- Precalculate Availed Days for Paged Employees Only
        CREATE TABLE #Availed (
            EmployeeId    VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL,
            LeaveTypeCode VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL,
            AvailedDays   DECIMAL(10,2),
            PRIMARY KEY CLUSTERED (EmployeeId, LeaveTypeCode)
        );

        INSERT INTO #Availed
        SELECT
            la.EmployeeId,
            lt.LeaveTypeCode,
            SUM(
                CASE
                    WHEN ISNULL(la.NoOfDay, 0) > 0 THEN la.NoOfDay
                    ELSE DATEDIFF(DAY,
                        CASE WHEN la.StartDate < @DateFrom THEN @DateFrom ELSE la.StartDate END,
                        CASE WHEN la.EndDate   > @DateTo   THEN @DateTo   ELSE la.EndDate   END
                    ) + 1
                END
            ) AS AvailedDays
        FROM #PagedEmployees emp
        JOIN #FilteredLeaves la ON la.EmployeeId = emp.EmployeeId
        JOIN #LeaveTypes lt     ON lt.LeaveTypeCode = la.RawLeaveTypeCode
        WHERE la.NormalizedStatus = 'Approved'
        GROUP BY la.EmployeeId, lt.LeaveTypeCode;

        -- Grid Dataset Output
        SELECT
            p.EmployeeId,
            p.EmployeeName                              AS [Name],
            p.Designation,
            p.DepartmentName,
            p.BranchName,
            p.CompanyName,
            ISNULL(CONVERT(VARCHAR(12), p.JoiningDate, 103), '')      AS JoiningDate,
            ISNULL(CONVERT(VARCHAR(12), p.AnniversaryStart, 103), '') AS AnniversaryStart,
            ISNULL(CONVERT(VARCHAR(12), p.AnniversaryEnd, 103), '')   AS AnniversaryEnd,
            lt.LeaveTypeCode,
            lt.ShortName                                AS LeaveShortName,
            CONVERT(DECIMAL(10,2),
                (lt.NoOfDay / 12.0) * p.RemainingMonths
            )                                           AS GrantedDays,
            ISNULL(av.AvailedDays, 0)                  AS AvailedDays,
            CONVERT(DECIMAL(10,2),
                ((lt.NoOfDay / 12.0) * p.RemainingMonths) - ISNULL(av.AvailedDays, 0)
            )                                           AS BalancedDays,
            p.RowNum,
            p.TotalCount
        FROM #PagedEmployees p
        CROSS JOIN #LeaveTypes lt
        LEFT JOIN  #Availed    av ON av.EmployeeId    = p.EmployeeId
                                  AND av.LeaveTypeCode = lt.LeaveTypeCode
        ORDER BY p.RowNum, lt.SortOrder, lt.ShortName
        OPTION (RECOMPILE);

        DROP TABLE #PagedEmployees;
        DROP TABLE #Availed;
    END

    -- ─────────────────────────────────────────────────────────────────
    -- [BRANCH B] Report Mode: Returns Single Detailed Result Set for Leave Report
    -- ─────────────────────────────────────────────────────────────────
    ELSE
    BEGIN
        SELECT 
            ISNULL(fl.CompanyCode, '') AS CompanyCode,
            ISNULL(fl.CompanyName, 'Data Path') AS CompanyName,
            ISNULL(fl.DepartmentName, '') AS DepartmentName,
            ISNULL(fl.LeaveAppEntryCode, '') AS LeaveAppEntryCode,
            ISNULL(fl.LeaveAppEntryId, '') AS LeaveAppEntryId,
            ISNULL(fl.EmployeeId, '') AS EmployeeId,
            ISNULL(fl.EmployeeFirstName, '') AS EmployeeFirstName,
            ISNULL(fl.DesignationName, '') AS DesignationName,
            ISNULL(fl.LeaveTypeId, '') AS LeaveTypeId,
            fl.StartDate,
            fl.EndDate,
            ISNULL(fl.NoOfDay, 0) AS NoOfDay,
            fl.ModifyDate,
            ISNULL(fl.ConfirmationRemarks, '') AS ConfirmationRemarks,
            ISNULL(fl.HODApprovalStatus, '') AS HODApprovalStatus,
            ISNULL(fl.HRApprovalRemarks, '') AS HRApprovalRemarks,
            ISNULL(fl.SickLeaveFilePath, '') AS SickLeaveFilePath,
            fl.NormalizedStatus AS HRApprovalStatus,
            ISNULL(fl.ApplyLeaveFormat, '') AS ApplyLeaveFormat,
            ISNULL(fl.HODFirstName, '') AS HODFirstName,
            ISNULL(fl.SupervisorFirstName, '') AS SupervisorFirstName,
            ISNULL(fl.Reason, '') AS Reason,
            fl.ShortLeaveFrom,
            fl.ShortLeaveTo,
            fl.ShortLeaveTime,
            ISNULL(fl.IsApproved, '') AS IsApproved,
            ISNULL(fl.FirstOrSecondHalf, '') AS FirstOrSecondHalf,
            COUNT(fl.LeaveAppEntryCode) OVER() AS TotalLeaves,
            ISNULL(SUM(CASE WHEN fl.NormalizedStatus = 'Pending'  THEN 1 ELSE 0 END) OVER(), 0) AS PendingLeaves,
            ISNULL(SUM(CASE WHEN fl.NormalizedStatus = 'Approved' THEN 1 ELSE 0 END) OVER(), 0) AS ApprovedLeaves,
            ISNULL(SUM(CASE WHEN fl.NormalizedStatus = 'Canceled' THEN 1 ELSE 0 END) OVER(), 0) AS RejectedLeaves
        FROM #FilteredLeaves fl
        WHERE (
            NOT EXISTS (SELECT 1 FROM @StatusFilter)
            OR LOWER(fl.NormalizedStatus) IN (SELECT Code FROM @StatusFilter)
            OR (fl.NormalizedStatus = 'Canceled' AND 'rejected' IN (SELECT Code FROM @StatusFilter))
        )
        ORDER BY fl.CompanyCode, fl.DepartmentName, fl.StartDate DESC
        OPTION (RECOMPILE);
    END

    -- Cleanup
    DROP TABLE #ActiveEmployees;
    DROP TABLE #FilteredLeaves;
    DROP TABLE #LeaveTypes;
END;
GO
