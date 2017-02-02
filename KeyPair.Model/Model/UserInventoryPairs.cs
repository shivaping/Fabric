using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace KeyPair.Model
{
    [Serializable]
    [DataContract(Name = "UserInventoryPairs", Namespace = "")]
    public class UserInventoryPairs
    {
        [DataMember(Name = "UserInventoryID", IsRequired = true)]
        public int UserInventoryID { get; set; }

        [DataMember(Name = "Pairs", IsRequired = true)]
        public Pairs Pairs { get; set; }
    }
}
