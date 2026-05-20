using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YanaulKioskPerfect
{
    public partial class HospitalMapPage : Page, ILocalizable
    {
        // Данные для этажей
        private string _floor1Text;
        private string _floor2Text;
        private string _floor3Text;

        public HospitalMapPage()
        {
            try
            {
                InitializeComponent();
                InitData();
                UpdateLanguage();
                ShowFloorMap(1); // Загружаем 1 этаж по умолчанию
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки карты: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitData()
        {
            // Тексты для 1 этажа
            _floor1Text = "• Кабинет 101 — Регистратура\n" +
                          "• Кабинет 105 — Лаборатория (Анализы крови, мочи)\n" +
                          "• Кабинет 112 — Аптека\n" +
                          "• Кабинет 115 — Приемное отделение\n" +
                          "• Туалет (слева от лестницы)\n" +
                          "• Выход к стоянке автомобилей";

            // Тексты для 2 этажа
            _floor2Text = "• Кабинеты 205–215 — Терапевты\n" +
                          "• Кабинет 220 — Невролог\n" +
                          "• Кабинет 225 — Офтальмолог (Окулист)\n" +
                          "• Кабинет 230 — Дерматолог\n" +
                          "• Процедурный кабинет (210)";

            // Тексты для 3 этажа
            _floor3Text = "• Кабинеты 305–310 — Хирурги\n" +
                          "• Кабинет 315 — Кардиолог\n" +
                          "• Кабинет 320 — Инфекционист\n" +
                          "• Кабинет 325 — Психиатр\n" +
                          "• Малая операционная (308)";
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "БОЛЬНИЦА КАРТАһЫ" : "КАРТА БОЛЬНИЦЫ";

            // Обновляем ComboBox
            if (FloorSelector.Items.Count > 0)
            {
                ((ComboBoxItem)FloorSelector.Items[0]).Content = MainWindow.IsBashkir ? "1-се ҡат" : "1 Этаж";
                ((ComboBoxItem)FloorSelector.Items[1]).Content = MainWindow.IsBashkir ? "2-се ҡат" : "2 Этаж";
                ((ComboBoxItem)FloorSelector.Items[2]).Content = MainWindow.IsBashkir ? "3-сө ҡат" : "3 Этаж";
            }

            // Перерисовываем текущий этаж
            if (FloorSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                string content = selectedItem.Content.ToString();
                if (content.Contains("1")) ShowFloorMap(1);
                else if (content.Contains("2")) ShowFloorMap(2);
                else if (content.Contains("3")) ShowFloorMap(3);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            try { MainWindow.Instance.NavigateToMainMenu(); }
            catch { }
        }

        private void FloorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FloorSelector.SelectedItem is ComboBoxItem item)
            {
                string content = item.Content.ToString();
                if (content.Contains("1") || content.Contains("1-се")) ShowFloorMap(1);
                else if (content.Contains("2") || content.Contains("2-се")) ShowFloorMap(2);
                else if (content.Contains("3") || content.Contains("3-сө")) ShowFloorMap(3);
            }
            // Сброс поиска при смене этажа
            if (SearchBox != null) SearchBox.Text = "";
            if (SearchResultText != null) SearchResultText.Visibility = Visibility.Collapsed;
        }

        private void ShowFloorMap(int floor)
        {
            if (MapTitle == null || MapContent == null || MapBorder == null) return;

            string rawText = "";
            string title = "";

            switch (floor)
            {
                case 1:
                    rawText = _floor1Text;
                    title = MainWindow.IsBashkir ? "1-СЕ ҠАТ" : "1 ЭТАЖ";
                    break;
                case 2:
                    rawText = _floor2Text;
                    title = MainWindow.IsBashkir ? "2-СЕ ҠАТ" : "2 ЭТАЖ";
                    break;
                case 3:
                    rawText = _floor3Text;
                    title = MainWindow.IsBashkir ? "3-СӨ ҠАТ" : "3 ЭТАЖ";
                    break;
            }

            MapTitle.Text = title;
            MapContent.Text = rawText;

            // Сброс цвета фона
            MapBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // #E8F5E9
            if (SearchResultText != null) SearchResultText.Visibility = Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox == null || MapContent == null || MapBorder == null || SearchResultText == null) return;

            string query = SearchBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(query))
            {
                MapContent.Foreground = Brushes.Black;
                MapBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                SearchResultText.Visibility = Visibility.Collapsed;
                return;
            }

            string currentMapText = MapContent.Text.ToLower();

            if (currentMapText.Contains(query))
            {
                // Найдено
                SearchResultText.Text = $"✅ Найдено на этом этаже: \"{query}\"";
                SearchResultText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Зеленый
                MapBorder.Background = new SolidColorBrush(Color.FromRgb(200, 230, 201)); // Темно-зеленый фон
                SearchResultText.Visibility = Visibility.Visible;
            }
            else
            {
                // Не найдено
                SearchResultText.Text = $"❌ На этом этаже не найдено \"{query}\". Попробуйте другой этаж.";
                SearchResultText.Foreground = new SolidColorBrush(Color.FromRgb(211, 47, 47)); // Красный
                MapBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Красноватый фон
                SearchResultText.Visibility = Visibility.Visible;
            }
        }

        private void CategoryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string category)
            {
                string message = "";
                int targetFloor = 1;

                switch (category)
                {
                    case "lab":
                        message = MainWindow.IsBashkir ?
                            "📍 Лаборатория: 1-се ҡат, 105-се бүлмә (08:00-15:00)" :
                            "📍 Лаборатория: 1 этаж, кабинет 105 (08:00-15:00)";
                        targetFloor = 1;
                        break;
                    case "pharmacy":
                        message = MainWindow.IsBashkir ?
                            "📍 Аптека: 1-се ҡат, 112-се бүлмә (08:00-19:00)" :
                            "📍 Аптека: 1 этаж, кабинет 112 (08:00-19:00)";
                        targetFloor = 1;
                        break;
                    case "registry":
                        message = MainWindow.IsBashkir ?
                            "📍 Регистратура: 1-се ҡат, 101-се бүлмә (08:00-20:00)" :
                            "📍 Регистратура: 1 этаж, кабинет 101 (08:00-20:00)";
                        targetFloor = 1;
                        break;
                    case "doctors":
                        message = MainWindow.IsBashkir ?
                            "📍 Докторҙар: 2-се һәм 3-сө ҡаттар" :
                            "📍 Врачи: 2 и 3 этажи";
                        targetFloor = 2;
                        break;
                }

                // Автоматическое переключение этажа
                if (targetFloor == 1) FloorSelector.SelectedIndex = 0;
                else if (targetFloor == 2) FloorSelector.SelectedIndex = 1;
                else FloorSelector.SelectedIndex = 2;

                MessageBox.Show(message, MainWindow.IsBashkir ? "УРЫНЛАШЫУ" : "Расположение",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Подсветка поиска
                if (SearchBox != null)
                {
                    SearchBox.Text = category == "lab" ? "лаборатория" :
                                     category == "pharmacy" ? "аптека" :
                                     category == "registry" ? "регистратура" : "врач";
                }
            }
        }
    }
}