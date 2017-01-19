using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Runtime.Serialization;
using Microsoft.ServiceFabric.Services.Client;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Fabric;

namespace KeyPair.Model
{
    public interface IUserKeyPair : IService
    {
        Task<string> helloworld();
        Task<UserKeyPair> GetPairs(Guid UserID);
    }

    [DataContract]
    public class UserKeyPair 
    {

        [DataMember]
        public Guid UserID { get; set; }
        [DataMember]
        public List<Pairs> Pairs { get; set; }

        public ServicePartitionKey GetPartitionKey()
        {
            return new ServicePartitionKey(HashUtil.getLongHashCode(this.UserID.ToString()));
        }
      
    }
    [Serializable]
    [XmlRoot(ElementName = "Pairs", Namespace = "", IsNullable = false)]
    [DataContract(Name = "Pairs", Namespace = "")]
    public class Pairs
    {
        [XmlElement(ElementName = "Pair")]
        [DataMember(Name = "Items", IsRequired = true)]
        public PairsPair[] Items { get; set; }

        [XmlElement(ElementName = "PairID")]
        [DataMember(Name = "PairID", IsRequired = true)]
        public int PairID { get; set; }
    }
    [Serializable]
    [XmlRoot(ElementName = "PairsPair", Namespace = "")]
    [DataContract(Name = "PairsPair", Namespace = "")]
    public class PairsPair
    {
        [XmlElement(ElementName = "PairKey")]
        [DataMember(Name = "PairKey", IsRequired = true)]
        public string PairKey { get; set; }

        [XmlElement(ElementName = "PairValue")]
        [DataMember(Name = "PairValue", IsRequired = true)]
        public string PairValue { get; set; }

        [XmlElement(ElementName = "UpdateDate")]
        [DataMember(Name = "UpdateDate", IsRequired = false)]
        public DateTime UpdateDate { get; set; }

        public int UserInventoryId { get; set; }
    }
    public class HashUtil
    {
        public static long getLongHashCode(string stringInput)
        {
            byte[] byteContents = Encoding.Unicode.GetBytes(stringInput);
            MD5CryptoServiceProvider hash = new MD5CryptoServiceProvider();
            byte[] hashText = hash.ComputeHash(byteContents);
            return BitConverter.ToInt64(hashText, 0) ^ BitConverter.ToInt64(hashText, 7);
        }

        public static int getIntHashCode(string stringInput)
        {
            return (int)getLongHashCode(stringInput);
        }
    }
    public class ServiceUriBuilder
    {
        public ServiceUriBuilder(string serviceInstance)
        {
            this.ActivationContext = FabricRuntime.GetActivationContext();
            this.ServiceInstance = serviceInstance;
        }

        public ServiceUriBuilder(ICodePackageActivationContext context, string serviceInstance)
        {
            this.ActivationContext = context;
            this.ServiceInstance = serviceInstance;
        }

        public ServiceUriBuilder(ICodePackageActivationContext context, string applicationInstance, string serviceInstance)
        {
            this.ActivationContext = context;
            this.ApplicationInstance = applicationInstance;
            this.ServiceInstance = serviceInstance;
        }

        /// <summary>
        /// The name of the application instance that contains he service.
        /// </summary>
        public string ApplicationInstance { get; set; }

        /// <summary>
        /// The name of the service instance.
        /// </summary>
        public string ServiceInstance { get; set; }

        /// <summary>
        /// The local activation context
        /// </summary>
        public ICodePackageActivationContext ActivationContext { get; set; }

        public Uri ToUri()
        {
            string applicationInstance = this.ApplicationInstance;

            if (String.IsNullOrEmpty(applicationInstance))
            {
                // the ApplicationName property here automatically prepends "fabric:/" for us
                applicationInstance = this.ActivationContext.ApplicationName.Replace("fabric:/", String.Empty);
            }

            return new Uri("fabric:/" + applicationInstance + "/" + this.ServiceInstance);
        }
    }
}
