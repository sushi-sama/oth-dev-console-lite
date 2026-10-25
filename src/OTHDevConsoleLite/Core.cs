using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppVehiclePhysics;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(OTHDevConsoleLite.Core), "OTH Dev Console Lite", "0.1.0", "local")]
[assembly: MelonGame(null, null)]

namespace OTHDevConsoleLite
{
    /// <summary>
    /// Minimal in-game console for "over the hill".
    /// Commands: money, day, car (plus help/clear/close).
    /// </summary>
    public sealed class Core : MelonMod
    {
        public static Core Instance { get; private set; }

        private sealed class Cmd
        {
            public string Usage;
            public string Help;
            public Func<string[], string> Run;
            public bool HostOnly;
        }

        private readonly Dictionary<string, Cmd> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _log = new();
        private readonly List<string> _history = new();
        private string _input = "";
        private int _historyIndex = -1;
        private bool _open;
        private bool _focusField;

        private const int MaxLogLines = 400;
        private const float LineHeight = 17f;

        public override void OnInitializeMelon()
        {
            Instance = this;
            RegisterCommands();

            MelonLogger.Msg("==============================================");
            MelonLogger.Msg(" OTH Dev Console Lite 0.1.0 loaded");
            MelonLogger.Msg($" game: {Application.version} | unity: {Application.unityVersion}");
            MelonLogger.Msg(" commands: money, day, car");
            MelonLogger.Msg(" toggle: F8 or ` (backquote)");
            MelonLogger.Msg("==============================================");
        }

        public override void OnDeinitializeMelon()
        {
            MelonLogger.Msg("OTH Dev Console Lite unloaded");
        }

        // ---------------------------------------------------------------- input

