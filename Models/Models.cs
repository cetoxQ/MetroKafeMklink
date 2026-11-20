using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MetroKafeMklink.Models;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum LinkState { Unknown, Linked, Missing, RealFolder, WrongTarget, TargetMissing }

public class LinkEntry : Observable
{
    private string _name = "";
    private string _group = "";
    private string _linkPath = "";
    private string _targetPath = "";
    private bool _enabled = true;
    private LinkState _state;
    private string _detail = "";
    private string _resolvedLink = "";
    private string _resolvedTarget = "";

    public string Name { get => _name; set => Set(ref _name, value); }
    public string Group { get => _group; set => Set(ref _group, value); }
    /// <summary>Junction'in duracagi yer (ornek: %APPDATA%\Riot Client)</summary>
    public string LinkPath { get => _linkPath; set => Set(ref _linkPath, value); }
    /// <summary>Verinin gercekte durdugu yer (ornek: {MERKEZ}\Riot\AppData\Riot Client)</summary>
    public string TargetPath { get => _targetPath; set => Set(ref _targetPath, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    [JsonIgnore]
    public LinkState State
    {
        get => _state;
        set { if (Set(ref _state, value)) OnPropertyChanged(nameof(StateText)); }
    }

    [JsonIgnore] public string Detail { get => _detail; set => Set(ref _detail, value); }
    [JsonIgnore] public string ResolvedLink { get => _resolvedLink; set => Set(ref _resolvedLink, value); }
    [JsonIgnore] public string ResolvedTarget { get => _resolvedTarget; set => Set(ref _resolvedTarget, value); }

    [JsonIgnore]
    public string StateText => State switch
    {
        LinkState.Linked => "Bağlı",
        LinkState.Missing => "Bağlı değil",
        LinkState.RealFolder => "Gerçek klasör",
        LinkState.WrongTarget => "Yanlış hedef",
        LinkState.TargetMissing => "Hedef yok",
        _ => "Bilinmiyor",
    };

    public LinkEntry Clone() => new()
    {
        Name = Name, Group = Group, LinkPath = LinkPath, TargetPath = TargetPath, Enabled = Enabled,
    };

    public void CopyFrom(LinkEntry o)
    {
        Name = o.Name; Group = o.Group; LinkPath = o.LinkPath; TargetPath = o.TargetPath; Enabled = o.Enabled;
    }
}

public class RegEntry : Observable
{
    private string _name = "";
    private string _group = "";
    private string _key = "";
    private string _fileName = "";
    private bool _enabled = true;
    private bool _keyExists;
    private bool _fileExists;
    private string _fileInfo = "";
    private string _resolvedFile = "";

    public string Name { get => _name; set => Set(ref _name, value); }
    public string Group { get => _group; set => Set(ref _group, value); }
    /// <summary>Ornek: HKLM\SOFTWARE\Classes\riotclient</summary>
    public string Key { get => _key; set => Set(ref _key, value); }
    /// <summary>{MERKEZ}\Registry altindaki .reg dosyasinin adi</summary>
    public string FileName { get => _fileName; set => Set(ref _fileName, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    [JsonIgnore] public bool KeyExists { get => _keyExists; set => Set(ref _keyExists, value); }
    [JsonIgnore] public bool FileExists { get => _fileExists; set => Set(ref _fileExists, value); }
    [JsonIgnore] public string FileInfo { get => _fileInfo; set => Set(ref _fileInfo, value); }
    [JsonIgnore] public string ResolvedFile { get => _resolvedFile; set => Set(ref _resolvedFile, value); }

    public RegEntry Clone() => new() { Name = Name, Group = Group, Key = Key, FileName = FileName, Enabled = Enabled };

    public void CopyFrom(RegEntry o)
    {
        Name = o.Name; Group = o.Group; Key = o.Key; FileName = o.FileName; Enabled = o.Enabled;
    }
}

public class AppSettings
{
    public string CenterPath { get; set; } = Directory.Exists(@"D:\") ? @"D:\MetroKafeMerkez" : @"C:\MetroKafeMerkez";
    public string CloseProcesses { get; set; } =
        "RiotClientServices, RiotClientUx, RiotClientUxRender, RiotClientCrashHandler, LeagueClient, LeagueClientUx, LeagueClientUxRender, EpicGamesLauncher";
    public bool CheckUpdatesOnStart { get; set; } = true;
    public string GitHubRepo { get; set; } = "cetoxQ/MetroKafeMklink";
    /// <summary>DPAPI (LocalMachine) ile sifrelenmis GitHub token</summary>
    public string EncryptedToken { get; set; } = "";
}

public class AppConfig
{
    public AppSettings Settings { get; set; } = new();
    public ObservableCollection<LinkEntry> Links { get; set; } = new();
    public ObservableCollection<RegEntry> Registry { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        var c = new AppConfig();
        void Link(string group, string name, string link, string target, bool enabled = true) =>
            c.Links.Add(new LinkEntry { Group = group, Name = name, LinkPath = link, TargetPath = target, Enabled = enabled });

        Link("Riot", "Riot Games (oyunlar)", @"C:\Riot Games", @"{MERKEZ}\Riot\Riot Games");
        Link("Riot", "Riot kurulum kayıtları", @"%PROGRAMDATA%\Riot Games", @"{MERKEZ}\Riot\ProgramData\Riot Games");
        Link("Riot", "Riot LocalAppData", @"%LOCALAPPDATA%\Riot Games", @"{MERKEZ}\Riot\LocalAppData\Riot Games");
        Link("Riot", "Riot Client (AppData)", @"%APPDATA%\Riot Client", @"{MERKEZ}\Riot\AppData\Riot Client");
        Link("Riot", "Riot Client UX", @"%APPDATA%\riot-client-ux", @"{MERKEZ}\Riot\AppData\riot-client-ux");
        Link("Epic", "Epic Launcher v2 verisi", @"%LOCALAPPDATA%\com.epicgames.eglv2", @"{MERKEZ}\Epic\LocalAppData\com.epicgames.eglv2", enabled: false);

        c.Registry.Add(new RegEntry
        {
            Group = "Riot", Name = "Riot Client protokolü", Key = @"HKLM\SOFTWARE\Classes\riotclient", FileName = "riotclient.reg",
        });
        return c;
    }
}
