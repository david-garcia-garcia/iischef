using System.Collections;
using iischef.logger;
using iischef.utils;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace iischef.cmdlet
{
    /// <summary>
    /// Upsert environment variables for pool.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "IISChefPoolEnvUpsert")]
    public class IISChefPoolEnvUpsert : ChefCmdletBase
    {
        /// <summary>
        /// 
        /// </summary>
        [Parameter]
        public SwitchParameter VerboseOut { get; set; }

        /// <summary>
        /// The certificate store location
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public string Pool { get; set; }

        /// <summary>
        /// New password for certificate private key.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, Mandatory = true)]
        public Hashtable Env { get; set; }

        /// <summary>
        /// 
        /// </summary>
        protected override void DoProcessRecord(ILoggerInterface logger)
        {
            logger.SetVerbose(this.VerboseOut.IsPresent);
            UtilsIis.UpsertPoolEnv(this.Pool, HashtableToDictionary<string, string>(this.Env));
        }

        public static Dictionary<T, T2> HashtableToDictionary<T, T2>(Hashtable table)
        {
            return table
                .Cast<DictionaryEntry>()
                .ToDictionary(kvp => (T)kvp.Key, kvp => (T2)kvp.Value);
        }
    }
}
