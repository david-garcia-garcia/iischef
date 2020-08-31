using iischef.core.SystemConfiguration;
using iischef.utils;
using Microsoft.SqlServer.Management.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using smo = Microsoft.SqlServer.Management.Smo;

namespace iischef.core.Services
{
    /// <summary>
    /// 
    /// </summary>
    public class SQLService : DeployerBase, IDeployerInterface
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected SQLServer GetSqlServer(string serviceId)
        {
            // We can have ad-hoc settings for the SQL Connection in the application template itself
            if (this.Deployment.installedApplicationSettings.configuration["sqlservice_" + serviceId] != null)
            {
                this.Logger.LogInfo(true, "Using ad-hoc sql server settings set at the application level");
                SQLServer server = this.Deployment.installedApplicationSettings.configuration["sqlservice_" + serviceId].ToObject<SQLServer>();

                try
                {
                    // Instead of specyfing a real connection string, we can simply default to a global target
                    var targetServer = this.GlobalSettings.GetSqlServer(server.connectionString);
                    server.connectionString = targetServer.connectionString;
                }
                catch
                {
                    // ignored
                }

                return server;
            }

            // We can have an app_setting configuration
            // to route a whole application to a specific sql server
            string sqlTarget = null;

            if (this.Deployment.installedApplicationSettings.configuration["sqltarget"] != null)
            {
                sqlTarget = Convert.ToString(this.Deployment.installedApplicationSettings.configuration["sqltarget"]);
                this.Logger.LogInfo(true, "SQL Target overriden in local application settings to " + sqlTarget);
            }

            var sqlServer = this.GlobalSettings.GetSqlServer(sqlTarget);
            this.Logger.LogInfo(true, "SQL Target: " + sqlServer.id);

            return sqlServer;
        }

        /// <summary>
        /// 
        /// </summary>
        public void deploy()
        {
            var sqlSettings = this.DeployerSettings.castTo<SQLServiceSettings>();

            if (string.IsNullOrWhiteSpace(sqlSettings.id))
            {
                throw new Exception("SQL Service request id must have a value.");
            }

            // Figure out what SQLServer settings to use for this instance
            var sqlServer = this.GetSqlServer(sqlSettings.id);

            var id = this.Deployment.installedApplicationSettings.GetId() + "_" + sqlSettings.id;

            // Keys to store username, password and databasename
            string keylogin = $"services.{sqlSettings.id}.username";
            string keypassword = $"services.{sqlSettings.id}.password";
            string keydatabase = $"services.{sqlSettings.id}.database";

            // Parse the connection string
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(sqlServer.connectionString);

            // Make sure we can connect to the server
            var utils = new UtilsSqlServer(this.Logger);
            this.Logger.LogInfo(true, "Getting SQL connection '{0}'", sqlServer.connectionString);
            var connection = utils.GetServerConnection(sqlServer.connectionString);

            if (connection == null)
            {
                throw new Exception("Could not connect to the server: " + sqlServer.connectionString);
            }

            // The actual database name that will be used
            string databaseName;

            if (string.IsNullOrWhiteSpace(sqlServer.databaseName))
            {
                databaseName = "chf_" + id;
            }
            else
            {
                databaseName = sqlServer.databaseName;
            }

            this.Deployment.SetRuntimeSetting(keydatabase, databaseName);

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new Exception("Database name cannot be empty or null.");
            }

            // Ensure we have database and login.
            this.Logger.LogInfo(true, "Getting SQL database '{0}'", databaseName);
            var database = utils.FindDatabase(connection, databaseName, true);

            if (database == null)
            {
                throw new Exception("Could not  find database " + databaseName);
            }

            if (!database.Status.HasFlag(smo.DatabaseStatus.Normal))
            {
                throw new Exception("Database should be in 'Normal' status. The current database status is not compatible with automated deployments: " +
                                    database.Status);
            }

            // The database name, username and password must remain the same between deployments.
            // If we generated new user/pwd for new deployment, rollback functionality would NOT work as expected
            // as it would require re-deploying the logins.
            string dbLogin;
            string dbPassword;

