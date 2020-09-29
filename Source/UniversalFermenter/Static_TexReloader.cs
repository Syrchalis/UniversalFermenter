using HarmonyLib;
using Verse;

namespace UniversalFermenter
{
    public static class TexReloader
    {
        public static void Reload(Thing t, string texPath)
        {
            Graphic graphic = GraphicDatabase.Get(t.def.graphicData.graphicClass, texPath, ShaderDatabase.LoadShader(t.def.graphicData.shaderType.shaderPath), t.def.graphicData.drawSize, t.DrawColor, t.DrawColorTwo);
            AccessTools.Field(typeof(Thing), "graphicInt").SetValue(t, graphic);
            t.Map?.mapDrawer.MapMeshDirty(t.Position, MapMeshFlag.Things);
        }
    }
}