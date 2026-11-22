using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;
using SharpDX;
using Vector2N = System.Numerics.Vector2;
// SquirrelUI.cs is a drop-in shared file in the global namespace; keep old call sites.
using UiHelpers = SquirrelUi;

namespace ModernBar
{
    public class ModernBar : BaseSettingsPlugin<ModernBarSettings>
    {
        private const int UPDATE_INTERVAL_MS = 1000;
        private const float HORIZONTAL_PADDING = 15f;
        private const float VERTICAL_PADDING = 5f;
        private const float CORNER_RADIUS = 10f;
        private const int PING_DOT_SIZE = 8;
        private const float SEGMENT_GAP = 12f;
        private const float LABEL_GAP = 6f;

        private TimeCache<bool> _calc;
        private TimeCache<bool> _hideCache;
        private TimeCache<float> _rosterCache;
        private bool _canRender = true;
        private readonly Queue<int> _pingHistory = new();
        private readonly Dictionary<string, RectangleF> _segmentBounds = new();
        private readonly Segment[] _segmentPool = new Segment[12];
        private readonly float[] _segmentWidths = new float[12];
        private int _segmentCount;
        private ColorScheme _cachedScheme;
        private static string _editingSliderId;
        private static bool _sliderEditFocus;

        private string _level = "";
        private int _levelDiff;
        private int _levelPen;
        private string _levelVerdict = "EVEN";
        private float _levelMarker = 0.5f;
        private string _area = "";
        private string _tier = "";
        private string _areaTime = "";
        private string _timeMerged = "";
        private string _xp = "";
        private float _percentGot;
        private string _gold = "";
        private string _goldPerHour = "";
        private string _ping = "";
        private Color _pingBarColor = Color.Green;

        private static readonly Color DpsPlayerColor = new Color((byte)255, (byte)85, (byte)85, (byte)255);
        private static readonly Color DpsMinionColor = new Color((byte)90, (byte)169, (byte)255, (byte)255);
        private TimeCache<bool> _dpsCalc;
        private readonly Dictionary<uint, int> _dpsLastHp = new();
        private readonly Queue<(DateTime At, int Amount)> _dpsWindow = new();
        private DateTime? _dpsPausedAt;
        private readonly HashSet<uint> _dpsSeen = new();
        private readonly List<uint> _dpsGone = new();
        private string _dpsText = "";
        private string _dpsPeakText = "";
        private double _dpsValue;
        private double _dpsPeak;
        private int _alliedCount;
        private bool _playerAttacking;
        private bool _inTown;
        private char _dpsLeader;
        private char _dpsPendingLeader;
        private int _dpsLeaderStreak;
        private string _dpsTag = "";
        private string _remPlusLabel = "";
        private int _remPlusHidden = -1;
        private int _remPlusFrom = -1;
        private Color _dpsTagColor;
        private float _contentBottom;

        private long _startXp;
        private long _areaStartXp;
        private int _areaStartLevel;
        private TimeSpan _areaAccumulated = TimeSpan.Zero;
        private DateTime _areaLastTick = DateTime.UtcNow;
        private TimeSpan _xpActive = TimeSpan.Zero;
        private double _lastAreaXpGained;
        private TimeSpan _lastAreaTimeSpent = TimeSpan.Zero;
        private AreaInstance _previousArea;

        private long _sessionGoldGained;
        private TimeSpan _sessionElapsed;
        private long _lastInvGold;
        private long _lastStoredGold;
        private long _lastAreaGoldGained;
        private DateTime _lastGoldActivity = DateTime.UtcNow;
        private TimeSpan _emptyIn = TimeSpan.Zero;
        private long _lastStoredForCountdown = -1;
        private string _areaDetail = "";

        private sealed class PreloadHit
        {
            public string Name;
            public Color Color;
            public string Group;
        }

        private sealed class PreloadChip
        {
            public string Label;
            public string Suffix;
            public Color Color;
            public string Title;
            public string GroupName;
            public bool IsRemainder;
            public int HitCount = 1;
            public int HiddenCount;
            public RectangleF Bounds;
            public float WidthPx;
            public readonly List<(string Text, Color Color)> Rows = new();
            public readonly List<(string Group, List<(string Text, Color Color)> Rows)> Groups = new();
        }

        private readonly List<CompiledPreload> _compiledPreloads = new();
        private readonly object _preloadLock = new();
        private List<PreloadHit> _pendingHits;
        private List<PreloadFileGroup> _preloadFile = new();
        private string _preloadFilePath;
        private int _preloadGroupSel;
        private bool _preloadDirty;
        private DateTime _lastPreloadEdit = DateTime.UtcNow;
        private bool _confirmDelGroup;
        private string _testPath = "";
        private string _testResult = "";
        private int _colorEditG = -1;
        private int _colorEditP = -1;
        private System.Numerics.Vector4 _colorEditValue;
        private bool _colorPickerOpen;
        private string _patternFilter = "";
        private readonly Dictionary<int, float> _plusWidths = new();
        private readonly Dictionary<string, string> _validatedText = new();
        private readonly Dictionary<string, bool> _validatedOk = new();
        private DateTime _preloadRescanAt = DateTime.MaxValue;
        private int _settingsTab;

        private readonly List<PreloadChip> _preloadChips = new();
        private PreloadChip _preloadRemainder;
        private RectangleF _barBounds;

        private sealed class Segment
        {
            public string Id;
            public string Label;
            public string Value;
            public Color ValueColor;
            public bool IsPing;
            public bool IsArea;
            public bool IsDps;
            public bool IsLevelCheck;
        }

        public override void OnLoad() => Order = 1000; // draw late: bar paints over Radar and the Order-0 crowd, under AutoAugury (5001).

        public override bool Initialise()
        {
            GameController.LeftPanel.WantUse(() => Settings.Enable);
            ResetSession(GameController.Player);
            Settings.ColorScheme.Values = new List<string>(Enum.GetNames(typeof(ColorSchemeList)));
            if (!Enum.TryParse(Settings.ColorScheme.Value, true, out ColorSchemeList _))
                Settings.ColorScheme.Value = nameof(ColorSchemeList.Default);
            _calc = new TimeCache<bool>(UpdateInfo, UPDATE_INTERVAL_MS);
            _calc.ForceUpdate();
            // Cover check: 10 Hz cache so Render pays one bool read, not ~49 IsVisible walks.
            _hideCache = new TimeCache<bool>(BarCovered, 100);
            _hideCache.ForceUpdate();
            // Boss-bar dodge for the preload strip only (10 Hz). The main bar
            // stays top — it never overlapped the roster, the strip did.
            _rosterCache = new TimeCache<float>(RosterBottom, 100);
            _rosterCache.ForceUpdate();
            _dpsCalc = new TimeCache<bool>(SampleDps, 250);
            LoadPreloadPatterns();
            StartPreloadScan();
            Settings.ResetThemeColors.OnPressed += ResetThemeColors;
            Settings.ColorScheme.OnValueSelected += _ => UpdateColorNodes();
            UpdateColorNodes();
            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            try
            {
                if (area == null) return;
                var wasFarming = _previousArea != null && !_previousArea.IsHideout && !_previousArea.IsTown;
                if (wasFarming)
                {
                    _lastAreaXpGained = (GameController.Player?.GetComponent<Player>()?.XP ?? 0) - _areaStartXp;
                    _lastAreaTimeSpent = _areaAccumulated;
                    _lastAreaGoldGained = _sessionGoldGained;
                }
                _areaAccumulated = TimeSpan.Zero;
                _areaLastTick = DateTime.UtcNow;
                _areaStartXp = GameController.Player?.GetComponent<Player>()?.XP ?? 0;
                _areaStartLevel = GameController.Player?.GetComponent<Player>()?.Level ?? 0;
                _sessionGoldGained = 0;
                _sessionElapsed = TimeSpan.Zero;
                _dpsPeak = 0;
                _dpsPeakText = "";
                _dpsWindow.Clear();
                _dpsPausedAt = null;
                _dpsLastHp.Clear();
                _dpsLeader = '\0';
                _dpsPendingLeader = '\0';
                _dpsLeaderStreak = 0;
                var (cg, sg) = GetGold();
                _lastInvGold = cg;
                _lastStoredGold = sg;
                _lastGoldActivity = DateTime.UtcNow;
                StartPreloadScan();
            }
            catch { }
        }

        private void ResetSession(Entity entity)
        {
            var p = entity?.GetComponent<Player>();
            if (p == null) return;
            _startXp = p.XP;
            _areaStartXp = p.XP;
            _areaStartLevel = p.Level;
            _areaAccumulated = TimeSpan.Zero;
            _xpActive = TimeSpan.Zero;
            _areaLastTick = DateTime.UtcNow;
        }

