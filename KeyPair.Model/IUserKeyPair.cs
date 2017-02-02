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
        Task<Dictionary<int, Pairs>> GetPairs(Guid UserID);
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
