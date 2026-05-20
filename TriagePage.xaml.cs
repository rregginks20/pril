using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class TriagePage : Page
    {
        public TriagePage()
        {
            InitializeComponent();
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            string input = TxtSymptoms.Text.ToLower();
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Пожалуйста, опишите симптомы.");
                return;
            }
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var rule = db.SymptomRules
                        .FirstOrDefault(r => r.IsActive == true && input.Contains(r.Keyword.ToLower()));

                    if (rule != null)
                    {
                        var doctor = db.Doctors
                            .Where(d => d.SpecialtyRU == rule.RecommendedSpecialty)
                            .OrderByDescending(d => d.Rating)
                            .FirstOrDefault();

                        var record = new TriageRecord
                        {
                            UserInput = TxtSymptoms.Text,
                            DetectedSymptoms = rule.Keyword,
                            RecommendedDoctorSpecialty = rule.RecommendedSpecialty,
                            RecommendedDoctorId = doctor?.DoctorId,
                            CreatedAt = DateTime.Now
                        };
                        db.TriageRecords.Add(record);
                        db.SaveChanges();

                        LblRecommendation.Text = $"Рекомендуемый специалист: {rule.RecommendedSpecialty}";
                        LblDoctorName.Text = doctor != null
                            ? $"Свободен врач: {doctor.FullNameRU} (Кабинет {doctor.Room})"
                            : "К сожалению, сейчас нет свободных врачей этой специальности.";

                        ResultBox.Tag = doctor;
                        ResultBox.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        MessageBox.Show("Не удалось определить специалиста. Выберите вручную в меню записи.", "Инфо");
                        ResultBox.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка анализа: " + ex.Message);
            }
        }

        private void BtnBook_Click(object sender, RoutedEventArgs e)
        {
            if (ResultBox.Tag is Doctor doc)
            {
                MainWindow.Instance.NavigateToTimeSelection(doc);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack) NavigationService.GoBack();
            else MainWindow.Instance.NavigateToMainMenu();
        }
    }
}