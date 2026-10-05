namespace GCTL.UI.Core.Views.LeaveApplicationEntry
{
    public static class LeaveStatistic
    {

        public const string GetLeaveReport100 = @"

           CREATE OR ALTER   PROCEDURE [dbo].[GetLeaveReport100]
                @DateFrom DATETIME = NULL,
                @DateTo DATETIME = NULL,
                @Company VARCHAR(MAX) = NULL,
                @Branch VARCHAR(MAX) = NULL,
                @Department VARCHAR(MAX) = NULL,
                @Employee VARCHAR(MAX) = NULL,
                @LeaveFormat VARCHAR(MAX) = NULL,
                @LeaveStatus VARCHAR(MAX) = NULL,
                @ReportFormat VARCHAR(50) = NULL
            AS
            BEGIN
                SET NOCOUNT ON;

                -- Create temporary tables for split values
                CREATE TABLE #TempCompany (Value VARCHAR(50))
                CREATE TABLE #TempBranch (Value VARCHAR(50))
                CREATE TABLE #TempDepartment (Value VARCHAR(50))
                CREATE TABLE #TempEmployee (Value VARCHAR(50))
                CREATE TABLE #TempLeaveFormat (Value VARCHAR(50))
                CREATE TABLE #TempLeaveStatus (Value VARCHAR(50))

                -- Helper variables for string splitting
                DECLARE @StartPos INT
                DECLARE @CommaPos INT
                DECLARE @Value VARCHAR(100)
                DECLARE @String VARCHAR(MAX)

                -- Split Company
                IF @Company IS NOT NULL
                BEGIN
                    SET @String = @Company
                    SET @StartPos = 1
                    SET @CommaPos = CHARINDEX(',', @String)
                    WHILE @CommaPos > 0
                    BEGIN
                        SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, @CommaPos - @StartPos)))
                        IF @Value <> ''
                            INSERT INTO #TempCompany (Value) VALUES (@Value)
                        SET @StartPos = @CommaPos + 1
                        SET @CommaPos = CHARINDEX(',', @String, @StartPos)
                    END
                    SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, LEN(@String) - @StartPos + 1)))
                    IF @Value <> ''
                        INSERT INTO #TempCompany (Value) VALUES (@Value)
                END

                -- Split Branch
                IF @Branch IS NOT NULL
                BEGIN
                    SET @String = @Branch
                    SET @StartPos = 1
                    SET @CommaPos = CHARINDEX(',', @String)
                    WHILE @CommaPos > 0
                    BEGIN
                        SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, @CommaPos - @StartPos)))
                        IF @Value <> ''
                            INSERT INTO #TempBranch (Value) VALUES (@Value)
                        SET @StartPos = @CommaPos + 1
                        SET @CommaPos = CHARINDEX(',', @String, @StartPos)
                    END
                    SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, LEN(@String) - @StartPos + 1)))
                    IF @Value <> ''
                        INSERT INTO #TempBranch (Value) VALUES (@Value)
                END

                -- Split Department
                IF @Department IS NOT NULL
                BEGIN
                    SET @String = @Department
                    SET @StartPos = 1
                    SET @CommaPos = CHARINDEX(',', @String)
                    WHILE @CommaPos > 0
                    BEGIN
                        SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, @CommaPos - @StartPos)))
                        IF @Value <> ''
                            INSERT INTO #TempDepartment (Value) VALUES (@Value)
                        SET @StartPos = @CommaPos + 1
                        SET @CommaPos = CHARINDEX(',', @String, @StartPos)
                    END
                    SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, LEN(@String) - @StartPos + 1)))
                    IF @Value <> ''
                        INSERT INTO #TempDepartment (Value) VALUES (@Value)
                END

                -- Split Employee
                IF @Employee IS NOT NULL
                BEGIN
                    SET @String = @Employee
                    SET @StartPos = 1
                    SET @CommaPos = CHARINDEX(',', @String)
                    WHILE @CommaPos > 0
                    BEGIN
                        SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, @CommaPos - @StartPos)))
                        IF @Value <> ''
                            INSERT INTO #TempEmployee (Value) VALUES (@Value)
                        SET @StartPos = @CommaPos + 1
                        SET @CommaPos = CHARINDEX(',', @String, @StartPos)
                    END
                    SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, LEN(@String) - @StartPos + 1)))
                    IF @Value <> ''
                        INSERT INTO #TempEmployee (Value) VALUES (@Value)
                END

                -- Split LeaveFormat
                IF @LeaveFormat IS NOT NULL
                BEGIN
                    SET @String = @LeaveFormat
                    SET @StartPos = 1
                    SET @CommaPos = CHARINDEX(',', @String)
                    WHILE @CommaPos > 0
                    BEGIN
                        SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, @CommaPos - @StartPos)))
                        IF @Value <> ''
                            INSERT INTO #TempLeaveFormat (Value) VALUES (@Value)
                        SET @StartPos = @CommaPos + 1
                        SET @CommaPos = CHARINDEX(',', @String, @StartPos)
                    END
                    SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, LEN(@String) - @StartPos + 1)))
                    IF @Value <> ''
                        INSERT INTO #TempLeaveFormat (Value) VALUES (@Value)
                END

                -- Split LeaveStatus
                IF @LeaveStatus IS NOT NULL
                BEGIN
                    SET @String = @LeaveStatus
                    SET @StartPos = 1
                    SET @CommaPos = CHARINDEX(',', @String)
                    WHILE @CommaPos > 0
                    BEGIN
                        SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, @CommaPos - @StartPos)))
                        IF @Value <> ''
                            INSERT INTO #TempLeaveStatus (Value) VALUES (@Value)
                        SET @StartPos = @CommaPos + 1
                        SET @CommaPos = CHARINDEX(',', @String, @StartPos)
                    END
                    SET @Value = LTRIM(RTRIM(SUBSTRING(@String, @StartPos, LEN(@String) - @StartPos + 1)))
                    IF @Value <> ''
                        INSERT INTO #TempLeaveStatus (Value) VALUES (@Value)
                END

                -- Main query
                SELECT 
                    l.CompanyCode,
                    dep.DepartmentName,
                    l.AutoId AS LeaveAppEntryCode,
                    l.LeaveAppEntryId,
                    e.EmployeeId,
                    e.FirstName AS EmployeeFirstName,
                    dep.DepartmentName AS DepartmentName,
                    desig.DesignationName AS DesignationName,
                    type.ShortName AS LeaveTypeId,
                    l.StartDate,
                    l.EndDate,
                    l.NoOfDay,
                    l.ModifyDate,
                    l.ConfirmationRemarks,
                    l.HodapprovalStatus AS HODApprovalStatus,
                    l.HrapprovalRemarks AS HRApprovalRemarks,
                    l.SickLeaveFilePath,
                    l.HrapprovalStatus AS HRApprovalStatus,
                    l.ApplyLeaveFormat,
                    hod.FirstName AS HODFirstName,
                    sup.FirstName AS SupervisorFirstName,
                    l.Reason,
                    l.ShortLeaveFrom,
                    l.ShortLeaveTo,
                    l.ShortLeaveTime,
                    l.IsApproved,
                    CASE
                        WHEN l.FirstOrSecondHalf = '1' THEN 'First Half'
                        WHEN l.FirstOrSecondHalf = '2' THEN 'Second Half'
                        ELSE NULL
                    END AS FirstOrSecondHalf,
                    COUNT(l.LeaveAppEntryId) AS TotalLeaves,
                    SUM(CASE WHEN l.HrapprovalStatus = 'pending' THEN 1 ELSE 0 END) AS PendingLeaves,
                    SUM(CASE WHEN l.HrapprovalStatus = 'approved' THEN 1 ELSE 0 END) AS ApprovedLeaves,
                    SUM(CASE WHEN l.HrapprovalStatus = 'rejected' THEN 1 ELSE 0 END) AS RejectedLeaves
                FROM HRM_LeaveApplicationEntry l
                LEFT JOIN HRM_Employee e ON l.EmployeeId = e.EmployeeId
                LEFT JOIN HRM_Employee hod ON l.Hod = hod.EmployeeId
                LEFT JOIN HRM_ATD_LeaveType type ON l.LeaveTypeId = type.LeaveTypeCode
                LEFT JOIN HRM_Employee sup ON l.BossEmpAutoId = sup.EmployeeId
                LEFT JOIN HRM_EmployeeOfficialInfo oe ON l.EmployeeId = oe.EmployeeId
                LEFT JOIN HRM_Def_Department dep ON oe.DepartmentCode = dep.DepartmentCode
                LEFT JOIN HRM_Def_Designation desig ON oe.DesignationCode = desig.DesignationCode
                WHERE 
                    (@LeaveStatus IS NULL OR l.HrapprovalStatus IN (SELECT Value FROM #TempLeaveStatus))
                    AND (@DateFrom IS NULL OR l.StartDate >= @DateFrom)
                    AND (@DateTo IS NULL OR l.EndDate <= @DateTo)
                    AND (@Company IS NULL OR l.CompanyCode IN (SELECT Value FROM #TempCompany))
                    AND (@Branch IS NULL OR oe.BranchCode IN (SELECT Value FROM #TempBranch))
                    AND (@Department IS NULL OR oe.DepartmentCode IN (SELECT Value FROM #TempDepartment))
                    AND (@Employee IS NULL OR l.EmployeeId IN (SELECT Value FROM #TempEmployee))
                    AND (@LeaveFormat IS NULL OR l.ApplyLeaveFormat IN (SELECT Value FROM #TempLeaveFormat))
                GROUP BY 
                    l.CompanyCode, dep.DepartmentName, e.EmployeeId, e.FirstName, 
                    desig.DesignationName, type.ShortName, l.AutoId, l.LeaveAppEntryId, 
                    l.StartDate, l.EndDate, l.NoOfDay, l.ModifyDate, l.ConfirmationRemarks, 
                    l.HodapprovalStatus, l.HrapprovalRemarks, l.SickLeaveFilePath, 
                    l.HrapprovalStatus, l.ApplyLeaveFormat, hod.FirstName, sup.FirstName, 
                    l.Reason, l.ShortLeaveFrom, l.ShortLeaveTo, l.ShortLeaveTime, 
                    l.IsApproved, l.FirstOrSecondHalf
                ORDER BY 
                    l.CompanyCode, dep.DepartmentName;

                -- Clean up temporary tables
                DROP TABLE #TempCompany
                DROP TABLE #TempBranch
                DROP TABLE #TempDepartment
                DROP TABLE #TempEmployee
                DROP TABLE #TempLeaveFormat
                DROP TABLE #TempLeaveStatus
            END


        ";
    }
}
