using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YanaulKioskPerfect.Helpers;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public class DoctorComboItem
    {
        public int DoctorId { get; set; }
        public string DisplayName { get; set; }
        public decimal Rating { get; set; }
    }

    public partial class FeedbackFormPage : Page, ILocalizable
    {
        private readonly FeedbackService _feedbackService = new FeedbackService();
        private int _selectedRating = 5;
        private List<DoctorComboItem> _doctors = new List<DoctorComboItem>();

        public FeedbackFormPage()
        {
            InitializeComponent();
            BuildStarButtons();
            LoadDoctors();
            LoadDoctorsRatingBoard();
            UpdateLanguage();
        }

        private void BuildStarButtons()
        {
            StarButtonsPanel.Children.Clear();
            for (int i = 1; i <= 5; i++)
            {
                int star = i;
                var btn = new Button
                {
                    Content = "★",
                    FontSize = 36,
                    Width = 52,
                    Height = 52,
                    Margin = new Thickness(4),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Brushes.Gold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = star
                };
                btn.Click += (s, e) => SetRating(star);
                StarButtonsPanel.Children.Add(btn);
            }
            UpdateStarVisuals();
        }

        private void SetRating(int rating)
        {
            _selectedRating = rating;
            UpdateStarVisuals();
            RatingHint.Text = $"{RatingHelper.StarsText(rating)} — {GetRatingWord(rating)}";
        }

        private string GetRatingWord(int r)
        {
            switch (r)
            {
                case 1: return "Плохо";
                case 2: return "Неудовлетворительно";
                case 3: return "Удовлетворительно";
                case 4: return "Хорошо";
                default: return "Отлично!";
            }
        }

        private void UpdateStarVisuals()
        {
            int i = 1;
            foreach (Button btn in StarButtonsPanel.Children)
            {
                btn.Foreground = i <= _selectedRating ? Brushes.Gold : Brushes.LightGray;
                i++;
            }
        }

        private void LoadDoctors()
        {
            _doctors.Clear();
            DoctorCombo.Items.Clear();
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    foreach (var doc in db.Doctors.OrderByDescending(d => d.Rating).ToList())
                    {
                        var item = new DoctorComboItem
                        {
                            DoctorId = doc.DoctorId,
                            DisplayName = (MainWindow.IsBashkir ? doc.FullNameBA : doc.FullNameRU)
                                + $" — {doc.SpecialtyRU} ({doc.Rating:F1}★)",
                            Rating = doc.Rating ?? 4.5m
                        };
                        _doctors.Add(item);
                        DoctorCombo.Items.Add(item);
                    }
                }
                if (DoctorCombo.Items.Count > 0)
                {
                    DoctorCombo.SelectedIndex = 0;
                    DoctorCombo.DisplayMemberPath = "DisplayName";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки врачей: " + ex.Message);
            }
        }

        private void LoadDoctorsRatingBoard()
        {
            DoctorsRatingList.Items.Clear();
            try
            {
                foreach (var doc in _feedbackService.GetDoctorsWithReviews())
                {
                    var card = new Border
                    {
                        Background = Brushes.White,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(12),
                        Margin = new Thickness(0, 0, 0, 10),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                        BorderThickness = new Thickness(1)
                    };

                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock
                    {
                        Text = MainWindow.IsBashkir ? doc.FullNameBA : doc.FullNameRU,
                        FontWeight = FontWeights.Bold,
                        FontSize = 15
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"{doc.SpecialtyRU} • каб. {doc.Room}",
                        FontSize = 12,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(0, 2, 0, 6)
                    });
                    stack.Children.Add(RatingHelper.CreateStarPanel(doc.Rating, 18, true));
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"Отзывов: {doc.ReviewCount}",
                        FontSize = 11,
                        Foreground = Brushes.DarkGray,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                    if (!string.IsNullOrWhiteSpace(doc.LastReview))
                    {
                        var preview = doc.LastReview.Length > 80 ? doc.LastReview.Substring(0, 80) + "…" : doc.LastReview;
                        stack.Children.Add(new TextBlock
                        {
                            Text = $"«{preview}»",
                            FontSize = 11,
                            FontStyle = FontStyles.Italic,
                            Foreground = Brushes.DimGray,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 4, 0, 0)
                        });
                    }

                    card.Child = stack;
                    DoctorsRatingList.Items.Add(card);
                }
            }
            catch { }
        }

        private void DoctorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        public void UpdateLanguage()
        {
            TitleText.Text = MainWindow.IsBashkir ? "ДОКТОРЛАРҒА ФЕКЕР" : "ОТЗЫВЫ О ВРАЧАХ";
            DoctorLabel.Text = MainWindow.IsBashkir ? "Доктор һайлағыҙ:" : "Выберите врача:";
            RatingLabel.Text = MainWindow.IsBashkir ? "Баһа:" : "Ваша оценка:";
            CommentLabel.Text = MainWindow.IsBashkir ? "Фекер:" : "Текст отзыва:";
            SubmitBtn.Content = MainWindow.IsBashkir ? "⭐ ЕБӘРЕҮ" : "⭐ ОТПРАВИТЬ ОТЗЫВ";
            LoadDoctors();
            LoadDoctorsRatingBoard();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.NavigateToMainMenu();

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (DoctorCombo.SelectedItem == null)
            {
                MessageBox.Show("Выберите врача из списка.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var doc = DoctorCombo.SelectedItem as DoctorComboItem;
            if (doc == null)
            {
                MessageBox.Show("Выберите врача из списка.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CommentBox.Text))
            {
                MessageBox.Show(MainWindow.IsBashkir ? "Фекер яҙығыҙ!" : "Напишите текст отзыва.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string policy = ChatService.NormalizePolicy(MainWindow.CurrentPatientPolicy);
            if (string.IsNullOrWhiteSpace(policy))
            {
                MessageBox.Show(
                    "Для отзыва войдите по номеру полиса на главном экране (или используйте быстрый доступ).",
                    "Идентификация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                MainWindow.Instance?.NavigateToIdentification();
                return;
            }

            string patientName = MainWindow.CurrentPatient?.PatientName ?? "Пациент";

            try
            {
                DatabaseHelper.EnsureGuestPatient();
                _feedbackService.SaveFeedback(doc.DoctorId, policy, patientName, _selectedRating, CommentBox.Text.Trim());
                MessageBox.Show(
                    MainWindow.IsBashkir ? "Рәхмәт! Фекерегеҙ ҡабул ителде." : $"Спасибо! Оценка {RatingHelper.StarsText(_selectedRating)} сохранена.\nРейтинг врача обновлён.",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                CommentBox.Clear();
                LoadDoctors();
                LoadDoctorsRatingBoard();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
