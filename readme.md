[TOC]

# Windows Chef

## What it is

Windows chef is a set of tools that can be run either through command line (powershell cmdlet) or as a windows service that automates the process of deploying applications based on descriptive yml files that are part of the application itself.

Think of this of a cheap self-hosted version of the "infrastructure as code" trend, but it only deals with infrastructure configuration at the application level, not with setting up the infrastructure itself.

You can use this tool to automate application deployment in production environments as well as local development to ensure that the application is deployed in exactly the same way in both places.

It is designed to deploy applications in high density multi-tenant (shared) resources with as much isolation as possible. So you can host as many applications as you want on a single web server sharing the same IIS, SQL Server and other resources.

The tool can also be used to automate modern devops practices such as "environment-per-branch".


## Is this tool helpful now that Windows Containers are a deployment trend?

Sure. This tool can automate the process of deploying your application inside the container and setting up any required configuration.

## How to get it

You can build the project yourself.

## Using the console

### Quick Start

To automatically setup the basic folder structure and files for chef use the following command:

```bash
Invoke-ChefAppSelfInstall "D:\PathToChef\"
```

This creates the following basic folder structure in the specified path:

* **_apps**: contains the environments and source for the installed applications
* **_artifact_temp**: temporary path for artifact manipulation, make sure that this directory is in the same drive as the final location for your _apps.
* **_configuration**: directory for chef's configuration files.
* **_configuration/config.json**: main configuration file for Chef (Server Settings)
* **_configuration/installed_apps**: storage for installed applications in current system
* **_configuration/deployments**: storage for active configuration for the installed applications
* **_contents**: storage for you application's contents
* **_log**: storage for application log
* **_temp**: storage for application's temporary files

On a production system you want to:

* Put the contents, log and temp folder in different logical/physical drives.
* Ensure a full and frequent backup of the _configuration directory, which contains information of all the currently deployed applications

To modify the location of any of this directories

### The environment file

Chef reads all it's configuration from a single file containing the "Server Settings".

The path to this configuration file is stored - by convention - in the following file:

```bash
C:\ProgramData\iischef\config\environment-file-path
```

This is a plain text file that contains the path to the server-settings.json file.

Server settings is a json formatted file that describes your system. You can see an example/reference implementation in:

iischef.tests/samples/server-settings.json

You can place this file any place you like in your system.

To globally install this configuration file, use the following command:

`Set-ChefEnvironmentFile c:\chef\server-settings.json`

### Adding deployment information to your application

The next step is to add to your application information regarding "what" services it needs to run. I.e. to run a Drupal site you will need a database backend, temporary, permanent and private disk storage, a caching backend such as couchbase, an site in iis, etc..

To describe this setup you create a folder in your artifact's root called "chef". Inside this folder you can place several configuration files that will be applied by chef depending on the environment, the files in this directory are combined and aggregated into a final 
configuration file according to the following rules:

* Only files that match the following pattern are considered as settings: chef*.yml
* The [scope-environment-regex, scope-branch-regex] are checked against current environment, they all must match for the configuration file to be considedred. (Note that they default to .* that means match all)
* Configuration files are sorted according to the scope-weight attribute
* Configuration files are merged into a single chef.yml file, that will be used to configure the currrent deployment.

There is a full reference sample file here:

[Chef Reference Application File](iischef.core/sample.yml?at=1.x&fileviewer=file-view-default)

### Installing an application

To deploy an application on a system you "install" the application, that is, provide the necessary details to chef so that it can obtain the required artifacts for deployment.

The way to deploy an application is to create an application definition YAML file, drop it into the installed application's directory:

```
_configuration/installed_apps/myapplication.yaml
```

And install it using the command line:

```
Invoke-ChefAppRedeploy -FromTemplate myapplication
```

Example for an application using a local deployer (local path):

```yml
id: 'php-test'
mount_strategy: '%MOUNTSTRATEGY%'
# Type of downloader/monitor to use,
# currently only appveyor
downloader:
# Currently only "appveyor" and "localpath" supported
 type: 'localpath'
 path: '%PATH%'
runtime_overrides:
 'deployment.custom.setting': 'my_custom_setting'
 'services.contents.mount.files.path': 'c:\\missingdir'
```

Example for an application using Appveyor to produce artifacts:

```yaml
id: 'sabentisplus'
# Type of downloader/monitor to use,
# currently only appveyor
downloader:
# Currently only "appveyor" and "localpath" supported
 type: 'appveyor'
 project: 'zzzz'
 username: 'yyyy'
 apitoken: 'xxxx'
 branch: 'trunk'
 publish_regex_filter: ''
```

Depending on the type of downloader, you might need to specify additional information. I.e. for the AppVeyor downloader you must provide a branch (plus the security credentials).

To re-deploy an application you can use:

```bash
Invoke-ChefAppRedeploy myapplicationname
```

Depending on the type of download Redeploy will have a different behavior. I.e in an AppVeyor downloader this will download the most recent successful build.

#### Deployment shortcuts

There are several shortcuts for application deployment.

To install an application from a path you can use the Invoke-ChefAppDeployPath command:

```bash
Invoke-ChefAppDeployPath "c:\applications\mycode" myapplicationname
```

Once installed, Chef internally stores the information of "where to grab" the artifact from and you can directly use other commands to interact with your application.

The previous example is a shortcut to deploy applications from local paths (mostly used in development environments).

You can also use local Zip files:

```bash
Invoke-ChefAppDeployZip "c:\productionartifacts\app0\version1.0.zip" myapplicationname
```

### Deploying a specific version of an application and doing rollbacks

If a faulty application has passed your CI and QA processes and was deployed, yet you need to rollback to a previous version Chef has the tools needed to do so.

By default when calling Invoke-ChefAppRedeploy the downloader will look for the latest available succesful build (if the downloader has this capability, currently only AppVeyorDownloader).

You can specify a specific BuildId:

```bash
Invoke-ChefAppRedeploy myaplicationid -Force -BuildId "1.0.233"
```

Each deployer has it's own interpration of what a BuildId is, i.e. for the LocalZip downloader you can use:

```bash
Invoke-ChefAppRedeploy myaplicationid -Force -BuildId "c:\productionartifacts\app0\version1.1.zip"
```

Once you have forced a deployment with a specific buildId, *only doing specific buildId deployments with the -Force option will work*. That means that automatic deployments will be locked until you release the verson constraint.

To release this constraint specify the literal "latest" as the buildId:

```bash
Invoke-ChefAppRedeploy myaplicationid -Force -BuildId "latest"
```

After doing so, automatic deployments to latest successful builds will be restored.

You must understand that going back to previous builds of an application can completely break it (i.e. a newer version alters a database schema and the old version is not prepared to deal with it).

### Managing applications

To see a list of deployed application in your system use:

```bash
Invoke-ChefAppFind
```

This will list (and also provide a powershell array) all the applications installed.

You can trigger redeployment of a single application like this:

```bash
Invoke-ChefAppRedeploy myapplication
```

Note that downloaders (such as Appveyor) will NOT redeploy an application unless a new artifact is available.

To force deployment use:

```bash
Invoke-ChefAppRedeploy myapplication -Force
```

If you don't specify an application name, the *Invoke-ChefAppRedeploy* command attempt to redeploy all installed applications.

### Uninstalling an application

To remove an application from the system, use the following command:

```powershell
Invoke-ChefAppRemove myapplicationname
```

Note that removing an application will **DELETE** any resources allocated to it, including
database, disk storage and others and should be used with caution in production systems.

## Installing the windows service

The Chef core is designed to work as a windows service that will attempt to redeploy all installed applications periodically.

To install the service download an extract the binaries to a location in your computer,
and run the installer:

```bash
iischef.service.exe --install
```

you can remove the service at any time using:

```bash
iischef.service.exe --uninstall
```

Service startup issues will be logged to system as an Application Event type with name "ChefApp".

The service executable has other helpful commands:

```bash
iischef.service.exe --test // start and stop the service
iischef.service.exe --run // run the service while in the console, output is redirected to the console in full verbose mode
```

## Merging Chef configuration files or handling settings per environment

In the same repository you can have multiple chef configuration files, and have them combined or applied depending on the deployment environment or branch.

At the application level you can limit the scope of a chef file using regular expressions, use these options at the root of your configuration file:

```yaml
scope-branch-regex: '.*'
scope-environment-regex: '.*'
scope-tags: 'mytag1, mytag2, mytag3'
scope-weight: 0
inherit: default
```

Internally what the Chef deployer will do is scan all chef configuration files, grab all the ones that either match the current environment or the current branch, sort them by scope weight
and the merge them using an additive strategy.

In example, to add debugging support to a PHP development environment we simply need to add a few extra lines to an environment specific chef file:

```yaml
# Only use this for environment with name "local"
scope-environment-regex: 'local.*'
inherit: default
deployers:
  php:
    runtime:
      - {type: 'ini', multivalue: true, 'key':zend_extension, 'value':php_xdebug.dll}
      - {type: 'ini','key':opcache.revalidate_freq , 'value': 5}
      - {type: 'ini','key':opcache.validate_timestamps , 'value': On}
```

The available configuration options are:

* **scope-branch-regex (Default to all):** What branches this configuration is applied to.
* **scope-environment-regex (Default to all):** What environments this configuration is applied to. An environment is the ID of a Chef Installation on a server set on the global chef configuration file.
* **scope-tags (Leave empty to not match): ** When deploying environments, these can have "tags" that can be matched here with a comma delimited list. All tags must exist in the environment for the criteria to  match. Tags are declared in the InstalledApplication settings. The default available tags are:
  * **app-id-{applicationid}:** The unique ID of the application
* **scope-weight:** Allows you to control what order are the chef settings files merged
* **inherit:** Let's you break the inheritance behavior, letting a specific chef configuration file override any other less specific configuration. Currently only supports the "break" keyword. When used, any higher level configuration will be dismissed (use scope-weight to control levels).

**NOTES:**

* There is no way to "clear" parent configurations partially.
* All scope options (scope-branch-regex, scope-environment, scope-tags) are additive. If one of them does not match the whole of the configuration file will not be considered for the environment.

## Application settings and environment settings

Once your application is deployed, it needs to know "where" and "how" to access the requested services. I.e. if you requested an SQL Server database, you need to know the
credentials, host and other relevant information to make the connection.

Besides the runtime settings defined by chef itself (to represent the information related to the requested environment services) you can add
any relevant information for your application (AKA application settings) in the "app_settings" attribute:

```yaml
app_settings:
  mail:
    host: 'mail.google.com'
    username: 'username'
    pwd: 'password'
```

**IMPORTANT**: By using Chef's inheritance and configuration combination rules you can use this to have different configuration
files per environment, branch or other criteria that chef supports.

All this information is stored in a JSON key-value pair file that can be accessed in several ways at application runtime.

A file is written to the web root with the name "chef-runtime.path". This is a plain text file that points to a JSON file on the file system containing 
the deployment settings, such as:

```json
{
  "environment.enable_mail_verification": "true",
  "environment.mail_send_enable": "true",
  "environment.mail_send_redirect": null,
  "iis.local.bindings.default.url": "http://xxx",
  "iis.public.bindings.default.url": "http://xxx",
  "iis.cdn.bindings.cdn.cdn.url": "http://xxx",
  "iis.cdnssl.bindings.cdn.cdnssl.url": "http://xxx",
  "iis.local.bindings.cdn.local.url": "http://xx",
  "deployment.shortId": "chf_upcplus_sfk",
  "deployment.appPath": "D:\\_Webs\\chf_upcplus_sfk\\app",
  "deployment.logPath": "E:\\ChefApplicationData\\Logs\\upcplus",
  "deployment.tempPath": "E:\\ChefApplicationData\\Temporary\\upcplus",
  "installedApp.id": "upcplus",
  "installedApp.branch": null,
  "services.disk.contents.mount.files.path": "E:\\ChefApplicationData\\Contents\\store_upcplus\\files",
  "services.disk.contents.mount.private.path": "E:\\ChefApplicationData\\Contents\\store_upcplus\\private",
  "services.disk.contents.mount.temp.path": "E:\\ChefApplicationData\\Contents\\store_upcplus\\temporary",
  "services.sqlsrv.default.username": "chf_upcplus_default",
  "services.sqlsrv.default.password": "5e603bd3-f4e6-4fac-9734-0d0fa2cb4576",
  "services.sqlsrv.default.database": "chf_upcplus_default",
  "services.sqlsrv.default.host": "192.168.3.2",
  "services.couchbase.default.uri": "couchbase://192.168.3.2",
  "services.couchbase.default.bucket-name": "couch",
  "services.couchbase.default.bucket-password": "couch"
}
```

You can parse this file from within your application and use the information as needed.

### Dump configuration files to a specific location

Use the configuration_dump_paths attribute to define a list of locations where the full runtime configuration files
will be copied to relative to the source path of the artifact.

```yaml
configuration_dump_paths:
  path0: 'ETG.SABENTISpro.IIS\Settings'
```

The above example will generate two JSON files:

