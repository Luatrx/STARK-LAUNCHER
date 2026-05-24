using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Explicit aliases to resolve CS0104 ambiguities caused by System.Windows.Forms
using WColor      = System.Windows.Media.Color;
using WColors     = System.Windows.Media.Colors;
using WBrush      = System.Windows.Media.Brush;
using WSolidBrush = System.Windows.Media.SolidColorBrush;
using WBrushes    = System.Windows.Media.Brushes;
using WFontFamily = System.Windows.Media.FontFamily;
using WImage      = System.Windows.Controls.Image;
using WPath       = System.Windows.Shapes.Path;
using WEllipse    = System.Windows.Shapes.Ellipse;
using WCursors    = System.Windows.Input.Cursors;
using WOrientation= System.Windows.Controls.Orientation;
using WApplication= System.Windows.Application;

namespace StarkFNLauncher
{
    public partial class MainWindow : Window
    {
        private const string LauncherVersion = "1.0";

        private string       _username  = "";
        private string       _token     = "";
        private string       _accountId = "";
        private DiscordUser? _discord;

        private readonly DispatcherTimer _statusTimer   = new() { Interval = TimeSpan.FromSeconds(30) };
        private readonly DispatcherTimer _clockTimer    = new() { Interval = TimeSpan.FromSeconds(1)  };
        private readonly DispatcherTimer _particleTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        private readonly DispatcherTimer _pulseTimer    = new() { Interval = TimeSpan.FromMilliseconds(1500) };
        private readonly DispatcherTimer _musicBarTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly DispatcherTimer _friendTimer   = new() { Interval = TimeSpan.FromSeconds(60) };
        private DispatcherTimer? _toastTimer;

        // ── MUSIC ─────────────────────────────────────────────────
        private readonly System.Windows.Media.MediaPlayer _mp = new();
        private bool _musicOn = false;
        private int  _barFrame = 0;

        // ── TOGGLES ───────────────────────────────────────────────
        private bool _eor=false, _texture=true, _dx11=true, _splash=true,
                     _autoPatch=true, _rpc=true, _tray=false;

        // ── BANNER ────────────────────────────────────────────────
        private int _bannerIdx = 0;
        private readonly (string Title, string Desc)[] _banners = {
            ("Nexus War",      "Relive Fortnite Chapter 2 Season 4 on Stark FN. Take out enemies, defeat bosses, and dominate the island alongside Marvel heroes."),
            ("Daily V-Bucks",  "Claim 150 V-Bucks every 24 hours via /daily in Discord. VIPs get 300 V-Bucks per day."),
            ("Season 5 Soon",  "Chapter 2 Season 5 — Zero Point — is in active development. Stay tuned in Discord for the release date."),
        };

        private readonly Random _rng = new();
        private List<(double X, double Y, double Sx, double Sy, double Size, double Opacity)> _particles = new();

        public MainWindow() { InitializeComponent(); }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitParticles();
            SetupMusic();
            await RunLoader();
        }

