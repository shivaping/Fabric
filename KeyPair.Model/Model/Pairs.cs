using System;
using System.Collections.Generic;
using System.Runtime.Serialization;


namespace KeyPair.Model
{
    [Serializable]
    [DataContract(Name = "Pairs", Namespace = "")]
    public class Pairs
    {
        [DataMember(Name = "Items", IsRequired = true)]
        public List<PairsPair> Items { get; set; }

        [DataMember(Name = "FromCache", IsRequired = true)]
        public bool FromCache { get; set; }
    }
}
