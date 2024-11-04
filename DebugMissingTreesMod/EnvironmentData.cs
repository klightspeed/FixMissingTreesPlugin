using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugSETrees
{
    [ProtoContract]
    public class EnvironmentData
    {
        [ProtoMember(1)]
        public List<EnvironmentItem> Items { get; set; }

        [ProtoMember(2)]
        public List<EnvironmentItem> AddItems { get; set; }

        [ProtoMember(3)]
        public List<EnvironmentItem> DeleteItems { get; set; }

        [ProtoMember(4)]
        public List<EnvironmentSector> Sectors { get; set; }

        [ProtoMember(5)]
        public List<EnvironmentSector> AddSectors { get; set; }

        [ProtoMember(6)]
        public List<EnvironmentSector> DeleteSectors { get; set; }

        [ProtoMember(7)]
        public long IdentityId { get; set; }

        [ProtoMember(8)]
        public bool FromServer { get; set; }
    }
}
