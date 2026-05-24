using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StarkFNLauncher
{
    public partial class LoginWindow : Window
    {
        private static readonly SolidColorBrush _focus  = new(System.Windows.Media.Color.FromRgb(99, 102, 241));
        private static readonly SolidColorBrush _normal = new(System.Windows.Media.Color.FromArgb(42, 255, 255, 255));
        private static readonly SolidColorBrush _error  = new(System.Windows.Media.Color.FromRgb(127, 29, 29));

        public LoginWindow() { InitializeComponent(); }

        
      
        private async void Discord_Click(object sender, RoutedEventArgs e)
        {
           
            var discord = await DiscordAuth.AuthenticateAsync(msg =>
                Dispatcher.Invoke(() => ShowStatus(msg, true)));
            if (discord == null)
            {
                ShowStatus("Discord login cancelled.", false);
                
                return;
            }
            ShowStatus("Connecting...", true);
            var r = await Api.LoginWithDiscord(discord);
            if (r.Success && r.Data != null)
            {
                Session.Save(r.Data.Token, r.Data.User?.Username ?? discord.Username);
                Api.SetToken(r.Data.Token);
                ShowStatus(r.Data.IsNewAccount ? "Account created!" : "Welcome back!", true);
                await Task.Delay(300);
                OpenMain(r.Data.User?.Username ?? discord.Username, r.Data.Token, discord);
            }
            else
            {
                ShowStatus(r.Error, false);
                
            }
        }

        private void OpenMain(string username, string token, DiscordUser? discord)
        {
            var main = new MainWindow();
            main.Show();
            main.ShowApp(username, token, discord);
            Close();
        }

        private void Register_Click(object s, MouseButtonEventArgs e) { new RegisterWindow().Show(); Close(); }
        private void TitleBar_Drag(object s, MouseButtonEventArgs e) => DragMove();
        private void Min_Click(object s, RoutedEventArgs e)   => WindowState = WindowState.Minimized;
        private void Close_Click(object s, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

        private void ShowStatus(string msg, bool ok)
        {
            StatusMsg.Text       = msg;
            StatusMsg.Foreground = ok
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(165, 180, 252))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 165, 165));
            StatusBorder.Visibility = Visibility.Visible;
        }
    }
}