            // If this is a passthrough authentication, propagate credentials as-is
            if (sqlServer.passThroughAuth)
            {
                dbLogin = builder.UserID;
                dbPassword = builder.Password;
            }
            else
            {
                dbLogin = "chf_" + id;
                dbPassword = this.Deployment.GetWindowsPassword();

                // This happens always, wether or not we have windows auth.
                this.Logger.LogInfo(true, "Adding SQL Login user '{0}' to database", dbLogin);
                smo.Login login = utils.EnsureLoginSql(connection, dbLogin, dbPassword, true);
                utils.BindUser(database, login, true);
            }

            this.Deployment.SetRuntimeSetting(keylogin, dbLogin);
            this.Deployment.SetRuntimeSetting(keypassword, dbPassword);

            // Create the database login, although we support windows auth,
            // the recommendation is to use SQL AUTH for portability reasons
            if (sqlServer.useWindowsAuth)
            {
                string sqlWindowsUserName = this.Deployment.WindowsUsernameFqdn(true);
                this.Logger.LogInfo(true, "Adding Windows Login user '{0}' to database", sqlWindowsUserName);

                // Depending on the setup this might fail, i.e. we are using a non-domain setup for chef (so the
                // application users are local and the server is in a domain).
                try
                {
                    smo.Login loginw = utils.EnsureLoginWindows(connection, sqlWindowsUserName, true);
                    utils.BindUser(database, loginw, true);
                }
                catch (Exception e)
                {
                    // 15401: "the domain controller for the domain where the login resides (the same or a different domain) is not available for some reason"
                    if ((e.InnerException?.InnerException as SqlException)?.Number != 15401)
                    {
                        throw;
                    }

                    this.Logger.LogError("Cannot add Windows login '{0}' to MSSQL Server '{1}'. This can happen if MSSQL and the local machine do not reside in the same domain.", sqlWindowsUserName, sqlServer.connectionString);
                }
            }

            this.Deployment.SetRuntimeSetting($"services.{sqlSettings.id}.host", builder.DataSource);

            // Build a connection string that the end user can handle
            SqlConnectionStringBuilder clientBuilder = new SqlConnectionStringBuilder();
            clientBuilder.UserID = dbLogin;
            clientBuilder.Password = dbPassword;
            clientBuilder.DataSource = builder.DataSource;
            clientBuilder.InitialCatalog = databaseName;
            this.Deployment.SetRuntimeSetting($"services.{sqlSettings.id}.connectionString", clientBuilder.ConnectionString);
            string preferredConnectionString = clientBuilder.ConnectionString;

            if (sqlServer.useWindowsAuth)
            {
                // Alternative connection string - integrated
                SqlConnectionStringBuilder clientBuilderWindowsAuth = new SqlConnectionStringBuilder();
                clientBuilderWindowsAuth.DataSource = builder.DataSource;
                clientBuilderWindowsAuth.IntegratedSecurity = true;
                clientBuilderWindowsAuth.InitialCatalog = databaseName;
                this.Deployment.SetRuntimeSetting($"services.{sqlSettings.id}.connectionStringWindowsAuth", clientBuilderWindowsAuth.ConnectionString);
                preferredConnectionString = clientBuilderWindowsAuth.ConnectionString;
            }

            this.Deployment.SetRuntimeSetting($"services.{sqlSettings.id}.connectionStringPreferred", preferredConnectionString);

