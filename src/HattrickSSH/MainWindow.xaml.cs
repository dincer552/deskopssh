using HattrickSSH.Models;
using HattrickSSH.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HattrickSSH;

public partial class MainWindow : Window
{
    private readonly LocalStore store = new();
    private readonly SecureKeyStore keyStore;
    private readonly CloudflaredInstaller cloudflared = new();
    private readonly SshConfigWriter sshConfig;
    private readonly UpdateService updater = new();
    private readonly ObservableCollection<ServerProfile> profiles;
    private AppSettings settings;
    private PseudoConsoleSession? session;
    private string? temporaryKeyPath;
    private bool terminalReady;
    private bool closing;

    public MainWindow()
    {
        InitializeComponent();
        keyStore = new SecureKeyStore(store);
        sshConfig = new SshConfigWriter(store);
        settings = store.LoadSettings();
        profiles = new ObservableCollection<ServerProfile>(store.LoadProfiles());
        ProfilesList.ItemsSource = profiles;
        if (profiles.Count > 0) ProfilesList.SelectedIndex = 0;
        VersionLabel.Text = "v" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0");
        LoadSettingsIntoUi();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await TerminalWeb.EnsureCoreWebView2Async();
            TerminalWeb.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            TerminalWeb.CoreWebView2.Settings.IsStatusBarEnabled = false;
            TerminalWeb.CoreWebView2.WebMessageReceived += Terminal_WebMessageReceived;
            TerminalWeb.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "hssh.local", Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                CoreWebView2HostResourceAccessKind.Allow);
            TerminalWeb.CoreWebView2.Navigate("https://hssh.local/index.html");
            terminalReady = true;
            await WriteTerminalAsync("Hattrick SSH • Güvenli terminal hazır\r\nSunucu profili seçip Bağlan düğmesine basın.\r\n\r\n");
        }
        catch (Exception ex)
        {
            ConnectionStatus.Text = "WebView2 terminal başlatılamadı.";
            MessageBox.Show("Terminal arayüzü başlatılamadı. Microsoft Edge WebView2 Runtime kurulu olmalı.\n\n" + ex.Message,
                "Terminal başlatma hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (settings.CheckUpdatesOnStartup)
            _ = CheckUpdatesAsync(showNoUpdate: false);
    }

    private void Terminal_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type == "input")
            {
                var data = root.GetProperty("data").GetString() ?? "";
                session?.WriteInput(data);
            }
            else if (type == "resize")
            {
                var cols = root.GetProperty("cols").GetInt32();
                var rows = root.GetProperty("rows").GetInt32();
                session?.Resize((short)Math.Clamp(cols, 20, 400), (short)Math.Clamp(rows, 5, 200));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Terminal bridge message rejected: " + ex.GetType().Name);
        }
    }

    private ServerProfile? SelectedProfile => ProfilesList.SelectedItem as ServerProfile;

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null) return;
        TerminalTitle.Text = profile.Name;
        TransportLabel.Text = profile.UseCloudflareAccess ? "   CLOUDFLARE ACCESS • SSH" : "   DIRECT SSH";
        ConnectionStatus.Text = $"● Hazır • {profile.Username}@{profile.Host}:{profile.Port}";
    }

    private void Navigation_Click(object sender, RoutedEventArgs e)
    {
        var key = (sender as Button)?.Tag?.ToString() ?? "servers";
        ServerWorkspace.Visibility = Visibility.Collapsed;
        SettingsWorkspace.Visibility = Visibility.Collapsed;
        UpdatesWorkspace.Visibility = Visibility.Collapsed;
        InfoWorkspace.Visibility = Visibility.Collapsed;

        switch (key)
        {
            case "servers":
            case "terminal":
                ServerWorkspace.Visibility = Visibility.Visible;
                PageTitle.Text = key == "terminal" ? "Terminal" : "Sunucular";
                PageSubtitle.Text = "Gerçek SSH oturumları • Cloudflare Access • özel anahtar";
                break;
            case "appearance":
            case "security":
                SettingsWorkspace.Visibility = Visibility.Visible;
                PageTitle.Text = key == "appearance" ? "Görünüm" : "Güvenlik";
                PageSubtitle.Text = key == "appearance"
                    ? "Terminal yazı tipi ve çalışma alanı tercihleri."
                    : "Yerel anahtar kasası ve SSH doğrulama seçenekleri.";
                break;
            case "updates":
                UpdatesWorkspace.Visibility = Visibility.Visible;
                PageTitle.Text = "Güncellemeler";
                PageSubtitle.Text = "GitHub Releases üzerinden sürüm kontrolü.";
                break;
            case "about":
                ShowInfo("Hattrick SSH", "Windows için yerel-first SSH çalışma alanı. Gerçek SSH oturumları Windows OpenSSH ve ConPTY üzerinden yürür. Cloudflare Access kimlik doğrulamasını cloudflared yönetir. Özel anahtarlar DPAPI CurrentUser ile şifrelenir.");
                break;
            case "sftp":
                ShowInfo("SFTP Dosyaları", "SFTP dosya tarayıcısı yol haritasında. Bu görünüm ilk terminal sürümünde henüz etkin değil; dosya transferi hazırmış gibi gösterilmez.");
                break;
            case "tunnels":
                ShowInfo("Port Yönlendirme", "Yerel, uzak ve SOCKS port yönlendirme özellikleri sonraki aşamada eklenecek. Bu sürümde henüz port açılmaz.");
                break;
            case "snippets":
                ShowInfo("Komut Parçacıkları", "Terminal altındaki hazır komutlar hostname, whoami, df -h / ve free -h komutlarını gerçek uzak shell'e gönderir. Kişisel komut kütüphanesi sonraki aşamada kalıcı hale getirilecek.");
                break;
            case "history":
                ShowInfo("Geçmiş", "Güvenlik nedeniyle ilk sürümde terminal girişleri veya komutlar diske kaydedilmez. Bağlantı geçmişi ve isteğe bağlı, gizli bilgileri filtrelenmiş çıktı kaydı sonraki aşamadadır.");
                break;
        }
    }

    private void ShowInfo(string title, string body)
    {
        InfoWorkspace.Visibility = Visibility.Visible;
        PageTitle.Text = title;
        PageSubtitle.Text = "Çalışma alanı ve özellik durumu";
        InfoTitle.Text = title;
        InfoBody.Text = body;
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = new ServerProfile { Name = "Yeni sunucu", Port = 22, Group = "Genel" };
        if (!ShowProfileEditor(profile, isNew: true)) return;
        profiles.Add(profile);
        ProfilesList.SelectedItem = profile;
        SaveProfiles();
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null) return;
        if (ShowProfileEditor(profile, isNew: false)) SaveProfiles();
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        var source = SelectedProfile;
        if (source is null) return;
        var clone = new ServerProfile
        {
            Name = source.Name + " (kopya)", Host = source.Host, Port = source.Port,
            Username = source.Username, UseCloudflareAccess = source.UseCloudflareAccess,
            PrivateKeyId = source.PrivateKeyId, Group = source.Group, Notes = source.Notes
        };
        profiles.Add(clone);
        ProfilesList.SelectedItem = clone;
        SaveProfiles();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null) return;
        if (MessageBox.Show($"'{profile.Name}' profili silinsin mi? Şifrelenmiş anahtar dosyası korunur.",
            "Sunucu profilini sil", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        profiles.Remove(profile);
        ProfilesList.SelectedIndex = profiles.Count > 0 ? 0 : -1;
        SaveProfiles();
    }

    private bool ShowProfileEditor(ServerProfile profile, bool isNew)
    {
        var dialog = new Window
        {
            Title = isNew ? "Sunucu profili ekle" : "Sunucu profilini düzenle",
            Owner = this, Width = 520, Height = 650, MinHeight = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, Background = (Brush)FindResource("PanelBrush"),
            Foreground = (Brush)FindResource("TextBrush")
        };
        var stack = new StackPanel { Margin = new Thickness(22) };
        stack.Children.Add(new TextBlock { Text = "Bağlantı ayarları", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,12) });
        var name = AddEditorField(stack, "Profil adı", profile.Name);
        var host = AddEditorField(stack, "Sunucu adresi", profile.Host);
        var user = AddEditorField(stack, "Kullanıcı adı", profile.Username);
        var port = AddEditorField(stack, "SSH portu", profile.Port.ToString());
        var group = AddEditorField(stack, "Grup", profile.Group);
        var cf = new CheckBox { Content = "Cloudflare Access kullan (cloudflared)", IsChecked = profile.UseCloudflareAccess, Margin = new Thickness(0,12,0,8) };
        stack.Children.Add(cf);
        var keyText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(profile.PrivateKeyId) ? "SSH anahtarı: seçilmedi (OpenSSH agent/default identities kullanılabilir)" : "SSH anahtarı: güvenli kasada kayıtlı",
            Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,8)
        };
        stack.Children.Add(keyText);
        var chooseKey = new Button { Content = "RSA / OpenSSH PRIVATE KEY İÇE AKTAR", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,0,0,10) };
        stack.Children.Add(chooseKey);
        string? keyId = profile.PrivateKeyId;
        chooseKey.Click += (_, _) =>
        {
            var picker = new OpenFileDialog { Title = "SSH private key seç", Filter = "SSH key|*.pem;*.key;id_*;*.ppk|Tüm dosyalar|*.*", CheckFileExists = true };
            if (picker.ShowDialog(this) != true) return;
            try
            {
                keyId = keyStore.Import(picker.FileName);
                keyText.Text = "SSH anahtarı: DPAPI CurrentUser ile şifrelenerek kaydedildi";
                keyText.Foreground = (Brush)FindResource("GreenBrush");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Anahtar içe aktarılamadı. Dosya biçimini kontrol edin.\n\n" + ex.Message,
                    "SSH anahtar hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        var note = new TextBlock
        {
            Text = "Cloudflare giriş bilgileri cloudflared tarafından tarayıcı/Windows profilinde yönetilir. Bu uygulama Access tokenlarını kaydetmez.",
            Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0,6,0,12)
        };
        stack.Children.Add(note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Vazgeç", Margin = new Thickness(0,0,8,0), MinWidth = 88 };
        var save = new Button { Content = "Kaydet", MinWidth = 100, Background = (Brush)FindResource("GreenBrush"), Foreground = Brushes.Black };
        buttons.Children.Add(cancel); buttons.Children.Add(save); stack.Children.Add(buttons);
        cancel.Click += (_, _) => dialog.DialogResult = false;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(host.Text) || string.IsNullOrWhiteSpace(user.Text) ||
                !int.TryParse(port.Text, out var parsedPort) || parsedPort is < 1 or > 65535)
            {
                MessageBox.Show("Profil adı, host, kullanıcı ve 1–65535 arası port zorunludur.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            profile.Name = name.Text.Trim();
            profile.Host = host.Text.Trim();
            profile.Username = user.Text.Trim();
            profile.Port = parsedPort;
            profile.Group = group.Text.Trim();
            profile.UseCloudflareAccess = cf.IsChecked == true;
            profile.PrivateKeyId = keyId;
            dialog.DialogResult = true;
        };
        dialog.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return dialog.ShowDialog() == true;
    }

    private static TextBox AddEditorField(Panel panel, string label, string value)
    {
        panel.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.LightGray, Margin = new Thickness(0,8,0,4) });
        var box = new TextBox { Text = value, Height = 35, VerticalContentAlignment = VerticalAlignment.Center };
        panel.Children.Add(box);
        return box;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e) => await ConnectSelectedAsync();

    private async Task ConnectSelectedAsync()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            MessageBox.Show("Önce bağlantı profili seçin.", "Hattrick SSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(profile.Host) || string.IsNullOrWhiteSpace(profile.Username))
        {
            MessageBox.Show("Sunucu profili eksik. Host ve kullanıcı adını düzenleyin.", "Profil eksik", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DisconnectSession(showMessage: false);
        ConnectionStatus.Text = "● SSH istemcisi hazırlanıyor…";
        ConnectionDot.Fill = (Brush)FindResource("MutedBrush");
        TerminalTitle.Text = profile.Name;
        TransportLabel.Text = profile.UseCloudflareAccess ? "   CLOUDFLARE ACCESS • SSH" : "   DIRECT SSH";
        await WriteTerminalAsync($"\r\n\x1b[38;5;42m[HATTRICK SSH]\x1b[0m {profile.Host}:{profile.Port} bağlantısı başlatılıyor…\r\n");

        try
        {
            var ssh = FindOpenSsh();
            string? cloudflaredPath = null;
            if (profile.UseCloudflareAccess)
            {
                ConnectionStatus.Text = "● cloudflared hazırlanıyor…";
                await WriteTerminalAsync("[CLOUDFLARE] Resmi Windows x64 istemcisi kontrol ediliyor…\r\n");
                cloudflaredPath = await cloudflared.EnsureInstalledAsync();
                await WriteTerminalAsync("[CLOUDFLARE] İstemci hazır. Access doğrulaması gerektiğinde tarayıcı açılacak.\r\n");
            }

            if (!string.IsNullOrWhiteSpace(profile.PrivateKeyId))
                temporaryKeyPath = keyStore.CreateTemporaryKey(profile.PrivateKeyId);

            var configPath = sshConfig.Write(profile, temporaryKeyPath, cloudflaredPath);
            var alias = "hssh-" + profile.Id;
            var args = $"-tt -F {QuoteArgument(configPath)} {alias}";
            session = PseudoConsoleSession.Start(ssh, args, 120, 34);
            session.OutputReceived += bytes =>
            {
                var base64 = Convert.ToBase64String(bytes);
                Dispatcher.BeginInvoke(() =>
                {
                    if (TerminalWeb.CoreWebView2 is not null)
                        TerminalWeb.CoreWebView2.PostWebMessageAsString(base64);
                });
            };
            session.Exited += code => Dispatcher.BeginInvoke(() =>
            {
                ConnectionDot.Fill = (Brush)FindResource("MutedBrush");
                ConnectionStatus.Text = $"● SSH işlemi sona erdi (çıkış kodu {code}). Yeniden bağlanabilirsiniz.";
                FooterRight.Text = "Oturum kapandı";
                keyStore.DeleteTemporaryKey(temporaryKeyPath);
                temporaryKeyPath = null;
                session = null;
            });

            ConnectionDot.Fill = (Brush)FindResource("GreenBrush");
            ConnectionStatus.Text = profile.UseCloudflareAccess
                ? "● cloudflared + OpenSSH çalışıyor • Access/SSH çıktısı terminalde"
                : "● OpenSSH çalışıyor • SSH el sıkışması bekleniyor";
            FooterRight.Text = "● Terminal süreci açık";
            await WriteTerminalAsync("[PTY] Windows ConPTY oluşturuldu. SSH istemcisi gerçek interaktif shell açıyor.\r\n");
        }
        catch (Exception ex)
        {
            DisconnectSession(showMessage: false);
            ConnectionDot.Fill = Brushes.IndianRed;
            ConnectionStatus.Text = "● Bağlantı başlatılamadı";
            FooterRight.Text = "Bağlantı hatası";
            await WriteTerminalAsync($"\r\n\x1b[31m[HATA] {ex.Message}\x1b[0m\r\n");
            MessageBox.Show(ex.Message, "SSH bağlantı hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FindOpenSsh()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "OpenSSH", "ssh.exe")
        };
        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null) return found;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var file = Path.Combine(directory.Trim(), "ssh.exe");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("Windows OpenSSH Client bulunamadı. Windows Ayarları → Sistem → İsteğe bağlı özellikler → Özellik ekle → OpenSSH Client kurun.");
    }

    private static string QuoteArgument(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private void Disconnect_Click(object sender, RoutedEventArgs e) => DisconnectSession(showMessage: true);
    private async void Reconnect_Click(object sender, RoutedEventArgs e)
    {
        DisconnectSession(showMessage: false);
        await ConnectSelectedAsync();
    }

    private void DisconnectSession(bool showMessage)
    {
        try { session?.Dispose(); } catch { }
        session = null;
        keyStore.DeleteTemporaryKey(temporaryKeyPath);
        temporaryKeyPath = null;
        ConnectionDot.Fill = (Brush)FindResource("MutedBrush");
        if (showMessage)
        {
            ConnectionStatus.Text = "● Oturum kapatıldı";
            FooterRight.Text = "Bağlantı kesildi";
            _ = WriteTerminalAsync("\r\n[HATTRICK SSH] Yerel SSH işlemi kapatıldı.\r\n");
        }
    }

    private async Task WriteTerminalAsync(string text)
    {
        if (!terminalReady || TerminalWeb.CoreWebView2 is null) return;
        try { await TerminalWeb.ExecuteScriptAsync("window.hsshWrite(" + JsonSerializer.Serialize(text) + ");"); }
        catch { }
    }

    private void Snippet_Click(object sender, RoutedEventArgs e)
    {
        var command = (sender as Button)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(command) || session is null) return;
        session.WriteInput(command + "\r");
    }

    private async void ClearTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (terminalReady) await TerminalWeb.ExecuteScriptAsync("window.hsshClear();");
    }

    private async void FontDown_Click(object sender, RoutedEventArgs e)
    {
        settings.TerminalFontSize = Math.Clamp(settings.TerminalFontSize - 1, 10, 30);
        await ApplyTerminalSettingsAsync();
        store.SaveSettings(settings);
    }

    private async void FontUp_Click(object sender, RoutedEventArgs e)
    {
        settings.TerminalFontSize = Math.Clamp(settings.TerminalFontSize + 1, 10, 30);
        await ApplyTerminalSettingsAsync();
        store.SaveSettings(settings);
    }

    private async Task ApplyTerminalSettingsAsync()
    {
        if (terminalReady)
            await TerminalWeb.ExecuteScriptAsync($"window.hsshSetFontSize({settings.TerminalFontSize});");
    }

    private void LoadSettingsIntoUi()
    {
        FontSizeBox.Text = settings.TerminalFontSize.ToString();
        ScrollbackBox.Text = settings.TerminalScrollback.ToString();
        AutoReconnectCheck.IsChecked = settings.AutoReconnect;
        AutoUpdateCheck.IsChecked = settings.CheckUpdatesOnStartup;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(FontSizeBox.Text, out var font) || font is < 10 or > 30 ||
            !int.TryParse(ScrollbackBox.Text, out var scroll) || scroll is < 500 or > 100000)
        {
            MessageBox.Show("Yazı boyutu 10–30, kaydırma geçmişi 500–100000 aralığında olmalı.", "Ayar geçersiz", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        settings.TerminalFontSize = font;
        settings.TerminalScrollback = scroll;
        settings.AutoReconnect = AutoReconnectCheck.IsChecked == true;
        settings.CheckUpdatesOnStartup = AutoUpdateCheck.IsChecked == true;
        store.SaveSettings(settings);
        _ = ApplyTerminalSettingsAsync();
        MessageBox.Show("Tercihler kaydedildi.", "Hattrick SSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(showNoUpdate: true);

    private async Task CheckUpdatesAsync(bool showNoUpdate)
    {
        try
        {
            UpdateStatusText.Text = "GitHub Releases kontrol ediliyor…";
            var current = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var update = await updater.CheckAsync(current);
            if (update is null)
            {
                UpdateStatusText.Text = "Yeni sürüm yok veya henüz bir GitHub Release yayımlanmadı.";
                if (showNoUpdate) MessageBox.Show(UpdateStatusText.Text, "Güncelleme", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            UpdateStatusText.Text = $"Yeni sürüm bulundu: v{update.Version}";
            if (MessageBox.Show($"Hattrick SSH v{update.Version} bulundu. SHA-256 doğrulamalı kurulum dosyası indirilsin mi?",
                "Güncelleme bulundu", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                UpdateStatusText.Text = "Kurulum dosyası indiriliyor ve doğrulanıyor…";
                await updater.DownloadAndLaunchInstallerAsync(update);
                closing = true;
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Güncelleme kontrolü başarısız: " + ex.Message;
            if (showNoUpdate) MessageBox.Show(ex.Message, "Güncelleme hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenReleases_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://github.com/dincer552/deskopssh/releases") { UseShellExecute = true });

    private void SaveProfiles() => store.SaveProfiles(profiles);

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        closing = true;
        DisconnectSession(showMessage: false);
    }
}
