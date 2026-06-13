using System;
using System.Collections.Generic;
using System.Linq;

namespace PlxParser
{
    public class CalculationResult
    {
        public string DisciplineName { get; set; }
        public string DepartmentName { get; set; }
        public decimal Credits { get; set; }
        public bool IsPhysical { get; set; }
        public bool IsGradWork { get; set; }
        public bool HasCourseWork { get; set; }
        public bool HasCourseProj { get; set; }
        public double Si { get; set; }
    }

    public class DepartmentResult
    {
        public string DepartmentName { get; set; }
        public double TotalSi { get; set; }
        public int DisciplineCount { get; set; }
    }

    public class FormulaCalculator
    {
        private readonly FormulaParams _params;
        private readonly int _studentCount;

        public FormulaCalculator(FormulaParams formulaParams, int studentCount)
        {
            _params = formulaParams;
            _studentCount = studentCount;
        }

        private double ComputeSi(decimal credits, bool isPhysical, bool isGradWork,
            bool hasCourseWork, bool hasCourseProj)
        {
            double k = (double)credits;
            double x = _studentCount;
            double Xst = _params.StudentsPerRate;

            double Ckr = hasCourseWork ? 1 : 0;
            double Ckp = hasCourseProj ? 1 : 0;
            double Cvkr = isGradWork ? 1 : 0;
            double Cf = isPhysical ? 1 : 0;

            double hkr = _params.HoursPerCourseWork;
            double hkp = _params.HoursPerCourseProj;
            double hvkr = _params.HoursPerGradWork;
            double hf = 2;

            double numerator = 36.0 * k
                + Cf * hf
                + x * (Ckr * hkr + Ckp * hkp + Cvkr * hvkr);

            double denominator = 36.0 * 60
                + Cf * hf
                + x * (Ckr * hkr + Ckp * hkp + Cvkr * hvkr);

            if (denominator == 0 || Xst == 0) return 0;

            return Math.Round(numerator / denominator * (x / Xst), 4);
        }

        public List<CalculationResult> Calculate(List<DisciplineResult> disciplines)
        {
            var results = new List<CalculationResult>();
            foreach (var d in disciplines)
            {
                bool hasCW = d.CourseWork > 0;
                bool hasCP = d.CourseProj > 0;

                results.Add(new CalculationResult
                {
                    DisciplineName = d.Name,
                    DepartmentName = d.DepartmentName,
                    Credits = d.Credits,
                    IsPhysical = d.IsPhysical,
                    IsGradWork = d.IsGradWork,
                    HasCourseWork = hasCW,
                    HasCourseProj = hasCP,
                    Si = ComputeSi(d.Credits, d.IsPhysical, d.IsGradWork, hasCW, hasCP)
                });
            }
            return results;
        }

        public double CalculateSp(List<CalculationResult> results)
        {
            return Math.Round(results.Sum(r => r.Si), 4);
        }

        public List<DepartmentResult> GroupByDepartment(List<CalculationResult> results)
        {
            return results
                .GroupBy(r => r.DepartmentName)
                .Select(g => new DepartmentResult
                {
                    DepartmentName = g.Key,
                    TotalSi = Math.Round(g.Sum(r => r.Si), 4),
                    DisciplineCount = g.Count()
                })
                .OrderByDescending(d => d.TotalSi)
                .ToList();
        }
    }
}
