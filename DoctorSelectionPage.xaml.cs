using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YanaulKioskPerfect
{
    public partial class DoctorSelectionPage : Page, ILocalizable
    {
        private string _specialty;
        private List<Doctor> _doctorsList;

        public DoctorSelectionPage(string specialty)
        {
            InitializeComponent();
            _specialty = specialty;
            UpdateLanguage();
            LoadDoctors();
        }

        private void LoadDoctors()
        {
            if (DoctorsPanel == null) return;
            DoctorsPanel.Children.Clear();

            if (SubtitleText != null)
            {
                SubtitleText.Text = MainWindow.IsBashkir ?
                    "Белгеслек: " + _specialty :
                    "Специальность: " + _specialty;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"\n=== ЗАГРУЗКА ВРАЧЕЙ ===");
                System.Diagnostics.Debug.WriteLine($"Специальность: {_specialty}");
                System.Diagnostics.Debug.WriteLine($"Всего врачей в базе: {MainWindow.Doctors.Count}");

                _doctorsList = MainWindow.Doctors
                    .Where(d =>
                        (!string.IsNullOrEmpty(d.SpecialtyRU) &&
                         d.SpecialtyRU.Trim().Equals(_specialty.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(d.SpecialtyBA) &&
                         d.SpecialtyBA.Trim().Equals(_specialty.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(d => d.Rating)
                    .ThenByDescending(d => d.ExperienceYears)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Найдено по специальности: {_doctorsList.Count}");

                if (_doctorsList.Count == 0)
                {
                    ShowNoDoctorsMessage();
                    return;
                }

                foreach (var doctor in _doctorsList)
                {
                    var card = CreateDoctorCard(doctor);
                    DoctorsPanel.Children.Add(card);
                    System.Diagnostics.Debug.WriteLine($"✓ Добавлен: {doctor.FullNameRU} (IsOnline={doctor.IsOnline})");
                }

                System.Diagnostics.Debug.WriteLine($"✓ Всего карточек: {DoctorsPanel.Children.Count}\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.ToString());
            }
        }

        private void ShowNoDoctorsMessage()
        {
            var messagePanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(40, 40, 40, 40)
            };

            messagePanel.Children.Add(new TextBlock
            {
                Text = "🔍",
                FontSize = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            messagePanel.Children.Add(new TextBlock
            {
                Text = "Врачи этой специальности временно не ведут прием.\nПопробуйте выбрать другую специальность.",
                FontSize = 20,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            });

            DoctorsPanel.Children.Add(messagePanel);
        }

        private Border CreateDoctorCard(Doctor doctor)
        {
            var card = new Border
            {
                Style = (Style)Application.Current.Resources["CardStyle"],
                Width = 400,
                Height = 620,
                Margin = new Thickness(20, 20, 20, 20),
                Background = Brushes.White,
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 5,
                    Opacity = 0.25
                }
            };

            string name = MainWindow.IsBashkir && !string.IsNullOrEmpty(doctor.FullNameBA)
                ? doctor.FullNameBA : doctor.FullNameRU;

            string spec = MainWindow.IsBashkir && !string.IsNullOrEmpty(doctor.SpecialtyBA)
                ? doctor.SpecialtyBA : doctor.SpecialtyRU;

            string room = doctor.Room ?? "—";
            string category = doctor.Category ?? "—";
            int experience = doctor.ExperienceYears ?? 0;
            string education = doctor.Education ?? "—";
            decimal rating = doctor.Rating ?? 0;
            bool isOnline = doctor.IsOnline ?? false;
            string photoUrl = doctor.PhotoUrl;

            var mainStack = new StackPanel { Margin = new Thickness(25, 25, 25, 25) };

            // ФОТО
            var photoElement = CreateDoctorPhotoWithPlaceholder(photoUrl, name);
            mainStack.Children.Add(photoElement);

            // ИМЯ
            mainStack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 105, 92)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 15, 0, 5)
            });

            // СПЕЦИАЛЬНОСТЬ
            mainStack.Children.Add(new TextBlock
            {
                Text = spec,
                FontSize = 17,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // ИНФОРМАЦИЯ
            mainStack.Children.Add(CreateInfoRow("🚪", "Кабинет:", room, Colors.Teal));
            mainStack.Children.Add(CreateInfoRow("📋", "Категория:", category, Colors.Gray));
            mainStack.Children.Add(CreateInfoRow("⏱️", "Стаж:", experience + " лет", Colors.Gray));

            if (!string.IsNullOrEmpty(education) && education != "—")
            {
                mainStack.Children.Add(new TextBlock
                {
                    Text = "🎓 " + education,
                    FontSize = 13,
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                });
            }

            // РЕЙТИНГ
            mainStack.Children.Add(CreateRatingDisplay(rating));

            // СТАТУС И КНОПКА
            if (isOnline)
            {
                // СВОБОДЕН — ЗЕЛЁНАЯ КНОПКА
                var statusBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    CornerRadius = new CornerRadius(15),
                    Padding = new Thickness(20, 8, 20, 8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 15)
                };
                statusBorder.Child = new TextBlock
                {
                    Text = "✅ Свободен для записи",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                mainStack.Children.Add(statusBorder);

                // КНОПКА ЗАПИСИ
                var btn = new Button
                {
                    Content = "📅 ЗАПИСАТЬСЯ НА ПРИЕМ",
                    Background = new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                    Foreground = Brushes.White,
                    Height = 60,
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Cursor = Cursors.Hand,
                    Tag = doctor,
                    Margin = new Thickness(0, 10, 0, 10),
                    Visibility = Visibility.Visible
                };

                btn.Click += (s, e) => BookButton_Click(doctor);
                btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(0, 137, 123));
                btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromRgb(0, 150, 136));

                mainStack.Children.Add(btn);
            }
            else
            {
                // ЗАНЯТ — СЕРЫЙ С ПОЯСНЕНИЕМ
                var statusBorder = new Border
                {
                    Background = Brushes.Gray,
                    CornerRadius = new CornerRadius(15),
                    Padding = new Thickness(20, 8, 20, 8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 15)
                };
                statusBorder.Child = new TextBlock
                {
                    Text = "⏸️ Временно не принимает",
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                mainStack.Children.Add(statusBorder);

                mainStack.Children.Add(new TextBlock
                {
                    Text = "Следующий прием:\nЗавтра в 09:00",
                    FontSize = 14,
                    Foreground = Brushes.Orange,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 5, 0, 15)
                });
            }

            card.Child = mainStack;
            return card;
        }

        private UIElement CreateDoctorPhotoWithPlaceholder(string photoUrl, string doctorName)
        {
            var photoBorder = new Border
            {
                Width = 140,
                Height = 140,
                CornerRadius = new CornerRadius(70),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0, 150, 136),
                    Color.FromRgb(0, 188, 212), 135),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(4, 4, 4, 4),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            if (!string.IsNullOrEmpty(photoUrl))
            {
                try
                {
                    var image = new Image { Width = 140, Height = 140, Stretch = Stretch.UniformToFill };
                    if (photoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        image.Source = new BitmapImage(new Uri(photoUrl));
                    }
                    else
                    {
                        string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoUrl.TrimStart('/', '\\'));
                        if (System.IO.File.Exists(fullPath))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(fullPath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            image.Source = bitmap;
                        }
                    }
                    if (image.Source != null)
                    {
                        photoBorder.Child = image;
                        return photoBorder;
                    }
                }
                catch { }
            }

            string initials = GetInitials(doctorName);
            photoBorder.Child = new TextBlock
            {
                Text = initials,
                FontSize = 44,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return photoBorder;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "ВР";
            var parts = name.Split(' ');
            if (parts.Length >= 2)
                return parts[0].Substring(0, 1).ToUpper() + parts[1].Substring(0, 1).ToUpper();
            return name.Substring(0, Math.Min(2, name.Length)).ToUpper();
        }

        private StackPanel CreateInfoRow(string icon, string label, string value, Color color)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 3)
            };
            stack.Children.Add(new TextBlock { Text = icon + " ", FontSize = 15 });
            stack.Children.Add(new TextBlock { Text = label + " ", FontSize = 15, Foreground = Brushes.Gray });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 15,
                Foreground = new SolidColorBrush(color),
                FontWeight = FontWeights.SemiBold
            });
            return stack;
        }

        private StackPanel CreateRatingDisplay(decimal rating)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            panel.Children.Add(new TextBlock { Text = "⭐ ", FontSize = 18 });
            for (int i = 1; i <= 5; i++)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = i <= rating ? "★" : "☆",
                    FontSize = 20,
                    Foreground = i <= rating ? Brushes.Gold : Brushes.LightGray,
                    Margin = new Thickness(1, 0, 0, 0)
                });
            }
            panel.Children.Add(new TextBlock
            {
                Text = " (" + rating.ToString("F1") + ")",
                FontSize = 16,
                Foreground = Brushes.Gray,
                Margin = new Thickness(8, 0, 0, 0)
            });
            return panel;
        }

        private void BookButton_Click(Doctor doctor)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"\n=== КНОПКА НАЖАТА ===");
                System.Diagnostics.Debug.WriteLine($"Врач: {doctor.FullNameRU}");
                System.Diagnostics.Debug.WriteLine($"ID: {doctor.DoctorId}");
                System.Diagnostics.Debug.WriteLine($"IsOnline: {doctor.IsOnline}\n");

                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.NavigateToTimeSelection(doctor);
                }
                else
                {
                    MessageBox.Show("MainWindow не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.ToString());
            }
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "ДОКТОР ҺАЙЛАУ" : "ВЫБОР ВРАЧА";
            if (BtnBack != null)
                BtnBack.Content = MainWindow.IsBashkir ? "⇦ Кире" : "⇦ Назад";
            if (DoctorsPanel != null)
                LoadDoctors();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
                MainWindow.Instance.NavigateToSpecialties();
        }
    }
}