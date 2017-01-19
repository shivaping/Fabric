using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyPair.Model;

using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;

namespace KeyPairClient
{
    class Program
    {
        private const string KeyPairStatesName = "KeyPairStates";
        static void Main(string[] args)
        {

            //ServiceUriBuilder builder = new ServiceUriBuilder(KeyPairStatesName);
            UserKeyPair keyPair = new UserKeyPair();
            keyPair.UserID = new Guid("59B6B998-CB57-4093-9E79-772DC2FF63B7");
            IUserKeyPair iUserServiceClient = ServiceProxy.Create<IUserKeyPair>(new Uri("fabric:/KeyPairStateFull/KeyPairStates"), keyPair.GetPartitionKey());
            Task<UserKeyPair> userKeyPair = iUserServiceClient.GetPairs(keyPair.UserID);
            UserKeyPair result = userKeyPair.Result;
          
        }
    }
}
