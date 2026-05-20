using System.Linq;
using System.Windows;

namespace YanaulKioskPerfect
{
    public partial class AdminLoginWindow : Window
    {
        public AdminLoginWindow() { InitializeComponent(); }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text.Trim();
            string pass = PasswordBox.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Введите логин и пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var db = new 
                
                YanaulCRBEntities2())
            {
                var user = db.Users.FirstOrDefault(u => u.Username == login && u.PasswordHash == pass);
                if (user != null)
                {
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                    PasswordBox.Clear();
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}