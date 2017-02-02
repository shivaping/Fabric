using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using KeyPair.Model;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Data;
using System.Fabric.Description;
using System.IO;
using System.Runtime.Serialization;
using KeyPairStates.Utility;
using System.Data.SqlClient;
using System.Data;

namespace KeyPairStates
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal class KeyPairStates : StatefulService, IUserKeyPair
    {
        internal const string KeyPairStatesType = "KeyPairStatesType";
        private const string KeyPairDictionaryName = "KeyPairItems";
        private BackupManagerType backupStorageType;
        private IBackupStore backupManager;
        private const string BackupCountDictionaryName = "BackupCountingDictionary";

        public KeyPairStates(StatefulServiceContext context)
            : base(context)
        { }

        #region Interface Implementation
        public async Task<Dictionary<int, Pairs>> GetPairs(Guid UserID)
        {
            IReliableDictionary<Guid, Dictionary<int, Pairs>> userKeyPairItems = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Dictionary<int, Pairs>>>(KeyPairDictionaryName);

            ConditionalValue<Dictionary<int, Pairs>> userKeyPair;
            //List<PairsPair> PairsPair1 = new List<KeyPair.Model.PairsPair>() { new PairsPair() { PairKey = "First", PairValue = "FirstValue", UpdateDate = DateTime.Now, UserInventoryId = 1 } };
            //List<PairsPair> PairsPair2 = new List<KeyPair.Model.PairsPair>() { new PairsPair() { PairKey = "Second", PairValue = "SecondValue", UpdateDate = DateTime.Now, UserInventoryId = 1 } };
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                userKeyPair = await userKeyPairItems.TryGetValueAsync(tx, UserID);
                //Not in state get from SQL/SVC/API
                if (!userKeyPair.HasValue)
                {
                    userKeyPair = new ConditionalValue<Dictionary<int, Pairs>>(true, LoadUserPairs(UserID));
                    await userKeyPairItems.SetAsync(tx, UserID, userKeyPair.Value);
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Created userPair item: {0}", Newtonsoft.Json.JsonConvert.SerializeObject(userKeyPair.Value));
                }
                else
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Retrieved userPair item: {0}", Newtonsoft.Json.JsonConvert.SerializeObject( userKeyPair.Value));
                }
                await tx.CommitAsync();
            }

            return userKeyPair.Value;
        }
        #endregion


        #region Listeners
        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            //return new ServiceReplicaListener[0];
            return new[]
           {
                new ServiceReplicaListener(context => this.CreateServiceRemotingListener(context))
            };
        }
        #endregion

        #region Load Data
        private Dictionary<int, Pairs> LoadUserPairs(Guid userid)
        {
            Dictionary<int, Pairs> p = new Dictionary<int, Pairs>();

            ICodePackageActivationContext codePackageContext = this.Context.CodePackageActivationContext;
            ConfigurationPackage configPackage = codePackageContext.GetConfigurationPackageObject("Config");
            ConfigurationSection configSection = configPackage.Settings.Sections["KeyPairStates.Settings"];
            string dbSettingValue = configSection.Parameters["DBConnection"].Value;
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
        #endregion

        #region Main Method
        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "inside RunAsync for KeyPairState Service");
                return Task.WhenAll(new List<Task>() { this.PeriodicTakeBackupAsync(cancellationToken) });
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "RunAsync Failed, {0}", e);
                throw;
            }

        }
        #endregion
        
        #region Back Up and Restore
        private async Task PeriodicTakeBackupAsync(CancellationToken cancellationToken)
        {
            long backupsTaken = 0;
            this.SetupBackupManager();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (this.backupStorageType == BackupManagerType.None)
                {
                    break;
                }
                else
                {
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Backup Initialized with a wait time of {0} seconds", this.backupManager.backupFrequencyInSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(this.backupManager.backupFrequencyInSeconds));
                    BackupDescription backupDescription = new BackupDescription(BackupOption.Full, this.BackupCallbackAsync);
                    await this.BackupAsync(backupDescription, TimeSpan.FromHours(1), cancellationToken);
                    backupsTaken++;
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Backup {0} taken", backupsTaken);
                }
            }
        }
        protected override async Task<bool> OnDataLossAsync(RestoreContext restoreCtx, CancellationToken cancellationToken)
        {
            ServiceEventSource.Current.ServiceMessage(this.Context, "OnDataLoss Invoked!");
            this.SetupBackupManager();

            try
            {
                string backupFolder;

                if (this.backupStorageType == BackupManagerType.None)
                {
                    //since we have no backup configured, we return false to indicate
                    //that state has not changed. This replica will become the basis
                    //for future replica builds
                    return false;
                }
                else
                {
                    backupFolder = await this.backupManager.RestoreLatestBackupToTempLocation(cancellationToken);
                }

                ServiceEventSource.Current.ServiceMessage(this.Context, "Restoration Folder Path " + backupFolder);

                RestoreDescription restoreRescription = new RestoreDescription(backupFolder, RestorePolicy.Force);

                await restoreCtx.RestoreAsync(restoreRescription, cancellationToken);

                ServiceEventSource.Current.ServiceMessage(this.Context, "Restore completed");

                DirectoryInfo tempRestoreDirectory = new DirectoryInfo(backupFolder);
                tempRestoreDirectory.Delete(true);

                return true;
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Restoration failed: " + "{0} {1}" + e.GetType() + e.Message);

                throw;
            }
        }
        private void SetupBackupManager()
        {
            string partitionId = this.Context.PartitionId.ToString("N");
            long minKey = ((Int64RangePartitionInformation)this.Partition.PartitionInfo).LowKey;
            long maxKey = ((Int64RangePartitionInformation)this.Partition.PartitionInfo).HighKey;

            if (this.Context.CodePackageActivationContext != null)
            {
                ICodePackageActivationContext codePackageContext = this.Context.CodePackageActivationContext;
                ConfigurationPackage configPackage = codePackageContext.GetConfigurationPackageObject("Config");
                ConfigurationSection configSection = configPackage.Settings.Sections["KeyPairStates.Settings"];

                string backupSettingValue = configSection.Parameters["BackupMode"].Value;

                if (string.Equals(backupSettingValue, "none", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.backupStorageType = BackupManagerType.None;
                }
                else if (string.Equals(backupSettingValue, "azure", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.backupStorageType = BackupManagerType.Azure;

                    ConfigurationSection azureBackupConfigSection = configPackage.Settings.Sections["KeyPairStates.BackupSettings.Azure"];

                    this.backupManager = new AzureBlobBackupManager(azureBackupConfigSection, partitionId, minKey, maxKey, codePackageContext.TempDirectory);
                }
                else if (string.Equals(backupSettingValue, "local", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.backupStorageType = BackupManagerType.Local;

                    ConfigurationSection localBackupConfigSection = configPackage.Settings.Sections["KeyPairStates.BackupSettings.Local"];

                    this.backupManager = new DiskBackupManager(localBackupConfigSection, partitionId, minKey, maxKey, codePackageContext.TempDirectory);
                }
                else
                {
                    throw new ArgumentException("Unknown backup type");
                }

                ServiceEventSource.Current.ServiceMessage(this.Context, "Backup Manager Set Up");
            }
        }
        private async Task<bool> BackupCallbackAsync(BackupInfo backupInfo, CancellationToken cancellationToken)
        {
            // ServiceEventSource.Current.ServiceMessage(this.Context, "Inside backup callback for replica {0}|{1}", this.Context.PartitionId, this.Context.ReplicaId);
            long totalBackupCount;

            IReliableDictionary<string, long> backupCountDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>(BackupCountDictionaryName);
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<long> value = await backupCountDictionary.TryGetValueAsync(tx, "backupCount");

                if (!value.HasValue)
                {
                    totalBackupCount = 0;
                }
                else
                {
                    totalBackupCount = value.Value;
                }

                await backupCountDictionary.SetAsync(tx, "backupCount", ++totalBackupCount);

                await tx.CommitAsync();
            }
            IReliableDictionary<Guid, Pairs> userKeyPairItems =
             await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, Pairs>>(KeyPairDictionaryName);
            userKeyPairItems.ToString();


            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<Guid, Pairs>> asyncEnumerable = await userKeyPairItems.CreateEnumerableAsync(tx);
                using (IAsyncEnumerator<KeyValuePair<Guid, Pairs>> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator())
                {
                    int i = 0;
                    while (await asyncEnumerator.MoveNextAsync(CancellationToken.None))
                    {
                        i++;

                        ServiceEventSource.Current.Message("Object {0}, Key : {1}, Value : {2}", i, asyncEnumerator.Current.Key.ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(asyncEnumerator.Current.Value.Items));
                    }
                }
            }
            //ServiceEventSource.Current.Message("Backup count dictionary updated, total backup count is {0}", totalBackupCount);

            try
            {
                //ServiceEventSource.Current.ServiceMessage(this.Context, "Archiving backup");
                await this.backupManager.ArchiveBackupAsync(backupInfo, cancellationToken);
                //ServiceEventSource.Current.ServiceMessage(this.Context, "Backup archived");
            }
            catch (Exception e)
            {
                // ServiceEventSource.Current.ServiceMessage(this.Context, "Archive of backup failed: Source: {0} Exception: {1}", backupInfo.Directory, e.Message);
            }

            await this.backupManager.DeleteBackupsAsync(cancellationToken);

            //ServiceEventSource.Current.Message("Backups deleted");

            return true;
        }
        private enum BackupManagerType
        {
            Azure,
            Local,
            None
        };

        #endregion

    }
}
