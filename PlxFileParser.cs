using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Linq;

namespace PlxParser
{
    public class PlxFileParser
    {
        private readonly string _filePath;

        public PlxFileParser(string filePath)
        {
            _filePath = filePath;
        }

        public PlxData Parse()
        {
            Console.WriteLine("Читаем файл...");

            string xml;
            using (var reader = new StreamReader(_filePath, Encoding.Unicode))
                xml = reader.ReadToEnd();

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("ms", "urn:schemas-microsoft-com:xml-msdata");
            ns.AddNamespace("dg", "urn:schemas-microsoft-com:xml-diffgram-v1");
            ns.AddNamespace("d", "http://tempuri.org/dsMMISDB.xsd");

            var root = doc.SelectSingleNode("//d:dsMMISDB", ns);
            if (root == null)
                throw new Exception("Не найден узел dsMMISDB — проверь файл PLX.");

            var data = new PlxData();

            Console.WriteLine("Парсим уровни образования...");
            data.EducationLvls = ParseEducationLvls(root, ns);

            Console.WriteLine("Парсим направления и профили...");
            ParseSpecialitiesAndProfiles(root, ns, data);

            Console.WriteLine("Парсим учебный план...");
            data.AcademicPlans = ParseAcademicPlans(root, ns, data);

            Console.WriteLine("Парсим кафедры...");
            data.Departments = ParseDepartments(root, ns);

            Console.WriteLine("Парсим виды работ...");
            data.WorkTypes = ParseWorkTypes(root, ns);

            Console.WriteLine("Парсим дисциплины...");
            ParseSubjectsAndDisciplines(root, ns, data);

            Console.WriteLine("Парсим часы по семестрам...");
            data.SubjectSections = ParseSubjectSections(root, ns, data);

            Console.WriteLine($"  Уровней образования   : {data.EducationLvls.Count}");
            Console.WriteLine($"  Направлений           : {data.Specialities.Count}");
            Console.WriteLine($"  Профилей              : {data.Profiles.Count}");
            Console.WriteLine($"  Учебных планов        : {data.AcademicPlans.Count}");
            Console.WriteLine($"  Кафедр                : {data.Departments.Count}");
            Console.WriteLine($"  Видов работ           : {data.WorkTypes.Count}");
            Console.WriteLine($"  Названий дисциплин    : {data.Disciplines.Count}");
            Console.WriteLine($"  Дисциплин             : {data.Subjects.Count}");
            Console.WriteLine($"  Записей SubjectSection: {data.SubjectSections.Count}");

            return data;
        }

        private List<EducationLvl> ParseEducationLvls(XmlNode root, XmlNamespaceManager ns)
        {
            var result = new List<EducationLvl>();
            foreach (XmlNode node in root.SelectNodes("d:УровеньОбразования", ns))
            {
                result.Add(new EducationLvl
                {
                    ID = Int(node, "Код"),
                    Title = Str(node, "Уровень")
                });
            }
            return result;
        }

        private void ParseSpecialitiesAndProfiles(XmlNode root,
            XmlNamespaceManager ns, PlxData data)
        {
            foreach (XmlNode node in root.SelectNodes("//d:ООП", ns))
            {
                bool isChild = node.Attributes["КодРодительскогоООП"] != null;

                if (!isChild)
                {
                    data.Specialities.Add(new Speciality
                    {
                        ID = Int(node, "Код"),
                        EducationLvlID = Int(node, "УровеньОбразования"),
                        Title = Str(node, "Название"),
                        Code = Str(node, "Шифр")
                    });
                }
                else
                {
                    data.Profiles.Add(new Profile
                    {
                        ID = Int(node, "Код"),
                        SpecialityID = Int(node, "КодРодительскогоООП"),
                        Name = Str(node, "Название")
                    });
                }
            }
        }

        private List<AcademicPlan> ParseAcademicPlans(XmlNode root,
            XmlNamespaceManager ns, PlxData data)
        {
            var formDict = new Dictionary<int, string>();
            foreach (XmlNode fn in root.SelectNodes("d:ФормаОбучения", ns))
                formDict[Int(fn, "Код")] = Str(fn, "ФормаОбучения");

            var result = new List<AcademicPlan>();
            foreach (XmlNode node in root.SelectNodes("d:Планы", ns))
            {
                int formCode = Int(node, "КодФормыОбучения");
                string formName = formDict.TryGetValue(formCode, out var f) ? f : "Очная";

                int oopKod = Int(node, "КодООП");
                var profile = data.Profiles.FirstOrDefault(p => p.SpecialityID == oopKod);
                int profileID = profile?.ID ?? 0;

                result.Add(new AcademicPlan
                {
                    ID = Int(node, "Код"),
                    ProfileID = profileID,
                    RecruitmentYear = Int(node, "ГодНачалаПодготовки"),
                    EducationForm = formName,
                    YearsNorm = Int(node, "СрокОбучения")
                });
            }
            return result;
        }

