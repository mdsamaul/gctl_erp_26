SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =====================================================================
-- Procedure: [dbo].[usp_GetLeaveDashboard]
-- Purpose: Returns leave dashboard summary cards, sorted leave types, and
--          paged employee leave balances.
-- Sequence: CL, SL, UL, ML, PL, MarL, HL, UmrL.
-- Fully handles database tables storing '' (empty string) instead of NULL.
-- =====================================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetLeaveDashboard]
    @CompanyCode     VARCHAR(50)  = NULL,
    @BranchCode      VARCHAR(50)  = NULL,
    @DepartmentCode  VARCHAR(50)  = NULL,
    @Year            INT          = NULL,
    @Page            INT          = 1,
    @PageSize        INT          = 10,
    @Search          VARCHAR(100) = NULL,
    @EmployeeId      VARCHAR(50)  = NULL,
    @LoginEmployeeId NVARCHAR(50) = NULL,   -- access control
    @AccessCodeId    NVARCHAR(50) = NULL    -- access control
AS
BEGIN
    SET NOCOUNT ON;

    -- ════════════════════════════════════════════════════════
    -- 0. Parameters Normalization (Convert '' to NULL)
    -- ════════════════════════════════════════════════════════
    SET @CompanyCode      = NULLIF(LTRIM(RTRIM(@CompanyCode)), '');
    SET @BranchCode       = NULLIF(LTRIM(RTRIM(@BranchCode)), '');
    SET @DepartmentCode   = NULLIF(LTRIM(RTRIM(@DepartmentCode)), '');
    SET @Search           = NULLIF(LTRIM(RTRIM(@Search)), '');
    SET @EmployeeId       = NULLIF(LTRIM(RTRIM(@EmployeeId)), '');
    SET @LoginEmployeeId  = NULLIF(LTRIM(RTRIM(@LoginEmployeeId)), '');
    SET @AccessCodeId     = NULLIF(LTRIM(RTRIM(@AccessCodeId)), '');

    IF @Year IS NULL OR @Year <= 1900
        SET @Year = YEAR(GETDATE());

    IF @Page IS NULL OR @Page < 1
        SET @Page = 1;

    IF @PageSize IS NULL OR @PageSize < 1
        SET @PageSize = 10;

    -- ════════════════════════════════════════════════════════
    -- 1. Access Scope Flags
    -- ════════════════════════════════════════════════════════
    DECLARE @IsAdmin      BIT = CASE WHEN @AccessCodeId = '0001'           THEN 1 ELSE 0 END;
    DECLARE @IsTeamLeader BIT = CASE WHEN @AccessCodeId IN ('0002','0003') THEN 1 ELSE 0 END;
    DECLARE @IsUser       BIT = CASE WHEN @AccessCodeId = '0005'           THEN 1 ELSE 0 END;

    IF @AccessCodeId IS NULL OR @LoginEmployeeId IS NULL
        SET @IsAdmin = 1;

    -- ════════════════════════════════════════════════════════
    -- 2. Active Employees (filtered set) + anniversary window
    -- ════════════════════════════════════════════════════════
    CREATE TABLE #Employees (
        EmployeeId       VARCHAR(50)   COLLATE DATABASE_DEFAULT NOT NULL,
        EmployeeName     NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        Designation      NVARCHAR(100) COLLATE DATABASE_DEFAULT,
        JoiningDate      DATE,
        AnniversaryStart DATE,
        AnniversaryEnd   DATE,
        RemainingMonths  INT
    );

    INSERT INTO #Employees
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
    FROM HRM_Employee              e
    JOIN  HRM_EmployeeOfficialInfo oi ON oi.EmployeeId     = e.EmployeeId
    LEFT JOIN HRM_Def_Designation  d  ON d.DesignationCode = oi.DesignationCode AND oi.DesignationCode <> ''
    WHERE (oi.EmployeeStatus = '01' OR oi.EmployeeStatus = 'Active' OR oi.EmployeeStatus = '1' OR oi.EmployeeStatus = '' OR oi.EmployeeStatus IS NULL)
      AND (oi.EmployeeId IS NOT NULL AND oi.EmployeeId <> '')
      AND (@CompanyCode    IS NULL OR oi.CompanyCode    = @CompanyCode)
      AND (@BranchCode     IS NULL OR oi.BranchCode     = @BranchCode)
      AND (@DepartmentCode IS NULL OR oi.DepartmentCode = @DepartmentCode)
      AND (@Year >= YEAR(CONVERT(DATE, oi.JoiningDate)) OR oi.JoiningDate IS NULL OR oi.JoiningDate = '1900-01-01')
      AND (@EmployeeId IS NULL OR e.EmployeeId = @EmployeeId)
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
            OR ISNULL(NULLIF(LTRIM(RTRIM(
                ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))),''), e.EmployeeId) LIKE '%' + @Search + '%'
      );

    CREATE UNIQUE CLUSTERED INDEX IX_Employees_EmployeeId ON #Employees (EmployeeId);

    -- ════════════════════════════════════════════════════════
    -- 3. Leave Types (Explicitly ordered: CL, SL, UL, ML, PL, MarL, HL, UmrL)
    -- ════════════════════════════════════════════════════════
    CREATE TABLE #LeaveTypes (
        LeaveTypeCode VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL,
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
    FROM HRM_ATD_LeaveType;

    CREATE CLUSTERED INDEX IX_LeaveTypes_Code ON #LeaveTypes (LeaveTypeCode);

    -- ════════════════════════════════════════════════════════
    -- 4. Paged Employees ONLY (avoids doing expensive calculation on all)
    -- ════════════════════════════════════════════════════════
    CREATE TABLE #PagedEmployees (
        EmployeeId       VARCHAR(50)   COLLATE DATABASE_DEFAULT NOT NULL,
        EmployeeName     NVARCHAR(200) COLLATE DATABASE_DEFAULT,
        Designation      NVARCHAR(100) COLLATE DATABASE_DEFAULT,
        JoiningDate      DATE,
        AnniversaryStart DATE,
        AnniversaryEnd   DATE,
        RemainingMonths  INT,
        RowNum           INT,
        TotalCount       INT
    );

    ;WITH EmpPaged AS (
        SELECT
            EmployeeId, EmployeeName, Designation, JoiningDate,
            AnniversaryStart, AnniversaryEnd, RemainingMonths,
            ROW_NUMBER() OVER (ORDER BY EmployeeName ASC) AS RowNum,
            COUNT(*)     OVER ()                          AS TotalCount
        FROM #Employees
    )
    INSERT INTO #PagedEmployees
    SELECT *
    FROM EmpPaged
    WHERE RowNum BETWEEN (@Page - 1) * @PageSize + 1 AND @Page * @PageSize;

    CREATE UNIQUE CLUSTERED INDEX IX_PagedEmployees_EmployeeId ON #PagedEmployees (EmployeeId);

    -- ════════════════════════════════════════════════════════
    -- 5. Availed Leave Days (Only for #PagedEmployees)
    -- ════════════════════════════════════════════════════════
    CREATE TABLE #Availed (
        EmployeeId    VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL,
        LeaveTypeCode VARCHAR(50) COLLATE DATABASE_DEFAULT NOT NULL,
        AvailedDays   DECIMAL(10,2)
    );

    INSERT INTO #Availed
    SELECT
        la.EmployeeID,
        lt.LeaveTypeCode,
        SUM(
            CASE
                WHEN ISNULL(la.NoOfDay, 0) > 0
                    THEN la.NoOfDay
                ELSE
                    DATEDIFF(DAY,
                        CASE WHEN la.StartDate < emp.AnniversaryStart
                             THEN emp.AnniversaryStart ELSE la.StartDate END,
                        CASE WHEN la.EndDate   > emp.AnniversaryEnd
                             THEN emp.AnniversaryEnd ELSE la.EndDate END
                    ) + 1
            END
        ) AS AvailedDays
    FROM #PagedEmployees emp
    JOIN HRM_LeaveApplicationEntry la
        ON la.EmployeeID = emp.EmployeeId
       AND (
            (la.StartDate >= emp.AnniversaryStart AND la.StartDate <= emp.AnniversaryEnd)
            OR
            (la.EndDate >= emp.AnniversaryStart AND la.EndDate <= emp.AnniversaryEnd)
       )
    JOIN HRM_ATD_LeaveType lt
        ON lt.LeaveTypeCode = la.LeaveTypeId
    WHERE
        (
            LOWER(ISNULL(la.IsApproved,''))          IN ('y','approved')
            OR LOWER(ISNULL(la.HODApprovalStatus,'')) IN ('y','approved')
            OR LOWER(ISNULL(la.HRApprovalStatus,''))  IN ('y','approved')
        )
    GROUP BY
        la.EmployeeID,
        lt.LeaveTypeCode;

    CREATE UNIQUE CLUSTERED INDEX IX_Availed_Emp_Type ON #Availed (EmployeeId, LeaveTypeCode);

    -- ════════════════════════════════════════════════════════
    -- RS1: Summary Cards (Full filtered set)
    -- ════════════════════════════════════════════════════════
    SELECT
        COUNT(*) AS TotalApplied,

        SUM(CASE
                WHEN LOWER(ISNULL(la.IsApproved,''))        IN ('y','approved')
                  OR LOWER(ISNULL(la.HODApprovalStatus,'')) IN ('y','approved')
                  OR LOWER(ISNULL(la.HRApprovalStatus,''))  IN ('y','approved')
                THEN 1 ELSE 0 END) AS Approved,

        SUM(CASE
                WHEN LOWER(ISNULL(la.IsApproved,''))        NOT IN ('y','approved')
                 AND LOWER(ISNULL(la.HODApprovalStatus,'')) NOT IN ('y','approved')
                 AND LOWER(ISNULL(la.HRApprovalStatus,''))  NOT IN ('y','approved')
                 AND (
                      LOWER(ISNULL(la.IsApproved,''))        IN ('n','canceled','rejected')
                   OR LOWER(ISNULL(la.HODApprovalStatus,'')) IN ('n','canceled','rejected')
                   OR LOWER(ISNULL(la.HRApprovalStatus,''))  IN ('n','canceled','rejected')
                 )
                THEN 1 ELSE 0 END) AS Canceled,

        SUM(CASE
                WHEN LOWER(ISNULL(la.IsApproved,''))        NOT IN ('y','approved','n','canceled','rejected')
                 AND LOWER(ISNULL(la.HODApprovalStatus,'')) NOT IN ('y','approved','n','canceled','rejected')
                 AND LOWER(ISNULL(la.HRApprovalStatus,''))  NOT IN ('y','approved','n','canceled','rejected')
                THEN 1 ELSE 0 END) AS Pending

    FROM #Employees emp
    JOIN HRM_LeaveApplicationEntry la
        ON la.EmployeeID = emp.EmployeeId
       AND (
            (la.StartDate >= emp.AnniversaryStart AND la.StartDate <= emp.AnniversaryEnd)
            OR
            (la.EndDate >= emp.AnniversaryStart AND la.EndDate <= emp.AnniversaryEnd)
       )
    OPTION (RECOMPILE);

    -- ════════════════════════════════════════════════════════
    -- RS2: Leave Types (Ordered by exact sequence: CL, SL, UL, ML, PL, MarL, HL, UmrL)
    -- ════════════════════════════════════════════════════════
    SELECT LeaveTypeCode, ShortName, NoOfDay
    FROM #LeaveTypes
    ORDER BY SortOrder, ShortName;

    -- ════════════════════════════════════════════════════════
    -- RS3: Paged Employee × LeaveType flat rows
    -- ════════════════════════════════════════════════════════
    SELECT
        p.EmployeeId,
        p.EmployeeName                              AS [Name],
        p.Designation,
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

    DROP TABLE #Employees;
    DROP TABLE #LeaveTypes;
    DROP TABLE #PagedEmployees;
    DROP TABLE #Availed;
END;
GO
