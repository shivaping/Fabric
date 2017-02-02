using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyPair.Model;

using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System.Data.SqlClient;
using System.Data;
using Microsoft.ServiceFabric.Services.Client;

namespace KeyPairClient
{
    class Program
    {
        private const string KeyPairStatesName = "KeyPairStates";
        static void Main(string[] args)
        {

            //ServiceUriBuilder builder = new ServiceUriBuilder(KeyPairStatesName);
            
            Guid UserID = new Guid("23103127-A5DF-495A-B672-C041D5166D89");
            
            // LoadUserPairs(UserID);
            IUserKeyPair iUserServiceClient = ServiceProxy.Create<IUserKeyPair>(new Uri("fabric:/KeyPairStateFull/KeyPairStates"), new ServicePartitionKey(HashUtil.getLongHashCode(UserID.ToString())));
            Task<Dictionary<int, Pairs>> userKeyPair = iUserServiceClient.GetPairs(UserID);
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject( userKeyPair.Result));
            Console.ReadLine();
        }
        private static Dictionary<int, Pairs> LoadUserPairs(Guid userid)
        {
            Dictionary<int, Pairs> p = new Dictionary<int, Pairs>();

            //ICodePackageActivationContext codePackageContext = this.Context.CodePackageActivationContext;
            //ConfigurationPackage configPackage = codePackageContext.GetConfigurationPackageObject("Config");
            //ConfigurationSection configSection = configPackage.Settings.Sections["PairActorService.Settings"];
            string dbSettingValue = "Server=KA-V-QADB01; Database=ContentServer; user ID=development; password=d3v3l0pm3nt#; Application Name=CONTENT_SERVER_SERVICE_SERVER_QA;";
            using (SqlConnection con = new SqlConnection(dbSettingValue))
            {
                using (SqlCommand cmd = new SqlCommand("KAP_GetKeyValuePair_V1", con))
                {

                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = "KAP_GetKeyValuePair_V1";
                    cmd.Parameters.Add("@UserID", SqlDbType.UniqueIdentifier).Value = userid;
                    con.Open();
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            int pairID = int.Parse(dr["PairID"].ToString()); string PairKey = dr["PairKey"].ToString(); string PairValue = dr["PairValue"].ToString();
                            if (!p.ContainsKey(pairID))
                                p[pairID] = new Pairs() { Items = new List<PairsPair>() { new PairsPair() { PairKey = PairKey, PairValue = PairValue } } };
                            else
                                p[pairID].Items.Add(new PairsPair() { PairKey = PairKey, PairValue = PairValue });
                        }
                    }
                }
            }
            return p;
        }
    }
}
