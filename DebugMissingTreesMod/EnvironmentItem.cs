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
    public struct EnvironmentItem
    {
        [ProtoMember(1)]
        public long EntityId { get; set; }

        [ProtoMember(2)]
        public string EntityType { get; set; }

        [ProtoMember(3)]
        public long LogicalSectorId { get; set; }

        [ProtoMember(4)]
        public int LodLevel { get; set; }
        
        [ProtoMember(5)]
        public int ItemNumber { get; set; }

        [ProtoMember(6)]
        public Vector3D ItemPosition { get; set; }

        [ProtoMember(7)]
        public Quaternion ItemRotation { get; set; }

        [ProtoMember(8)]
        public short ItemDefinitionIndex { get; set; }

        [ProtoMember(9)]
        public short ItemModelIndex { get; set; }

        [ProtoMember(10)]
        public string ItemDefinitionType { get; set; }

        [ProtoMember(11)]
        public string ItemDefinitionSubtype { get; set; }

        [ProtoMember(12)]
        public string ItemModelName { get; set; }

        [ProtoMember(13)]
        public string ItemModelType { get; set; }

        [ProtoMember(14)]
        public string ItemModelSubtype { get; set; }

        [ProtoMember(15)]
        public double ModelSize { get; set; }

        [ProtoMember(16)]
        public double ModelMass { get; set; }

        [ProtoMember(17)]
        public bool ItemEnabled { get; set; }

        [ProtoMember(18)]
        public bool FromServer { get; set; }

        [ProtoMember(19)]
        public int MinLod { get; set; }

        [ProtoMember(20)]
        public int MaxLod { get; set; }

        [ProtoMember(21)]
        public Vector3D SectorRelativePosition { get; set; }

        public List<int> SeenLods { get; set; }

        public double DistanceFromPlayer { get; set; }


        public bool Equals(EnvironmentItem other)
        {
            return Equals(ref other);
        }

        public bool Equals(ref EnvironmentItem other, double maxdist = 0)
        {
            return this.EntityId == other.EntityId
                && this.LogicalSectorId == other.LogicalSectorId
                && this.ItemNumber == other.ItemNumber
                && Vector3D.DistanceSquared(this.ItemPosition, other.ItemPosition) <= maxdist * maxdist
                && this.ItemRotation == other.ItemRotation
                && this.ItemDefinitionIndex == other.ItemDefinitionIndex
                && this.ItemModelType == other.ItemModelType
                && this.ItemModelSubtype == other.ItemModelSubtype
                && this.ItemModelName == other.ItemModelName;
        }

        public override bool Equals(object obj)
        {
            return obj is EnvironmentItem && Equals((EnvironmentItem)obj);
        }

        public override int GetHashCode()
        {
            return LogicalSectorId.GetHashCode() ^ EntityId.GetHashCode();
        }
    }
}
