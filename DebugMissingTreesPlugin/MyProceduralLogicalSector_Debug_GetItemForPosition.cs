using HarmonyLib;
using Sandbox.Game.WorldEnvironment;
using Sandbox.Game.WorldEnvironment.Definitions;
using System.Collections.Generic;
using VRage.Library.Utils;
using VRage.Utils;

namespace DebugSETreesPlugin
{
    //[HarmonyPatch(typeof(MyProceduralLogicalSector), "GetItemForPosition")]
    public class MyProceduralLogicalSector_Debug_GetItemForPosition
    {
        public static Dictionary<MyProceduralLogicalSector, List<DebugItemInfo>> ItemEnvironmentInfo = new Dictionary<MyProceduralLogicalSector, List<DebugItemInfo>>();

        public struct DebugItemInfo
        {
            public long EntityId;
            public long SectorId;
            public int Index;
            public MySurfaceParams SurfaceParams;
            public int Lod;
            public MyEnvironmentItemMapping[] Mappings;
            public MyDiscreteSampler<MyRuntimeEnvironmentItemInfo>[] Samplers;
            public int HashCode;
            public float Sample;
            public MyDiscreteSampler<MyRuntimeEnvironmentItemInfo> ChosenSampler;
            public MyRuntimeEnvironmentItemInfo ItemInfo;
            public MyRuntimeEnvironmentItemInfo RetItemInfo;
        }

        public static MyRuntimeEnvironmentItemInfo Postfix(
                MyRuntimeEnvironmentItemInfo __result,
                ref MySurfaceParams surface,
                int lod,
                MyProceduralLogicalSector __instance,
                MyProceduralEnvironmentDefinition ___m_environment,
                int ___m_totalSpawned
            )
        {
            var candidates = new List<MyDiscreteSampler<MyRuntimeEnvironmentItemInfo>>();
            var key = new MyBiomeMaterial(surface.Biome, surface.Material);
            MyDiscreteSampler<MyRuntimeEnvironmentItemInfo> sampler;

            if (___m_environment.MaterialEnvironmentMappings.TryGetValue(key, out var mappings))
            {
                foreach (MyEnvironmentItemMapping item in mappings)
                {
                    sampler = item.Sampler(lod);
                    if (sampler != null && item.Rule.Check(surface.HeightRatio, surface.Latitude, surface.Longitude, surface.Normal.Z))
                    {
                        candidates.Add(sampler);
                    }
                }
            }

            int hashCode = surface.Position.GetHashCode();
            float sample = MyHashRandomUtils.UniformFloatFromSeed(hashCode);
            MyRuntimeEnvironmentItemInfo iteminfo;

            if (candidates.Count == 0)
            {
                sampler = null;
            }
            else if (candidates.Count == 1)
            {
                sampler = candidates[0];
            }
            else
            {
                sampler = candidates[(int)(MyHashRandomUtils.UniformFloatFromSeed(~hashCode) * (float)candidates.Count)];
            };

            iteminfo = sampler?.Sample(sample);

            var sectorId = __instance.Id;
            var entityId = __instance.Owner.Entity.EntityId;

            if (!ItemEnvironmentInfo.TryGetValue(__instance, out var iteminfos))
            {
                ItemEnvironmentInfo[__instance] = iteminfos = new List<DebugItemInfo>();
            }

            iteminfos.Add(new DebugItemInfo
            {
                EntityId = entityId,
                SectorId = sectorId,
                Index = ___m_totalSpawned,
                SurfaceParams = surface,
                Lod = lod,
                Mappings = mappings?.ToArray(),
                Samplers = candidates.ToArray(),
                HashCode = hashCode,
                Sample = sample,
                ChosenSampler = sampler,
                ItemInfo = iteminfo,
                RetItemInfo = __result
            });

            System.Diagnostics.Trace.WriteLine(
                $"GetItemForPosition:" +
                $" EntityId={entityId}" +
                $" SectorId={sectorId}" +
                $" Index={___m_totalSpawned}" +
                $" Biome={surface.Biome}" +
                $" Material={surface.Material}" +
                $" HeightRatio={surface.HeightRatio}" +
                $" Latitude={surface.Latitude}" +
                $" Longitude={surface.Longitude}" +
                $" Normal.Z={surface.Normal.Z}" +
                $" Position=({surface.Position.X}, {surface.Position.Y}, {surface.Position.Z})" +
                $" LOD={lod}" +
                $" Candidates.Count={candidates.Count}" +
                $" HashCode={hashCode}" +
                $" Sample={sample}" +
                $" DefinitionIndex={iteminfo?.Index}");

            return __result;
        }
    }
}