        private bool UpdateInfo()
        {
            var now = DateTime.UtcNow;
            try
            {
                var area = GameController.Area.CurrentArea;
                // Sampling never stops for menus: XP/gold/time accrue while panels are
                // open (minions kill, rent ticks). Only a missing area hides the bar.
                if (area == null)
                {
                    _canRender = false;
                    _areaLastTick = now;
                    return false;
                }
                _canRender = true;
                _inTown = area.IsHideout || area.IsTown;

                List<PreloadHit> ready = null;
                lock (_preloadLock)
                {
                    if (_pendingHits != null) { ready = _pendingHits; _pendingHits = null; }
                }
                if (Settings.ShowPreloads.Value && now >= _preloadRescanAt)
                {
                    _preloadRescanAt = DateTime.MaxValue;
                    var areaNow = GameController.Area.CurrentArea;
                    if (areaNow != null && ((!areaNow.IsHideout && !areaNow.IsTown) || Settings.ShowPreloadInHideout.Value))
                    {
                        var changeCount = GameController.Game.AreaChangeCount;
                        var areaName = areaNow.DisplayName ?? "";
                        var snapshot = _compiledPreloads.ToArray();
                        Task.Run(() => ScanPreloads(changeCount, areaName, snapshot));
                    }
                }
                if (ready != null)
                {
                    ready.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                    BuildPreloadChips(ready);
                    CachePreloadWidths();
                }
                if (_preloadDirty && (now - _lastPreloadEdit).TotalSeconds >= 2)
                {
                    SavePreloadFile();
                    _preloadDirty = false;
                }

                var player = GameController.Player?.GetComponent<Player>();
                if (player == null) return false;
                var level = player.Level;
                var xp = player.XP;
                _level = $"{level}";

                var suffix = area.RealLevel >= 68 ? area.RealLevel - 67 : 0;
                _area = area.DisplayName ?? "";
                var dash = _area.LastIndexOf(" (", StringComparison.Ordinal);
                if (dash > 0 && area.RealLevel < 68) _area = _area.Substring(0, dash);
                _tier = suffix > 0 ? $"T{suffix}" : "";

                _areaTime = AreaInstance.GetTimeString(_areaAccumulated);
                _areaDetail = $"ACT {area.Act}  MONSTER LEVEL {area.RealLevel}";
                _levelDiff = area.RealLevel - level;
                int safeZone = 3 + level / 16;
                int effDiff = Math.Max(Math.Abs(_levelDiff) - safeZone, 0);
                double mult = effDiff <= 0 ? 1.0 : Math.Max(Math.Pow((level + 5) / (level + 5 + Math.Pow(effDiff, 2.5)), 1.5), 0.01);
                _levelPen = (int)Math.Round((1 - mult) * 100);
                _levelVerdict = effDiff <= 0 ? "EVEN" : (area.RealLevel > level ? "UNDER" : "OVER");
                int half = Math.Max(safeZone + 5, 8);
                _levelMarker = Math.Max(0.03f, Math.Min(0.97f, 0.5f + (float)_levelDiff / half * 0.5f));

                // Single frozen clock: menus, loading and pause stop TIME, XP/H and G/H together.
                var frozen = GameController.IsLoading
                    || (GameController.Game.IngameState.Data?.IsPaused ?? false)
                    || MenusOpenForTimer();
                var dtActive = now - _areaLastTick;
                if (!frozen)
                {
                    _areaAccumulated += dtActive;
                    _xpActive += dtActive;
                    if (!area.IsHideout && !area.IsTown && !area.IsPeaceful)
                        _sessionElapsed += dtActive;
                }
                _areaLastTick = now;

                // XP rate + merged ETA
                var elapsedH = _xpActive.TotalHours;
                var rate = elapsedH > 0 ? (xp - _startXp) / elapsedH : 0;
                string next = "--", to100 = "--";
                _percentGot = 0f;
                if (level < 100 && level >= 0 && level + 1 < Constants.PlayerXpLevels.Length)
                {
                    var span = (float)(Constants.PlayerXpLevels[level + 1] - Constants.PlayerXpLevels[level]);
                    if (span > 0) _percentGot = Math.Max(0f, Math.Min(1f, (float)(xp - Constants.PlayerXpLevels[level]) / span));
                    if (rate > 1)
                    {
                        var left = Constants.PlayerXpLevels[level + 1] - xp;
                        var t = TimeSpan.FromHours(left / rate);
                        next = $"{(int)t.TotalHours}h {t.Minutes:00}m";
                        var target = Constants.PlayerXpLevels.Length > 100 ? Constants.PlayerXpLevels[100] : Constants.PlayerXpLevels[^1];
                        var rem = target - xp;
                        if (rem > 0)
                        {
                            var t100 = TimeSpan.FromHours(rem / rate);
                            to100 = t100.Days > 0 ? $"{t100.Days}d {t100.Hours:00}h" : $"{(int)t100.TotalHours}h {t100.Minutes:00}m";
                        }
                    }
                }
                _xp = $"{ConvertHelper.ToShorten(rate, "0.00")} XP/H {_percentGot:P0}".ToUpperInvariant();
                _timeMerged = $"NEXT {next} / 100 {to100}";

                // Gold
                var (cg2, sg2) = GetGold();
                var dInv = cg2 - _lastInvGold;
                if (dInv > 0) { _sessionGoldGained += dInv; _lastGoldActivity = DateTime.UtcNow; }
                _lastInvGold = cg2;
                _lastStoredGold = sg2;
                if (DateTime.UtcNow - _lastGoldActivity > TimeSpan.FromMinutes(5))
                {
                    _sessionGoldGained = 0;
                    _sessionElapsed = TimeSpan.Zero;
                    _lastGoldActivity = DateTime.UtcNow;
                }
                var gRate = _sessionElapsed.TotalHours > 0 ? _sessionGoldGained / _sessionElapsed.TotalHours : 0;
                _gold = $"{cg2:N0} ({sg2:N0})";
                _goldPerHour = area.IsHideout || area.IsTown ? "N/A" : ConvertHelper.ToShorten(gRate, "0.0");
                UpdateCountdown(TimeSpan.FromMilliseconds(UPDATE_INTERVAL_MS), sg2);

                // Ping
                var latency = GameController.Game.IngameState.ServerData.Latency;
                _ping = $"{latency}";
                _pingBarColor = latency <= 100 ? Color.Green : latency <= 150 ? Color.Yellow : Color.Red;
                _pingHistory.Enqueue(latency);
                int pingCap = Settings.PingHistoryMinutes.Value * 60;
                while (_pingHistory.Count > pingCap) _pingHistory.Dequeue();

                char det = '\0';
                if (_alliedCount == 0 && _dpsValue > 0) det = 'P';
                else if (_alliedCount > 0 && !_playerAttacking && _dpsValue > 0) det = 'M';
                if (det != '\0')
                {
                    if (det == _dpsPendingLeader) { _dpsLeaderStreak++; if (_dpsLeaderStreak >= 2) _dpsLeader = det; }
                    else { _dpsPendingLeader = det; _dpsLeaderStreak = 1; }
                }
                return true;
            }
            catch
            {
                _canRender = false;
                return false;
            }
        }

        private bool BarCovered()
        {
            // Hide predicate: only panels that actually cover the bar. The inventory
            // is NOT here on purpose: it sits on the right and never covers center-top,
            // and players run maps with it open — hiding + freezing then corrupts data.
            try
            {
                var ui = GameController.Game.IngameState.IngameUi;
                if (ui.OpenLeftPanel.IsVisible) return true;
                if (ui.OpenRightPanel.IsVisible) return true;
                if (ui.TreePanel.IsVisible) return true;
                if (ui.Atlas.IsVisible) return true;
                try { if (ui.AtlasTreePanel.IsVisible) return true; } catch { }
                try { if (ui.AtlasPanel.IsVisible) return true; } catch { }
                try { if (ui.SyndicatePanel.IsVisibleLocal) return true; } catch { }
                try { if (ui.NpcDialog.IsVisible) return true; } catch { }
                try { if (ui.QuestRewardWindow.IsVisible) return true; } catch { }
                // FullscreenPanels: passive trees (x2), Atlas, Betrayal, Settlers. LargePanels: vendors,
                // Delve/Incursion/Ritual/Ultimatum/etc. Looping both covers current + future league windows
                // without enumerating each one by name.
                try
                {
                    var fs = ui.FullscreenPanels;
                    if (fs != null) for (int i = 0; i < fs.Count; i++)
                    {
                        try { if (fs[i] != null && fs[i].IsVisible) return true; } catch { }
                    }
                }
                catch { }
                try
                {
                    var lp = ui.LargePanels;
                    if (lp != null) for (int i = 0; i < lp.Count; i++)
                    {
                        try { if (lp[i] != null && lp[i].IsVisible) return true; } catch { }
                    }
                }
                catch { }
                return false;
            }
            catch { return false; }
        }

        private float RosterBottom()
        {
            // Arena/boss roster bottom edge, or 0. The container stays IsVisible
            // with an empty corpse (h~80, verified live); only a live roster
            // (h~169) counts. BarCovered's loops already hide the whole bar for
            // fullscreen panels — this is for the strip sliding under the roster.
            try
            {
                var roster = GameController.Game.IngameState.IngameUi.PVPTreePanel;
                if (roster == null || !roster.IsVisible) return 0f;
                var r = roster.GetClientRect();
                if (r.Height <= 85 || r.Width < 100) return 0f;
                return r.Bottom + 4;
            }
            catch { return 0f; }
        }

        private bool MenusOpenForTimer()
        {
            // Freeze wall-clocks only when the game truly stops. A menu open mid-map
            // still accrues XP/gold/time (minions kill, rent ticks) — freezing then
            // inflates XP/H on close (XP rises, clock doesn't) and zeroes DPS.
            try
            {
                return GameController.IsLoading
                    || (GameController.Game.IngameState.Data?.IsPaused ?? false);
            }
            catch { return false; }
        }

        private void LoadPreloadPatterns()
        {
            try
            {
                _preloadFilePath = PreloadFileStore.ResolvePath(DirectoryFullName, GetType().Assembly.GetName().Name ?? "ModernBar");
                if (_preloadFilePath == null)
                {
                    _compiledPreloads.Clear();
                    _preloadFile = new List<PreloadFileGroup>();
                    return;
                }
                _preloadFile = PreloadFileStore.Load(_preloadFilePath);
                CompilePreloadFile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModernBar] LoadPreloadPatterns error: {ex.Message}");
            }
        }

        private void CompilePreloadFile()
        {
            _compiledPreloads.Clear();
            foreach (var g in _preloadFile)
            {
                if (g == null || !g.Enable || g.Patterns == null) continue;
                foreach (var p in g.Patterns)
                {
                    if (p == null || !p.Enable || string.IsNullOrWhiteSpace(p.Path) || string.IsNullOrWhiteSpace(p.Display)) continue;
                    try
                    {
                        _compiledPreloads.Add(new CompiledPreload
                        {
                            Group = g.Name,
                            Display = p.Display,
                            Regex = new Regex(p.Path, RegexOptions.Compiled),
                            Color = PreloadFileStore.ParseHex(p.ColorHex, new Color((byte)255, (byte)255, (byte)255, (byte)255)),
                        });
                    }
                    catch { }
                }
            }
        }

        private void TouchPreloads()
        {
            _preloadDirty = true;
            _lastPreloadEdit = DateTime.UtcNow;
        }

        private void SavePreloadFile()
        {
            if (_preloadFilePath == null) return;
            PreloadFileStore.Save(_preloadFilePath, _preloadFile);
            CompilePreloadFile();
            StartPreloadScan();
        }

        private void StartPreloadScan()
        {
            try
            {
                var area = GameController.Area.CurrentArea;
                if (area == null) return;
                if ((area.IsHideout || area.IsTown) && !Settings.ShowPreloadInHideout.Value)
                {
                    lock (_preloadLock) _pendingHits = new List<PreloadHit>();
                    _preloadRescanAt = DateTime.MaxValue;
                    return;
                }
                if (_compiledPreloads.Count == 0) return;
                var changeCount = GameController.Game.AreaChangeCount;
                var areaName = area.DisplayName ?? "";
                // Snapshot: CompilePreloadFile mutates _compiledPreloads on the main thread;
                // handing the worker a copy avoids a race that silently drops the scan.
                var snapshot = _compiledPreloads.ToArray();
                Task.Run(() => ScanPreloads(changeCount, areaName, snapshot));
                _preloadRescanAt = DateTime.UtcNow.AddSeconds(5);
            }
            catch { }
        }

