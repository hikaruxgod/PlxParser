using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace PlxParser
{
    public class DatabaseImporter
    {
        private readonly string _connectionString;

        public DatabaseImporter(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Import(PlxData data)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            Console.WriteLine("Подключение к БД установлено.");

            Console.WriteLine("Загружаем уровни образования...");
            InsertEducationLvls(conn, data.EducationLvls);

            Console.WriteLine("Загружаем направления подготовки...");
            var specialityIdMap = InsertSpecialities(conn, data.Specialities);

            Console.WriteLine("Загружаем профили...");
            var profileIdMap = InsertProfiles(conn, data.Profiles, specialityIdMap);

            Console.WriteLine("Загружаем учебные планы...");
            var academicPlanIdMap = InsertAcademicPlans(conn, data.AcademicPlans, profileIdMap);

            Console.WriteLine("Загружаем кафедры...");
            var departmentIdMap = InsertDepartments(conn, data.Departments);

            Console.WriteLine("Загружаем виды работ...");
            InsertWorkTypes(conn, data.WorkTypes);

            Console.WriteLine("Загружаем названия дисциплин...");
            var disciplineIdMap = InsertDisciplines(conn, data.Disciplines);

            Console.WriteLine("Загружаем дисциплины...");
            var subjectIdMap = InsertSubjects(conn, data.Subjects, departmentIdMap, disciplineIdMap, academicPlanIdMap);

            Console.WriteLine("Загружаем часы по семестрам...");
            InsertSubjectSections(conn, data.SubjectSections, subjectIdMap);
        }

        private void InsertEducationLvls(SqlConnection conn, List<EducationLvl> items)
        {
            foreach (var item in items)
            {
                using var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM EducationLvl WHERE Title = @Title)
                        INSERT INTO EducationLvl (ID, Title) VALUES (@ID, @Title)", conn);
                cmd.Parameters.AddWithValue("@ID", item.ID);
                cmd.Parameters.AddWithValue("@Title", item.Title);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine($"  Обработано: {items.Count}");
        }

        private Dictionary<int, int> InsertSpecialities(SqlConnection conn, List<Speciality> items)
        {
            var idMap = new Dictionary<int, int>();
            foreach (var item in items)
            {
                int existingId = 0;
                using (var checkCmd = new SqlCommand(
                    "SELECT ID FROM Speciality WHERE Code = @Code", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Code", item.Code);
                    var res = checkCmd.ExecuteScalar();
                    if (res != null) existingId = Convert.ToInt32(res);
                }

                if (existingId > 0)
                {
                    idMap[item.ID] = existingId;
                }
                else
                {
                    int realEduId = item.EducationLvlID;
                    using (var eduCmd = new SqlCommand(
                        "SELECT TOP 1 ID FROM EducationLvl", conn))
                    {
                        var res = eduCmd.ExecuteScalar();
                        if (res != null) realEduId = Convert.ToInt32(res);
                    }

                    using var cmd = new SqlCommand(@"
                        INSERT INTO Speciality (ID, EducationLvlID, Title, Code)
                        VALUES (@ID, @EducationLvlID, @Title, @Code);
                        SELECT SCOPE_IDENTITY();", conn);
                    int newId = GetNextId(conn, "Speciality");
                    cmd.Parameters.AddWithValue("@ID", newId);
                    cmd.Parameters.AddWithValue("@EducationLvlID", realEduId);
                    cmd.Parameters.AddWithValue("@Title", item.Title);
                    cmd.Parameters.AddWithValue("@Code", item.Code);
                    cmd.ExecuteNonQuery();
                    idMap[item.ID] = newId;
                }
            }
            Console.WriteLine($"  Обработано: {items.Count}");
            return idMap;
        }

        private int GetNextId(SqlConnection conn, string table)
        {
            using var cmd = new SqlCommand(
                $"SELECT ISNULL(MAX(ID), 9999) + 1 FROM {table}", conn);
            int next = Convert.ToInt32(cmd.ExecuteScalar());
            return next < 10000 ? 10000 : next;
        }

        private Dictionary<int, int> InsertProfiles(SqlConnection conn, List<Profile> items,
            Dictionary<int, int> specialityIdMap)
        {
            var idMap = new Dictionary<int, int>();
            foreach (var item in items)
            {
                int realSpecId = specialityIdMap.TryGetValue(item.SpecialityID, out var s)
                    ? s : item.SpecialityID;

                int existingId = 0;
                using (var checkCmd = new SqlCommand(
                    "SELECT TOP 1 ID FROM Profile WHERE SpecialityID = @SpecID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@SpecID", realSpecId);
                    var res = checkCmd.ExecuteScalar();
                    if (res != null) existingId = Convert.ToInt32(res);
                }

                if (existingId > 0)
                {
                    idMap[item.ID] = existingId;
                }
                else
                {
                    int newId = GetNextId(conn, "Profile");
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Profile (ID, SpecialityID, Name)
                        VALUES (@ID, @SpecialityID, @Name)", conn);
                    cmd.Parameters.AddWithValue("@ID", newId);
                    cmd.Parameters.AddWithValue("@SpecialityID", realSpecId);
                    cmd.Parameters.AddWithValue("@Name", item.Name);
                    cmd.ExecuteNonQuery();
                    idMap[item.ID] = newId;
                }
            }
            Console.WriteLine($"  Обработано: {items.Count}");
            return idMap;
        }

        private Dictionary<int, int> InsertAcademicPlans(SqlConnection conn,
            List<AcademicPlan> items, Dictionary<int, int> profileIdMap)
        {
            var idMap = new Dictionary<int, int>();
            foreach (var item in items)
            {
                int realProfileId = profileIdMap.TryGetValue(item.ProfileID, out var p)
                    ? p : item.ProfileID;

                int existingId = 0;
                using (var checkCmd = new SqlCommand(@"
                    SELECT ID FROM AcademicPlan
                    WHERE ProfileID = @ProfileID AND RecruitmentYear = @Year", conn))
                {
                    checkCmd.Parameters.AddWithValue("@ProfileID", realProfileId);
                    checkCmd.Parameters.AddWithValue("@Year", item.RecruitmentYear);
                    var res = checkCmd.ExecuteScalar();
                    if (res != null) existingId = Convert.ToInt32(res);
                }

                if (existingId > 0)
                {
                    if (!string.IsNullOrEmpty(item.Description))
                    {
                        using var upd = new SqlCommand(
                            "UPDATE AcademicPlan SET Description=@D WHERE ID=@ID", conn);
                        upd.Parameters.AddWithValue("@D", item.Description);
                        upd.Parameters.AddWithValue("@ID", existingId);
                        upd.ExecuteNonQuery();
                    }
                    idMap[item.ID] = existingId;
                }
                else
                {
                    int newId = GetNextId(conn, "AcademicPlan");
                    using var cmd = new SqlCommand(@"
                        INSERT INTO AcademicPlan
                            (ID, ProfileID, RecruitmentYear, EducationForm, YearsNorm, Description)
                        VALUES
                            (@ID, @ProfileID, @RecruitmentYear, @EducationForm, @YearsNorm, @Description)", conn);
                    cmd.Parameters.AddWithValue("@ID", newId);
                    cmd.Parameters.AddWithValue("@ProfileID", realProfileId);
                    cmd.Parameters.AddWithValue("@RecruitmentYear", item.RecruitmentYear);
                    cmd.Parameters.AddWithValue("@EducationForm", item.EducationForm);
                    cmd.Parameters.AddWithValue("@YearsNorm", item.YearsNorm);
                    cmd.Parameters.AddWithValue("@Description",
                        string.IsNullOrEmpty(item.Description)
                            ? (object)DBNull.Value
                            : item.Description);
                    cmd.ExecuteNonQuery();
                    idMap[item.ID] = newId;
                }
            }
            Console.WriteLine($"  Обработано: {items.Count}");
            return idMap;
        }

        private Dictionary<int, int> InsertDepartments(SqlConnection conn, List<Department> items)
        {
            var idMap = new Dictionary<int, int>();

            foreach (var item in items)
            {
                int existingId = 0;
                using (var checkCmd = new SqlCommand(
                    "SELECT ID FROM Department WHERE Title = @Title", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Title", item.Title);
                    var result = checkCmd.ExecuteScalar();
                    if (result != null)
                        existingId = Convert.ToInt32(result);
                }

                if (existingId > 0)
                {
                    idMap[item.Number] = existingId;
                }
                else
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Department (Number, Title)
                        VALUES (@Number, @Title);
                        SELECT SCOPE_IDENTITY();", conn);
                    cmd.Parameters.AddWithValue("@Number", item.Number);
                    cmd.Parameters.AddWithValue("@Title", item.Title);
                    int newId = Convert.ToInt32(cmd.ExecuteScalar());
                    idMap[item.Number] = newId;
                }
            }

            Console.WriteLine($"  Обработано: {items.Count}");
            return idMap;
        }

        private void InsertWorkTypes(SqlConnection conn, List<WorkType> items)
        {
            foreach (var item in items)
            {
                using var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM WorkType WHERE ID = @ID)
                        INSERT INTO WorkType (ID, Code, Title)
                        VALUES (@ID, @Code, @Title)", conn);
                cmd.Parameters.AddWithValue("@ID", item.ID);
                cmd.Parameters.AddWithValue("@Code", item.Code);
                cmd.Parameters.AddWithValue("@Title", item.Title);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine($"  Обработано: {items.Count}");
        }

        private Dictionary<int, int> InsertDisciplines(SqlConnection conn, List<Discipline> items)
        {
            var idMap = new Dictionary<int, int>();

            foreach (var item in items)
            {
                int existingId = 0;
                using (var checkCmd = new SqlCommand(
                    "SELECT ID FROM Discipline WHERE Name = @Name", conn))
                {
                    checkCmd.Parameters.AddWithValue("@Name", item.Name);
                    var result = checkCmd.ExecuteScalar();
                    if (result != null)
                        existingId = Convert.ToInt32(result);
                }

                if (existingId > 0)
                {
                    idMap[item.ID] = existingId;
                }
                else
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Discipline (Name)
                        VALUES (@Name);
                        SELECT SCOPE_IDENTITY();", conn);
                    cmd.Parameters.AddWithValue("@Name", item.Name);
                    int newId = Convert.ToInt32(cmd.ExecuteScalar());
                    idMap[item.ID] = newId;
                }
            }

            Console.WriteLine($"  Обработано: {items.Count}");
            return idMap;
        }

        private Dictionary<int, int> InsertSubjects(SqlConnection conn, List<Subject> items,
            Dictionary<int, int> departmentIdMap, Dictionary<int, int> disciplineIdMap,
            Dictionary<int, int> academicPlanIdMap)
        {
            var subjectIdMap = new Dictionary<int, int>();
            foreach (var item in items)
            {
                int realPlanId = academicPlanIdMap.TryGetValue(item.AcademicPlanID, out var ap)
                    ? ap : item.AcademicPlanID;
                int deptId = departmentIdMap.TryGetValue(item.DepartmentID, out var d) ? d : 0;
                int discId = disciplineIdMap.TryGetValue(item.DisciplineID, out var di) ? di : 0;

                using (var checkCmd = new SqlCommand(@"
                    SELECT 1 FROM Subject
                    WHERE AcademicPlanID = @PlanID AND DisciplineID = @DiscID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@PlanID", realPlanId);
                    checkCmd.Parameters.AddWithValue("@DiscID", discId);
                    if (checkCmd.ExecuteScalar() != null) continue;
                }

                int newId = GetNextId(conn, "Subject");
                using var cmd = new SqlCommand(@"
                    INSERT INTO Subject
                        (ID, AcademicPlanID, DepartmentID, DisciplineID, Code,
                         LabourIntensity, Credits, IsOptional, IsPhysical, IsGradWork)
                    VALUES
                        (@ID, @AcademicPlanID, @DepartmentID, @DisciplineID, @Code,
                         @LabourIntensity, @Credits, @IsOptional, @IsPhysical, @IsGradWork)", conn);
                cmd.Parameters.AddWithValue("@ID", newId);
                cmd.Parameters.AddWithValue("@AcademicPlanID", realPlanId);
                cmd.Parameters.AddWithValue("@DepartmentID", deptId);
                cmd.Parameters.AddWithValue("@DisciplineID", discId);
                cmd.Parameters.AddWithValue("@Code",
                    string.IsNullOrEmpty(item.Code) ? (object)DBNull.Value : item.Code);
                cmd.Parameters.AddWithValue("@LabourIntensity", item.LabourIntensity);
                cmd.Parameters.AddWithValue("@Credits", item.Credits);
                cmd.Parameters.AddWithValue("@IsOptional", item.IsOptional);
                cmd.Parameters.AddWithValue("@IsPhysical", item.IsPhysical);
                cmd.Parameters.AddWithValue("@IsGradWork", item.IsGradWork);
                cmd.ExecuteNonQuery();
                subjectIdMap[item.ID] = newId;
            }
            Console.WriteLine($"  Обработано: {items.Count}");
            return subjectIdMap;
        }

        private void InsertSubjectSections(SqlConnection conn, List<SubjectSection> items,
            Dictionary<int, int> subjectIdMap)
        {
            foreach (var item in items)
            {
                int realSubjectId = subjectIdMap.TryGetValue(item.SubjectID, out var sid)
                    ? sid : item.SubjectID;

                using (var checkCmd = new SqlCommand(@"
                    SELECT 1 FROM SubjectSection
                    WHERE SubjectID = @SubjectID AND Semester = @Semester", conn))
                {
                    checkCmd.Parameters.AddWithValue("@SubjectID", realSubjectId);
                    checkCmd.Parameters.AddWithValue("@Semester", item.Semester);
                    if (checkCmd.ExecuteScalar() != null) continue;
                }

                using var cmd = new SqlCommand(@"
                    INSERT INTO SubjectSection
                        (SubjectID, Semester, SemesterWeek,
                         Lectures, LaboratoryWorks, PracticalLessons,
                         IndependentWork, CourseProject, CourseWork, Test)
                    VALUES
                        (@SubjectID, @Semester, @SemesterWeek,
                         @Lectures, @LaboratoryWorks, @PracticalLessons,
                         @IndependentWork, @CourseProject, @CourseWork, @Test)", conn);
                cmd.Parameters.AddWithValue("@SubjectID", realSubjectId);
                cmd.Parameters.AddWithValue("@Semester", item.Semester);
                cmd.Parameters.AddWithValue("@SemesterWeek", item.SemesterWeek);
                cmd.Parameters.AddWithValue("@Lectures", item.Lectures);
                cmd.Parameters.AddWithValue("@LaboratoryWorks", item.LaboratoryWorks);
                cmd.Parameters.AddWithValue("@PracticalLessons", item.PracticalLessons);
                cmd.Parameters.AddWithValue("@IndependentWork", item.IndependentWork);
                cmd.Parameters.AddWithValue("@CourseProject", item.CourseProject);
                cmd.Parameters.AddWithValue("@CourseWork", item.CourseWork);
                cmd.Parameters.AddWithValue("@Test", item.Test);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine($"  Обработано: {items.Count}");
        }
    }
}
