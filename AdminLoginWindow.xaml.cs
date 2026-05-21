using System;
using System.Linq;
using System.Windows;

namespace YanaulKioskPerfect
{
    public partial class AdminLoginWindow : Window
    {
        public AdminLoginWindow() { InitializeComponent(); }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = LoginBox.Text.Trim();
                string pass = PasswordBox.Password;

                // Отладочная информация
                System.Diagnostics.Debug.WriteLine($"Попытка входа: логин='{login}', пароль длина={pass?.Length ?? 0}");

                if (string.IsNullOrEmpty(login))
                {
                    MessageBox.Show("Введите логин", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoginBox.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(pass))
                {
                    MessageBox.Show("Введите пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordBox.Focus();
                    return;
                }

                using (var db = new YanaulCRBEntities2())
                {
                    // Проверяем подключение к БД
                    var testConn = db.Database.Connection;
                    if (testConn == null)
                    {
                        MessageBox.Show("Ошибка: подключение к базе данных не настроено.", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Проверяем, есть ли вообще пользователи в базе
                    var usersCount = db.Users.Count();
                    System.Diagnostics.Debug.WriteLine($"Пользователей в БД: {usersCount}");
                    
                    if (usersCount == 0)
                    {
                        MessageBox.Show("База данных пользователей пуста!\n\nДобавьте администратора через БД.\nТаблица: Users", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Выводим список логинов для отладки
                    var allLogins = db.Users.Select(u => u.Username).ToList();
                    System.Diagnostics.Debug.WriteLine($"Доступные логины: {string.Join(", ", allLogins)}");

                    // Ищем пользователя - пробуем разные варианты сравнения
                    var user = db.Users.FirstOrDefault(u => u.Username == login);
                    
                    if (user == null)
                    {
                        MessageBox.Show($"Пользователь '{login}' не найден!\n\nДоступные логины: {string.Join(", ", allLogins)}", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                        PasswordBox.Clear();
                        return;
                    }

                    // Проверяем пароль
                    System.Diagnostics.Debug.WriteLine($"Найден пользователь. Role={user.Role}, PasswordHash={(user.PasswordHash == null ? "NULL" : "есть")}");
                    
                    if (user.PasswordHash == null)
                    {
                        MessageBox.Show($"У пользователя '{login}' не установлен пароль в базе данных!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        PasswordBox.Clear();
                        return;
                    }

                    // Сравниваем пароли
                    if (user.PasswordHash == pass)
                    {
                        System.Diagnostics.Debug.WriteLine("Пароль совпал!");
                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Пароль не совпал. Введено: '{pass}', В БД: '{user.PasswordHash}'");
                        MessageBox.Show("Неверный пароль!\n\nПроверьте раскладку клавиатуры и регистр символов.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                        PasswordBox.Clear();
                    }
                }
            }
            catch (System.Data.Entity.Core.EntityException dbEx)
            {
                MessageBox.Show($"Ошибка подключения к базе данных:\n{dbEx.Message}\n\nВнутренняя ошибка: {dbEx.InnerException?.Message ?? \"нет деталей\"}\n\nПроверьте:\n1. Запущен ли SQL Server\n2. Существует ли база YanaulCRB\n3. Есть ли таблица Users", 
                    "Критическая ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"КРИТИЧЕСКАЯ ОШИБКА ПРИ ВХОДЕ:\n\nТип: {ex.GetType().Name}\nСообщение: {ex.Message}\n\nДетали: {ex.InnerException?.Message ?? \"нет внутренних ошибок\"}\n\nСтек trace:\n{ex.StackTrace}", 
                    "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}