        public override void OnUpdate()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f8Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)
                Toggle();
        }

        private void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                _focusField = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private void Close() => Toggle();

        // ------------------------------------------------------------------ gui

        public override void OnGUI()
        {
            if (!_open) return;

            GUI.depth = -1000;
            var e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Submit();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.UpArrow)
                {
                    HistoryStep(-1);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow)
                {
                    HistoryStep(1);
                    e.Use();
                }
            }

            float w = Screen.width * 0.72f;
            float h = Screen.height * 0.52f;
            var window = new Rect(12, 12, w, h);

            GUI.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 0.94f);
            GUI.Box(window, GUIContent.none);

            var header = new Rect(window.x + 8, window.y + 6, window.width - 16, 20);
            GUI.color = new Color(1f, 0.72f, 0.25f);
            GUI.Label(header, "over the hill - dev console lite   [F8 close | help]");
            GUI.color = Color.white;

            // log area
            var logRect = new Rect(window.x + 8, window.y + 28, window.width - 16, window.height - 64);
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            GUI.Box(logRect, GUIContent.none);

            var visible = Mathf.Max(1, Mathf.FloorToInt(logRect.height / LineHeight) - 1);
            int start = Mathf.Max(0, _log.Count - visible);

            float y = logRect.y + 4;
            for (int i = start; i < _log.Count; i++)
            {
                var line = _log[i];
                GUI.color = line.StartsWith("!") ? new Color(1f, 0.42f, 0.38f)
                          : line.StartsWith(">") ? new Color(0.62f, 0.86f, 1f)
                          : new Color(0.86f, 0.88f, 0.9f);
                GUI.Label(new Rect(logRect.x + 6, y, logRect.width - 12, LineHeight), line);
                y += LineHeight;
            }
            GUI.color = Color.white;

            // input
            var inputRect = new Rect(window.x + 8, window.yMax - 30, window.width - 16, 24);
            GUI.backgroundColor = new Color(0.14f, 0.15f, 0.17f, 1f);
            GUI.SetNextControlName("othcmd");
            _input = GUI.TextField(inputRect, _input, 4096);

            if (_focusField && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("othcmd");
                _focusField = false;
            }
        }

        // ------------------------------------------------------------- commands

        private void RegisterCommands()
        {
            Add("help", "help [command]", "list commands, or detail one", a =>
            {
                if (a.Length > 1 && _commands.TryGetValue(a[1], out var c))
                    return $"{c.Usage}\n  {c.Help}";

                var sb = new StringBuilder();
                foreach (var kv in _commands.OrderBy(k => k.Key))
                    sb.AppendLine($"  {kv.Value.Usage,-24} {kv.Value.Help}{(kv.Value.HostOnly ? " *" : "")}");
                sb.AppendLine("  * = host only (refused when you are a client)");
                return sb.ToString().TrimEnd();
            });

            Add("clear", "clear", "clear the console", _ => { _log.Clear(); return null; });
            Add("close", "close", "close the console", _ => { Close(); return null; });

            Add("money", "money <amount>", "add money to the local player", a =>
            {
                if (a.Length < 2 || !int.TryParse(a[1], out var amount))
                    return "!usage: money <amount>";
                var mgr = Il2Cpp.PlayerInventoryManager.Instance;
                if (mgr == null) return "!not in a game (no PlayerInventoryManager)";
                if (mgr.Inventory == null) return "!inventory not ready";

                mgr.Inventory.AddItem(mgr.MoneyItemId, amount, false, true);
                return $"added {amount} money (item {mgr.MoneyItemId})";
            }, hostOnly: true);

            Add("day", "day [hour] [minute]", "set the time of day (default 12:00)", a =>
            {
                int hour = 12, minute = 0;
                if (a.Length > 1 && !int.TryParse(a[1], out hour)) return "!usage: day [hour 0-23] [minute 0-59]";
                if (a.Length > 2 && !int.TryParse(a[2], out minute)) return "!usage: day [hour 0-23] [minute 0-59]";
                hour = Mathf.Clamp(hour, 0, 23);
                minute = Mathf.Clamp(minute, 0, 59);

                try
                {
                    var tm = Il2Cpp.TimeManager.Instance;
                    if (tm != null)
                    {
                        tm.Hours = hour;
                        tm.Minutes = minute;
                        try { tm.ApplyWeatherAndTime(); } catch { }
                        return $"time -> {hour:00}:{minute:00} (networked)";
                    }

                    var offline = Il2Cpp.OfflineTimeManager.Instance;
                    if (offline != null)
                    {
                        offline.Hours = hour;
                        offline.Minutes = minute;
                        return $"time -> {hour:00}:{minute:00} (offline)";
                    }

                    var cm = Cheats();
                    if (cm != null)
                    {
                        cm.RPC_SetTime(hour, minute);
                        return $"time -> {hour:00}:{minute:00} (synced to session)";
                    }
                }
                catch (Exception ex)
                {
                    return "!time change failed: " + ex.Message;
                }

                return "!no time manager found (start a run)";
            }, hostOnly: true);

            Add("car", "car", "show the current vehicle and its stats", _ =>
            {
                var v = LocalVehicle();
                if (v == null) return "!no vehicle found (are you driving?)";

                var rb = v.cachedRigidbody;
                var sb = new StringBuilder();
                sb.AppendLine($"vehicle: {v.name}");
                sb.AppendLine($"  speed      {v.speed:F2} m/s");
                sb.AppendLine($"  mass       {(rb != null ? rb.mass.ToString("F0") : "?")} kg");
                sb.AppendLine($"  wheels     {v.wheelCount}");
                sb.AppendLine($"  climbStep  {v.climbStepAngle:F1} deg");
                sb.AppendLine($"  substeps   {v.solverSubsteps}");
                return sb.ToString().TrimEnd();
            });
        }

        private void Add(string name, string usage, string help, Func<string[], string> run, bool hostOnly = false)
            => _commands[name] = new Cmd { Usage = usage, Help = help, Run = run, HostOnly = hostOnly };

        // ------------------------------------------------------------- plumbing

        private void Submit()
        {
            var line = _input.Trim();
            _input = "";
            if (line.Length == 0) return;

            _history.Add(line);
            _historyIndex = _history.Count;
            Append("> " + line);
            Append(Execute(line));
        }

        private string Execute(string line)
        {
            var parts = Tokenize(line);
            if (parts.Count == 0) return null;

            if (!_commands.TryGetValue(parts[0], out var cmd))
                return $"!unknown command '{parts[0]}' - try help";

            if (cmd.HostOnly && !IsHost())
                return "!host only - you are a client in this session";

            try
            {
                return cmd.Run(parts.ToArray());
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[cmd:{parts[0]}] {ex}");
                return $"!{ex.GetType().Name}: {ex.Message}";
            }
        }

        private static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;

            foreach (var ch in line)
            {
                if (ch == '"') { quoted = !quoted; continue; }
                if (char.IsWhiteSpace(ch) && !quoted)
                {
                    if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(ch);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        private void Append(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            foreach (var l in line.Split('\n'))
                _log.Add(l.TrimEnd('\r'));
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        private void HistoryStep(int delta)
        {
            if (_history.Count == 0) return;
            _historyIndex = Mathf.Clamp(_historyIndex + delta, 0, _history.Count);
            _input = _historyIndex >= _history.Count ? "" : _history[_historyIndex];
        }

        // ---------------------------------------------------------- game access

        private static VehicleBase LocalVehicle()
        {
            var all = FindAll<VehicleBase>();
            if (all.Length == 0) return null;

            foreach (var v in all)
                if (v.HasInputAuthority) return v;

            return all[0];
        }

        private static T[] FindAll<T>() where T : Il2CppObjectBase
        {
            var arr = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<T>());
            if (arr == null || arr.Length == 0) return Array.Empty<T>();

            var result = new T[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                result[i] = arr[i].TryCast<T>();
            return result;
        }

        private static Il2Cpp.CheatsManager Cheats()
        {
            try
            {
                return Il2Cpp.CheatsManager.Instance;
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------ host gate

        private static Il2CppFusion.NetworkRunner Runner()
        {
            try
            {
                var all = FindAll<Il2CppFusion.NetworkRunner>();
                foreach (var r in all)
                    if (r != null && r.IsRunning) return r;
                return all.Length > 0 ? all[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsHost()
        {
            var r = Runner();
            if (r == null) return true;      // menu / offline
            if (r.IsSinglePlayer) return true;
            if (r.IsServer) return true;     // co-op host (or a dedicated server)
            if (r.IsSharedModeMasterClient) return true;
            return false;
        }
    }
}
