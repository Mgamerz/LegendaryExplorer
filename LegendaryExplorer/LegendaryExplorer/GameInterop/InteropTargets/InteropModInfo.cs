namespace LegendaryExplorer.GameInterop.InteropTargets
{
    /// <summary>
    /// Data class to store information and capabilities of an Interop DLC Mod
    /// </summary>
    public record InteropModInfo
    {
        private static int InteropModVersion = 9;
        public string InteropModName { get; }
        public bool CanUseLLE { get; }
        public bool CanUseCamPath { get; init; }
        public bool CanUseAnimViewer { get; init; }
        public string LiveEditorFilename { get; init; }
        public string TempMapName { get; init; }
        public int Version { get; init; } = InteropModVersion;

        public InteropModInfo(string interopModName, bool canUseLLE)
        {
            InteropModName = interopModName;
            CanUseLLE = canUseLLE;
        }
    }
}