            if (!string.IsNullOrWhiteSpace(sqlSettings.customScript))
            {
                using (var clientConnection = new SqlConnection(preferredConnectionString))
                {
                    clientConnection.Open();
                    var clientCommand = new SqlCommand(sqlSettings.customScript, clientConnection);
                    clientCommand.ExecuteNonQuery();
                }
            }
        }

        public void undeploy(bool isUninstall = false)
        {
            if (!isUninstall)
            {
                return;
            }

            var sqlSettings = this.DeployerSettings.castTo<SQLServiceSettings>();
            var sqlServer = this.GetSqlServer(sqlSettings.id);

            string keylogin = $"services.{sqlSettings.id}.username";
            string keydatabase = $"services.{sqlSettings.id}.database";

            // The database name, username and password must remain the same between deployments.
            // If we generated new user/pwd for new deployment, rollback functionality would NOT work as expected
            // as it would require re-deploying the logins.
            var dbLogin = this.Deployment.GetRuntimeSetting(keylogin, null);
            var dbDatabase = this.Deployment.GetRuntimeSetting(keydatabase, null);

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(sqlServer.connectionString);

            var utils = new UtilsSqlServer(this.Logger);

            var connection = utils.GetServerConnection(sqlServer.connectionString);

            if (connection != null)
            {
                // Only remove if the database was autogenerated
                if (sqlServer.databaseName.IsNullOrDefault())
                {
                    utils.DeleteDatabase(connection, dbDatabase);
                }

                // Only remove if the login is not the same as the one in the master connection
                if (builder.UserID != dbLogin)
                {
                    utils.DeleteLogin(connection, dbLogin);
                }

                utils.DeleteLogin(connection, this.Deployment.WindowsUsernameFqdn(true));
            }
        }

        public void start()
        {
        }

        public void stop()
        {
        }

        public void deploySettings(
            string jsonSettings,
            string jsonSettingsNested,
            RuntimeSettingsReplacer replacer)
        {
        }

        public void sync()
        {
            base.syncCommon<SQLService>();
        }

        public override void _sync(object input)
        {
            var sqlSettings = this.DeployerSettings.castTo<SQLServiceSettings>();

            SQLService parent = (SQLService)input;

            string database = this.Deployment.GetRuntimeSettingsToDeploy()["services." + this.DeployerSettings.castTo<SQLServiceSettings>().id + ".database"];
            string parentDatabase = parent.Deployment.GetRuntimeSettingsToDeploy()["services." + parent.DeployerSettings.castTo<SQLServiceSettings>().id + ".database"];

            var sqlServer = this.GetSqlServer(sqlSettings.id);

            using (SqlConnection connection = new SqlConnection(sqlServer.connectionString))
            {
                connection.Open();

                ServerConnection serv = new ServerConnection(connection);

                smo.Server serverTemp = new smo.Server(serv);

                string backupDir = serverTemp.BackupDirectory;
                string dataDir = serverTemp.MasterDBPath;

                this.Logger.LogInfo(true, "SQL Server Version: " + serverTemp.VersionString);
                this.Logger.LogInfo(true, "SQL Server Edition: " + serverTemp.Edition);

                var backupName = database + DateTime.Now.ToString("yyyyMMddHHmmssffff");
                var backupFile = UtilsSystem.CombinePaths(backupDir, backupName + ".bak");

                SqlCommand cmd;

                // Timeout for long running processes (i.e. backup and restore)
                int longProcessTimeout = 120;

                try
                {
                    string query = null;

                    // EngineEdition Database Engine edition of the instance of SQL Server installed on the server.
                    //  1 = Personal or Desktop Engine(Not available in SQL Server 2005 and later versions.)
                    //  2 = Standard(This is returned for Standard, Web, and Business Intelligence.)
                    //  3 = Enterprise(This is returned for Evaluation, Developer, and both Enterprise editions.)
                    //  4 = Express(This is returned for Express, Express with Tools and Express with Advanced Services)
                    //  5 = SQL Database
                    //  6 - SQL Data Warehouse

                    bool supportsCompression = serverTemp.EngineEdition != smo.Edition.Express;

                    string compressionOption = supportsCompression ? "COMPRESSION," : string.Empty;

                    query = string.Format(
                        "BACKUP DATABASE [{0}] to disk = '{1}' WITH {3} name = '{2}'",
                        parentDatabase,
                        backupFile,
                        backupName,
                        compressionOption);

                    this.Logger.LogInfo(true, "CMD: {0}", query);
                    cmd = new SqlCommand(query, connection);
                    cmd.CommandTimeout = longProcessTimeout;
                    cmd.ExecuteNonQuery();

                    query = string.Format(
                        @"DECLARE @kill varchar(8000) = '';  
                        SELECT @kill = @kill + 'kill ' + CONVERT(varchar(5), session_id) + ';'  
                        FROM sys.dm_exec_sessions
                        WHERE database_id  = db_id('{0}')",
                        database);

                    this.Logger.LogInfo(true, "CMD: {0}", query);
                    cmd = new SqlCommand(query, connection);
                    cmd.ExecuteNonQuery();

                    query = string.Format("ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", database);

                    this.Logger.LogInfo(true, "CMD: {0}", query);
                    cmd = new SqlCommand(query, connection);
                    cmd.ExecuteNonQuery();

                    query = string.Format("RESTORE filelistonly FROM disk='{0}'", backupFile);

                    // Before restoring, print out logical file names
                    cmd = new SqlCommand(query, connection);
                    Dictionary<string, string> info = new Dictionary<string, string>();
                    using (var reader = cmd.ExecuteReader())
                    {
                        int row = 0;

                        while (reader.Read())
                        {
                            for (var i = 0; i < reader.FieldCount; i++)
                            {
                                var columname = reader.GetName(i);
                                var columnvalue = Convert.ToString(reader[i]);
                                info.Add(row.ToString() + "_" + columname, columnvalue);
                            }

                            row++;
                        }
                    }

                    // logger.LogInfo(true, "BACKUP DETAILS: {0}", Newtonsoft.Json.JsonConvert.SerializeObject(info, Newtonsoft.Json.Formatting.Indented));

                    Dictionary<string, string> dataFilesToMove = new Dictionary<string, string>();

                    for (int x = 0; x < 500; x++)
                    {
                        if (!info.ContainsKey($"{x}_LogicalName"))
                        {
                            break;
                        }

                        var logicalName = info[$"{x}_LogicalName"];
                        var physicalName = info[$"{x}_PhysicalName"];
                        var fileName = System.IO.Path.GetFileName(physicalName);

                        dataFilesToMove.Add(logicalName, $"{dataDir}\\2_{fileName}");
                    }

                    List<string> fileMoves = new List<string>();

                    foreach (var dataFile in dataFilesToMove)
                    {
                        fileMoves.Add($"MOVE '{dataFile.Key}' TO '{dataFile.Value}'");
                    }

                    query = string.Format($"RESTORE DATABASE [{database}] FROM DISK = '{backupFile}' WITH REPLACE, {string.Join(",", fileMoves)};");

                    this.Logger.LogInfo(true, "CMD: {0}", query);
                    cmd = new SqlCommand(query, connection);
                    cmd.CommandTimeout = longProcessTimeout;
                    cmd.ExecuteNonQuery();

                    query = $"ALTER DATABASE [{database}] SET MULTI_USER";

                    this.Logger.LogInfo(true, "CMD: {0}", query);
                    cmd = new SqlCommand(query, connection);
                    cmd.ExecuteNonQuery();

                    serverTemp.ConnectionContext.ExecuteNonQuery("EXEC sp_configure 'show advanced options', 1");
                    serverTemp.ConnectionContext.ExecuteNonQuery("RECONFIGURE");
                    serverTemp.ConnectionContext.ExecuteNonQuery("EXEC sp_configure 'xp_cmdshell', 1");
                    serverTemp.ConnectionContext.ExecuteNonQuery("RECONFIGURE");
                    serverTemp.ConnectionContext.ExecuteNonQuery($"xp_cmdshell 'del \"{backupFile}\"'");
                    serverTemp.ConnectionContext.ExecuteNonQuery("EXEC sp_configure 'xp_cmdshell', 0");
                    serverTemp.ConnectionContext.ExecuteNonQuery("EXEC sp_configure 'show advanced options', 0");
                    serverTemp.ConnectionContext.ExecuteNonQuery("RECONFIGURE");
                }
                finally
                {
                    // Make sure that we remove single_user_mode
                    try
                    {
                        string query = null;

                        query = $"ALTER DATABASE [{database}] SET MULTI_USER";
                        cmd = new SqlCommand(query, connection);
                        cmd.ExecuteNonQuery();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                connection.Close();
            }

            // After doing the SYNC we need to "re-deploy" the database so that
            // user accounts are properly setup for the new application.
            this.deploy();
        }
    }
}
