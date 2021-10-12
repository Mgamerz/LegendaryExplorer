using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.GameInterop.LiveEditors
{
    /// <summary>
    /// Handles communication between the Live Level Editor and the Interop Mod at a high level
    /// </summary>
    public class LiveEditor
    {
        protected readonly InteropTarget Target;
        public bool AllowEdits { get; set; } = true;

        public LiveEditor(InteropTarget target)
        {
            Target = target;
        }

        public virtual void SelectActor(int actorListIndex)
        {
            Target.ExecuteConsoleCommands(VarCmd(actorListIndex, (int)IntVarIndexes.ActorArrayIndex), "ce SelectActor");
        }

        public virtual void UpdateLocation(int x, int y, int z)
        {
            if (!AllowEdits) return;
            Target.ExecuteConsoleCommands(VarCmd((float)x, (int)FloatVarIndexes.XPos),
                VarCmd((float)y, (int)FloatVarIndexes.YPos),
                VarCmd((float)z, (int)FloatVarIndexes.ZPos),
                "ce SetLocation");
        }

        public virtual void UpdateRotation(int pitch, int yaw, int roll = 0)
        {
            if (!AllowEdits) return;
            int pitchUnrealUnits = ((float)pitch).DegreesToUnrealRotationUnits();
            int yawUnrealUnits = ((float)yaw).DegreesToUnrealRotationUnits();
            if (Target.Game is MEGame.ME3)
            {
                int rollUnrealUnits = ((float)roll).DegreesToUnrealRotationUnits();
                Target.ExecuteConsoleCommands(VarCmd(pitchUnrealUnits, (int)IntVarIndexes.ME3Pitch),
                    VarCmd(yawUnrealUnits, (int)IntVarIndexes.ME3Yaw),
                    VarCmd(rollUnrealUnits, (int)IntVarIndexes.ME3Roll),
                    "ce SetRotation");
            }
            else
            {
                var rot = new Rotator(pitchUnrealUnits, yawUnrealUnits, 0).GetDirectionalVector();
                Target.ExecuteConsoleCommands(VarCmd(rot.X, (int)FloatVarIndexes.XRotComponent),
                    VarCmd(rot.Y, (int)FloatVarIndexes.YRotComponent),
                    VarCmd(rot.Z, (int)FloatVarIndexes.ZRotComponent),
                    "ce SetRotation");
            }
        }

        protected virtual string VarCmd(float value, int index)
        {
            return $"initplotmanagervaluebyindex {index} float {value}";
        }

        protected virtual string VarCmd(bool value, int index)
        {
            return $"initplotmanagervaluebyindex {index} bool {(value ? 1 : 0)}";
        }

        protected virtual string VarCmd(int value, int index)
        {
            return $"initplotmanagervaluebyindex {index} int {value}";
        }

        private enum FloatVarIndexes
        {
            XPos = 1,
            YPos = 2,
            ZPos = 3,
            XRotComponent = 4,
            YRotComponent = 5,
            ZRotComponent = 6,
        }

        private enum BoolVarIndexes
        {
        }

        private enum IntVarIndexes
        {
            ActorArrayIndex = 1,
            ME3Pitch = 2,
            ME3Yaw = 3,
            ME3Roll = 4,
        }
    }

}