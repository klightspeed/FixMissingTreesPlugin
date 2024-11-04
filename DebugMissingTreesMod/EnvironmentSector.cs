using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DebugSETrees
{
    [ProtoContract]
    public struct EnvironmentSector
    {
        [ProtoMember(1)]
        public long EntityId { get; set; }

        [ProtoMember(2)]
        public long LogicalSectorId { get; set; }

        [ProtoMember(3)]
        public int LodLevel { get; set; }

        [ProtoMember(4)]
        public Vector3D SectorPosition { get; set; }

        [ProtoMember(5)]
        public Vector3D[] SectorBounds { get; set; }

        [ProtoMember(6)]
        public bool IsLogicalSector { get; set; }

        public int SectorX { get; set; }

        public int SectorY { get; set; }

        public int SectorFace { get; set; }

        public int SectorLod { get; set; }

        public List<EnvironmentItem> Items { get; set; }

        public bool Equals(EnvironmentSector other)
        {
            return Equals(ref other);
        }

        public bool Equals(ref EnvironmentSector other)
        {
            return this.EntityId == other.EntityId
                && this.LogicalSectorId == other.LogicalSectorId
                && this.LodLevel == other.LodLevel
                && this.SectorPosition == other.SectorPosition
                && Enumerable.SequenceEqual(this.SectorBounds, other.SectorBounds);
        }

        public override bool Equals(object obj)
        {
            return obj is EnvironmentSector && Equals((EnvironmentSector)obj);
        }

        public override int GetHashCode()
        {
            return LogicalSectorId.GetHashCode() ^ EntityId.GetHashCode();
        }
    }
}