        private void ScanPreloads(int areaChangeCount, string areaName, CompiledPreload[] patterns)
        {
            var hits = new List<PreloadHit>();
            try
            {
                var files = new FilesFromMemory(GameController.Memory).GetAllFiles();
                foreach (var kv in files)
                {
                    if (kv.Value.ChangeCount != areaChangeCount) continue;
                    var text = kv.Key;
                    var at = text.IndexOf('@');
                    if (at >= 0) text = text.Substring(0, at);
                    foreach (var p in patterns)
                    {
                        bool match;
                        try { match = p.Regex.IsMatch(text); }
                        catch { continue; }
                        if (!match) continue;
                        hits.Add(new PreloadHit { Name = p.Display, Color = p.Color, Group = p.Group });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModernBar] ScanPreloads error: {ex.Message}");
                return;
            }
            lock (_preloadLock) _pendingHits = hits;
        }

        private bool SampleDps()
        {
            // Segment hidden in town and when disabled: skip the full entity scan.
            if (!Settings.ShowDps.Value || _inTown) return false;
            // Menus don't stop DPS either — only true pause freezes (handled below).
            // True pause (Esc): freeze the window. Without this the 3 s window drains
            // while paused and DPS melts to zero. Menus deliberately don't freeze —
            // an open inventory mid-fight still decays.
            bool paused = false;
            try { paused = GameController.Game.IngameState.Data?.IsPaused == true; } catch { }
            if (paused) { _dpsPausedAt ??= DateTime.UtcNow; return true; }
            if (_dpsPausedAt != null)
            {
                var shift = DateTime.UtcNow - _dpsPausedAt.Value;
                _dpsPausedAt = null;
                if (shift > TimeSpan.Zero && _dpsWindow.Count > 0)
                {
                    var tmp = _dpsWindow.ToArray();
                    _dpsWindow.Clear();
                    foreach (var (at, amt) in tmp) _dpsWindow.Enqueue((at + shift, amt));
                }
            }
            try
            {
                var now = DateTime.UtcNow;
                _dpsSeen.Clear();
                int allies = 0;
                bool attacking = false;
                var player = GameController.Player;
                if (player == null) return false;
                var actor = player.GetComponent<Actor>();
                if (actor != null) attacking = actor.isAttacking;
                foreach (var e in GameController.Entities)
                {
                    if (e == null || !e.IsValid || !e.IsAlive) continue;
                    if (e.Type != EntityType.Monster) continue;
                    var life = e.GetComponent<Life>();
                    if (life == null || life.MaxHP <= 0) continue;
                    if (e.IsHostile)
                    {
                        int cur = life.CurHP > int.MaxValue ? int.MaxValue : (int)life.CurHP;
                        _dpsSeen.Add(e.Id);
                        if (_dpsLastHp.TryGetValue(e.Id, out var prev))
                        {
                            int dealt = prev - cur;
                            if (dealt > 0) _dpsWindow.Enqueue((now, dealt));
                        }
                        _dpsLastHp[e.Id] = cur;
                    }
                    else if (e.Id != player.Id)
                    {
                        allies++;
                    }
                }
                _playerAttacking = attacking;
                _alliedCount = allies;
                _dpsGone.Clear();
                foreach (var kv in _dpsLastHp) if (!_dpsSeen.Contains(kv.Key)) _dpsGone.Add(kv.Key);
                foreach (var id in _dpsGone) _dpsLastHp.Remove(id);
                while (_dpsWindow.Count > 0 && (now - _dpsWindow.Peek().At).TotalSeconds > 3) _dpsWindow.Dequeue();
                int sum = 0;
                foreach (var s in _dpsWindow) sum += s.Amount;
                double span = 0.5;
                if (_dpsWindow.Count > 0) span = Math.Max(0.5, (now - _dpsWindow.Peek().At).TotalSeconds);
                _dpsValue = sum / span;
                if (_dpsValue > _dpsPeak) _dpsPeak = _dpsValue;
                _dpsText = FormatNumber(_dpsValue);
                _dpsPeakText = FormatNumber(_dpsPeak);
                return true;
            }
            catch { return false; }
        }

        private (long inv, long stored) GetGold()
        {
            try
            {
                return (GameController.Game.IngameState.ServerData.Gold,
                    GameController.Game.IngameState.IngameUi.VillageScreen?.CurrentGold ?? 0);
            }
            catch { return (0, 0); }
        }

        private void UpdateCountdown(TimeSpan dt, long stored)
        {
            try
            {
                var vs = GameController.Game.IngameState.IngameUi.VillageScreen;
                var wage = vs?.TotalWagePerHour ?? 0;
                if (stored > 0 && wage > 0)
                {
                    if (stored != _lastStoredForCountdown)
                    {
                        _emptyIn = TimeSpan.FromHours((double)stored / wage);
                        _lastStoredForCountdown = stored;
                    }
                    else _emptyIn -= dt;
                    if (_emptyIn < TimeSpan.Zero) _emptyIn = TimeSpan.Zero;
                }
                else _emptyIn = TimeSpan.Zero;
            }
            catch { _emptyIn = TimeSpan.Zero; }
        }

        internal static string FormatNumber(double num)
        {
            if (num >= 1000000000) return (num / 1000000000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "b";
            if (num >= 1000000) return (num / 1000000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "m";
            if (num >= 1000) return (num / 1000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k";
            return num.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Color LerpColor(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return new Color((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)255);
        }

        // Hover cards use the theme bg: white text vanishes on light themes (Inverted).
        private static Color CardText(ColorScheme scheme)
        {
            var bg = scheme.Background;
            float lum = (0.299f * bg.R + 0.714f * bg.G + 0.072f * bg.B) / 255f;
            return lum > 0.6f ? new Color((byte)20, (byte)20, (byte)20, (byte)255) : Color.White;
        }

        private ColorScheme Scheme() => _cachedScheme ??= new CustomColorScheme(Settings);

        private ColorScheme SchemeFor(ColorSchemeList s) => s switch
        {
            ColorSchemeList.SolarizedDark => new SolarizedDarkColorScheme(),
            ColorSchemeList.Dracula => new DraculaColorScheme(),
            ColorSchemeList.Inverted => new InvertedColorScheme(),
            ColorSchemeList.Cyberpunk2077 => new Cyberpunk2077ColorScheme(),
            ColorSchemeList.Minecraft => new MinecraftColorScheme(),
            ColorSchemeList.Valorant => new ValorantColorScheme(),
            ColorSchemeList.Diablo => new DiabloColorScheme(),
            ColorSchemeList.Monochrome => new MonochromeColorScheme(),
            _ => new DefaultColorScheme(),
        };

        private void UpdateColorNodes()
        {
            if (!Enum.TryParse(Settings.ColorScheme.Value, true, out ColorSchemeList s) || s == ColorSchemeList.Custom) return;
            var c = SchemeFor(s);
            Settings.BackgroundColor.Value = c.Background;
            Settings.LabelColor.Value = c.Label;
            Settings.GoldColor.Value = c.Gold;
            Settings.TimerColor.Value = c.Timer;
            Settings.PingColor.Value = c.Ping;
            Settings.AreaColor.Value = c.Area;
            Settings.TimeLeftColor.Value = c.TimeLeft;
            Settings.XphColor.Value = c.Xph;
            Settings.XphGetLeftColor.Value = c.XphGetLeft;
        }

        private void ResetThemeColors()
        {
            if (!Enum.TryParse(Settings.ColorScheme.Value, true, out ColorSchemeList s)) return;
            if (s == ColorSchemeList.Custom)
            {
                Settings.BackgroundColor.Value = Color.White;
                Settings.LabelColor.Value = Color.White;
                Settings.GoldColor.Value = Color.White;
                Settings.TimerColor.Value = Color.White;
                Settings.PingColor.Value = Color.White;
                Settings.AreaColor.Value = Color.White;
                Settings.TimeLeftColor.Value = Color.White;
                Settings.XphColor.Value = Color.White;
                Settings.XphGetLeftColor.Value = Color.White;
                return;
            }
            var c = SchemeFor(s);
            Settings.BackgroundColor.Value = c.Background;
            Settings.LabelColor.Value = c.Label;
            Settings.GoldColor.Value = c.Gold;
            Settings.TimerColor.Value = c.Timer;
            Settings.PingColor.Value = c.Ping;
            Settings.AreaColor.Value = c.Area;
            Settings.TimeLeftColor.Value = c.TimeLeft;
            Settings.XphColor.Value = c.Xph;
            Settings.XphGetLeftColor.Value = c.XphGetLeft;
        }

        public override void Render()
        {
            if (!Settings.Enable.Value) return;
            _ = _calc.Value;
            _ = _dpsCalc.Value;
            _previousArea = GameController.Area.CurrentArea;
            // Immediate check: _canRender refreshes at 1 Hz, so a freshly opened panel would
            // linger up to a second without this. 10 Hz cache = ~1 bool read per frame.
            if (!_canRender || _hideCache.Value)
            {
                foreach (var c in _preloadChips) c.Bounds = RectangleF.Empty;
                if (_preloadRemainder != null) { _preloadRemainder.Bounds = RectangleF.Empty; _preloadRemainder.HiddenCount = 0; }
                return;
            }

            var scheme = Scheme();
            BuildSegments(scheme);
            _dpsTag = (_dpsLeader == 'P' || _dpsLeader == 'M') ? _dpsLeader.ToString() : "";
            _dpsTagColor = _dpsLeader == 'M' ? DpsMinionColor : DpsPlayerColor;

            var orig = GameController.LeftPanel.StartDrawPoint;
            _segmentBounds.Clear();
            if (_segmentCount == 0)
            {
                GameController.LeftPanel.StartDrawPoint = new Vector2(orig.X, orig.Y);
                return;
            }

            float maxH = 0f;
            for (int si = 0; si < _segmentCount; si++)
            {
                var s = _segmentPool[si];
                var h = Math.Max(Graphics.MeasureText(s.Label).Y, Graphics.MeasureText(s.Value).Y);
                if (s.IsArea && !string.IsNullOrEmpty(_tier))
                    h = Math.Max(h, Graphics.MeasureText(_tier).Y);
                if (h > maxH) maxH = h;
            }

            float totalW = HORIZONTAL_PADDING * 2;
            for (int si = 0; si < _segmentCount; si++)
            {
                var s = _segmentPool[si];
                float w = 0f;
                if (!string.IsNullOrEmpty(s.Label)) w += Graphics.MeasureText(s.Label).X + LABEL_GAP;
                w += Graphics.MeasureText(s.Value).X;
                if (s.IsArea && !string.IsNullOrEmpty(_tier))
                    w += 6 + Graphics.MeasureText(_tier).X + 12;
                if (s.IsPing) w += 6 + 8;
                if (s.IsDps) w += 6 + DpsChipWidth();
                if (s.IsLevelCheck) w += 80;
                _segmentWidths[si] = w;
                totalW += w + SEGMENT_GAP * 2;
            }
            totalW -= SEGMENT_GAP * 2;

            var winW = GameController.Window.GetWindowRectangle().Width;
            var dx = new Vector2N((winW - totalW) / 2 + Settings.DrawXOffset.Value,
                orig.Y + Settings.DrawYOffset.Value);
            var barH = maxH + VERTICAL_PADDING * 2;
            var bg = scheme.Background;
            bg.A = (byte)Settings.BackgroundAlpha.Value;
            _barBounds = new RectangleF(dx.X, dx.Y, totalW, barH);
            Graphics.DrawBox(new RectangleF(dx.X, dx.Y, totalW, barH), bg, CORNER_RADIUS);

            var divCol = new Color((byte)255, (byte)255, (byte)255, (byte)Settings.DividerAlpha.Value);
            float cx = dx.X + HORIZONTAL_PADDING;
            for (int i = 0; i < _segmentCount; i++)
            {
                var s = _segmentPool[i];
                var startX = cx - SEGMENT_GAP;
                if (i > 0)
                    Graphics.DrawBox(new RectangleF(cx - SEGMENT_GAP, dx.Y + 6, 1, barH - 12), divCol);
                if (!string.IsNullOrEmpty(s.Label))
                {
                    var ls = Graphics.DrawText(s.Label, new Vector2N(cx, dx.Y + VERTICAL_PADDING), scheme.Label);
                    cx += ls.X + LABEL_GAP;
                }
                var vs = Graphics.DrawText(s.Value, new Vector2N(cx, dx.Y + VERTICAL_PADDING), s.ValueColor);
                cx += vs.X;
                if (s.IsArea && !string.IsNullOrEmpty(_tier))
                {
                    var chipX = cx + 6;
                    var chipText = _tier;
                    var tw = Graphics.MeasureText(chipText).X;
                    var chipBounds = new RectangleF(chipX, dx.Y + VERTICAL_PADDING + 1, tw + 12, maxH - 2);
                    var chipBg = new Color((byte)0, (byte)0, (byte)0, (byte)110);
                    Graphics.DrawBox(chipBounds, chipBg, 5f);
                    Graphics.DrawText(chipText, new Vector2N(chipX + 6, dx.Y + VERTICAL_PADDING), scheme.Area);
                    cx = chipBounds.Right;
                }
                if (s.IsPing)
                {
                    cx += 6;
                    var pingTextH = Graphics.MeasureText(s.Value).Y;
                    var dotY = dx.Y + VERTICAL_PADDING + (pingTextH - PING_DOT_SIZE) / 2 + 2;
                    var dotBounds = new RectangleF(cx, dotY, PING_DOT_SIZE, PING_DOT_SIZE);
                    Graphics.DrawBox(dotBounds, _pingBarColor, PING_DOT_SIZE / 2f);
                    cx += PING_DOT_SIZE;
                }
                if (s.IsDps)
                {
                    float vw = Graphics.MeasureText(_dpsText).X;
                    float slashW = Graphics.MeasureText("/ ").X;
                    float pkw = Graphics.MeasureText(_dpsPeakText).X;
                    bool hasTag = !string.IsNullOrEmpty(_dpsTag);
                    float tagW = hasTag ? Graphics.MeasureText(_dpsTag).X + 10 : 0;
                    float chipX = cx + 6;
                    float chipW = 6 + vw + 4 + slashW + pkw + (hasTag ? 4 + tagW : 0) + 6;
                    float chipY = dx.Y + VERTICAL_PADDING + 1;
                    float chipH = maxH - 2;
                    Color chipBg, chipBorder, peakCol;
                    if (_dpsLeader == 'P')
                    {
                        chipBg = new Color((byte)255, (byte)85, (byte)85, (byte)36);
                        chipBorder = new Color((byte)255, (byte)85, (byte)85, (byte)140);
                        peakCol = new Color((byte)200, (byte)130, (byte)130, (byte)255);
                    }
                    else if (_dpsLeader == 'M')
                    {
                        chipBg = new Color((byte)90, (byte)169, (byte)255, (byte)36);
                        chipBorder = new Color((byte)90, (byte)169, (byte)255, (byte)140);
                        peakCol = new Color((byte)130, (byte)170, (byte)220, (byte)255);
                    }
                    else
                    {
                        chipBg = new Color((byte)0, (byte)0, (byte)0, (byte)110);
                        chipBorder = new Color((byte)255, (byte)255, (byte)255, (byte)40);
                        peakCol = scheme.Timer;
                    }
                    Graphics.DrawBox(new RectangleF(chipX, chipY, chipW, chipH), chipBg, 6f);
                    Graphics.DrawBox(new RectangleF(chipX, chipY, chipW, 1), chipBorder);
                    Graphics.DrawBox(new RectangleF(chipX, chipY + chipH - 1, chipW, 1), chipBorder);
                    Graphics.DrawBox(new RectangleF(chipX, chipY + 1, 1, chipH - 2), chipBorder);
                    Graphics.DrawBox(new RectangleF(chipX + chipW - 1, chipY + 1, 1, chipH - 2), chipBorder);
                    Graphics.DrawText(_dpsText, new Vector2N(chipX + 6, dx.Y + VERTICAL_PADDING), s.ValueColor);
                    float nx = chipX + 6 + vw + 4;
                    Graphics.DrawText("/ ", new Vector2N(nx, dx.Y + VERTICAL_PADDING), peakCol);
                    nx += slashW;
                    Graphics.DrawText(_dpsPeakText, new Vector2N(nx, dx.Y + VERTICAL_PADDING), peakCol);
                    nx += pkw;
                    if (hasTag)
                    {
                        nx += 4;
                        var tb = new RectangleF(nx, chipY + (chipH - 16) / 2, tagW, 16);
                        Graphics.DrawBox(tb, new Color((byte)0, (byte)0, (byte)0, (byte)110), 3f);
                        var th = Graphics.MeasureText(_dpsTag).Y;
                        Graphics.DrawText(_dpsTag, new Vector2N(nx + 5, tb.Y + (16 - th) / 2), _dpsTagColor);
                        nx += tagW;
                    }
                    cx = chipX + chipW;
                }
                if (s.IsLevelCheck)
                {
                    const float trackW = 80f;
                    float trackX = cx;
                    float trackY = dx.Y + VERTICAL_PADDING + (maxH - 8) / 2;
                    Graphics.DrawBox(new RectangleF(trackX, trackY, trackW, 8), new Color((byte)0, (byte)0, (byte)0, (byte)140), 4f);
                    const float slice = 4f;
                    for (float sx = 0; sx < trackW; sx += slice)
                    {
                        float f = Math.Abs((sx + slice / 2) / trackW - 0.5f) * 2;
                        Color gc = f <= 0.5f
                            ? LerpColor(new Color((byte)64, (byte)255, (byte)96, (byte)255), new Color((byte)218, (byte)165, (byte)32, (byte)255), f * 2)
                            : LerpColor(new Color((byte)218, (byte)165, (byte)32, (byte)255), new Color((byte)255, (byte)96, (byte)96, (byte)255), (f - 0.5f) * 2);
                        gc.A = 160;
                        Graphics.DrawBox(new RectangleF(trackX + sx, trackY, Math.Min(slice, trackW - sx), 8), gc, 0f);
                    }
                    float mx = trackX + _levelMarker * trackW;
                    Graphics.DrawBox(new RectangleF(mx - 2, trackY - 2, 4, 12), Color.Black, 0f);
                    Graphics.DrawBox(new RectangleF(mx - 1, trackY - 2, 2, 12), new Color((byte)255, (byte)255, (byte)255, (byte)255), 0f);
                    cx = trackX + trackW;
                }
                _segmentBounds[s.Id] = new RectangleF(startX, dx.Y, (cx + SEGMENT_GAP) - startX, barH);
                cx += SEGMENT_GAP * 2;
            }

            float extra = 0f;
            if (Settings.ShowXpProgress.Value && Settings.ShowXpRate.Value && !_inTown)
            {
                // Outside the bar, glued to its bottom edge: zero gap.
                var trackY = dx.Y + barH;
                var trackBounds = new RectangleF(dx.X + 12, trackY, totalW - 24, 3);
                Graphics.DrawBox(trackBounds, new Color((byte)0, (byte)0, (byte)0, (byte)140), 1.5f);
                if (_percentGot > 0.001f)
                {
                    var fill = new Color(scheme.Xph.R, scheme.Xph.G, scheme.Xph.B, (byte)255);
                    Graphics.DrawBox(new RectangleF(trackBounds.X, trackY, trackBounds.Width * _percentGot, 3), fill, 1.5f);
                }
                extra = 3f;
            }

            float preloadExtra = 0f;
            float stripBottom = dx.Y + barH + extra;
            if (Settings.ShowPreloads.Value && _preloadChips.Count > 0)
            {
                // Strip ducks under a live boss roster; the main bar never moves.
                float stripY = dx.Y + barH + extra;
                stripY = Math.Max(stripY, _rosterCache.Value);
                preloadExtra = DrawPreloadLine(dx.X, stripY, scheme);
                stripBottom = stripY + preloadExtra;
            }

            _contentBottom = Math.Max(dx.Y + barH + extra + preloadExtra, stripBottom);
            HandleTooltips(scheme);

            GameController.LeftPanel.StartDrawPoint = new Vector2(orig.X, orig.Y + barH + extra + 4 + preloadExtra);
        }

        private void BuildSegments(ColorScheme scheme)
        {
            _segmentCount = 0;
            // Stale hover rects (e.g. DPS from the last zone) must not survive into town/hideout.
            _segmentBounds.Clear();
            if (Settings.ShowPlayerLevel.Value)
                SetSegment("Level", "LVL", _level, scheme.XphGetLeft, false, false);
            if (Settings.ShowLevelCheck.Value && !_inTown)
                SetSegment("LvlCheck", "", "", scheme.Timer, false, false, false, true);
            if (Settings.ShowAreaName.Value)
                SetSegment("Area", "", _area, scheme.Area, false, true);
            if (Settings.ShowDps.Value && !_inTown)
                SetSegment("Dps", "DPS", "", DpsColor(scheme), false, false, true);
            if (Settings.ShowAreaTime.Value)
                SetSegment("Time", "TIME", _areaTime, scheme.Timer, false, false);
            if ((Settings.ShowTimeLeft.Value || Settings.ShowTimeTo100.Value) && !_inTown)
            {
                var v = _timeMerged;
                if (!Settings.ShowTimeLeft.Value) v = _timeMerged.Contains("/") ? _timeMerged.Substring(_timeMerged.IndexOf("/", StringComparison.Ordinal) + 1).Trim() : v;
                else if (!Settings.ShowTimeTo100.Value) v = _timeMerged.Contains("/") ? _timeMerged.Substring(0, _timeMerged.IndexOf("/", StringComparison.Ordinal)).Trim() : v;
                SetSegment("Eta", "", v, scheme.TimeLeft, false, false);
            }
            if (Settings.ShowXpRate.Value && !_inTown)
                SetSegment("Xp", "", _xp, scheme.Xph, false, false);
            if (Settings.ShowGold.Value)
                SetSegment("Gold", "GOLD", _gold, scheme.Gold, false, false);
            if (Settings.ShowGoldPerHour.Value && !_inTown)
                SetSegment("Gph", "G/H", _goldPerHour, scheme.Gold, false, false);
            if (Settings.ShowPing.Value)
                SetSegment("Ping", "PING", _ping, scheme.Ping, true, false);
        }

        private void SetSegment(string id, string label, string value, Color color, bool isPing, bool isArea, bool isDps = false, bool isLevelCheck = false)
        {
            if (_segmentCount >= _segmentPool.Length) return;
            var s = _segmentPool[_segmentCount] ??= new Segment();
            s.Id = id;
            s.Label = label;
            s.Value = value;
            s.ValueColor = color;
            s.IsPing = isPing;
            s.IsArea = isArea;
            s.IsDps = isDps;
            s.IsLevelCheck = isLevelCheck;
            _segmentCount++;
        }

        private Color DpsColor(ColorScheme scheme)
        {
            if (_dpsLeader == 'P') return DpsPlayerColor;
            if (_dpsLeader == 'M') return DpsMinionColor;
            return scheme.Timer;
        }

        private float DpsChipWidth()
        {
            float w = 6 + Graphics.MeasureText(_dpsText).X + 4 + Graphics.MeasureText("/ ").X + Graphics.MeasureText(_dpsPeakText).X;
            if (!string.IsNullOrEmpty(_dpsTag)) w += 4 + Graphics.MeasureText(_dpsTag).X + 10;
            return w + 6;
        }

        private static PreloadChip MakeChip(string label, string suffix, Color color, string title, string group)
        {
            return new PreloadChip { Label = label, Suffix = suffix, Color = color, Title = title, GroupName = group };
        }

        private void BuildPreloadChips(List<PreloadHit> hits)
        {
            _preloadChips.Clear();
            _preloadRemainder = null;

            // Pass 1: exact collapse, keep first-appearance order.
            var order = new List<string>();
            var byKey = new Dictionary<string, List<PreloadHit>>();
            foreach (var h in hits)
            {
                var key = h.Name + "|" + h.Color.R + "," + h.Color.G + "," + h.Color.B;
                if (!byKey.TryGetValue(key, out var list)) { list = new List<PreloadHit>(); byKey[key] = list; order.Add(key); }
                list.Add(h);
            }
            var entries = new List<(string Label, Color Color, string Group, List<PreloadHit> Members)>();
            foreach (var key in order)
            {
                var list = byKey[key];
                entries.Add((list[0].Name, list[0].Color, list[0].Group, list));
            }

            // Pass 2: emit in order; family-merge 3+ single-member entries of one non-Unsorted group.
            var famIdx = new Dictionary<string, List<int>>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Members.Count > 1 || e.Group == "Unsorted") continue;
                if (!famIdx.TryGetValue(e.Group, out var li)) { li = new List<int>(); famIdx[e.Group] = li; }
                li.Add(i);
            }
            var emittedFam = new HashSet<string>();
            var chips = new List<PreloadChip>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Members.Count > 1)
                {
                    var chip = MakeChip(e.Label, "", e.Color, e.Label.ToUpperInvariant(), e.Group);
                    chip.HitCount = e.Members.Count;
                    chips.Add(chip);
                    continue;
                }
                if (e.Group == "Unsorted")
                {
                    chips.Add(MakeChip(e.Label, "", e.Color, e.Label.ToUpperInvariant(), e.Group));
                    continue;
                }
                if (emittedFam.Contains(e.Group)) continue;
                emittedFam.Add(e.Group);
                var idxs = famIdx[e.Group];
                if (idxs.Count >= 3)
                {
                    var members = new List<PreloadHit>();
                    foreach (var j in idxs) members.Add(entries[j].Members[0]);
                    var chip = MakeChip(e.Group, "x" + members.Count, members[0].Color, e.Group.ToUpperInvariant() + " (" + members.Count + ")", e.Group);
                    chip.HitCount = members.Count;
                    foreach (var m in members) chip.Rows.Add((m.Name, m.Color));
                    chips.Add(chip);
                }
                else
                {
                    foreach (var j in idxs)
                    {
                        var s = entries[j];
                        chips.Add(MakeChip(s.Label, "", s.Color, s.Label.ToUpperInvariant(), s.Group));
                    }
                }
            }

            foreach (var c in chips) _preloadChips.Add(c);
            if (_preloadRemainder == null) _preloadRemainder = new PreloadChip { IsRemainder = true, GroupName = "" };
        }

        private float DrawPreloadLine(float x, float y, ColorScheme scheme)
        {
            var lineH = Graphics.MeasureText("Ag").Y + 4;
            float avail = _barBounds.Width - HORIZONTAL_PADDING * 2;
            float avail1 = avail * 1.25f;
            int n = _preloadChips.Count;
            int totalHits = 0;
            for (int i = 0; i < n; i++) totalHits += _preloadChips[i].HitCount;

            // Row 1: fill to the bar width. Overflow flows to row 2, so no +N reserved here.
            int cut1 = n, shown1 = 0;
            float used1 = 0f;
            for (int i = 0; i < n; i++)
            {
                float gap = used1 > 0 ? 6 : 0;
                if (used1 + gap + _preloadChips[i].WidthPx <= avail1)
                {
                    used1 += gap + _preloadChips[i].WidthPx;
                    shown1 += _preloadChips[i].HitCount;
                }
                else { cut1 = i; break; }
            }

            // Row 2: the remainder, with +N reserved for anything still left over.
            int cut2 = n, shown2 = 0;
            float used2 = 0f;
            for (int i = cut1; i < n; i++)
            {
                int hiddenAfter = totalHits - (shown1 + shown2 + _preloadChips[i].HitCount);
                float plusW = 0f;
                if (hiddenAfter > 0 && !_plusWidths.TryGetValue(hiddenAfter, out plusW))
                {
                    plusW = 6 + Graphics.MeasureText("+" + hiddenAfter).X + 8;
                    _plusWidths[hiddenAfter] = plusW;
                }
                float gap = used2 > 0 ? 6 : 0;
                if (used2 + gap + _preloadChips[i].WidthPx + plusW <= avail)
                {
                    used2 += gap + _preloadChips[i].WidthPx;
                    shown2 += _preloadChips[i].HitCount;
                }
                else { cut2 = i; break; }
            }

            float row1Left = x + HORIZONTAL_PADDING + (avail - avail1) / 2;
            float cx = row1Left + Math.Max(0, (avail1 - used1) / 2);
            for (int i = 0; i < cut1; i++)
            {
                DrawPreloadChip(_preloadChips[i], cx, y, lineH, scheme);
                cx += _preloadChips[i].WidthPx + 6;
            }

            var rem = _preloadRemainder;
            rem.Groups.Clear();
            rem.Rows.Clear();

            float extra = 0f;
            int hidden = totalHits - shown1 - shown2;
            if (cut1 < n)
            {
                float remW = 0f;
                if (hidden > 0 && !_plusWidths.TryGetValue(hidden, out remW))
                {
                    remW = 6 + Graphics.MeasureText("+" + hidden).X + 8;
                    _plusWidths[hidden] = remW;
                }
                float y2 = y + lineH + 4;
                float cx2 = x + HORIZONTAL_PADDING + Math.Max(0, (avail - used2 - remW) / 2);
                for (int i = cut1; i < cut2; i++)
                {
                    DrawPreloadChip(_preloadChips[i], cx2, y2, lineH, scheme);
                    cx2 += _preloadChips[i].WidthPx + 6;
                }
                for (int i = cut2; i < n; i++) _preloadChips[i].Bounds = RectangleF.Empty;
                if (hidden > 0) DrawPreloadRemainder(rem, cut2, n, hidden, cx2, y2, lineH, scheme);
                else rem.Bounds = RectangleF.Empty;
                rem.HiddenCount = hidden;
                extra = 4 + lineH;
            }
            else
            {
                for (int i = cut1; i < n; i++) _preloadChips[i].Bounds = RectangleF.Empty;
                rem.Bounds = RectangleF.Empty;
                rem.HiddenCount = 0;
            }
            return lineH + 4 + extra;
        }

        private void DrawPreloadChip(PreloadChip chip, float cx, float y, float lineH, ColorScheme scheme)
        {
            chip.Bounds = new RectangleF(cx, y + 1, chip.WidthPx, lineH);
            Graphics.DrawBox(chip.Bounds, new Color((byte)27, (byte)22, (byte)38, (byte)255), 0f);
            float swY = y + 5 + (lineH - 4 - 7) / 2 + 2;
            Graphics.DrawBox(new RectangleF(cx + 3, swY, 7, 7), chip.Color, 0f);
            float lx = cx + 3 + 7 + 4;
            var drawn = Graphics.DrawText(chip.Label, new Vector2N(lx, y + 5), chip.Color);
            if (!string.IsNullOrEmpty(chip.Suffix))
                Graphics.DrawText(chip.Suffix, new Vector2N(lx + drawn.X + 4, y + 5), scheme.Timer);
        }

        private void DrawPreloadRemainder(PreloadChip rem, int from, int to, int hidden, float cx, float y, float lineH, ColorScheme scheme)
        {
            if (hidden != _remPlusHidden || from != _remPlusFrom)
            {
                _remPlusHidden = hidden;
                _remPlusFrom = from;
                rem.Groups.Clear();
                rem.Rows.Clear();
                var gOrder = new List<string>();
                var gRows = new Dictionary<string, List<(string Text, Color Color)>>();
                for (int i = from; i < to; i++)
                {
                    var c = _preloadChips[i];
                    if (c.Rows.Count > 0)
                    {
                        foreach (var r in c.Rows) AddRow(c.GroupName, r);
                    }
                    else AddRow(c.GroupName, (c.Label, c.Color));
                }
                void AddRow(string g, (string Text, Color Color) r)
                {
                    if (!gRows.TryGetValue(g, out var l)) { l = new List<(string, Color)>(); gRows[g] = l; gOrder.Add(g); }
                    l.Add(r);
                }
                foreach (var g in gOrder) rem.Groups.Add((g, gRows[g]));
                rem.Label = "+" + hidden;
                rem.Title = "ALSO IN ZONE (" + hidden + ")";
            }
            var size = Graphics.DrawText(rem.Label, new Vector2N(cx + 6, y + 5), scheme.Timer);
            rem.Bounds = new RectangleF(cx + 6, y + 1, size.X + 8, lineH);
        }

        private void CachePreloadWidths()
        {
            foreach (var chip in _preloadChips)
            {
                float w = 3 + 7 + 4 + Graphics.MeasureText(chip.Label).X;
                if (!string.IsNullOrEmpty(chip.Suffix))
                    w += 4 + Graphics.MeasureText(chip.Suffix).X;
                chip.WidthPx = w + 4;
            }
        }

        private void ShowPreloadChipCard(PreloadChip chip, ColorScheme scheme)
        {
            if (chip.Rows.Count == 0) return;
            var lines = new List<(string Text, Color Color, bool Header)>();
            foreach (var r in chip.Rows) lines.Add((r.Text, r.Color, false));
            DrawPreloadCard(chip.Title, lines, chip.Bounds, scheme);
        }

        private void ShowRemainderCard(ColorScheme scheme)
        {
            var rem = _preloadRemainder;
            if (rem == null) return;
            var lines = new List<(string Text, Color Color, bool Header)>();
            foreach (var g in rem.Groups)
            {
                lines.Add((g.Group.ToUpperInvariant(), scheme.Timer, true));
                foreach (var r in g.Rows) lines.Add((r.Text, r.Color, false));
            }
            DrawPreloadCard(rem.Title, lines, rem.Bounds, scheme);
        }

        private void DrawPreloadCard(string title, List<(string Text, Color Color, bool Header)> lines, RectangleF anchor, ColorScheme scheme)
        {
            const int MAX_LINES = 12;
            bool overflow = lines.Count > MAX_LINES;
            int count = overflow ? MAX_LINES : lines.Count;
            bool plain = true;
            for (int i = 0; i < count; i++) if (lines[i].Header) { plain = false; break; }
            bool twoCol = plain && count > 8;

            float textH = Graphics.MeasureText("Ag").Y;
            float rowH = textH + 4;
            float titleH = textH + 6;
            const float dotBox = 8 + 8;
            float colW = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = Graphics.MeasureText(lines[i].Text).X + (lines[i].Header ? 0 : dotBox);
                if (w > colW) colW = w;
            }
            int rows = twoCol ? (count + 1) / 2 : count;
            float cardW = (twoCol ? colW * 2 + 26 : colW) + 30;
            float cardH = 5 + titleH + rows * rowH + (overflow ? rowH : 0) + 5;

            float x = anchor.X;
            if (x + cardW > _barBounds.Right) x = _barBounds.Right - cardW;
            float y = Math.Max(anchor.Bottom + 5, _contentBottom + 5);

            var bg = scheme.Background;
            bg.A = 235;
            Graphics.DrawBox(new RectangleF(x, y, cardW, cardH), bg, 6f);

            Graphics.DrawText(title, new Vector2N(x + 15, y + 5), scheme.Timer);
            for (int i = 0; i < count; i++)
            {
                int r = twoCol ? i % rows : i;
                int c = twoCol ? i / rows : 0;
                float lx = x + 15 + c * (colW + 26);
                float ly = y + 5 + titleH + r * rowH;
                var l = lines[i];
                if (!l.Header)
                {
                    Graphics.DrawBox(new RectangleF(lx, ly + 2 + (textH - 8) / 2, 8, 8), l.Color, 4f);
                    lx += dotBox;
                }
                Graphics.DrawText(l.Text, new Vector2N(lx, ly + 2), l.Color);
            }
            if (overflow)
                Graphics.DrawText("+" + (lines.Count - MAX_LINES) + " more", new Vector2N(x + 15, y + 5 + titleH + rows * rowH), scheme.Timer);
        }

