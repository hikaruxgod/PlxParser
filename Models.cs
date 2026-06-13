namespace PlxParser
{
    public class EducationLvl
    {
        public int ID { get; set; }
        public string Title { get; set; }
    }

    public class Speciality
    {
        public int ID { get; set; }
        public int EducationLvlID { get; set; }
        public string Title { get; set; }
        public string Code { get; set; }
    }

    public class Profile
    {
        public int ID { get; set; }
        public int SpecialityID { get; set; }
        public string Name { get; set; }
    }

    public class AcademicPlan
    {
        public int ID { get; set; }
        public int ProfileID { get; set; }
        public int RecruitmentYear { get; set; }
        public string EducationForm { get; set; }
        public int YearsNorm { get; set; }
        public string Description { get; set; }
    }

    public class WorkType
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
    }

    public class Department
    {
        public int ID { get; set; }
        public int Number { get; set; }
        public string Title { get; set; }
    }

    public class Discipline
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }

    public class Subject
    {
        public int ID { get; set; }
        public int AcademicPlanID { get; set; }
        public int DepartmentID { get; set; }
        public int DisciplineID { get; set; }
        public string Code { get; set; }
        public int LabourIntensity { get; set; }
        public decimal Credits { get; set; }
        public bool IsOptional { get; set; }
        public bool IsPhysical { get; set; }
        public bool IsGradWork { get; set; }
    }

    public class SubjectSection
    {
        public int SubjectID { get; set; }
        public int Semester { get; set; }
        public int SemesterWeek { get; set; }
        public int Lectures { get; set; }
        public int LaboratoryWorks { get; set; }
        public int PracticalLessons { get; set; }
        public int IndependentWork { get; set; }
        public int CourseProject { get; set; }
        public int CourseWork { get; set; }
        public bool Test { get; set; }
    }

    public class PlxData
    {
        public List<EducationLvl> EducationLvls { get; set; } = new();
        public List<Speciality> Specialities { get; set; } = new();
        public List<Profile> Profiles { get; set; } = new();
        public List<AcademicPlan> AcademicPlans { get; set; } = new();
        public List<WorkType> WorkTypes { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<Discipline> Disciplines { get; set; } = new();
        public List<Subject> Subjects { get; set; } = new();
        public List<SubjectSection> SubjectSections { get; set; } = new();
    }
}