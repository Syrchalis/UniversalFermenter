using System.Linq;
using Multiplayer.API;
using Verse;

namespace UniversalFermenter
{
    [StaticConstructorOnStartup]
    internal static class MultiplayerCompatibility
    {
        static MultiplayerCompatibility()
        {
            if (!MP.enabled) return;

            // Sync all gizmo clicks

            MP.RegisterSyncMethod(typeof(Command_Quality), nameof(Command_Quality.ChangeQuality)).SetContext(SyncContext.MapSelected);

            string[] methods =
            {
                nameof(UF_Utility.FinishProcess),
                nameof(UF_Utility.ProgressOneDay),
                nameof(UF_Utility.ProgressHalfQuadrum),
                nameof(UF_Utility.EmptyObject),
                nameof(UF_Utility.LogSpeedFactors)
            };
            foreach (string methodName in methods)
            {
                MP.RegisterSyncMethod(typeof(UF_Utility), methodName);
            }

            MP.RegisterSyncWorker<UF_Process>(UF_Process_SyncWorker, shouldConstruct: false);
        }

        // This is only called whenever user changes process, which is seldom.
        private static void UF_Process_SyncWorker(SyncWorker sync, ref UF_Process obj)
        {
            if (sync.isWriting)
            {
                sync.Write(obj.uniqueID);
            }
            else
            {
                int id = sync.Read<int>();

                obj = UF_Utility.allUFProcesses.First(p => p.uniqueID == id);
            }
        }
    }
}