        public override void DrawSettings()
        {
            UiHelpers.PushSettingsStyle();
            try
            {
                if (ImGui.BeginChild("##mbSide", new Vector2N(140, 0), Hexa.NET.ImGui.ImGuiChildFlags.None, Hexa.NET.ImGui.ImGuiWindowFlags.None))
                {
                    SettingsTab("Info Bar", 0);
                    SettingsTab("Preloads", 1);
                    SettingsTab("Themes", 2);
                }
                ImGui.EndChild();
                ImGui.SameLine();
                if (ImGui.BeginChild("##mbMain", new Vector2N(0, 0), Hexa.NET.ImGui.ImGuiChildFlags.Borders, Hexa.NET.ImGui.ImGuiWindowFlags.None))
                {
                    var ver = GetType().Assembly.GetName().Version?.ToString(2);
                    UiHelpers.HouseHeader("ModernBar", ver);
                    if (_settingsTab == 0) DrawBarTab();
                    else if (_settingsTab == 1) DrawPreloadsTab();
                    else DrawThemesTab();
                }
                ImGui.EndChild();
            }
            finally
            {
                UiHelpers.PopSettingsStyle();
            }
        }

        private void SettingsTab(string label, int id)
        {
            if (UiHelpers.SidebarRow(label, null, _settingsTab == id, "mbtab" + id, 0)) _settingsTab = id;
        }