        // ── PARTICLES ─────────────────────────────────────────────
        private void InitParticles()
        {
            for (int i = 0; i < 45; i++)
                _particles.Add((_rng.NextDouble()*1220, _rng.NextDouble()*740,
                    (_rng.NextDouble()-.5)*.3, (_rng.NextDouble()-.5)*.3,
                    _rng.NextDouble()*2+.4, _rng.NextDouble()*.3+.04));
            _particleTimer.Tick += (_, _) =>
            {
                ParticleCanvas.Children.Clear();
                for (int i = 0; i < _particles.Count; i++)
                {
                    var p = _particles[i];
                    double nx = (p.X + p.Sx + 1220) % 1220;
                    double ny = (p.Y + p.Sy + 740)  % 740;
                    _particles[i] = (nx, ny, p.Sx, p.Sy, p.Size, p.Opacity);
                    var el = new System.Windows.Shapes.Ellipse { Width=p.Size, Height=p.Size,
                        Fill=new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(p.Opacity*180),99,102,241)) };
                    Canvas.SetLeft(el, nx); Canvas.SetTop(el, ny);
                    ParticleCanvas.Children.Add(el);
                }
            };
            _particleTimer.Start();
        }

        // ── MUSIC ─────────────────────────────────────────────────
        private void SetupMusic()
        {
            try
            {
                // Try exe directory first (file on disk)
                string exeDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                string mp3Path = System.IO.Path.Combine(exeDir, "lobby.mp3");

                Uri musicUri;
                if (System.IO.File.Exists(mp3Path))
                {
                    // File exists on disk next to exe
                    musicUri = new Uri(mp3Path, UriKind.Absolute);
                }
                else
                {
                    // Fall back to embedded resource
                    musicUri = new Uri("pack://application:,,,/lobby.mp3", UriKind.Absolute);
                }

                _mp.Open(musicUri);
                _mp.Volume = 0.6;
                _mp.MediaEnded += (_, _) => Dispatcher.Invoke(() =>
                {
                    _mp.Position = TimeSpan.Zero;
                    _mp.Play();
                });
                _mp.MediaFailed += (_, e) =>
                    Dispatcher.Invoke(() => ShowToast("Music failed: " + e.ErrorException?.Message));
            }
            catch { }

            _musicBarTimer.Tick += (_, _) =>
            {
                if (!_musicOn) return;
                _barFrame++;
                double t = _barFrame * 0.45;
                MBar1.Height = Math.Max(3, 8  * (.5 + .5 * Math.Sin(t)));
                MBar2.Height = Math.Max(3, 14 * (.5 + .5 * Math.Sin(t + 1.2)));
                MBar3.Height = Math.Max(3, 6  * (.5 + .5 * Math.Sin(t + 2.4)));
                MBar4.Height = Math.Max(3, 10 * (.5 + .5 * Math.Sin(t + .6)));
            };
        }

        private void StartMusic()
        {
            try { _mp.Volume = VolumeSlider?.Value / 100.0 ?? 0.6; _mp.Play(); } catch { }
            _musicOn = true;
            MusicIcon.Text        = "♫";
            MusicStatusTxt.Text   = "Playing";
            MusicPill.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(130, 99, 102, 241));
            VolumeRow.Visibility     = Visibility.Visible;
            NowPlayingRow.Visibility = Visibility.Visible;
            _musicBarTimer.Start();
            // Sync Settings toggle
            var grn = System.Windows.Media.Color.FromRgb(16,185,129); var grnBg = System.Windows.Media.Color.FromRgb(26,58,34);
            ToggleMusicThumb.Background = new SolidColorBrush(grn);
            ToggleMusic.Background      = new SolidColorBrush(grnBg);
            MusicThumbTx.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation{To=20,Duration=TimeSpan.FromMilliseconds(180),
                    EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
        }

        private void StopMusic()
        {
            try { _mp.Pause(); } catch { }
            _musicOn = false;
            MusicIcon.Text        = "♪";
            MusicStatusTxt.Text   = "Music";
            MusicPill.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(14,255,255,255));
            VolumeRow.Visibility     = Visibility.Collapsed;
            NowPlayingRow.Visibility = Visibility.Collapsed;
            _musicBarTimer.Stop();
            // Sync Settings toggle
            var gry = System.Windows.Media.Color.FromRgb(75,85,99); var gryBg = System.Windows.Media.Color.FromRgb(30,32,48);
            ToggleMusicThumb.Background = new SolidColorBrush(gry);
            ToggleMusic.Background      = new SolidColorBrush(gryBg);
            MusicThumbTx.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation{To=0,Duration=TimeSpan.FromMilliseconds(180),
                    EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
        }

        private void Music_Click(object sender, MouseButtonEventArgs e)
        { if (_musicOn) StopMusic(); else StartMusic(); }

        // ── LOADER ────────────────────────────────────────────────
        private async Task RunLoader()
        {
            var steps = new[] {
                (.16, "Initializing..."),
                (.30, "Checking for updates..."),
                (.46, "Connecting to Stark FN..."),
                (.60, "Loading Chapter 2 Season 4..."),
                (.76, "Checking session..."),
                (.90, "Fetching server status..."),
                (1.0, "Ready!"),
            };
            foreach (var (p, msg) in steps)
            {
                await Task.Delay(320);
                LoadMsg.Text = msg;
                AnimateBar(p);
                // Check update at step 2
                if (msg == "Checking for updates...") _ = CheckForUpdate();
            }
            await Task.Delay(350);

            var (token, username) = Session.Load();
            if (!string.IsNullOrEmpty(token))
            {
                LoadMsg.Text = "Restoring session...";
                var r = await Api.Verify(token);
                if (r.Success)
                {
                    _token = token; _username = r.Data!.Username;
                    Api.SetToken(token);
                    ShowApp(); return;
                }
                Session.Clear();
            }
            new LoginWindow().Show();
            Close();
        }

        private void AnimateBar(double to)
        {
            double w = to * 320;
            LoadBar.BeginAnimation(WidthProperty, new DoubleAnimation { To = w,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            DotTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation { To = w,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        }

        // ── AUTO UPDATE ───────────────────────────────────────────
        private async Task CheckForUpdate()
        {
            var r = await Api.CheckUpdate(LauncherVersion);
            if (!r.Success || r.Data == null) return;
            var upd = r.Data;
            if (upd.Version == LauncherVersion) return;
            await Dispatcher.InvokeAsync(() =>
            {
                UpdateVersionTxt.Text = $"Version {upd.Version} is available (you have {LauncherVersion})";
                UpdateNotesTxt.Text   = upd.Notes;
                UpdateModal.Visibility = Visibility.Visible;
            });
        }

        private async void DoUpdate_Click(object sender, RoutedEventArgs e)
        {
            var upd = (await Api.CheckUpdate(LauncherVersion)).Data;
            if (upd == null) return;
            UpdateBtn.IsEnabled = false;
            UpdateProgressWrap.Visibility = Visibility.Visible;
            UpdateProgressTxt.Text = "Downloading update...";
            try
            {
                using var http = new HttpClient();
                var data = await http.GetByteArrayAsync(upd.DownloadUrl);
                UpdateProgressBar.BeginAnimation(WidthProperty, new DoubleAnimation { To = 400,
                    Duration = TimeSpan.FromMilliseconds(500) });
                UpdateProgressTxt.Text = "Installing...";
                await Task.Delay(600);
                string path = Path.Combine(Path.GetTempPath(), "StarkFNLauncher_Update.exe");
                await File.WriteAllBytesAsync(path, data);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                UpdateProgressTxt.Text = $"Download failed: {ex.Message}";
                UpdateBtn.IsEnabled = true;
            }
        }

        private void SkipUpdate_Click(object sender, RoutedEventArgs e)
            => UpdateModal.Visibility = Visibility.Collapsed;

        // ── BUILD INTEGRITY ───────────────────────────────────────
        private void CheckIntegrity_Click(object sender, RoutedEventArgs e)
        {
            string bp = Session.GamePath;
            if (string.IsNullOrEmpty(bp) || !Directory.Exists(bp))
            { ShowToast("Set your build path in Library first."); SetPage("Library"); return; }
            IntegrityModal.Visibility = Visibility.Visible;
            IntegrityResult.Visibility  = Visibility.Collapsed;
            IntegrityCloseBtn.Visibility = Visibility.Collapsed;
            IntegrityBar.Width    = 0;
            IntegrityPct.Text     = "0%";
            IntegrityTitle.Text   = "Checking Build Integrity...";
            IntegritySubTxt.Text  = "Scanning your Fortnite 14.60 build files...";
            _ = RunIntegrityCheck(bp);
        }

        private async Task RunIntegrityCheck(string folder)
        {
            var result = await Task.Run(() =>
            {
                var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                int total = files.Length, done = 0, missing = 0, corrupted = 0;
                var issues = new List<string>();
                // Required key files
                var required = new[] {
                    "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe",
                    "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping_EAC.exe",
                    "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping_BE.exe",
                };
                foreach (var req in required)
                {
                    var full = Path.Combine(folder, req);
                    if (!File.Exists(full)) { missing++; issues.Add(req); }
                }
                foreach (var f in files)
                {
                    done++;
                    Dispatcher.Invoke(() => {
                        double pct = (double)done / total;
                        IntegrityBar.Width = pct * 440;
                        IntegrityPct.Text  = $"{(int)(pct*100)}%";
                    });
                    // Check for obviously corrupt files (0 bytes)
                    try { if (new FileInfo(f).Length == 0) { corrupted++; issues.Add(Path.GetFileName(f)); } }
                    catch { }
                    if (done % 100 == 0) System.Threading.Thread.Sleep(1);
                }
                return new IntegrityResult { Ok = missing==0&&corrupted==0, Checked=total, Missing=missing, Corrupted=corrupted, Issues=issues };
            });

            Dispatcher.Invoke(() =>
            {
                IntegrityBar.Width = 440;
                IntegrityPct.Text  = "100%";
                IntChecked.Text    = result.Checked.ToString();
                IntMissing.Text    = result.Missing.ToString();
                IntCorrupt.Text    = result.Corrupted.ToString();
                IntegrityTitle.Text = result.Ok ? "Build Looks Good ✓" : "Issues Found";
                IntegritySubTxt.Text = result.Ok
                    ? "No missing or corrupted files detected."
                    : $"{result.Missing} missing, {result.Corrupted} corrupted files found.";
                IntStatusTxt.Text       = result.Ok ? "" : string.Join("\n", result.Issues.GetRange(0, Math.Min(5, result.Issues.Count)));
                IntStatusTxt.Foreground = result.Ok
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(16,185,129))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(239,68,68));
                IntegrityResult.Visibility   = Visibility.Visible;
                IntegrityCloseBtn.Visibility = Visibility.Visible;
            });
        }

        private void CloseIntegrity_Click(object sender, RoutedEventArgs e)
            => IntegrityModal.Visibility = Visibility.Collapsed;

        // ── FRIENDS ───────────────────────────────────────────────
        private List<FriendData> _friends = new();

        private async Task LoadFriends()
        {
            var r = await Api.GetFriends(_token);
            if (!r.Success || r.Data == null) return;
            _friends = r.Data;
            Dispatcher.Invoke(RenderFriends);
        }

        private void RenderFriends()
        {
            FriendsOnlineList.Children.Clear();
            FriendsAllList.Children.Clear();
            var online = _friends.FindAll(f => f.Status == "online" || f.Status == "ingame");

            if (online.Count == 0)
            {
                FriendsEmptyTxt.Visibility = Visibility.Visible;
                var empty = new System.Windows.Controls.Border {
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(20),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(14,255,255,255)),
                    BorderThickness = new Thickness(1)
                };
                empty.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30,12,13,26));
                empty.Child = new TextBlock {
                    Text = "No friends online right now",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(55,65,81))
                };
                FriendsOnlineList.Children.Add(empty);
            }
            else
            {
                FriendsEmptyTxt.Visibility = Visibility.Collapsed;
                foreach (var f in online)
                    FriendsOnlineList.Children.Add(MakeFriendCard(f));
            }

            foreach (var f in _friends)
                FriendsAllList.Children.Add(MakeFriendCard(f));

            // Green dot on nav button if anyone online
            // FriendsBadge is inside a ControlTemplate - find it at runtime
            if (BtnFriends.Template.FindName("FriendsBadge", BtnFriends) is System.Windows.Shapes.Ellipse badge)
                badge.Visibility = online.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private UIElement MakeFriendCard(FriendData f)
        {
            var statusColor = f.Status switch {
                "ingame" => System.Windows.Media.Color.FromRgb(99,102,241),
                "online" => System.Windows.Media.Color.FromRgb(16,185,129),
                _        => System.Windows.Media.Color.FromRgb(75,85,99)
            };
            var statusText = f.Status switch {
                "ingame" => "In Game",
                "online" => "Online",
                _        => "Offline"
            };
            var card = new System.Windows.Controls.Border {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14,12,14,12),
                Margin  = new Thickness(0,0,0,8),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(14,255,255,255)),
                BorderThickness = new Thickness(1)
            };
            card.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30,12,13,26));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatar
            var avatarBorder = new System.Windows.Controls.Border {
                Width=40, Height=40, CornerRadius=new CornerRadius(10),
                Background=new SolidColorBrush(System.Windows.Media.Color.FromRgb(99,102,241)),
                ClipToBounds=true, Margin=new Thickness(0,0,14,0)
            };
            if (!string.IsNullOrEmpty(f.AvatarUrl))
                try { avatarBorder.Child = new System.Windows.Controls.Image { Source=new BitmapImage(new Uri(f.AvatarUrl)), Stretch=System.Windows.Media.Stretch.UniformToFill }; }
                catch { avatarBorder.Child = new TextBlock { Text=f.Username.Substring(0,1).ToUpper(), FontSize=16, FontWeight=System.Windows.FontWeights.Bold, Foreground=System.Windows.Media.Brushes.White, HorizontalAlignment=System.Windows.HorizontalAlignment.Center, VerticalAlignment=System.Windows.VerticalAlignment.Center }; }
            else
                avatarBorder.Child = new TextBlock { Text=f.Username.Substring(0,1).ToUpper(), FontSize=16, FontWeight=System.Windows.FontWeights.Bold, Foreground=System.Windows.Media.Brushes.White, HorizontalAlignment=System.Windows.HorizontalAlignment.Center, VerticalAlignment=System.Windows.VerticalAlignment.Center };
            Grid.SetColumn(avatarBorder, 0);

            // Info
            var info = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center };
            var nameTb = new TextBlock { Text=f.Username, FontFamily=new System.Windows.Media.FontFamily("Segoe UI"), FontSize=13, FontWeight=System.Windows.FontWeights.SemiBold, Foreground=System.Windows.Media.Brushes.White };
            var statusRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0,3,0,0) };
            var dot = new System.Windows.Shapes.Ellipse { Width=6, Height=6, Fill=new SolidColorBrush(statusColor), Margin=new Thickness(0,0,6,0), VerticalAlignment=System.Windows.VerticalAlignment.Center };
            var statusTb = new TextBlock { Text=statusText, FontFamily=new System.Windows.Media.FontFamily("Segoe UI"), FontSize=11, Foreground=new SolidColorBrush(System.Windows.Media.Color.FromRgb(107,114,128)) };
            var statsTb = new TextBlock { Text=$"  ·  {f.Wins}W  {f.Kills}K", FontFamily=new System.Windows.Media.FontFamily("Segoe UI"), FontSize=11, Foreground=new SolidColorBrush(System.Windows.Media.Color.FromRgb(55,65,81)) };
            statusRow.Children.Add(dot); statusRow.Children.Add(statusTb); statusRow.Children.Add(statsTb);
            info.Children.Add(nameTb); info.Children.Add(statusRow);
            Grid.SetColumn(info, 1);

            // Invite button (online only)
            if (f.Status == "ingame" || f.Status == "online")
            {
                var invBtn = new System.Windows.Controls.Button {
                    Content = new TextBlock { Text="Invite", FontFamily=new System.Windows.Media.FontFamily("Segoe UI"), FontSize=11, FontWeight=System.Windows.FontWeights.SemiBold, Foreground=System.Windows.Media.Brushes.White },
                    Style = (Style)Resources["PrimaryBtn"],
                    Padding = new Thickness(12,6,12,6),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                invBtn.Click += (_, _) => ShowToast($"Invite sent to {f.Username}!");
                Grid.SetColumn(invBtn, 2);
                grid.Children.Add(invBtn);
            }

            grid.Children.Add(avatarBorder);
            grid.Children.Add(info);
            card.Child = grid;
            return card;
        }

        private void RefreshFriends_Click(object sender, RoutedEventArgs e) => _ = LoadFriends();

        private async void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            string name = AddFriendBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) { ShowToast("Enter a username first."); return; }
            ShowToast($"Friend request sent to {name}!");
            AddFriendBox.Text = "";
            await LoadFriends();
        }

        // ── SHOW APP ──────────────────────────────────────────────
        public void ShowApp(string username="", string token="",
                            DiscordUser? discord=null, string accountId="")
        {
            if (!string.IsNullOrEmpty(username))  _username  = username;
            if (!string.IsNullOrEmpty(token))     _token     = token;
            if (!string.IsNullOrEmpty(accountId)) _accountId = accountId;
            if (discord != null)                  _discord   = discord;

            LoadingScreen.Visibility = Visibility.Collapsed;
            AppShell.Visibility      = Visibility.Visible;

            int h = DateTime.Now.Hour;
            GreetingTxt.Text = (h<12?"Good morning":h<17?"Good afternoon":"Good evening") + ",  " + _username;
            HeaderUsername.Text = _username;
            SetUsername.Text    = _username;
            PopupUsername.Text  = _username;
            StatsUsername.Text  = _username;

            if (_discord != null)
            {
                SetDiscord.Text = _discord.Tag;
                try
                {
                    var bmp = new BitmapImage(new Uri(_discord.AvatarUrl));
                    HeaderAvatarImg.Source = bmp;
                    PopupAvatarImg.Source  = bmp;
                    StatsAvatarImg.Source  = bmp;
                }
                catch { }
            }

            string bp = Session.GamePath;
            if (!string.IsNullOrEmpty(bp)) BuildPathTxt.Text = bp;

            UpdatePatchUI();

            _clockTimer.Tick += (_, _) => ClockTxt.Text = DateTime.Now.ToString("HH:mm");
            _clockTimer.Start(); ClockTxt.Text = DateTime.Now.ToString("HH:mm");

            _statusTimer.Tick += async (_, _) => await RefreshStatus();
            _statusTimer.Start(); _ = RefreshStatus();

            _pulseTimer.Tick += (_, _) =>
                StatServerDotOuter.BeginAnimation(System.Windows.Shapes.Ellipse.OpacityProperty,
                    new DoubleAnimation { From=.3, To=.75, Duration=TimeSpan.FromMilliseconds(1400), AutoReverse=true });
            _pulseTimer.Start();

            // Friends auto refresh
            _friendTimer.Tick += async (_, _) => await LoadFriends();
            _friendTimer.Start(); _ = LoadFriends();

            SetPage("Home");
        }

        // ── STATUS ────────────────────────────────────────────────
        private async Task RefreshStatus()
        {
            var r = await Api.GetStatus();
            Dispatcher.Invoke(() =>
            {
                bool on  = r.Success && r.Data?.Online == true;
                var  col = on ? System.Windows.Media.Color.FromRgb(16,185,129) : System.Windows.Media.Color.FromRgb(239,68,68);
                var  brush = new SolidColorBrush(col);
                var  outer = new SolidColorBrush(System.Windows.Media.Color.FromArgb(48,col.R,col.G,col.B));
                StatServerDot.Fill      = brush; StatServerDotOuter.Fill = outer;
                StatServerTxt.Text      = on ? "Online" : "Offline";
                StatOnline.Text         = r.Data?.Players.ToString() ?? "0";
                TopServerDot.Fill       = brush; TopServerOuter.Fill     = outer;
                TopServerTxt.Text       = on ? "Online" : "Offline";
                TopPlayersTxt.Text      = (r.Data?.Players ?? 0) + " Online";
            });
        }

        private void UpdatePatchUI()
        {
            bool p   = HostsPatcher.IsPatched();
            var  col = p ? System.Windows.Media.Color.FromRgb(16,185,129) : System.Windows.Media.Color.FromRgb(239,68,68);
            PatchDot.Fill        = new SolidColorBrush(col);
            PatchDotOuter.Fill   = new SolidColorBrush(System.Windows.Media.Color.FromArgb(48,col.R,col.G,col.B));
            PatchTxt.Text        = p ? "Patched" : "Not Patched";
            SetPatchDot.Fill     = new SolidColorBrush(col);
            SetPatchDotOuter.Fill= new SolidColorBrush(System.Windows.Media.Color.FromArgb(48,col.R,col.G,col.B));
            SetPatchTxt.Text     = p ? "Patched" : "Not Patched";
        }

        // ── NAVIGATION ────────────────────────────────────────────
        private void Nav_Click(object sender, RoutedEventArgs e)
        { if (sender is System.Windows.Controls.Button b && b.Tag is string tag) SetPage(tag); }

        private void SetTabActive(System.Windows.Controls.Button btn, bool active)
        {
            if (btn.Template.FindName("bd", btn) is System.Windows.Controls.Border bd)
            {
                bd.Background   = active
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22,99,102,241))
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x0A,255,255,255));
                bd.BorderBrush  = active
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x44,99,102,241))
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x12,255,255,255));
            }
        }

        private void SetPage(string page)
        {
            PageHome.Visibility     = page=="Home"     ? Visibility.Visible : Visibility.Collapsed;
            PageLibrary.Visibility  = page=="Library"  ? Visibility.Visible : Visibility.Collapsed;
            PageNews.Visibility     = page=="News"     ? Visibility.Visible : Visibility.Collapsed;
            PageStats.Visibility    = page=="Stats"    ? Visibility.Visible : Visibility.Collapsed;
            PageFriends.Visibility  = page=="Friends"  ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = page=="Settings" ? Visibility.Visible : Visibility.Collapsed;

            SetTabActive(BtnHome,     page=="Home");
            SetTabActive(BtnLibrary,  page=="Library");
            SetTabActive(BtnNews,     page=="News");
            SetTabActive(BtnStats,    page=="Stats");
            SetTabActive(BtnFriends,  page=="Friends");
            SetTabActive(BtnSettings, page=="Settings");

            if (page == "Settings") UpdatePatchUI();
            if (page == "Stats")    _ = LoadStats();
            if (page == "Friends")  _ = LoadFriends();
            ProfilePopup.Visibility = Visibility.Collapsed;
        }

        // ── BANNER ────────────────────────────────────────────────
        private void Banner_Next(object sender, MouseButtonEventArgs e)
        {
            _bannerIdx = (_bannerIdx+1) % _banners.Length;
            BannerTitle.Text = _banners[_bannerIdx].Title;
            BannerDesc.Text  = _banners[_bannerIdx].Desc;
        }

        // ── LAUNCH ────────────────────────────────────────────────
        private async void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (GameLauncher.State == LaunchState.Running) { ShowToast("Game is already running."); return; }
            string bp = Session.GamePath;
            if (string.IsNullOrEmpty(bp)||!Directory.Exists(bp))
            { ShowToast("Set your build path in Library first."); SetPage("Library"); return; }

            BannerPlayBtn.IsEnabled = false;
            if (_autoPatch)
            {
                ShowToast("Patching hosts file...");
                HostsPatcher.KillEpic();
                await Task.Delay(1500);
                bool ok = await HostsPatcher.PatchAsync();
                UpdatePatchUI();
                if (!ok) { ShowToast("Hosts patch failed — run as Administrator."); BannerPlayBtn.IsEnabled=true; return; }
            }
            GameLauncher.Launch(bp, _username, _accountId, _token);
            BannerPlayLbl.Text = "Playing...";
            _ = PollLaunch();
        }

        private async Task PollLaunch()
        {
            while (GameLauncher.State == LaunchState.Launching) await Task.Delay(500);
            Dispatcher.Invoke(() => { BannerPlayBtn.IsEnabled=true; BannerPlayLbl.Text="Play Now"; });
        }

        // ── LIBRARY ───────────────────────────────────────────────
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog { Description="Select your Fortnite 14.60 folder", UseDescriptionForTitle=true };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                bool found = Directory.GetFiles(dlg.SelectedPath,"FortniteClient-Win64-Shipping*.exe",SearchOption.AllDirectories).Length > 0;
                if (!found) { ShowToast("No Fortnite exe found in that folder."); return; }
                Session.GamePath  = dlg.SelectedPath;
                BuildPathTxt.Text = dlg.SelectedPath;
                ShowToast("Build path saved!");
            }
        }
        private void Browse_MouseDown(object sender, MouseButtonEventArgs e) => Browse_Click(sender, e);

        // ── NEWS ──────────────────────────────────────────────────
        private void NavNews_Click(object sender, RoutedEventArgs e) => SetPage("News");
        private void NavNews_MouseDown(object sender, MouseButtonEventArgs e) => SetPage("News");

        // ── STATS ─────────────────────────────────────────────────
        private async Task LoadStats()
        {
            var r = await Api.GetStats(_token);
            if (!r.Success||r.Data==null) return;
            Dispatcher.Invoke(() => {
                var s = r.Data;
                StatsWins.Text     = s.Wins.ToString();
                StatsKills.Text    = s.Kills.ToString();
                StatsKD.Text       = s.Deaths==0 ? s.Kills.ToString("0.00") : (s.Kills/(double)s.Deaths).ToString("0.00");
                StatsMatches.Text  = s.Matches.ToString();
                StatsVbucks.Text   = s.Vbucks.ToString();
                StatsPlaytime.Text = s.Playtime + " min";
            });
        }
        private void RefreshStats_Click(object sender, RoutedEventArgs e)
        { StatsWins.Text=StatsKills.Text=StatsKD.Text=StatsMatches.Text=StatsVbucks.Text=StatsPlaytime.Text="..."; _ = LoadStats(); }

        // ── SETTINGS TOGGLES ─────────────────────────────────────
        private void DoToggle(System.Windows.Controls.Border bg, System.Windows.Controls.Border thumb,
                              TranslateTransform tx, ref bool state)
        {
            state = !state;
            var grn = System.Windows.Media.Color.FromRgb(16,185,129); var gry = System.Windows.Media.Color.FromRgb(75,85,99);
            var grnBg = System.Windows.Media.Color.FromRgb(26,58,34);  var gryBg = System.Windows.Media.Color.FromRgb(30,32,48);
            thumb.Background = new SolidColorBrush(state ? grn : gry);
            bg.Background    = new SolidColorBrush(state ? grnBg : gryBg);
            tx.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation {
                To=state?20:0, Duration=TimeSpan.FromMilliseconds(180),
                EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
        }

        private void Toggle_Music(object s, MouseButtonEventArgs e)
        {
            bool dummy = !_musicOn;
            if (dummy) StartMusic(); else StopMusic();
        }
        private void Toggle_EOR(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleEOR,ToggleEORThumb,EORThumbTx,ref _eor); ShowToast(_eor?"Edit on Release enabled":"EOR disabled"); }
        private void Toggle_Texture(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleTexture,ToggleTextureThumb,TextureThumbTx,ref _texture); ShowToast(_texture?"Texture streaming disabled":"Texture streaming enabled"); }
        private void Toggle_DX11(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleDX11,ToggleDX11Thumb,DX11ThumbTx,ref _dx11); ShowToast(_dx11?"DirectX 11 forced":"DX11 force removed"); }
        private void Toggle_Splash(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleSplash,ToggleSplashThumb,SplashThumbTx,ref _splash); ShowToast(_splash?"Splash skipped":"Splash enabled"); }
        private void Toggle_AutoPatch(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleAutoPatch,ToggleAutoPatchThumb,AutoPatchThumbTx,ref _autoPatch); ShowToast(_autoPatch?"Auto patch enabled":"Auto patch disabled"); }
        private void Toggle_RPC(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleRPC,ToggleRPCThumb,RPCThumbTx,ref _rpc); ShowToast(_rpc?"Rich Presence on":"Rich Presence off"); }
        private void Toggle_Tray(object s, MouseButtonEventArgs e)
        { DoToggle(ToggleTray,ToggleTrayThumb,TrayThumbTx,ref _tray); ShowToast(_tray?"Will minimize to tray":"Will close normally"); }

        private void Volume_Changed(object s, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        { if (VolumeTxt!=null) VolumeTxt.Text=$"{(int)e.NewValue}%"; try{_mp.Volume=e.NewValue/100.0;}catch{} }

        // ── HOSTS ─────────────────────────────────────────────────
        private async void PatchHosts_Click(object sender, RoutedEventArgs e)
        { ShowToast("Patching..."); bool ok=await HostsPatcher.PatchAsync(); UpdatePatchUI(); ShowToast(ok?"Hosts patched!":"Patch failed — run as Admin."); }
        private void RestoreHosts_Click(object sender, RoutedEventArgs e)
        { HostsPatcher.RestoreHosts(); UpdatePatchUI(); ShowToast("Hosts restored."); }
        private void OpenDiscord_Click(object sender, RoutedEventArgs e)
            => Process.Start(new ProcessStartInfo{FileName="https://discord.gg/starkfnfn",UseShellExecute=true});

        // ── PROFILE POPUP ─────────────────────────────────────────
        private void Avatar_MouseDown(object sender, MouseButtonEventArgs e)
            => ProfilePopup.Visibility = ProfilePopup.Visibility==Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        private void PopupStats_Click(object sender, RoutedEventArgs e)
        { ProfilePopup.Visibility=Visibility.Collapsed; SetPage("Stats"); }
        private void PopupSettings_Click(object sender, RoutedEventArgs e)
        { ProfilePopup.Visibility=Visibility.Collapsed; SetPage("Settings"); }

        // ── TOAST ─────────────────────────────────────────────────
        private void ShowToast(string msg)
        {
            ToastTxt.Text=msg; ToastBar.Visibility=Visibility.Visible;
            _toastTimer?.Stop();
            _toastTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
            _toastTimer.Tick+=(_, _)=>{_toastTimer!.Stop();ToastBar.Visibility=Visibility.Collapsed;};
            _toastTimer.Start();
        }

        // ── WINDOW ────────────────────────────────────────────────
        private void TitleBar_Drag(object s, MouseButtonEventArgs e)
        { if(e.ButtonState==MouseButtonState.Pressed) DragMove(); }
        private void Min_Click(object s, RoutedEventArgs e) => WindowState=WindowState.Minimized;
        private void Close_Click(object s, RoutedEventArgs e)
        {
            _statusTimer.Stop();_clockTimer.Stop();_particleTimer.Stop();
            _pulseTimer.Stop();_musicBarTimer.Stop();_friendTimer.Stop();
            try{_mp.Stop();_mp.Close();}catch{}
            System.Windows.Application.Current.Shutdown();
        }
        private void Logout_Click(object s, RoutedEventArgs e)
        {
            _statusTimer.Stop();_clockTimer.Stop();_particleTimer.Stop();
            _pulseTimer.Stop();_musicBarTimer.Stop();_friendTimer.Stop();
            try{_mp.Stop();_mp.Close();}catch{}
            Session.Clear(); new LoginWindow().Show(); Close();
        }
    }
}
