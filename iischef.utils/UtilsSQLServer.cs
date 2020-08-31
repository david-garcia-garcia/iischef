using iischef.logger;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;

namespace iischef.utils
{
    public class UtilsSqlServer
    {
        /// <summary>
        /// The logger service
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// Get an instance of UtilsSQLServer
        /// </summary>
        public UtilsSqlServer(ILoggerInterface logger)
        {
            this.Logger = logger;
        }

        public ServerConnection GetServerConnection(string connectionString)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            ServerConnection connection = new ServerConnection(conn);

            // Test the connection
            var s = this.FindServer(connection);

            if (s == null)
            {
                return null;
            }

            return connection;
        }

        /// <summary>
        /// Find a server using connection settings
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public Server FindServer(ServerConnection connection)
        {
            var instance = new Server(connection);

            try
            {
                var a = instance.Version.Major;
            }
            catch (Exception e)
            {
                this.Logger.LogException(e);
                return null;
            }

            return instance;
        }

        public void DeleteDatabase(ServerConnection connection, string databasename)
        {
            if (string.IsNullOrWhiteSpace(databasename))
            {
                return;
            }

            var instance = this.FindServer(connection);
            if (instance == null)
            {
                return;
            }

            Database database = null;
            foreach (Database d in instance.Databases)
            {
                if (d.Name == databasename)
                {
                    database = d;
                    break;
                }
            }

            // Creamos la base de datos
            if (database != null)
            {
                instance.KillDatabase(databasename);
            }

            return;
        }

        /// <summary>
        /// Find or create a database
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="databasename"></param>
        /// <param name="create"></param>
        /// <returns></returns>
        public Database FindDatabase(ServerConnection connection, string databasename, bool create)
        {
            var instance = this.FindServer(connection);

            if (instance == null)
            {
                return null;
            }

            Database database = this.FindDatabaseByNameInInstance(instance, databasename);

            // Creamos la base de datos
            if (database == null && create)
            {
                string fileName = Path.Combine(instance.DefaultFile, databasename + ".mdf");
                string oltpPath = Path.Combine(instance.DefaultFile, databasename + "_XTP_CHKPOINT");
                string logName = Path.Combine(instance.DefaultLog, databasename + ".ldf");

                var script = $@"
CREATE DATABASE [{databasename}]
ON PRIMARY(NAME = databasename_DATA, 
  FILENAME = '{fileName}'),
FILEGROUP databasename_XTP_FG CONTAINS MEMORY_OPTIMIZED_DATA
    (NAME = databasename_XTP_CHKPOINT, 
        FILENAME = '{oltpPath}')
LOG ON (NAME = databasename_LOG, 
                FILENAME='{logName}')";

                // Create the database
                connection.ExecuteNonQuery(script);

                // Default to simple recovery mode
                connection.ExecuteNonQuery($"ALTER DATABASE [{databasename}] SET RECOVERY SIMPLE;");

                database = this.FindDatabaseByNameInInstance(instance, databasename);
            }

            return database;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected Database FindDatabaseByNameInInstance(Server instance, string name)
        {
            instance.Refresh();
            instance.Databases.Refresh(true);

            foreach (Database d in instance.Databases)
            {
                if (d.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return d;
                }
            }

            return null;
        }

        /// <summary>
        /// Create or update a login
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="username"></param>
        /// <param name="create"></param>
        /// <returns></returns>
        public Login EnsureLoginWindows(ServerConnection connection, string username, bool create)
        {
            var instance = this.FindServer(connection);

            if (instance == null)
            {
                throw new Exception("SQL Server not found with connection: " + connection);
            }

            Login login = null;

            foreach (Login l in instance.Logins)
            {
                if (l.Name.Equals(username, StringComparison.CurrentCultureIgnoreCase))
                {
                    login = l;
                    break;
                }
            }

            if (login == null && create)
            {
                login = new Login(instance, username);
                login.LoginType = LoginType.WindowsUser;
                login.Create();
            }

            return login;
        }

        /// <summary>
        /// Create or update a login
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="create"></param>
        /// <returns></returns>
        public Login EnsureLoginSql(ServerConnection connection, string username, string password, bool create)
        {
            var instance = this.FindServer(connection);

            if (instance == null)
            {
                throw new Exception("SQL Server not found with connection: " + connection);
            }

            if (instance.LoginMode != ServerLoginMode.Mixed)
            {
                throw new Exception("Please, configure your SQL Server for Mixed Mode authentication: https://stackoverflow.com/questions/1393654/how-can-i-change-from-sql-server-windows-mode-to-mixed-mode-sql-server-2008");
            }

            Login login = null;

            foreach (Login l in instance.Logins)
            {
                if (l.Name == username)
                {
                    login = l;
                    break;
                }
            }

            if (login == null && create)
            {
                login = new Login(instance, username);
                login.LoginType = LoginType.SqlLogin;
                login.PasswordExpirationEnabled = false;
                login.PasswordPolicyEnforced = false;
                login.Create(password);
            }
            else
            {
                if (login != null)
                {
                    login.ChangePassword(password);
                }
            }

            return login;
        }

        public void DeleteLogin(ServerConnection connection, string username)
        {
            var instance = this.FindServer(connection);

            Login login = null;

            foreach (Login l in instance.Logins)
            {
                if (l.Name == username)
                {
                    login = l;
                    break;
                }
            }

            login?.Drop();

            if (login == null)
            {
                this.Logger.LogInfo(true, "Could not drop sql user '{0}' because it was not found.", username);
            }
        }

        /// <summary>
        /// Find or create a user
        /// </summary>
        /// <param name="database"></param>
        /// <param name="login"></param>
        /// <param name="create"></param>
        /// <returns></returns>
        public User BindUser(Database database, Login login, bool create)
        {
            User result = null;
            bool mapped = false;

            foreach (User user in database.Users)
            {
                if (login.Name.Equals(user.Name, StringComparison.CurrentCultureIgnoreCase)
                    && (user.LoginType == LoginType.SqlLogin || user.LoginType == LoginType.WindowsUser))
                {
                    result = user;
                    mapped = !string.IsNullOrWhiteSpace(user.Login);
                    break;
                }
            }

            // If the user is not mapped... delete and recreate, it probably
            // comes from an external backup or similar.
            // A null value here represents an empty mapping.
            if (!mapped && result != null)
            {
                // Big chance this is an invalid user (i.e. a restore
                // between servers or similar) so we drop
                // and recreate.
                this.Logger.LogInfo(true, "Dropping sql user '{0}' as it has no bound Login.", result.Name);
                result.Drop();
                result = null;
            }

            if (result == null && create)
            {
                result = new User(database, login.Name);
                result.Login = login.Name;
                result.Name = login.Name;
                result.DefaultSchema = "dbo";
                result.Create();
            }

            List<string> roles = new List<string>() 
            {
                 "db_datawriter",
                 "db_datareader",
                 "db_ddladmin",
                 
                 // TODO: Find how to deal with the exec permission issue...
                 "db_owner"
            };

            // Add the roles!!
            foreach (var r in roles)
            {
                result.AddToRole(r);
            }

            return result;
        }
    }
}