        private void DrawBarTab()
        {
            UiHelpers.SectionHeader("General");
            Settings.Enable.Value = ToggleRow("Enable", Settings.Enable, "enable", "Turns the bar on or off.");
            UiHelpers.SectionHeader("Segments");
            Settings.ShowPlayerLevel.Value = ToggleRow("Player level", Settings.ShowPlayerLevel, "lvl");
            Settings.ShowLevelCheck.Value = ToggleRow("Level check", Settings.ShowLevelCheck, "lvlcheck", "Shows whether the area is safe for your level.");
            Settings.ShowAreaName.Value = ToggleRow("Area name", Settings.ShowAreaName, "area");
            Settings.ShowAreaTime.Value = ToggleRow("Area time", Settings.ShowAreaTime, "atime");
            Settings.ShowTimeLeft.Value = ToggleRow("Time left to level", Settings.ShowTimeLeft, "tleft");
            Settings.ShowTimeTo100.Value = ToggleRow("Time to 100", Settings.ShowTimeTo100, "t100");
            Settings.ShowXpRate.Value = ToggleRow("XP rate", Settings.ShowXpRate, "xp");
            Settings.ShowXpProgress.Value = ToggleRow("XP progress line", Settings.ShowXpProgress, "xpp");
            Settings.ShowDps.Value = ToggleRow("DPS", Settings.ShowDps, "dps");
            Settings.ShowGold.Value = ToggleRow("Gold", Settings.ShowGold, "gold");
            Settings.ShowGoldPerHour.Value = ToggleRow("Gold per hour", Settings.ShowGoldPerHour, "gph");
            Settings.ShowPing.Value = ToggleRow("Ping", Settings.ShowPing, "ping");
            UiHelpers.SectionHeader("Layout");
            SliderRow(Settings.DrawXOffset, "Draw X offset", "Moves the bar horizontally.");
            SliderRow(Settings.DrawYOffset, "Draw Y offset", "Moves the bar vertically.");
            SliderRow(Settings.PingHistoryMinutes, "Ping history (min)", "How much ping history the graph keeps.");
        }

