using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Screenshoter
{
    public partial class SettingsWindow : Window
    {
        public ModifierKeys SelectedModifiers { get; private set; }
        public Key SelectedKey { get; private set; }

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();

            SelectedModifiers = current.Modifiers;
            SelectedKey = current.Key;
            HotkeyText.Text = current.ToDisplayString();

            Loaded += (_, __) => RecorderBox.Focus();
            RecorderBox.MouseDown += (_, __) => RecorderBox.Focus();
            RecorderBox.GotKeyboardFocus += (_, __) =>
                RecorderBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF));
            RecorderBox.LostKeyboardFocus += (_, __) =>
                RecorderBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));
            RecorderBox.PreviewKeyDown += OnRecorderKeyDown;

            BtnCancel.Click += (_, __) => { DialogResult = false; Close(); };
            BtnSave.Click += OnSave;
        }

        private void OnRecorderKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // игнорируем нажатие самих модификаторов как основной клавиши
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                    or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
            {
                HotkeyText.Text = AppSettings.ToDisplayString(Keyboard.Modifiers, Key.None) + " + …";
                return;
            }

            SelectedModifiers = Keyboard.Modifiers;
            SelectedKey = key;
            HotkeyText.Text = AppSettings.ToDisplayString(SelectedModifiers, SelectedKey);
            HintText.Text = SelectedModifiers == ModifierKeys.None
                ? "Рекомендуется добавить модификатор (Ctrl/Shift/Alt)."
                : "";
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (SelectedKey == Key.None)
            {
                HintText.Text = "Задайте клавишу.";
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