* ETG.SABENTISpro.IIS\Settings\chef-settings.json
* ETG.SABENTISpro.IIS\Settings\chef-settings-nested.json

They both contain exactly the same configuration, but structured diferently (key-value pairs vs nested json objects).

### Replacements in regular configurations files

If you have an application that already has configuration files, you can instruct chef to perform string based replacements when deploying. The available replacements are the ones that are written to the chef-configuration.json file as stated in the previous section.

You can for example have a config.inc.template.php file in your project such as:

```ini
;;;;;;;;;;;;;;;;;;;;;
; Database Settings ;
;;;;;;;;;;;;;;;;;;;;;

[database]

driver = mysql
host = "{@orpjournal.database.host@}"
username = "{@orpjournal.database.username@}"
password = "{@orpjournal.database.password@}"
name = "{@orpjournal.database.database@}"
```

Then on the your chef settings file instruct the app deployer to perform the replacements and rename the file:

```yaml
- type: 'app'
  configuration_replacement_files:
   'config.inc.template.php': 'config.inc.php'
```

This will load the contents of "/config.inc.template.php", replace any matching values from the runtime settings and save the resulting file to "/config.inc.php"

You can filter the replaced string before being rendered:

```typescript
// Escapes the value of orpjournal.database.database to be used inside a JSON literal
name = "{@orpjournal.database.database|filter:jsonescape@}"

// Escapes the value of orpjournal.database.database to be used inside an XML literal
name = "{@orpjournal.database.database|filter:xmlescape@}"

// Replaces all backwards slashes by forward slashes
name = "{@orpjournal.database.database|filter:allforward@}"

// Trims whitespace, backslash and forwardslash from the begginng and end of the string
name = "{@orpjournal.database.database|filter:trimpath@}"
```

You can insert any subset of the configuration as a json serialized string using an XPath selector, by prepending "!" to the key name:

```typescript
// Writes the full configuration file, with the "$" xpath selector
fullJsonConfiguration = "{@!$|filter:jsonescape@}"

// Writes the subset of configuration result of selecting "$.cdn"
fullJsonConfiguration = "{@!$.cdn|filter:jsonescape@}"
```

## Application limit and contention

Though chef is mostly focused at having the application manage it's own configuration, some settings are server specific and totally unrelated to the application's nature itself. Furthermore, some settings are needed in order to ensure resource contention and to avoid infrastructure collapse due to a malfunctioning application.

This parametrization is implemented through the use of Application Limits, that you can define at either the server level:

```json
  "defaultApplicationLimits": {
    "FastCgiMaxInstances": 5,
    "IisPoolMaxCpuLimitPercent": 60,
    "IisPoolCpuLimitAction": "ThrottleUnderLoad",
    "IisPoolMaxPrivateMemoryLimitKb": 3145728,
    "IisPoolStartupModeAllowAlwaysRunning": true,
    "IisVirtualDirectoryAllowPreloadEnabled": true
  }
```

Or per application template:

```yaml
application_limits:
  FastCgiMaxInstances: 5
  IisPoolMaxCpuLimitPercent: 40
  IisPoolCpuLimitAction: Throttle
  IisPoolMaxPrivateMemoryLimitKb: 36700189
  IisPoolStartupModeAllowAlwaysRunning: true
  IisVirtualDirectoryAllowPreloadEnabled: true
```

The available limits are:

* **FastCgiMaxInstances**: for fastCgi applications, the maximum amount of processes that can be spin up by IIS.
* **IisPoolMaxCpuLimitPercent**: The limit percentage of CPU usage to enforce for each application
* **IisPoolCpuLimitAction**: The limiting strategy for the application, one of KillW3wp, Throttle, ThrottleUnderLoad
* **IisPoolMaxPrivateMemoryLimitKb**: Max private bytes for the pool before triggering a recycle
* **IisPoolStartupModeAllowAlwaysRunning**: Allor or disallow the usage of the AlwaysRunning IIS pool option.
* **IisVirtualDirectoryAllowPreloadEnabled**: Allow or disallow the usage of the PreloadEnabled IIS application option

See this related links:

* https://blogs.iis.net/shauneagan/cpu-throttling-iis-7-vs-iis-8
* https://serverfault.com/questions/167851/how-to-limit-the-memory-used-by-an-application-in-iis
* https://forums.iis.net/t/1201712.aspx?FastCGI+MaxInstances+setting

## SQL Targeting

