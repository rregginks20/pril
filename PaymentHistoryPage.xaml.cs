using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class PaymentHistoryPage : Page, ILocalizable
    {
        private readonly string _policy;

        public PaymentHistoryPage(string policyNumber)
        {
            InitializeComponent();
            _policy = policyNumber ?? "";
            LoadPayments();
            UpdateLanguage();
        }

        private void LoadPayments()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var payments = db.Payments
                        .Where(p => p.PolicyNumber == _policy)
                        .OrderByDescending(p => p.PaymentDate)
                        .ToList();

                    PaymentsList.ItemsSource = payments;

                    decimal total = payments.Sum(p => p.Amount);
                    SummaryText.Text = MainWindow.IsBashkir
                        ? $"Барлығы операциялар: {payments.Count}"
                        : $"Всего операций: {payments.Count}";
                    TotalSumText.Text = MainWindow.IsBashkir
                        ? $"Сумма: {total:N0} ₽"
                        : $"Сумма: {total:N0} ₽";

                    EmptyText.Visibility = payments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    PaymentsList.Visibility = payments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки платежей: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                MainWindow.Instance?.NavigateToMedicalCard();
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "💳 Түләү тарихы" : "💳 История платежей";
        }
    }
}
