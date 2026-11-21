using System;
using System.Collections.Generic;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;
using ExileCore.Shared.Attributes;

namespace ModernBar
{
    public enum ColorSchemeList
    {
        Default,
        SolarizedDark,
        Dracula,
        Inverted,
        Cyberpunk2077,
        Minecraft,
        Valorant,
        Diablo,
        Monochrome,
        Custom
    }

    public class ModernBarSettings : ISettings
    {
        [Menu("Enable", "Master switch for ModernBar.")]
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Draw X Offset", "Shifts the bar left or right.")]
        public RangeNode<int> DrawXOffset { get; set; } = new RangeNode<int>(0, -150, 150);

        [Menu("Draw Y Offset", "Pushes the bar down (dodge boss/minion UI). Adds to auto-dodge.")]
        public RangeNode<int> DrawYOffset { get; set; } = new RangeNode<int>(0, -50, 300);

        [Menu("Background Alpha", "Opacity of the bar background (0-255). High values cover the icons behind the bar.")]
        public RangeNode<int> BackgroundAlpha { get; set; } = new RangeNode<int>(200, 0, 255);

        [Menu("Divider Alpha", "Transparency of the 1px segment dividers (0-255).")]
        public RangeNode<int> DividerAlpha { get; set; } = new RangeNode<int>(60, 0, 120);

        [Menu("Show Player Level", "Current character level.")]
        public ToggleNode ShowPlayerLevel { get; set; } = new ToggleNode(true);

        [Menu("Show Level Check", "Micro-bar showing zone level vs your level (XP penalty safe zone).")]
        public ToggleNode ShowLevelCheck { get; set; } = new ToggleNode(true);

        [Menu("Show Area Name", "Zone name plus map-tier chip.")]
        public ToggleNode ShowAreaName { get; set; } = new ToggleNode(true);

        [Menu("Show Area Time", "Time spent in the current area.")]
        public ToggleNode ShowAreaTime { get; set; } = new ToggleNode(true);

        [Menu("Show Time Left to Level", "ETA to next level at current XP rate. Merged with Time to 100 in one segment.")]
        public ToggleNode ShowTimeLeft { get; set; } = new ToggleNode(true);

        [Menu("Show Time to 100", "ETA to level 100. Merged with Time Left in one segment.")]
        public ToggleNode ShowTimeTo100 { get; set; } = new ToggleNode(true);

        [Menu("Show XP Rate", "XP per hour plus level percent. Progress line uses the percent.")]
        public ToggleNode ShowXpRate { get; set; } = new ToggleNode(true);

        [Menu("Show XP Progress Line", "Thin progress line under the bar driven by level percent.")]
        public ToggleNode ShowXpProgress { get; set; } = new ToggleNode(true);

        [Menu("Show DPS", "Damage per second with peak. P/M tag shows whether you or your minions lead.")]
        public ToggleNode ShowDps { get; set; } = new ToggleNode(true);

        [Menu("Show Gold", "Current inventory gold plus stored village gold.")]
        public ToggleNode ShowGold { get; set; } = new ToggleNode(true);

        [Menu("Show G/H", "Gold per hour.")]
        public ToggleNode ShowGoldPerHour { get; set; } = new ToggleNode(true);

        [Menu("Show Ping", "Latency number plus color dot. Hover for the full history panel.")]
        public ToggleNode ShowPing { get; set; } = new ToggleNode(true);

        [Menu("Ping History (min)", "How much latency history to keep for the ping panel (1-120 minutes).")]
        public RangeNode<int> PingHistoryMinutes { get; set; } = new RangeNode<int>(30, 1, 120);

        [Menu("Show Preloads", "Preload strip under the bar, collapsed by family.")]
        public ToggleNode ShowPreloads { get; set; } = new ToggleNode(true);

        [Menu("Show Preloads in Hideout", "Run the preload scan in hideouts too.")]
        public ToggleNode ShowPreloadInHideout { get; set; } = new ToggleNode(false);

        [Menu("Color Scheme", "Theme preset for the bar.")]
        public ListNode ColorScheme { get; set; } = new ListNode
        {
            Value = "Diablo",
            Values = new List<string>(Enum.GetNames(typeof(ColorSchemeList)))
        };

        [Menu("Background Color", "Bar background.")]
        public ColorNode BackgroundColor { get; set; } = new ColorNode(Color.White);

        [Menu("Timer Color", "Area-time value and secondary text.")]
        public ColorNode TimerColor { get; set; } = new ColorNode(Color.White);

        [Menu("Ping Color", "Ping segment and graph.")]
        public ColorNode PingColor { get; set; } = new ColorNode(Color.White);

        [Menu("Area Color", "Area name segment.")]
        public ColorNode AreaColor { get; set; } = new ColorNode(Color.White);

        [Menu("Time Left Color", "Merged time-estimates segment.")]
        public ColorNode TimeLeftColor { get; set; } = new ColorNode(Color.White);

        [Menu("XP Rate Color", "XP rate segment.")]
        public ColorNode XphColor { get; set; } = new ColorNode(Color.White);

        [Menu("Player Level Color", "Player level value.")]
        public ColorNode XphGetLeftColor { get; set; } = new ColorNode(Color.White);

        [Menu("Label Color", "Segment labels (LVL, TIME, GOLD, G/H, PING).")]
        public ColorNode LabelColor { get; set; } = new ColorNode(Color.White);

        [Menu("Gold Color", "Gold amounts.")]
        public ColorNode GoldColor { get; set; } = new ColorNode(Color.White);

        [Menu("Reset Theme Colors", "Resets custom colors to the selected theme defaults.")]
        public ButtonNode ResetThemeColors { get; set; } = new ButtonNode();
    }
}
