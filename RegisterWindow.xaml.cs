using System.Diagnostics;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static System.Net.WebRequestMethods;

namespace StarkFNLauncher
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow() => InitializeComponent();

        private void TitleBar_Drag(object s, MouseButtonEventArgs e) => DragMove();
        private void Close_Click(object s, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
        private void Back_Click(object s, MouseButtonEventArgs e) { new LoginWindow().Show(); Close(); }

     
        

            
        private void ShowStatus(string msg, string hex)
        {
            StatusMsg.Text = msg;
            StatusMsg.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
            StatusBorder.Visibility = Visibility.Visible;
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {

            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/H7w8CRUXu",
                UseShellExecute = true,

            }); 
        }
    }
}