When you configure your application, you can request sql services to be provisioned for you:

```yaml
services:
  # Pedir una segunda base de datos
  sql-primary: { type: 'sqlsrv' }
  sql-logs: { type: 'sqlsrv' }
```

By default these databases will be routed to the default SQL server available on your instance.

If you want to specify ad-hoc settings for one of these services you can do so through the application template:

```yaml
id: 'php-test'
mount_strategy: 'copy'
runtime_overrides:
 'deployment.custom.setting': 'my_custom_setting'
 'services.contents.mount.files.path': 'c:\\missingdir'
# Use the key "sqlservice_XXX" where XXX is the application's requested sql service
sqlservice_sqlsrv-logs:
  # You can specify an ad-hoc connection string, or use a global target (use empty or default to use the default global sql server target)
  connectionString: ''
  # You can specify a custom database name to avoid chef automatically generating one for you
  databaseName: 'alternate-database-name2'
  # Use passThroughAuth to prevent chef from managing database credentials for the application, the one's specified in the connection string will be used
  passThroughAuth: true
```

## Mirroring applications

One of the advantages of fully automated deployment processes is that it makes it easy to spin up cloned environments that can easily sync contents and databases between environments.

To spin up an application instance that "inherits" from another application, use the inherit keyword and specify the parent application id:

```yaml
inherit: 'upcplus-production'
id: 'upcplus-dev'
mount_strategy: 'move'
downloader:
 type: 'appveyor'
 project: 'drupal7'
 username: '---'
 apitoken: '---'
 branch: 'b-upcplus-dev'
```

In this case, we are creating a mirror application of "upcplus-production". To all effects, these are independent applications (you need to ensure there are no hostname or other resource collisions by using specific chef configuration files). The only "relationship" between both applications is that persistent storage (database and disk) will by "syncronized". This synchronization takes place automatically when first installing the application. Any future synchronizations must be manually triggered through a command:

```bash
Invoke-ChefAppSync upcplus-dev
```

## Shared CDN pull zone

### Quick setup

Chef has an automated setup that allows different IIS sites to share the same CDN pull-zone.

To setup CDN support for your chef installation, add an origin configuration to your server settings:

```json
  "cdn_bindings": [
    {
      "id":  "cdn0", 
      "OriginBindings": [
        {
          "id": "binding1",
          "hostname": "origin-url",
          "port": 80,
          "interface": "*",
          "addtohosts": true
        }
      ],
      "EdgeUrls": [
        "http://non-httpsedge.netdna-cdn.com",
        "https://httpsedge.netdna-ssl.com"
      ]
    }
  ]
```

This will create a site in IIS under the name "__chef_cdn".

Using IIS rewriting capabilities, incoming requests to this site are forwarded to your actual sites in the local host according to a per-site prefix assigned by chef.

In the runtime settings generated for you application, you will find the assigned CDN prefix for your site:

```json
{
  "cdn.preferred_prefix": "https://httpsedge.netdna-ssl.com/cdn_predow/"
}
```

Note than only HTTPS edge will be used to calculate a preferred prefix, if none is available, no preferred prefix will be provided, yet all the CDN information will be provided:

```json
  "cdn": {
    "cdn0": {
      "origins": {
        "binding1_chef_canonical": {
          "uri": "http://non-httpsedge.netdna-cdn.com/cdn_predow",
          "hostname": "non-httpsedge.netdna-cdn.com"
        }
      }
    },
    "preferred_prefix": ""
  },
```

# Command reference

## Invoke-ChefAppCleanup

Helps application deployers get rid of unused resources, left overs from deployments that did not succeed or got stuck.

**Arguments**: N/A

## Invoke-ChefAppCron

Runs the maintenance loop:

* Clean-up log directories (zip big files and deletes very old ones)
* Deletes old files in the temp folder

**Arguments**: N/A

## Invoke-ChefAppRemove

Removes an application, completely deleting it's storage, databases and any other resources that have been provisioned by chef.

**Arguments**

* **Id** [string]: The identifier of the application to remove.
* **Force** [switch]: Enables a more strict remove strategy, use for very broken installation  

**Returns**

* Void

**Examples**

```bash
Invoke-ChefAppRemove myapplication -Force
```

## Invoke-ChefAppDeploy

Installs an application from a given application yaml settings file.

**Arguments**:

* Path [string]: Full path name to the yaml settings file.

**Returns**

* Void

## Invoke-ChefAppDeployPath

