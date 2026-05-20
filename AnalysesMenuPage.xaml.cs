using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class AnalysesMenuPage : Page, ILocalizable
    {
        public AnalysesMenuPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadAnalyses();
            UpdateLanguage();
        }

        private void LoadAnalyses()
        {
            var displayData = MainWindow.AnalysisTypes
                .Where(a => a.IsActive == true)
                .Select(a => new
                {
                    NameRU = MainWindow.IsBashkir ? a.NameBA : a.NameRU,
                    Preparation = string.IsNullOrWhiteSpace(a.Preparation) ? "Уточните в регистратуре" : a.Preparation,
                    Price = a.Price,
                    AnalysisId = a.AnalysisId,
                    Days = a.DaysToResult
                })
                .ToList();

            AnalysesList.ItemsSource = displayData;
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "ЛАБОРАТОРИЯҺЕҢДЕ ТАҺЛИЛДӘР" : "ЛАБОРАТОРИЯ И АНАЛИЗЫ";
            LoadAnalyses();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.NavigateToMainMenu();

        private void BtnBookAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || btn.Tag == null) return;

            int analysisId = Convert.ToInt32(btn.Tag);
            string policy = MainWindow.CurrentPatientPolicy;
            if (string.IsNullOrWhiteSpace(policy))
            {
                MessageBox.Show("Войдите по полису для заказа анализа.", "Вход", MessageBoxButton.OK, MessageBoxImage.Information);
                MainWindow.Instance.NavigateToIdentification();
                return;
            }

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var type = db.AnalysisTypes.Find(analysisId);
                    if (type == null) return;

                    db.AnalysisResults.Add(new AnalysisResult
                    {
                        PolicyNumber = policy,
                        AnalysisId = analysisId,
                        OrderDate = DateTime.Now,
                        ResultDate = DateTime.Now.AddDays(type.DaysToResult > 0 ? type.DaysToResult : 1),
                        StatusRU = "Заказан",
                        StatusBA = "Заказлан",
                        Details = $"Заказ через киоск. Стоимость: {type.Price:N0} ₽. {type.Preparation}",
                        DoctorComment = null
                    });

                    db.Notifications.Add(new Notification
                    {
                        PatientPolicy = policy,
                        Message = $"🧪 Заказан анализ: {type.NameRU}. Ожидайте приглашения в лабораторию.",
                        Type = "анализ",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    db.SaveChanges();
                }

                MessageBox.Show(
                    "✅ Анализ заказан!\n\nЗаявка сохранена в медицинской карте.\nРезультаты появятся в разделе «Мои результаты».",
                    "Лаборатория",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка заказа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMyResults_Click(object sender, RoutedEventArgs e)
        {
            string policy = MainWindow.CurrentPatientPolicy;
            if (string.IsNullOrEmpty(policy))
            {
                MessageBox.Show("Сначала войдите по полису.", "Результаты", MessageBoxButton.OK, MessageBoxImage.Information);
                MainWindow.Instance.NavigateToIdentification();
                return;
            }
            NavigationService?.Navigate(new AnalysisResultsPage(policy));
        }

        private void BtnSearchResults_Click(object sender, RoutedEventArgs e)
        {
            string policy = PolicySearchBox?.Text?.Trim() ?? "";
            if (policy.Length < 10)
            {
                MessageBox.Show("Введите номер полиса (16 цифр) или используйте «Мои результаты».", "Поиск",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            policy = new string(policy.Where(char.IsDigit).ToArray());
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    int count = db.AnalysisResults.Count(a => a.PolicyNumber == policy);
                    if (count == 0)
                    {
                        ResultInfo.Text = $"По полису {policy} результатов не найдено.";
                        return;
                    }
                    NavigationService?.Navigate(new AnalysisResultsPage(policy));
                }
            }
            catch (Exception ex)
            {
                ResultInfo.Text = "Ошибка: " + ex.Message;
            }
        }
    }
}
