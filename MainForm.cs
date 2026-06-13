using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PlxParser
{
    public partial class MainForm : Form
    {
        private readonly string _connectionString =
            "Server=localhost;Database=AcademicWorkload;" +
            "Trusted_Connection=True;TrustServerCertificate=True;";

        private DatabaseLoader _loader;
        private List<SpecialityItem> _specialities;
        private List<AcademicPlanView> _allPlans;

        public MainForm()
        {
            InitializeComponent();
            _loader = new DatabaseLoader(_connectionString);
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            try
            {
                cmbSpeciality.Items.Clear();
                cmbSpecSettings.Items.Clear();
                cmbGroupProfile.Items.Clear();

                // Загружаем все планы для выбора
                _allPlans = _loader.LoadAllPlans();
                _specialities = _loader.LoadSpecialities();

                foreach (var plan in _allPlans)
                {
                    cmbSpeciality.Items.Add(plan.DisplayName);
                    cmbSpecSettings.Items.Add(plan.DisplayName);
                }

                foreach (var spec in _specialities)
                    cmbGroupProfile.Items.Add($"{spec.Code} — {spec.Title}");

                if (_allPlans.Count > 0)
                {
                    cmbSpeciality.SelectedIndex = 0;
                    cmbSpecSettings.SelectedIndex = 0;
                }

                if (_specialities.Count > 0)
                    cmbGroupProfile.SelectedIndex = 0;

                RefreshStudentGroups();
                LoadPlansList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к БД:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshStudentGroups()
        {
            // Обновляем список групп на вкладке Настройки
            cmbStudentGroup.Items.Clear();
            var allGroups = _loader.LoadAllGroups();
            foreach (var g in allGroups)
                cmbStudentGroup.Items.Add(g.Name);
            if (cmbStudentGroup.Items.Count > 0)
                cmbStudentGroup.SelectedIndex = 0;

            // Обновляем список групп на вкладке Нагрузка
            if (cmbSpeciality.SelectedIndex >= 0 && _specialities != null &&
                cmbSpeciality.SelectedIndex < _specialities.Count)
            {
                var selectedPlan2 = _allPlans[cmbSpeciality.SelectedIndex];
                int profileID = _loader.LoadProfileIDByPlan(selectedPlan2.ID);
                cmbGroup.Items.Clear();
                var groups = _loader.LoadGroups(profileID);
                foreach (var g in groups)
                    cmbGroup.Items.Add(g.Name);
                if (cmbGroup.Items.Count > 0)
                    cmbGroup.SelectedIndex = 0;
            }
        }

        private void cmbSpeciality_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbSpeciality.SelectedIndex < 0 || _allPlans == null ||
                cmbSpeciality.SelectedIndex >= _allPlans.Count) return;

            var plan = _allPlans[cmbSpeciality.SelectedIndex];
            int profileID = _loader.LoadProfileIDByPlan(plan.ID);

            cmbGroup.Items.Clear();
            var groups = _loader.LoadGroups(profileID);
            foreach (var g in groups)
                cmbGroup.Items.Add(g.Name);

            if (cmbGroup.Items.Count > 0)
                cmbGroup.SelectedIndex = 0;
        }

        private void cmbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            int mode = cmbMode.SelectedIndex;

            // Режим 3.1 — курс не нужен, 3.2 и 3.3 — курс нужен
            cmbCourse.Enabled = mode > 0;

            bool is33 = mode == 2;

            // Обновляем список курсов в зависимости от режима
            cmbCourse.Items.Clear();
            if (is33)
                cmbCourse.Items.Add("Весь план");
            cmbCourse.Items.Add("1 курс");
            cmbCourse.Items.Add("2 курс");
            cmbCourse.Items.Add("3 курс");
            cmbCourse.Items.Add("4 курс");
            cmbCourse.SelectedIndex = 0;

            clbSpecialities.Visible = is33;
            lblSelectPlans.Visible = is33;
            cmbSpeciality.Visible = !is33;
            lblSpeciality.Visible = !is33;
            cmbGroup.Visible = !is33;
            lblGroup.Visible = !is33;

            if (is33)
                RefreshSpecialitiesCheckList();
        }

        private void RefreshSpecialitiesCheckList()
        {
            clbSpecialities.Items.Clear();
            if (_allPlans == null) return;
            foreach (var plan in _allPlans)
                clbSpecialities.Items.Add(plan.DisplayName, false);
        }

        private void cmbSpecSettings_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadFormulaParams();
        }

        private void numYearSettings_ValueChanged(object sender, EventArgs e)
        {
            // Год больше не используется - оставлен для совместимости
        }

        private void LoadFormulaParams()
        {
            if (cmbSpecSettings.SelectedIndex < 0) return;

            var plan33 = _allPlans[cmbSpecSettings.SelectedIndex];
            var p = _loader.LoadFormulaParams(plan33.ID);

            numStudentsPerRate.Value = p.StudentsPerRate;
            numHoursCW.Value = p.HoursPerCourseWork;
            numHoursCP.Value = p.HoursPerCourseProj;
            numHoursGW.Value = p.HoursPerGradWork;
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog();
            dialog.Filter = "PLX файлы (*.plx)|*.plx|Все файлы (*.*)|*.*";
            dialog.Title = "Выберите файл учебного плана";

            if (dialog.ShowDialog() == DialogResult.OK)
                txtFilePath.Text = dialog.FileName;
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text))
            {
                MessageBox.Show("Выберите файл PLX для импорта.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!File.Exists(txtFilePath.Text))
            {
                MessageBox.Show("Файл не найден.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            txtLog.Clear();
            btnImport.Enabled = false;

            try
            {
                var logWriter = new RichTextBoxWriter(txtLog);
                Console.SetOut(logWriter);

                var parser = new PlxFileParser(txtFilePath.Text);
                var data = parser.Parse();

                if (data.AcademicPlans.Count > 0)
                    data.AcademicPlans[0].Description = txtDescription.Text.Trim();

                var importer = new DatabaseImporter(_connectionString);
                importer.Import(data);

                _specialities = _loader.LoadSpecialities();
                LoadInitialData();

                Log("", System.Drawing.Color.White);
                Log("Готово! Данные успешно загружены.", System.Drawing.Color.LightGreen);
            }
            catch (Exception ex)
            {
                Log($"Ошибка: {ex.Message}", System.Drawing.Color.Red);
            }
            finally
            {
                btnImport.Enabled = true;
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            }
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            if (cmbSpeciality.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите направление подготовки.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbGroup.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите группу.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var plan = _allPlans[cmbSpeciality.SelectedIndex];
                int planID = plan.ID;
                int profileID = _loader.LoadProfileIDByPlan(planID);

                var groups = _loader.LoadGroups(profileID);
                var group = groups[cmbGroup.SelectedIndex];

                int planYear = plan.RecruitmentYear;
                int studentCount = _loader.LoadStudentCount(group.ID, planYear);
                if (studentCount == 0)
                {
                    MessageBox.Show($"Нет данных о студентах для группы «{group.Name}» за {planYear} год.\nДобавьте данные на вкладке Настройки.",
                        "Нет данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var formulaParams = _loader.LoadFormulaParams(planID);
                var calculator = new FormulaCalculator(formulaParams, studentCount);

                int mode = cmbMode.SelectedIndex;

                if (mode == 2)
                {
                    // 3.3 — несколько направлений для выбранного курса
                    // SelectedIndex 0 = Весь план, 1-4 = курсы
                    bool allCourses33 = cmbCourse.SelectedIndex == 0;
                    int course = cmbCourse.SelectedIndex; // 1=1курс, 2=2курс и тд

                    // Собираем выбранные планы
                    var selectedPlans = new List<AcademicPlanView>();
                    for (int i = 0; i < clbSpecialities.Items.Count; i++)
                        if (clbSpecialities.GetItemChecked(i))
                            selectedPlans.Add(_allPlans[i]);

                    if (selectedPlans.Count == 0)
                    {
                        MessageBox.Show("Выберите хотя бы один план в списке.",
                            "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var planResults = new List<(AcademicPlanView Plan,
                        List<DepartmentResult> DeptResults, double Sp)>();

                    foreach (var selectedPlan in selectedPlans)
                    {
                        int selProfileID = _loader.LoadProfileIDByPlan(selectedPlan.ID);

                        // Загружаем количество студентов для этого плана
                        var specGroups = _loader.LoadGroups(selProfileID);
                        int specStudents = 20; // по умолчанию
                        if (specGroups.Count > 0)
                        {
                            int cnt = _loader.LoadStudentCount(specGroups[0].ID, selectedPlan.RecruitmentYear);
                            if (cnt > 0) specStudents = cnt;
                        }

                        var fp33 = _loader.LoadFormulaParams(selectedPlan.ID);
                        var calc33 = new FormulaCalculator(fp33, specStudents);
                        var discs33 = allCourses33
                            ? _loader.LoadDisciplines(selectedPlan.ID)
                            : _loader.LoadDisciplinesByCourse(selectedPlan.ID, course); // course = 1-4
                        var res33 = calc33.Calculate(discs33);
                        var dept33 = calc33.GroupByDepartment(res33);
                        double sp33 = calc33.CalculateSp(res33);

                        planResults.Add((selectedPlan, dept33, sp33));
                    }

                    if (planResults.Count == 0)
                    {
                        MessageBox.Show("Не найдено планов для выбранных направлений.",
                            "Нет данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    ShowCompareResults(planResults, course);
                    return;
                }

                List<DisciplineResult> disciplines;

                if (mode == 0)
                {
                    // 3.1 — весь план
                    disciplines = _loader.LoadDisciplines(planID);
                }
                else
                {
                    // 3.2 — по курсу одного плана (индекс 0 = 1 курс, 1 = 2 курс и тд)
                    disciplines = _loader.LoadDisciplinesByCourse(planID, cmbCourse.SelectedIndex + 1);
                }

                var results = calculator.Calculate(disciplines);
                double sp = calculator.CalculateSp(results);
                var deptRes = calculator.GroupByDepartment(results);

                ShowResults(results, deptRes, sp);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при расчёте:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowResults(List<CalculationResult> results,
            List<DepartmentResult> deptResults, double sp)
        {
            // Таблица по дисциплинам
            dgvResults.Columns.Clear();
            dgvResults.Rows.Clear();

            dgvResults.Columns.Add("Name", "Дисциплина");
            dgvResults.Columns.Add("Department", "Кафедра");
            dgvResults.Columns.Add("Credits", "ЗЕТ");
            dgvResults.Columns.Add("Physical", "Физкультура");
            dgvResults.Columns.Add("GradWork", "ВКР");
            dgvResults.Columns.Add("CW", "КР");
            dgvResults.Columns.Add("CP", "КП");
            dgvResults.Columns.Add("Si", "Si");

            dgvResults.Columns["Name"].FillWeight = 30;
            dgvResults.Columns["Department"].FillWeight = 25;
            dgvResults.Columns["Si"].FillWeight = 8;

            foreach (var r in results)
            {
                int row = dgvResults.Rows.Add(
                    r.DisciplineName,
                    r.DepartmentName,
                    r.Credits,
                    r.IsPhysical ? "Да" : "Нет",
                    r.IsGradWork ? "Да" : "Нет",
                    r.HasCourseWork ? "Да" : "Нет",
                    r.HasCourseProj ? "Да" : "Нет",
                    r.Si.ToString("F4")
                );

                if (r.IsPhysical)
                    dgvResults.Rows[row].DefaultCellStyle.BackColor =
                        System.Drawing.Color.FromArgb(255, 255, 220);
                if (r.IsGradWork)
                    dgvResults.Rows[row].DefaultCellStyle.BackColor =
                        System.Drawing.Color.FromArgb(220, 240, 255);
            }

            // Таблица по кафедрам
            dgvDepartments.Columns.Clear();
            dgvDepartments.Rows.Clear();

            dgvDepartments.Columns.Add("Dept", "Кафедра");
            dgvDepartments.Columns.Add("Count", "Дисциплин");
            dgvDepartments.Columns.Add("Si", "Ставок (Sп)");

            dgvDepartments.Columns["Dept"].FillWeight = 60;
            dgvDepartments.Columns["Count"].FillWeight = 15;
            dgvDepartments.Columns["Si"].FillWeight = 15;

            foreach (var d in deptResults)
                dgvDepartments.Rows.Add(d.DepartmentName, d.DisciplineCount, d.TotalSi.ToString("F4"));

            lblSpValue.Text = sp.ToString("F4");
        }

        private void btnRefreshPlans_Click(object sender, EventArgs e)
        {
            LoadPlansList();
        }

        private void btnDeletePlan_Click(object sender, EventArgs e)
        {
            if (dgvPlans.SelectedRows.Count == 0)
            {
                MessageBox.Show("Выберите план для удаления.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dgvPlans.SelectedRows[0];
            string planInfo = $"{row.Cells["Direction"].Value} {row.Cells["Year"].Value}г." +
                (row.Cells["Desc"].Value?.ToString() != ""
                    ? $" ({row.Cells["Desc"].Value})" : "");

            var confirm = MessageBox.Show(
                $"Удалить учебный план:\n{planInfo}\n\nВсе дисциплины и часы будут удалены.",
                "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                // Получаем ID плана из тега строки
                int planID = (int)dgvPlans.SelectedRows[0].Tag;
                _loader.DeleteAcademicPlan(planID);
                LoadPlansList();
                LoadInitialData();
                MessageBox.Show("План удалён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadPlansList()
        {
            try
            {
                dgvPlans.Columns.Clear();
                dgvPlans.Rows.Clear();

                dgvPlans.Columns.Add("Direction", "Направление");
                dgvPlans.Columns.Add("Profile", "Профиль");
                dgvPlans.Columns.Add("Year", "Год набора");
                dgvPlans.Columns.Add("Form", "Форма обучения");
                dgvPlans.Columns.Add("Years", "Срок (лет)");
                dgvPlans.Columns.Add("Desc", "Описание");

                var plans = _loader.LoadAcademicPlans();
                foreach (var p in plans)
                {
                    int rowIdx = dgvPlans.Rows.Add(p.SpecialityCode, p.ProfileName,
                        p.RecruitmentYear, p.EducationForm, p.YearsNorm, p.Description);
                    dgvPlans.Rows[rowIdx].Tag = p.ID;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveFormula_Click(object sender, EventArgs e)
        {
            if (cmbSpecSettings.SelectedIndex < 0) return;

            var plan33 = _allPlans[cmbSpecSettings.SelectedIndex];
            var p = new FormulaParams
            {
                StudentsPerRate = (int)numStudentsPerRate.Value,
                HoursPerCourseWork = (int)numHoursCW.Value,
                HoursPerCourseProj = (int)numHoursCP.Value,
                HoursPerGradWork = (int)numHoursGW.Value
            };

            try
            {
                _loader.SaveFormulaParams(plan33.ID, p);
                MessageBox.Show("Параметры формулы сохранены.",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAddGroup_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtGroupName.Text))
            {
                MessageBox.Show("Введите название группы.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbGroupProfile.SelectedIndex < 0) return;

            var spec = _specialities[cmbGroupProfile.SelectedIndex];
            int profileID = _loader.LoadProfileID(spec.ID);

            try
            {
                _loader.SaveGroup(profileID, txtGroupName.Text.Trim());
                txtGroupName.Clear();
                RefreshStudentGroups();
                MessageBox.Show("Группа добавлена.",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDeleteGroup_Click(object sender, EventArgs e)
        {
            if (cmbStudentGroup.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите группу для удаления.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string groupName = cmbStudentGroup.SelectedItem.ToString();
            var confirm = MessageBox.Show(
                $"Удалить группу «{groupName}»?\nВсе данные о студентах будут удалены.",
                "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var groups = _loader.LoadAllGroups();
                var group = groups[cmbStudentGroup.SelectedIndex];
                _loader.DeleteGroup(group.ID);
                RefreshStudentGroups();
                MessageBox.Show("Группа удалена.", "Готово",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveStudents_Click(object sender, EventArgs e)
        {
            if (cmbStudentGroup.SelectedIndex < 0)
            {
                MessageBox.Show("Выберите группу.",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var groups = _loader.LoadAllGroups();
                var group = groups[cmbStudentGroup.SelectedIndex];

                _loader.SaveStudentCount(group.ID, (int)numStudentYear.Value, (int)numStudentCount.Value);
                MessageBox.Show("Данные сохранены.",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowCompareResults(
            List<(AcademicPlanView Plan, List<DepartmentResult> DeptResults, double Sp)> planResults,
            int course)
        {
            // Очищаем таблицу дисциплин — покажем сводку по планам
            dgvResults.Columns.Clear();
            dgvResults.Rows.Clear();

            dgvResults.Columns.Add("Plan", "Учебный план");
            string courseLabel = course == 0 ? "весь план" : $"курс {course}";
            dgvResults.Columns.Add("Sp", "Sп (" + courseLabel + ")");
            dgvResults.Columns.Add("Dept", "Кафедр в расчёте");
            dgvResults.Columns["Plan"].FillWeight = 60;
            dgvResults.Columns["Sp"].FillWeight = 20;
            dgvResults.Columns["Dept"].FillWeight = 20;

            foreach (var (plan, dept, sp) in planResults)
            {
                string planName = $"{plan.SpecialityCode} {plan.RecruitmentYear}г." +
                    (string.IsNullOrEmpty(plan.Description) ? "" : $" ({plan.Description})");
                dgvResults.Rows.Add(planName, sp.ToString("F4"), dept.Count);
            }

            // Таблица по кафедрам — объединяем все планы
            dgvDepartments.Columns.Clear();
            dgvDepartments.Rows.Clear();

            dgvDepartments.Columns.Add("Dept", "Кафедра");
            foreach (var (plan, _, _) in planResults)
            {
                string col = $"{plan.SpecialityCode}\n{plan.RecruitmentYear}г.";
                dgvDepartments.Columns.Add($"Plan_{plan.ID}", col);
            }
            dgvDepartments.Columns["Dept"].FillWeight = 40;

            // Собираем все уникальные кафедры
            var allDepts = new HashSet<string>();
            foreach (var (_, dept, _) in planResults)
                foreach (var d in dept)
                    allDepts.Add(d.DepartmentName);

            foreach (var deptName in allDepts)
            {
                var row = new List<object> { deptName };
                foreach (var (_, dept, _) in planResults)
                {
                    var d = dept.FirstOrDefault(x => x.DepartmentName == deptName);
                    row.Add(d != null ? d.TotalSi.ToString("F4") : "—");
                }
                dgvDepartments.Rows.Add(row.ToArray());
            }

            // Итоговая строка Sп
            var totalRow = new List<object> { "ИТОГО Sп" };
            foreach (var (_, _, sp) in planResults)
                totalRow.Add(sp.ToString("F4"));
            int lastRow = dgvDepartments.Rows.Add(totalRow.ToArray());
            dgvDepartments.Rows[lastRow].DefaultCellStyle.Font =
                new System.Drawing.Font(dgvDepartments.Font, System.Drawing.FontStyle.Bold);

            // Показываем суммарный Sп первого плана в метке
            lblSpValue.Text = planResults.Count > 0
                ? string.Join(" / ", planResults.Select(p => p.Sp.ToString("F4")))
                : "—";
        }

        private void Log(string text, System.Drawing.Color color)
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color;
            txtLog.AppendText(text + "\n");
            txtLog.ScrollToCaret();
        }
    }

    public class RichTextBoxWriter : TextWriter
    {
        private readonly RichTextBox _box;

        public RichTextBoxWriter(RichTextBox box) => _box = box;

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void WriteLine(string value)
        {
            if (_box.InvokeRequired)
                _box.Invoke(new Action(() => AppendLine(value)));
            else
                AppendLine(value);
        }

        public override void Write(string value)
        {
            if (_box.InvokeRequired)
                _box.Invoke(new Action(() => _box.AppendText(value)));
            else
                _box.AppendText(value);
        }

        private void AppendLine(string value)
        {
            _box.SelectionColor = System.Drawing.Color.FromArgb(200, 255, 200);
            _box.AppendText(value + "\n");
            _box.ScrollToCaret();
        }
    }
}