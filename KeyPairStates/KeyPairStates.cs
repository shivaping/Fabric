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

        public async Task<UserKeyPair> GetPairs(Guid UserID)
        {
            IReliableDictionary<Guid, UserKeyPair> userKeyPairItems =
               await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserKeyPair>>(KeyPairDictionaryName);

            ConditionalValue<UserKeyPair> userKeyPair;
            List<PairsPair> PairsPair1 = new List<KeyPair.Model.PairsPair>() { new PairsPair() { PairKey = "First", PairValue = "FirstValue", UpdateDate = DateTime.Now, UserInventoryId = 1 } };
            List<PairsPair> PairsPair2 = new List<KeyPair.Model.PairsPair>() { new PairsPair() { PairKey = "Second", PairValue = "SecondValue", UpdateDate = DateTime.Now, UserInventoryId = 1 } };
            using (ITransaction tx = this.StateManager.CreateTransaction())
            {
                userKeyPair = await userKeyPairItems.TryGetValueAsync(tx, UserID);
                //Not in state get from SQL
                if (!userKeyPair.HasValue)
                    userKeyPair = new ConditionalValue<UserKeyPair>(true, new UserKeyPair()
                    {
                        Pairs = new List<Pairs>()
                        {
                            new Pairs()
                            {
                                Items = PairsPair1.ToArray(), PairID = 1
                            },
                              new Pairs()
                            {
                                Items = PairsPair2.ToArray(), PairID = 2
                            }
                        }
                    });

                if (userKeyPair.HasValue)
                {
                    //await userKeyPairItems.AddAsync(tx, UserID, userKeyPair);
                    await userKeyPairItems.SetAsync(tx, UserID, userKeyPair.Value);
                    await tx.CommitAsync();
                    ServiceEventSource.Current.ServiceMessage(this.Context, "Created userPair item: {0}", userKeyPair.Value);
                }
            }

            return userKeyPair.Value;
        }

        public Task<string> helloworld()
        {
            return Task.FromResult("Hello!");
        }
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

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "inside RunAsync for Inventory Service");
                return Task.WhenAll(new List<Task>() { this.PeriodicTakeBackupAsync(cancellationToken) });
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "RunAsync Failed, {0}", e);
                throw;
            }

        }
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
            ServiceEventSource.Current.ServiceMessage(this.Context, "Inside backup callback for replica {0}|{1}", this.Context.PartitionId, this.Context.ReplicaId);
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

            ServiceEventSource.Current.Message("Backup count dictionary updated, total backup count is {0}", totalBackupCount);

            try
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Archiving backup");
                await this.backupManager.ArchiveBackupAsync(backupInfo, cancellationToken);
                ServiceEventSource.Current.ServiceMessage(this.Context, "Backup archived");
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceMessage(this.Context, "Archive of backup failed: Source: {0} Exception: {1}", backupInfo.Directory, e.Message);
            }

            await this.backupManager.DeleteBackupsAsync(cancellationToken);

            ServiceEventSource.Current.Message("Backups deleted");

            return true;
        }
        private enum BackupManagerType
        {
            Azure,
            Local,
            None
        };
    }
}