Deploys an application from a given physical directory that contains the artifact files.

**Arguments**

* Path [string]: The directory that contains the artifact source
* Id [string]: The identifier of the application to install
* MountStrategy [string, optional]: One of "link", "copy" or "original" determines how the actual application relates to the source
  * "link" (default): The application is symlinked to the given path, very helpful to debug or during development
  * "copy": All the source files are copied
  * "original": The application is directly pointing to the given directory
* Install [switch]: If provided, the application will be fully installed (the application's yaml settings wil be created in the installed_apps folder).

**Returns**

* Deployment

**Examples**

```bash
Invoke-ChefAppDeployPath "d:\repositories\myapplication" myapplicationid -Install
```

## Invoke-ChefAppDeployZip

Deploys an application from a given zip file containing the artifact files.

**Arguments**

* Path [string]: The zip containing the artifact files
* Id [string]: The identifier of the application to install
* Install [switch]: If provided, the application will be fully installed (the application's yaml settings wil be created in the installed_apps folder).

**Returns**

* Deployment

**Examples**

## Invoke-ChefAppFind

Finds the deployment object for the application, or all the applications if Id is empty. Helpful to introspect all the deployed applications.

**Arguments**:

* Id: the application id to look for

**Returns**:

* InstalledApplication when an application ID is provided
* List\<InstalledApplication\> when no ID is provided

## Invoke-ChefAppDeploySsl

Trigger SSL deployment for IIS sites. Useful when certificate provisioning is automatically taken care of by Chef using Let's Encrypt.

**Arguments**

* Id [string, options]: the application id to look for. Loops through all applications if not provided.
* Force [switch]: ignore any preventive SSL renewal restrictions (such as a recent failure or Let's encrypt API limits) or even force renewal even if a vaild certificate currently exists.

**Returns**

* Void

**Example**

```powershell
# Force renewal on a site
Invoke-ChefAppDeploySsl myapplication -Force

# Trigger a SSL renewal loop, only certificates that actually need (according to chef's internal renewing algorithm) it will be renewed
Invoke-ChefAppDeploySsl
```

## Invoke-ChefSelfInstall

Does self initialization of Chef configuration files into the specified destination path.

**Arguments**

* Path [string]: the path to install the chef configuration files into. It creates 

**Returns**

* Void

**Example**

```powershell
Invoke-ChefSelfInstall d:\chef
```

## Invoke-ChefAppRedeploy

Redeploy the given application. 

**Arguments**

* Id [string]: The application id
* BuildId [string, optional]: The build ID as reported by the artifact downloader. If build ID is specified, the deployer will look for that artifact version and that application **will be stuck into that version**. Use the keyword "latest" as the BuildId to make Chef grab the latest available artifact, and unlock the application from using a fixed version/build.
* FromTemplate [switch]: By default, the application uses it's last configuration settings to redeploy itself. If you specify this switch, it will refresh the application download details from the installed application file.
* Force [switch]: Force application deployment, even if the version number has not changed. 
* MergeTags [string]: A comma delimited string of tags that will be merged with the tags defined for this application.

**Returns**

* Void

**Example**

```powershell
# Redeploy updating the configuration template and forcing even if no new version is available
Invoke-ChefAppRedeploy myapplication -FromTemplate -Force

# Redeploy using current settings
Invoke-ChefAppRedeploy myapplication
```

## Invoke-ChefAppStart / Invoke-ChefAppStop

Chef can internally track a stoped/start state for an application. When stopped, an application's site, scheduler and any other services that might lock the application's resources are stopped. If stopped and redeployed, the application will start again in a stopped state unless explicitly taken back online with a start command

**Arguments**

* Id [string]: The application id

**Returns**

* Void

**Example**

```powershell
# Starts an application that is stopped
Invoke-ChefAppStart myapplication

# Starts all application
Invoke-ChefAppStart

# Stop all applications
Invoke-ChefAppStop

# Stop a specific application
Invoke-ChefAppStop myapplication
```

## Invoke-ChefAppRmdir

Utility command to use Chef's internal directory deletion algorithm. Very useful to remove directories with edge characteristics such as:

* Long path names
* Contains windows reserved file names such as nul that prevent windows explorer from working
* Permissions issues
* Files in use or locked by processes

**Arguments**

* Path [string]: Path to the directory to be removed

**Returns**

* Void

**Example**

```powershell
Invoke-ChefAppRmdir "d:\myspeciallockedpath\myfiles"
```