        private void DrawPreloadsTab()
        {
            UiHelpers.SectionHeader("Tracking");
            Settings.ShowPreloads.Value = ToggleRow("Show preloads", Settings.ShowPreloads, "pload", "Shows the preload list on the bar.");
            Settings.ShowPreloadInHideout.Value = ToggleRow("Show in hideout", Settings.ShowPreloadInHideout, "phide");
            UiHelpers.SectionHeader("Editor");
            ImGui.TextWrapped("File: " + (_preloadFilePath ?? "not found").Replace("%", "%%"));
            if (_preloadFile.Count == 0) { ImGui.Text("No groups loaded."); return; }
            if (_preloadGroupSel < 0 || _preloadGroupSel >= _preloadFile.Count) _preloadGroupSel = 0;

            if (ImGui.BeginChild("##plGroups", new Vector2N(190, 260), Hexa.NET.ImGui.ImGuiChildFlags.Borders, Hexa.NET.ImGui.ImGuiWindowFlags.None))
            {
                for (int gi = 0; gi < _preloadFile.Count; gi++)
                {
                    var gg = _preloadFile[gi];
                    if (UiHelpers.SidebarRow(gg.Name, gg.Patterns.Count.ToString(), _preloadGroupSel == gi, "plg" + gi, 14))
                    {
                        _preloadGroupSel = gi;
                        _confirmDelGroup = false;
                        _colorEditG = -1;
                    }
                }
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("##plDetail", new Vector2N(0, 260), Hexa.NET.ImGui.ImGuiChildFlags.Borders, Hexa.NET.ImGui.ImGuiWindowFlags.None))
            {
                DrawPreloadGroupDetail();
            }
            ImGui.EndChild();

            UiHelpers.SectionHeader("Regex tester");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##ptest", ref _testPath, 512, Hexa.NET.ImGui.ImGuiInputTextFlags.None);
            if (UiHelpers.Button("Test path", "ptest", 0))
            {
                _testResult = "no match";
                foreach (var p in _compiledPreloads)
                {
                    bool m;
                    try { m = p.Regex.IsMatch(_testPath); }
                    catch { continue; }
                    if (m) { _testResult = "MATCH: " + p.Group + " -- " + p.Display; break; }
                }
            }
            if (!string.IsNullOrEmpty(_testResult)) ImGui.Text(_testResult.Replace("%", "%%"));

            UiHelpers.SectionHeader("File");
            if (_preloadDirty) ImGui.TextColored(new System.Numerics.Vector4(1f, 0.81f, 0.3f, 1f), "* unsaved changes");
            var fileRowStart = ImGui.GetCursorScreenPos();
            int fclicked = UiHelpers.ButtonRow(("Save to preloads.json", "psave", 1), ("Revert", "prevert", 0), ("+ Pattern", "padd", 0), ("+ Group", "pgadd", 0));
            var fileRowAfter = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2N(fileRowStart.X, fileRowAfter.Y));
            if (fclicked == 0)
            {
                SavePreloadFile();
                _preloadDirty = false;
                _confirmDelGroup = false;
            }
            else if (fclicked == 1)
            {
                LoadPreloadPatterns();
                StartPreloadScan();
                _preloadDirty = false;
                _confirmDelGroup = false;
                _colorEditG = -1;
            }
            else if (fclicked == 2) { _preloadFile[_preloadGroupSel].Patterns.Add(new PreloadFilePattern()); TouchPreloads(); }
            else if (fclicked == 3) { _preloadFile.Add(new PreloadFileGroup { Name = "New Group" }); _preloadGroupSel = _preloadFile.Count - 1; TouchPreloads(); }
        }

        private bool PatternValid(string key, string pattern)
        {
            if (_validatedText.TryGetValue(key, out var t) && t == pattern && _validatedOk.TryGetValue(key, out var ok)) return ok;
            bool good;
            try { _ = new Regex(pattern); good = true; }
            catch { good = false; }
            _validatedText[key] = pattern;
            _validatedOk[key] = good;
            return good;
        }

        private static System.Numerics.Vector4 ToV4(Color c) => new System.Numerics.Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

        private void DrawPreloadGroupDetail()
        {
            var g = _preloadFile[_preloadGroupSel];
            var hStart = ImGui.GetCursorScreenPos();
            float hAvail = ImGui.GetContentRegionAvail().X;
            float hTextH = ImGui.GetTextLineHeight();
            float hRowH = hTextH + 8;
            float hx = hStart.X;
            ImGui.SetCursorScreenPos(new Vector2N(hx, hStart.Y));
            bool gen = UiHelpers.PillMini(g.Enable, "pgen" + _preloadGroupSel, hRowH);
            if (gen != g.Enable) { g.Enable = gen; TouchPreloads(); }
            hx += 30 + 8;
            string htitle = g.Name + " -- " + g.Patterns.Count + " patterns";
            float htw = ImGui.CalcTextSize(htitle).X;
            ImGui.SetCursorScreenPos(new Vector2N(hx, hStart.Y + (hRowH - hTextH) / 2));
            ImGui.Text(htitle.Replace("%", "%%"));
            hx += htw + 8;
            if (!_confirmDelGroup)
            {
                ImGui.SetCursorScreenPos(new Vector2N(hx, hStart.Y));
                if (UiHelpers.Button("Delete group", "pdelgrp", 2)) _confirmDelGroup = true;
                hx += ImGui.CalcTextSize("Delete group").X + 20 + 8;
            }
            else
            {
                ImGui.SetCursorScreenPos(new Vector2N(hx, hStart.Y));
                int dclicked = UiHelpers.ButtonRow(("Sure?", "pdelgrp2", 2), ("Cancel", "pdelgrpc", 0));
                hx += ImGui.CalcTextSize("Sure?").X + 20 + 8 + ImGui.CalcTextSize("Cancel").X + 20 + 8;
                if (dclicked == 0) { _preloadFile.RemoveAt(_preloadGroupSel); _preloadGroupSel = 0; _confirmDelGroup = false; TouchPreloads(); }
                else if (dclicked == 1) _confirmDelGroup = false;
            }
            ImGui.SetCursorScreenPos(new Vector2N(hx, hStart.Y + (hRowH - hTextH) / 2));
            ImGui.Text("Filter:");
            hx += ImGui.CalcTextSize("Filter:").X + 8;
            float iAvail = (hStart.X + hAvail) - hx;
            ImGui.SetCursorScreenPos(new Vector2N(hx, hStart.Y));
            ImGui.SetNextItemWidth(Math.Max(50, iAvail));
            ImGui.InputText("##pfilter", ref _patternFilter, 128, Hexa.NET.ImGui.ImGuiInputTextFlags.None);
            ImGui.SetCursorScreenPos(new Vector2N(hStart.X, hStart.Y + hRowH));
            var duplicatePalette = new[]
            {
                new System.Numerics.Vector4(1f, 0.81f, 0.30f, 1f),  // gold
                new System.Numerics.Vector4(0.35f, 0.90f, 1f, 1f),  // cyan
                new System.Numerics.Vector4(1f, 0.48f, 0.75f, 1f),  // pink
                new System.Numerics.Vector4(0.50f, 1f, 0.55f, 1f),  // green
                new System.Numerics.Vector4(1f, 0.58f, 0.30f, 1f),  // orange
                new System.Numerics.Vector4(0.72f, 0.68f, 1f, 1f),  // lavender
            };
            var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pp in g.Patterns)
            {
                var name = pp.Display ?? "";
                nameCounts.TryGetValue(name, out var count);
                nameCounts[name] = count + 1;
            }
            var duplicateNames = nameCounts.Where(kv => kv.Value > 1)
                .Select(kv => kv.Key)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var duplicateColors = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            var duplicateTextColors = new Dictionary<string, System.Numerics.Vector4>(StringComparer.OrdinalIgnoreCase);
            for (int di = 0; di < duplicateNames.Count; di++)
            {
                var tint = duplicatePalette[di % duplicatePalette.Length];
                duplicateTextColors[duplicateNames[di]] = tint;
                duplicateColors[duplicateNames[di]] = ImGui.GetColorU32(tint);
            }
            if (duplicateNames.Count > 0)
            {
                ImGui.TextColored(duplicatePalette[0],
                    duplicateNames.Count + " duplicate display name" + (duplicateNames.Count == 1 ? "" : "s") + ":");
                foreach (var dupName in duplicateNames.Take(6))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(duplicateTextColors[dupName], "[" + dupName.Replace("%", "%%") + "]");
                }
                if (duplicateNames.Count > 6)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("+" + (duplicateNames.Count - 6) + " more");
                }
            }
            for (int pi = 0; pi < g.Patterns.Count; pi++)
            {
                var p = g.Patterns[pi];
                string key = "g" + _preloadGroupSel + "p" + pi;
                var prowStart = ImGui.GetCursorScreenPos();
                float erowH = ImGui.GetTextLineHeight() + 8;
                if (!string.IsNullOrEmpty(_patternFilter) &&
                    p.Display.IndexOf(_patternFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    p.Path.IndexOf(_patternFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!PatternValid(key, p.Path))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.35f, 0.35f, 1f), "!");
                    ImGui.SameLine();
                }
                bool pen = UiHelpers.PillMini(p.Enable, "pe" + key, erowH);
                if (pen != p.Enable) { p.Enable = pen; TouchPreloads(); }
                ImGui.SameLine();
                var scol = ToV4(PreloadFileStore.ParseHex(p.ColorHex, new Color((byte)255, (byte)255, (byte)255, (byte)255)));
                if (UiHelpers.ColorSwatch(scol, "psw" + key, erowH))
                {
                    _colorEditG = _preloadGroupSel;
                    _colorEditP = pi;
                    _colorEditValue = scol;
                    _colorPickerOpen = true;
                    ImGui.OpenPopup("##preloadColorPicker");
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(170);
                if (ImGui.InputText("##pdn" + key, ref p.Display, 128, Hexa.NET.ImGui.ImGuiInputTextFlags.None)) TouchPreloads();
                if (duplicateColors.TryGetValue(p.Display ?? "", out var dupTint))
                    ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), dupTint, 3f, 2f);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(Math.Max(50f, ImGui.GetContentRegionAvail().X - 52));
                if (ImGui.InputText("##ppn" + key, ref p.Path, 256, Hexa.NET.ImGui.ImGuiInputTextFlags.None)) TouchPreloads();
                ImGui.SameLine();
                bool delRow = UiHelpers.Button("x", "pdel" + key, 0);
                ImGui.SetCursorScreenPos(new Vector2N(prowStart.X, ImGui.GetCursorScreenPos().Y));
                if (delRow) { g.Patterns.RemoveAt(pi); TouchPreloads(); pi--; continue; }
                ImGui.SetCursorScreenPos(new Vector2N(prowStart.X, ImGui.GetCursorScreenPos().Y));
            }

            if (ImGui.BeginPopupModal("##preloadColorPicker", ref _colorPickerOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped("Set the color for this preload pattern.");
                if (ImGui.ColorPicker4("##picker", ref _colorEditValue))
                {
                    if (_colorEditG >= 0 && _colorEditG == _preloadGroupSel &&
                        _colorEditP >= 0 && _colorEditP < g.Patterns.Count)
                    {
                        var cv = _colorEditValue;
                        g.Patterns[_colorEditP].ColorHex =
                            $"#{(byte)(cv.X * 255):X2}{(byte)(cv.Y * 255):X2}{(byte)(cv.Z * 255):X2}{(byte)(cv.W * 255):X2}";
                        TouchPreloads();
                    }
                }
                if (UiHelpers.ButtonRow(("Done", "preloadColorDone", 1)) == 0)
                {
                    _colorPickerOpen = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawThemesTab()
        {
            var items = Settings.ColorScheme.Values.ToArray();
            var cur = items.ToList().IndexOf(Settings.ColorScheme.Value);
            if (cur < 0) cur = 0;
            if (ImGui.Combo("Color scheme", ref cur, items, items.Length))
            {
                Settings.ColorScheme.Value = items[cur];
                UpdateColorNodes();
            }
            UiHelpers.SectionHeader("Alpha");
            SliderRow(Settings.BackgroundAlpha, "Background alpha", "How solid the bar background is.");
            SliderRow(Settings.DividerAlpha, "Divider alpha", "How visible the segment dividers are.");
            UiHelpers.SectionHeader("Colors");
            ColorRow("Background", Settings.BackgroundColor);
            ColorRow("Labels", Settings.LabelColor);
            ColorRow("Gold", Settings.GoldColor);
            ColorRow("Timer", Settings.TimerColor);
            ColorRow("Ping", Settings.PingColor);
            ColorRow("Area", Settings.AreaColor);
            ColorRow("Time left", Settings.TimeLeftColor);
            ColorRow("XP rate", Settings.XphColor);
            ColorRow("Player level", Settings.XphGetLeftColor);
            ImGui.Dummy(new Vector2N(0, 12f));
            if (UiHelpers.ActionButton("Reset colors", "resetTheme", 0,
                    "Restores the default theme palette.")) ResetThemeColors();
        }

        // House UI v2 row grid: control at x=0, label at x=44, track at x=270.
        private static bool ToggleRow(string label, ToggleNode node, string id, string help = null)
        {
            var start = ImGui.GetCursorScreenPos();
            float lineH = ImGui.GetTextLineHeight();
            float rowH = lineH + 8f;
            float w = ImGui.GetContentRegionAvail().X;

            ImGui.SetCursorScreenPos(start);
            var pillValue = UiHelpers.PillMini(node.Value, "mb_pill_" + id, rowH);
            ImGui.SetCursorScreenPos(start + new Vector2N(44f, 0f));
            ImGui.InvisibleButton("##mb_row_" + id, new Vector2N(w - 44f, rowH));
            bool rowClicked = ImGui.IsItemClicked();
            bool hovered = ImGui.IsItemHovered();

            ImGui.SetCursorScreenPos(start + new Vector2N(44f, (rowH - lineH) * 0.5f));
            ImGui.TextColored(hovered ? new System.Numerics.Vector4(1f, 1f, 1f, 1f) : UiHelpers.Muted, label);
            if (help != null)
            {
                ImGui.SetCursorScreenPos(start + new Vector2N(44f + ImGui.CalcTextSize(label).X + 8f, (rowH - lineH) * 0.5f));
                UiHelpers.HelpMarker(help);
            }

            if (pillValue != node.Value) node.Value = pillValue;
            else if (rowClicked) node.Value = !node.Value;
            ImGui.SetCursorScreenPos(new Vector2N(start.X, start.Y + rowH));
            return node.Value;
        }

        private static void AlignedSlider(RangeNode<int> node, string label, string id, string help = null)
        {
            float value = node.Value;
            DrawAlignedSlider(label, ref value, node.Min, node.Max, id, true, help, v => node.Value = (int)Math.Round(v));
        }

        private static void DrawAlignedSlider(string label, ref float value, float min, float max, string id, bool isInt, string help, Action<float> commit)
        {
            var start = ImGui.GetCursorScreenPos();
            float lineH = ImGui.GetTextLineHeight();
            float h = lineH + 8f;
            float w = ImGui.GetContentRegionAvail().X;
            const float trackStart = 270f;
            const float valueGutter = 44f;
            float trackEnd = w - valueGutter;
            float trackY = start.Y + h * 0.5f;

            ImGui.SetCursorScreenPos(start + new Vector2N(trackStart, 0f));
            ImGui.InvisibleButton("##mb_slider_" + id, new Vector2N(trackEnd - trackStart, h));
            bool active = ImGui.IsItemActive();
            bool hovered = ImGui.IsItemHovered();

            var text = isInt ? ((int)Math.Round(value)).ToString() : value.ToString("0.0");
            var textSize = ImGui.CalcTextSize(text);
            var textPos = new Vector2N(start.X + w - textSize.X, start.Y + (h - lineH) * 0.5f);
            bool editing = _editingSliderId == id;

            if (!editing && ImGui.IsMouseHoveringRect(textPos - new Vector2N(6f, 3f), textPos + textSize + new Vector2N(6f, 3f))
                && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _editingSliderId = id;
                _sliderEditFocus = true;
                editing = true;
            }

            var dl = ImGui.GetWindowDrawList();
            uint accent = ImGui.GetColorU32(new System.Numerics.Vector4(0.486f, 0.360f, 1f, 1f));
            uint trackCol = ImGui.GetColorU32(new System.Numerics.Vector4(0.165f, 0.180f, 0.208f, 1f));
            uint labelCol = ImGui.GetColorU32(new System.Numerics.Vector4(0.91f, 0.92f, 0.93f, 1f));
            uint valueCol = ImGui.GetColorU32(new System.Numerics.Vector4(0.72f, 0.68f, 1f, 1f));

            float norm = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
            float lineStart = start.X + trackStart;
            float lineEnd = start.X + trackEnd;
            float dotX = lineStart + norm * (lineEnd - lineStart);

            dl.AddText(new Vector2N(start.X + 44f, start.Y + (h - lineH) * 0.5f), labelCol, label);
            if (help != null)
            {
                ImGui.SetCursorScreenPos(new Vector2N(start.X + 44f + ImGui.CalcTextSize(label).X + 8f, start.Y + (h - lineH) * 0.5f));
                UiHelpers.HelpMarker(help);
            }
            if (!editing) dl.AddText(textPos, valueCol, text);
            dl.AddLine(new Vector2N(lineStart, trackY), new Vector2N(lineEnd, trackY), trackCol, 3f);
            if (dotX > lineStart) dl.AddLine(new Vector2N(lineStart, trackY), new Vector2N(dotX, trackY), accent, 3f);
            dl.AddCircleFilled(new Vector2N(dotX, trackY), hovered || active ? 6.5f : 5.5f, ImGui.GetColorU32(new System.Numerics.Vector4(1f, 1f, 1f, 1f)));

            if (editing)
            {
                ImGui.SetCursorScreenPos(new Vector2N(start.X + w - 64f, start.Y + (h - lineH) * 0.5f - 2f));
                ImGui.SetNextItemWidth(64f);
                bool justOpened = false;
                if (_sliderEditFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    _sliderEditFocus = false;
                    justOpened = true;
                }
                bool enter = ImGui.InputFloat("##mb_edit_" + id, ref value, 0f, 0f, "%g", ImGuiInputTextFlags.EnterReturnsTrue);
                value = Math.Clamp(value, min, max);
                commit(value);
                if (enter || (!justOpened && ImGui.IsItemDeactivated()))
                {
                    value = Math.Clamp(value, min, max);
                    commit(value);
                    _editingSliderId = null;
                }
            }
            else if (active && max > min && lineEnd > lineStart)
            {
                float mx = ImGui.GetMousePos().X;
                float nn = Math.Clamp((mx - lineStart) / (lineEnd - lineStart), 0f, 1f);
                var next = min + nn * (max - min);
                if (Math.Abs(next - value) > 0.0001f)
                {
                    value = next;
                    commit(value);
                }
            }
            ImGui.SetCursorScreenPos(new Vector2N(start.X, start.Y + h));
        }

        private static void SliderRow(RangeNode<int> node, string label, string help = null) =>
            AlignedSlider(node, label, "mb_" + label, help);

        private static void ColorRow(string label, ColorNode node, string help = null)
        {
            var start = ImGui.GetCursorScreenPos();
            float lineH = ImGui.GetTextLineHeight();
            float rowH = lineH + 8f;
            var c = node.Value;
            var vv = new System.Numerics.Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            ImGui.SetCursorScreenPos(start);
            if (ImGui.ColorEdit4("##mb_color_" + label, ref vv, Hexa.NET.ImGui.ImGuiColorEditFlags.NoInputs))
                node.Value = new Color((byte)(vv.X * 255), (byte)(vv.Y * 255), (byte)(vv.Z * 255), (byte)(vv.W * 255));
            ImGui.SetCursorScreenPos(start + new Vector2N(44f, (rowH - lineH) * 0.5f));
            ImGui.TextColored(UiHelpers.Muted, label);
            if (help != null)
            {
                ImGui.SetCursorScreenPos(start + new Vector2N(44f + ImGui.CalcTextSize(label).X + 8f, (rowH - lineH) * 0.5f));
                UiHelpers.HelpMarker(help);
            }
            ImGui.SetCursorScreenPos(new Vector2N(start.X, start.Y + rowH));
        }

        private void HandleTooltips(ColorScheme scheme)
        {
            var mp = ExileCore.Input.MousePositionNum;
            foreach (var kv in _segmentBounds)
            {
                if (!kv.Value.Contains(mp)) continue;
                if (kv.Key == "Area")
                {
                    DrawCard(new List<string> { _area, _areaDetail }, kv.Value, scheme);
                    return;
                }
                if (kv.Key == "LvlCheck")
                {
                    var safeTxt = _levelVerdict == "EVEN" ? "in safe zone" : (_levelDiff > 0 ? $"zone {_levelDiff} above you" : $"zone {-_levelDiff} below you");
                    DrawCard(new List<string> { $"{_levelVerdict} ({safeTxt})", _levelPen <= 0 ? "XP: 100% (no penalty)" : $"XP penalty: {_levelPen}%" }, kv.Value, scheme);
                    return;
                }
                if (kv.Key == "Dps")
                {
                    string basis = _alliedCount == 0 ? "Solo -- all damage is yours"
                        : (!_playerAttacking ? "Player idle -- damage is allies'" : "Mixed damage -- split unverified");
                    DrawCard(new List<string> { $"DPS {_dpsText} (PEAK {_dpsPeakText})", basis, $"Allied damage sources: {_alliedCount}" }, kv.Value, scheme);
                    return;
                }
                if (kv.Key == "Xp")
                {
                    var lines = new List<string> { $"XP gained in area  {FormatNumber(_lastAreaXpGained)}" };
                    if (_areaStartLevel > 0 && _areaStartLevel < 100)
                    {
                        var span = (float)Constants.PlayerXpLevels[_areaStartLevel + 1] - Constants.PlayerXpLevels[_areaStartLevel];
                        if (span > 0) lines.Add($"Share of level {_areaStartLevel}  {_lastAreaXpGained / span:P1}");
                    }
                    lines.Add($"Area time  {_lastAreaTimeSpent:mm\\:ss}");
                    DrawCard(lines, kv.Value, scheme);
                    return;
                }
                if (kv.Key == "Gold" || kv.Key == "Gph")
                {
                    var lines = new List<string> { $"Gold gained in area  {FormatNumber(_lastAreaGoldGained)}" };
                    if (_emptyIn > TimeSpan.Zero)
                        lines.Add(_emptyIn.Days > 0
                            ? $"Village gold lasts  {_emptyIn.Days}d {_emptyIn.Hours:00}:{_emptyIn.Minutes:00}:{_emptyIn.Seconds:00}"
                            : $"Village gold lasts  {_emptyIn.Hours:00}:{_emptyIn.Minutes:00}:{_emptyIn.Seconds:00}");
                    DrawCard(lines, kv.Value, scheme);
                    return;
                }
                if (kv.Key == "Ping")
                {
                    DrawPingCard(kv.Value, scheme);
                    return;
                }
            }
            if (Settings.ShowPreloads.Value)
            {
                foreach (var chip in _preloadChips)
                {
                    if (chip.Rows.Count == 0) continue;
                    if (!chip.Bounds.IsEmpty && chip.Bounds.Contains(mp))
                    {
                        ShowPreloadChipCard(chip, scheme);
                        return;
                    }
                }
                var rem = _preloadRemainder;
                if (rem != null && rem.HiddenCount > 0 && !rem.Bounds.IsEmpty && rem.Bounds.Contains(mp))
                    ShowRemainderCard(scheme);
            }
        }

        private void DrawCard(List<string> lines, RectangleF anchor, ColorScheme scheme)
        {
            float w = 0f, h = 0f;
            foreach (var l in lines)
            {
                var s = Graphics.MeasureText(l);
                if (s.X > w) w = s.X;
                h += s.Y + 2;
            }
            w += 30;
            var pos = new Vector2N(anchor.X + (anchor.Width - w) / 2, Math.Max(anchor.Y + anchor.Height + 5, _contentBottom + 5));
            var bg = scheme.Background;
            bg.A = 235;
            Graphics.DrawBox(new RectangleF(pos.X, pos.Y, w, h + 8), bg, CORNER_RADIUS * 0.6f);
            var tp = pos + new Vector2N(15, 5);
            var tc = CardText(scheme);
            foreach (var l in lines)
            {
                var s = Graphics.DrawText(l, tp, tc);
                tp.Y += s.Y + 2;
            }
        }

        private void DrawPingCard(RectangleF anchor, ColorScheme scheme)
        {
            if (_pingHistory.Count == 0) return;
            var vals = _pingHistory.ToArray();
            int cur = vals[vals.Length - 1];
            double avg = vals.Average();
            int min = vals.Min();
            int max = vals.Max();

            string title;
            int cap = Settings.PingHistoryMinutes.Value * 60;
            if (vals.Length >= cap && cap >= 60) title = $"PING -- LAST {cap / 60} MIN";
            else if (vals.Length >= 120) title = $"PING -- LAST {vals.Length / 60} MIN";
            else title = $"PING -- LAST {vals.Length} SEC";
            var labels = new[] { "CUR", "AVG", "MIN", "MAX" };
            var values = new[] { $"{cur} ms", $"{avg:0} ms", $"{min} ms", $"{max} ms" };
            var ink = CardText(scheme);
            var valueCols = new[] { ink, ink, new Color((byte)61, (byte)220, (byte)133, (byte)255), new Color((byte)255, (byte)85, (byte)85, (byte)255) };

            float textH = Graphics.MeasureText("Ag").Y;
            float rowH = textH + 2;
            float titleW = Graphics.MeasureText(title).X;
            float[] colW = new float[4];
            float colsW = 0;
            for (int i = 0; i < 4; i++)
            {
                colW[i] = Math.Max(Graphics.MeasureText(labels[i]).X, Graphics.MeasureText(values[i]).X);
                colsW += colW[i];
            }
            string foot = $"{vals.Length} samples, 1 per second";
            float footW = Graphics.MeasureText(foot).X;
            const float graphH = 40f;
            float graphW = Math.Max(200, colsW + 3 * 26);
            float cardW = Math.Max(Math.Max(titleW, colsW + 3 * 26), graphW + 34 + 4) + 30;
            float cardH = 5 + rowH + 4 + rowH * 2 + 8 + graphH + 4 + rowH + 5;

            float x = anchor.X + (anchor.Width - cardW) / 2;
            float y = Math.Max(anchor.Bottom + 5, _contentBottom + 5);
            var bg = scheme.Background;
            bg.A = 235;
            Graphics.DrawBox(new RectangleF(x, y, cardW, cardH), bg, CORNER_RADIUS * 0.6f);

            var dim = CardText(scheme);
            dim.A = 200;
            Graphics.DrawText(title, new Vector2N(x + 15, y + 5), dim);
            float cy = y + 5 + rowH + 4;
            float lx = x + 15;
            for (int i = 0; i < 4; i++)
            {
                Graphics.DrawText(labels[i], new Vector2N(lx, cy), dim);
                Graphics.DrawText(values[i], new Vector2N(lx, cy + rowH), valueCols[i]);
                lx += colW[i] + 26;
            }
            float gy = cy + rowH * 2 + 8;
            float gx = x + 15;
            float gw = cardW - 30 - 34;
            if (vals.Length >= 2)
            {
                const int maxPts = 240;
                int step = Math.Max(1, (vals.Length - 1) / (maxPts - 1));
                double range = max - min;
                if (Math.Abs(range) < 0.0001) range = 1;
                Vector2N? prev = null;
                for (int i = 0; i < vals.Length; i += step)
                {
                    float px = gx + (float)i / (vals.Length - 1) * gw;
                    float py = gy + graphH - (float)((vals[i] - min) / range * graphH);
                    var pt = new Vector2N(px, py);
                    if (prev != null) Graphics.DrawLine(prev.Value, pt, 2f, scheme.Ping);
                    prev = pt;
                }
                float lastX = gx + gw;
                float lastY = gy + graphH - (float)((vals[vals.Length - 1] - min) / range * graphH);
                if (prev == null || Math.Abs(lastX - prev.Value.X) > 0.5f)
                    Graphics.DrawLine(prev ?? new Vector2N(lastX, lastY), new Vector2N(lastX, lastY), 2f, scheme.Ping);
                Graphics.DrawText(max + " ms", new Vector2N(gx + gw + 4, gy - 2), new Color((byte)255, (byte)85, (byte)85, (byte)255));
                Graphics.DrawText(min + " ms", new Vector2N(gx + gw + 4, gy + graphH - textH), new Color((byte)61, (byte)220, (byte)133, (byte)255));
            }
            Graphics.DrawText(foot, new Vector2N(x + 15, gy + graphH + 4), dim);
        }
    }
}
