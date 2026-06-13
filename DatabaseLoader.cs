using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace PlxParser
{
    public class GroupItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int ProfileID { get; set; }
    }

    public class SpecialityItem
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Code { get; set; }
    }

    public class DisciplineResult
    {
        public int SubjectID { get; set; }
        public string Name { get; set; }
        public string DepartmentName { get; set; }
        public decimal Credits { get; set; }
        public bool IsPhysical { get; set; }
        public bool IsGradWork { get; set; }
        public int CourseWork { get; set; }
        public int CourseProj { get; set; }
        public double Si { get; set; }
    }

    public class FormulaParams
    {
        public int StudentsPerRate { get; set; }
        public int HoursPerCourseWork { get; set; }
        public int HoursPerCourseProj { get; set; }
        public int HoursPerGradWork { get; set; }
    }

    public class AcademicPlanView
    {
        public int ID { get; set; }
        public string SpecialityCode { get; set; }
        public string SpecialityTitle { get; set; }
        public string ProfileName { get; set; }
        public int RecruitmentYear { get; set; }
        public string EducationForm { get; set; }
        public int YearsNorm { get; set; }
        public string Description { get; set; }

        public string DisplayName =>
            $"{SpecialityCode} — {SpecialityTitle} ({RecruitmentYear})" +
            (string.IsNullOrEmpty(Description) ? "" : $" [{Description}]");
    }

    public class DatabaseLoader
    {
        private readonly string _conn;

        public DatabaseLoader(string connectionString)
        {
            _conn = connectionString;
        }

        public List<SpecialityItem> LoadSpecialities()
        {
            var result = new List<SpecialityItem>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand("SELECT ID, Title, Code FROM Speciality", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SpecialityItem
                {
                    ID = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Code = reader.GetString(2)
                });
            }
            return result;
        }

        public List<GroupItem> LoadGroups(int profileID)
        {
            var result = new List<GroupItem>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(
                "SELECT ID, Name, ProfileID FROM [Group] WHERE ProfileID = @ProfileID", conn);
            cmd.Parameters.AddWithValue("@ProfileID", profileID);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new GroupItem
                {
                    ID = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ProfileID = reader.GetInt32(2)
                });
            }
            return result;
        }

        public List<GroupItem> LoadAllGroups()
        {
            var result = new List<GroupItem>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand("SELECT ID, Name, ProfileID FROM [Group]", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new GroupItem
                {
                    ID = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ProfileID = reader.GetInt32(2)
                });
            }
            return result;
        }

        public FormulaParams LoadFormulaParams(int academicPlanID)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT StudentsPerRate, HoursPerCourseWork, HoursPerCourseProj, HoursPerGradWork
                FROM Formula
                WHERE AcademicPlanID = @AcademicPlanID", conn);
            cmd.Parameters.AddWithValue("@AcademicPlanID", academicPlanID);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new FormulaParams
                {
                    StudentsPerRate = reader.GetInt32(0),
                    HoursPerCourseWork = reader.GetInt32(1),
                    HoursPerCourseProj = reader.GetInt32(2),
                    HoursPerGradWork = reader.GetInt32(3)
                };
            }

            return new FormulaParams
            {
                StudentsPerRate = 14,
                HoursPerCourseWork = 2,
                HoursPerCourseProj = 4,
                HoursPerGradWork = 21
            };
        }

        public int LoadStudentCount(int groupID, int year)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT Count FROM StudentCount
                WHERE GroupID = @GroupID AND Year = @Year", conn);
            cmd.Parameters.AddWithValue("@GroupID", groupID);
            cmd.Parameters.AddWithValue("@Year", year);

            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public List<DisciplineResult> LoadDisciplines(int academicPlanID)
        {
            return LoadDisciplinesInternal(academicPlanID, null);
        }

        public List<DisciplineResult> LoadDisciplinesByCourse(int academicPlanID, int course)
        {
            return LoadDisciplinesInternal(academicPlanID, course);
        }

        private List<DisciplineResult> LoadDisciplinesInternal(int academicPlanID, int? course)
        {
            var result = new List<DisciplineResult>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string courseFilter = course.HasValue
                ? $@"AND s.ID IN (
                        SELECT DISTINCT SubjectID FROM SubjectSection
                        WHERE Semester IN ({(course.Value - 1) * 2 + 1}, {(course.Value - 1) * 2 + 2})
                    )"
                : "";

            string sql = $@"
                SELECT
                    s.ID,
                    d.Name,
                    dep.Title AS DepartmentName,
                    s.Credits,
                    s.IsPhysical,
                    s.IsGradWork,
                    SUM(ss.CourseWork)    AS CourseWork,
                    SUM(ss.CourseProject) AS CourseProject
                FROM Subject s
                JOIN Discipline d      ON s.DisciplineID  = d.ID
                JOIN Department dep    ON s.DepartmentID  = dep.ID
                JOIN SubjectSection ss ON ss.SubjectID    = s.ID
                WHERE s.AcademicPlanID = @PlanID
                {courseFilter}
                GROUP BY s.ID, d.Name, dep.Title, s.Credits, s.IsPhysical, s.IsGradWork";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PlanID", academicPlanID);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new DisciplineResult
                {
                    SubjectID = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    DepartmentName = reader.GetString(2),
                    Credits = reader.GetDecimal(3),
                    IsPhysical = reader.GetBoolean(4),
                    IsGradWork = reader.GetBoolean(5),
                    CourseWork = reader.GetInt32(6),
                    CourseProj = reader.GetInt32(7)
                });
            }
            return result;
        }

        public int LoadAcademicPlanID(int profileID)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(
                "SELECT TOP 1 ID FROM AcademicPlan WHERE ProfileID = @ProfileID", conn);
            cmd.Parameters.AddWithValue("@ProfileID", profileID);

            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public int LoadProfileID(int specialityID)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(
                "SELECT TOP 1 ID FROM Profile WHERE SpecialityID = @SpecialityID", conn);
            cmd.Parameters.AddWithValue("@SpecialityID", specialityID);

            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public int LoadProfileIDByPlan(int academicPlanID)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(
                "SELECT ProfileID FROM AcademicPlan WHERE ID = @PlanID", conn);
            cmd.Parameters.AddWithValue("@PlanID", academicPlanID);

            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public List<AcademicPlanView> LoadAllPlans()
        {
            var result = new List<AcademicPlanView>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT ap.ID, sp.Code, sp.Title, pr.Name,
                       ap.RecruitmentYear, ap.EducationForm, ap.YearsNorm, ap.Description
                FROM AcademicPlan ap
                JOIN Profile pr    ON ap.ProfileID    = pr.ID
                JOIN Speciality sp ON pr.SpecialityID = sp.ID
                ORDER BY sp.Code, ap.RecruitmentYear DESC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new AcademicPlanView
                {
                    ID = reader.GetInt32(0),
                    SpecialityCode = reader.GetString(1),
                    SpecialityTitle = reader.GetString(2),
                    ProfileName = reader.GetString(3),
                    RecruitmentYear = reader.GetInt32(4),
                    EducationForm = reader.GetString(5),
                    YearsNorm = reader.GetInt32(6),
                    Description = reader.IsDBNull(7) ? "" : reader.GetString(7)
                });
            }
            return result;
        }

        public List<AcademicPlanView> LoadAcademicPlans()
        {
            var result = new List<AcademicPlanView>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT ap.ID, sp.Code, sp.Title, pr.Name, ap.RecruitmentYear,
                       ap.EducationForm, ap.YearsNorm, ap.Description
                FROM AcademicPlan ap
                JOIN Profile pr    ON ap.ProfileID    = pr.ID
                JOIN Speciality sp ON pr.SpecialityID = sp.ID
                ORDER BY sp.Code, ap.RecruitmentYear DESC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new AcademicPlanView
                {
                    ID = reader.GetInt32(0),
                    SpecialityCode = reader.GetString(1),
                    SpecialityTitle = reader.GetString(2),
                    ProfileName = reader.GetString(3),
                    RecruitmentYear = reader.GetInt32(4),
                    EducationForm = reader.GetString(5),
                    YearsNorm = reader.GetInt32(6),
                    Description = reader.IsDBNull(7) ? "" : reader.GetString(7)
                });
            }
            return result;
        }

        public void SaveFormulaParams(int academicPlanID, FormulaParams p)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM Formula WHERE AcademicPlanID=@AcademicPlanID)
                    UPDATE Formula SET StudentsPerRate=@StudentsPerRate, HoursPerCourseWork=@HoursPerCourseWork,
                        HoursPerCourseProj=@HoursPerCourseProj, HoursPerGradWork=@HoursPerGradWork
                    WHERE AcademicPlanID=@AcademicPlanID
                ELSE
                    INSERT INTO Formula
                        (AcademicPlanID, StudentsPerRate, HoursPerCourseWork, HoursPerCourseProj, HoursPerGradWork)
                    VALUES
                        (@AcademicPlanID, @StudentsPerRate, @HoursPerCourseWork, @HoursPerCourseProj, @HoursPerGradWork)",
                conn);
            cmd.Parameters.AddWithValue("@AcademicPlanID", academicPlanID);
            cmd.Parameters.AddWithValue("@StudentsPerRate", p.StudentsPerRate);
            cmd.Parameters.AddWithValue("@HoursPerCourseWork", p.HoursPerCourseWork);
            cmd.Parameters.AddWithValue("@HoursPerCourseProj", p.HoursPerCourseProj);
            cmd.Parameters.AddWithValue("@HoursPerGradWork", p.HoursPerGradWork);
            cmd.ExecuteNonQuery();
        }
        public void DeleteGroup(int groupID)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand(
                "DELETE FROM StudentCount WHERE GroupID = @GroupID", conn))
            {
                cmd.Parameters.AddWithValue("@GroupID", groupID);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(
                "DELETE FROM [Group] WHERE ID = @GroupID", conn))
            {
                cmd.Parameters.AddWithValue("@GroupID", groupID);
                cmd.ExecuteNonQuery();
            }
        }

        public void SaveGroup(int profileID, string name)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(
                "INSERT INTO [Group] (ProfileID, Name) VALUES (@ProfileID, @Name)", conn);
            cmd.Parameters.AddWithValue("@ProfileID", profileID);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.ExecuteNonQuery();
        }

        public void SaveStudentCount(int groupID, int year, int count)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM StudentCount WHERE GroupID=@GroupID AND Year=@Year)
                    UPDATE StudentCount SET Count=@Count WHERE GroupID=@GroupID AND Year=@Year
                ELSE
                    INSERT INTO StudentCount (GroupID, Year, Count) VALUES (@GroupID, @Year, @Count)",
                conn);
            cmd.Parameters.AddWithValue("@GroupID", groupID);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Count", count);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAcademicPlan(int planID)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand(@"
                DELETE FROM Stream
                WHERE SubjectSection2ID IN (
                    SELECT ss.ID FROM SubjectSection ss
                    JOIN Subject s ON ss.SubjectID = s.ID
                    WHERE s.AcademicPlanID = @PlanID
                )", conn))
            {
                cmd.Parameters.AddWithValue("@PlanID", planID);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(
                "DELETE FROM AcademicPlan WHERE ID = @PlanID", conn))
            {
                cmd.Parameters.AddWithValue("@PlanID", planID);
                cmd.ExecuteNonQuery();
            }
        }

        public List<AcademicPlanView> LoadAllPlansBySpeciality(int specialityID)
        {
            var result = new List<AcademicPlanView>();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT ap.ID, sp.Code, pr.Name, ap.RecruitmentYear,
                       ap.EducationForm, ap.YearsNorm, ap.Description
                FROM AcademicPlan ap
                JOIN Profile pr    ON ap.ProfileID    = pr.ID
                JOIN Speciality sp ON pr.SpecialityID = sp.ID
                WHERE sp.ID = @SpecialityID
                ORDER BY ap.RecruitmentYear DESC", conn);

            cmd.Parameters.AddWithValue("@SpecialityID", specialityID);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new AcademicPlanView
                {
                    ID = reader.GetInt32(0),
                    SpecialityCode = reader.GetString(1),
                    SpecialityTitle = reader.GetString(2),
                    ProfileName = reader.GetString(3),
                    RecruitmentYear = reader.GetInt32(4),
                    EducationForm = reader.GetString(5),
                    YearsNorm = reader.GetInt32(6),
                    Description = reader.IsDBNull(7) ? "" : reader.GetString(7)
                });
            }
            return result;
        }
    }
}
