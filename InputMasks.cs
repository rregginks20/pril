using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace YanaulKioskPerfect
{
    public static class InputMasks
    {
        // Маска для телефона: +7 (XXX) XXX-XX-XX
        public static void ApplyPhoneMask(TextBox textBox)
        {
            textBox.MaxLength = 18;
            textBox.Text = "+7 (";
            textBox.SelectionStart = textBox.Text.Length;

            textBox.PreviewTextInput += (s, e) =>
            {
                if (!IsDigitsOnly(e.Text)) e.Handled = true;
            };

            textBox.TextChanged += (s, e) =>
            {
                string text = Regex.Replace(textBox.Text, "[^0-9]+", "");
                if (string.IsNullOrEmpty(text)) return;

                if (text.StartsWith("8")) text = text.Substring(1);
                if (!text.StartsWith("7")) text = "7" + text;

                string formatted = "+7 (";
                if (text.Length > 1) formatted += text.Substring(1, Math.Min(3, text.Length - 1));
                if (text.Length >= 5) formatted += ") " + text.Substring(4, Math.Min(3, text.Length - 4));
                if (text.Length >= 8) formatted += "-" + text.Substring(7, Math.Min(2, text.Length - 7));
                if (text.Length >= 10) formatted += "-" + text.Substring(9, Math.Min(2, text.Length - 9));
                if (text.Length >= 12) formatted += "-" + text.Substring(11, Math.Min(2, text.Length - 11));

                textBox.Text = formatted;
                textBox.SelectionStart = textBox.Text.Length;
            };
        }

        // Маска для полиса: 0000 0000 0000 0000
        public static void ApplyPolicyMask(TextBox textBox)
        {
            textBox.MaxLength = 19; // 16 цифр + 3 пробела

            textBox.PreviewTextInput += (s, e) =>
            {
                if (!IsDigitsOnly(e.Text)) e.Handled = true;
            };

            textBox.TextChanged += (s, e) =>
            {
                string text = Regex.Replace(textBox.Text, "[^0-9]+", "");
                if (text.Length > 16) text = text.Substring(0, 16);

                string formatted = "";
                for (int i = 0; i < text.Length; i++)
                {
                    if (i > 0 && i % 4 == 0) formatted += " ";
                    formatted += text[i];
                }

                if (textBox.SelectionStart == textBox.Text.Length || string.IsNullOrWhiteSpace(textBox.Text))
                {
                    textBox.Text = formatted;
                    textBox.SelectionStart = textBox.Text.Length;
                }
            };
        }

        private static bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
    }
}