        private List<Department> ParseDepartments(XmlNode root, XmlNamespaceManager ns)
        {
            var result = new List<Department>();
            foreach (XmlNode node in root.SelectNodes("d:Кафедры", ns))
            {
                if (Str(node, "Удалена").ToLower() == "true") continue;

                result.Add(new Department
                {
                    ID = Int(node, "Код"),
                    Number = Int(node, "Номер"),
                    Title = Str(node, "Название")
                });
            }
            return result;
        }

        private List<WorkType> ParseWorkTypes(XmlNode root, XmlNamespaceManager ns)
        {
            var neededCodes = new HashSet<int> { 1, 2, 3, 4, 5, 101, 102, 103, 107 };

            var result = new List<WorkType>();
            foreach (XmlNode node in root.SelectNodes("d:СправочникВидыРабот", ns))
            {
                int id = Int(node, "Код");
                if (!neededCodes.Contains(id)) continue;

                result.Add(new WorkType
                {
                    ID = id,
                    Code = Str(node, "Аббревиатура"),
                    Title = Str(node, "Название")
                });
            }
            return result;
        }

        private void ParseSubjectsAndDisciplines(XmlNode root,
            XmlNamespaceManager ns, PlxData data)
        {
            int planID = data.AcademicPlans.Count > 0
                ? data.AcademicPlans[0].ID
                : 0;

            var disciplineMap = new Dictionary<string, int>();
            int tempId = 1;

            foreach (XmlNode node in root.SelectNodes("d:ПланыСтроки", ns))
            {
                string name = Str(node, "Дисциплина");
                if (string.IsNullOrWhiteSpace(name)) continue;

                int tipOb = Int(node, "ТипОбъекта");
                if (tipOb != 2 && tipOb != 6) continue;

                if (!disciplineMap.ContainsKey(name))
                {
                    disciplineMap[name] = tempId;
                    data.Disciplines.Add(new Discipline { ID = tempId, Name = name });
                    tempId++;
                }

                data.Subjects.Add(new Subject
                {
                    ID = Int(node, "Код"),
                    AcademicPlanID = planID,
                    DepartmentID = Int(node, "КодКафедры"),
                    DisciplineID = disciplineMap[name],
                    Code = Str(node, "ДисциплинаКод"),
                    LabourIntensity = Int(node, "ЧасовПоПлану"),
                    Credits = Decimal(node, "ТрудоемкостьКредитов"),
                    IsOptional = Bool(node, "Факультатив"),
                    IsPhysical = Bool(node, "ПризнакФизкультуры"),
                    IsGradWork = tipOb == 6
                });
            }
        }

        private List<SubjectSection> ParseSubjectSections(XmlNode root,
            XmlNamespaceManager ns, PlxData data)
        {
            var subjectIds = new HashSet<int>(data.Subjects.Select(s => s.ID));
            var dict = new Dictionary<(int, int), SubjectSection>();

            foreach (XmlNode node in root.SelectNodes("d:ПланыНовыеЧасы", ns))
            {
                int subjectId = Int(node, "КодОбъекта");
                int workTypeId = Int(node, "КодВидаРаботы");
                int kurs = Int(node, "Курс");
                int sem = Int(node, "Семестр");
                int qty = Int(node, "Количество");
                int nedeli = Int(node, "Недель");

                if (!subjectIds.Contains(subjectId)) continue;

                int semesterGlobal = (kurs - 1) * 2 + sem;

                var key = (subjectId, semesterGlobal);
                if (!dict.TryGetValue(key, out var section))
                {
                    section = new SubjectSection
                    {
                        SubjectID = subjectId,
                        Semester = semesterGlobal,
                        SemesterWeek = nedeli
                    };
                    dict[key] = section;
                }

                if (section.SemesterWeek == 0 && nedeli > 0)
                    section.SemesterWeek = nedeli;

                switch (workTypeId)
                {
                    case 101: section.Lectures += qty; break;
                    case 102: section.LaboratoryWorks += qty; break;
                    case 103: section.PracticalLessons += qty; break;
                    case 107: section.IndependentWork += qty; break;
                    case 4: section.CourseProject += qty; break;
                    case 5: section.CourseWork += qty; break;
                    case 2:
                    case 3: section.Test = true; break;
                }
            }

            return dict.Values.ToList();
        }

        private string Str(XmlNode node, string attr)
            => node.Attributes?[attr]?.Value ?? string.Empty;

        private int Int(XmlNode node, string attr)
        {
            var val = node.Attributes?[attr]?.Value;
            return int.TryParse(val, out int result) ? result : 0;
        }

        private decimal Decimal(XmlNode node, string attr)
        {
            var val = node.Attributes?[attr]?.Value;
            if (val == null) return 0;
            val = val.Replace(',', '.');
            return decimal.TryParse(val,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal result) ? result : 0;
        }

        private bool Bool(XmlNode node, string attr)
            => Str(node, attr).ToLower() == "true";
    }
}
