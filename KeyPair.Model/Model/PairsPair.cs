using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace KeyPair.Model
{
    [Serializable]
    [DataContract(Name = "PairsPair", Namespace = "")]
    public class PairsPair
    {
        [DataMember(Name = "PairKey", IsRequired = true)]
        public string PairKey { get; set; }

        [DataMember(Name = "PairValue", IsRequired = true)]
        public string PairValue { get; set; }

        [DataMember(Name = "UpdateDate", IsRequired = false)]
        public DateTime UpdateDate { get; set; }

        public int UserInventoryId { get; set; }
    }
}
