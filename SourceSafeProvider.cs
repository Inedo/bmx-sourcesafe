using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Authentication;
using System.Security.Permissions;
using System.Text;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Microsoft.Win32;

namespace Inedo.BuildMasterExtensions.SourceSafe
{
    /// <summary>
    /// Represents a provider that connects to a SourceSafe database
    /// </summary>
    [ProviderProperties(
        "Visual SourceSafe",
        "Supports Microsoft Visual SourceSafe (VSS) 6.0 and later; requires that VSS is installed.")]
    [CustomEditor(typeof(SourceSafeProviderEditor))]
    public sealed class SourceSafeProvider : SourceControlProviderBase, IVersioningProvider
    {
        /// <summary>
        /// Gets or sets the user defined source safe client exe path.
        /// </summary>
        [Persistent]
        public string UserDefinedSourceSafeClientExePath { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [Persistent]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        [Persistent]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the db file path.
        /// </summary>
        [Persistent]
        public string DbFilePath { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds before the SS.exe process is killed in order to prevent 
        /// it from running indefinitely.
        /// </summary>
        [Persistent]
        public int Timeout { get; set; }

        /// <summary>
        /// Gets the <see cref="T:System.Char"/> used by the
        /// provider to separate directories/files in a path string.
        /// </summary>
        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceSafeProvider"/> class.
        /// </summary>
        public SourceSafeProvider()
        {
            this.Timeout = 30;
        }
        
        private static class SourceSafeCommands
        {
            public const string CP = "CP";
            public const string ABOUT = "ABOUT";
            public const string ADD = "ADD";
            public const string BRANCH = "BRANCH";
            public const string CHECKIN = "CHECKIN";
            public const string CHECKOUT = "CHECKOUT";
            public const string CLOAK = "CLOAK";
            public const string COMMENT = "COMMENT";
            public const string COPY = "COPY";
            public const string CREATE = "CREATE";
            public const string DECLOAK = "DECLOAK";
            public const string DELETE = "DELETE";
            public const string DEPLOY = "DEPLOY";
            public const string DESTROY = "DESTROY";
            public const string DIFF = "DIFF";
            public const string DIR = "DIR";
            public const string FILETYPE = "FILETYPE";
            public const string FINDINFILES = "FINDINFILES";
            public const string GET = "GET";
            public const string HELP = "HELP";
            public const string HISTORY = "HISTORY";
            public const string LABEL = "LABEL";
            public const string LINKS = "LINKS";
            public const string LOCATE = "LOCATE";
            public const string MERGE = "MERGE";
            public const string MOVE = "MOVE";
            public const string PASSWORD = "PASSWORD";
            public const string PATHS = "PATHS";
            public const string PHYSICAL = "PHYSICAL";
            public const string PIN = "PIN";
            public const string PROJECT = "PROJECT";
            public const string PROPERTIES = "PROPERTIES";
            public const string PURGE = "PURGE";
            public const string RECOVER = "RECOVER";
            public const string RENAME = "RENAME";
            public const string ROLLBACK = "ROLLBACK";
            public const string SHARE = "SHARE";
            public const string STATUS = "STATUS";
            public const string UNDOCHECKOUT = "UNDOCHECKOUT";
            public const string UNPIN = "UNPIN";
            public const string VIEW = "VIEW";
            public const string WHOAMI = "WHOAMI";
            public const string WORKFOLD = "WORKFOLD";
        }

        public override bool IsAvailable()
        {
            return !string.IsNullOrEmpty(FindSourceSafeClientExePath());
        }

        public override void ValidateConnection()
        {
            RunCommand(SourceSafeCommands.DIR);
        }

        public override void GetLatest(string sourcePath, string targetPath)
        {
            targetPath = targetPath.TrimEnd(Path.DirectorySeparatorChar);
            Util.Files.ValidateInputPath(targetPath);
            if (!System.IO.Directory.Exists(targetPath))
                System.IO.Directory.CreateDirectory(targetPath);

            RunCommand(SourceSafeCommands.GET,
                "\"" + sourcePath + "\"",
                "-GTM",
                "-GL\"" + targetPath + "\"",
                "-R",
                "-I-N");
        }

        public void ApplyLabel(string label, string sourcePath)
        {
            RunCommand(SourceSafeCommands.LABEL, "\"" + sourcePath + "\"", "-L\"" + label + "\"", "-I-N");
        }

        public void GetLabeled(string label, string sourcePath, string targetPath)
        {
            targetPath = targetPath.TrimEnd(Path.DirectorySeparatorChar);
            Util.Files.ValidateInputPath(targetPath);
            if (!System.IO.Directory.Exists(targetPath))
                System.IO.Directory.CreateDirectory(targetPath);

            RunCommand(SourceSafeCommands.GET,
                "\"" + sourcePath + "\"",
                "-GL\"" + targetPath + "\"",
                "-Vl" + label,
                "-GTM",
                "-R",
                "-I-N");
        }

        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            List<string> dirParams = new List<string>();
            dirParams.Add("\"" + sourcePath + "\"");
            dirParams.Add("-F"); // list folders AND files

            string dirList = RunCommand(SourceSafeCommands.DIR, dirParams.ToArray());
            SsNode rootNode = new SsNode();
            List<DirectoryEntryInfo> subdirs = new List<DirectoryEntryInfo>();
            List<FileEntryInfo> files = new List<FileEntryInfo>();

            // Special case if no path is provided (show default root)
            // A vault command of DIR "" and DIR "$" display the same thing.  Since there's no persistent property of
            // a source path, BuildMaster uses this to always display the $ as a folder if no path is provided.
            // Called after an unnecessary DIR command so that the connection is validated.
            if (string.IsNullOrEmpty(sourcePath))
            {
                rootNode.Path = "";
                subdirs.Add(new DirectoryEntryInfo("$", "$", null, null));
            }
            else
            {
                foreach (string dirSection in dirList.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.None))
                {
                    StringBuilder currentDirBuilder = new StringBuilder();
                    bool foundDirName = false;
                    foreach (string currentDirRow in dirSection.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!foundDirName)
                        {
                            // root folder
                            currentDirBuilder.Append(currentDirRow.TrimEnd(':'));
                            if (currentDirRow.Contains(":"))
                            {
                                rootNode = new SsNode(currentDirBuilder.ToString(), new string[] { });
                                foundDirName = true;
                            }
                        }
                        else
                        {
                            // subfolder
                            if (currentDirRow.StartsWith("$"))
                                subdirs.Add(new DirectoryEntryInfo(
                                    currentDirRow.TrimStart('$'),
                                    rootNode.Path + DirectorySeparator + currentDirRow.TrimStart('$'),
                                    null,
                                    null));
                            else if (!string.IsNullOrEmpty(currentDirRow) && !currentDirRow.EndsWith(" item(s)"))
                                files.Add(new FileEntryInfo(currentDirRow, currentDirBuilder.ToString() + DirectorySeparator + currentDirBuilder));
                        }
                    }
                }
            }

            return new DirectoryEntryInfo(
                rootNode.Name,
                rootNode.Path,
                subdirs.ToArray(),
                files.ToArray());
        }
        public override byte[] GetFileContents(string filePath)
        {
            string targetPath = Path.GetTempPath();
            if (string.IsNullOrEmpty(targetPath)) throw new ArgumentException("targetPath");

            targetPath = targetPath.TrimEnd(Path.DirectorySeparatorChar);
            RunCommand(
                SourceSafeCommands.GET,
                new []
                {
                    "\"" + filePath + "\"",
                    "-GTM",
                    "-GL\"" + targetPath + "\"",
                    "-I-N"
                });

            string[] temp = filePath.Split(DirectorySeparator);
            string filename = temp[temp.Length - 1];
            byte[] fileContents = File.ReadAllBytes(Path.Combine(targetPath, filename));
            return fileContents;
        }

        public override string ToString()
        {
            string dbName = null;
            if (DbFilePath.EndsWith(Path.DirectorySeparatorChar + "srcsafe.ini"))
            {
                dbName = DbFilePath.Substring(0, DbFilePath.LastIndexOf(Path.DirectorySeparatorChar + "srcsafe.ini"));
                dbName = dbName.Substring(dbName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            }
            if (!string.IsNullOrEmpty(null))
                return string.Format("Visual SourceSafe {0} database", dbName);
            else
                return string.Format("Visual SourceSafe");
        }

        private string FindSourceSafeClientExePath()
        {
            // Always use user-defined path (if provided)
            if (!string.IsNullOrEmpty(UserDefinedSourceSafeClientExePath))
            {
                return File.Exists(UserDefinedSourceSafeClientExePath)
                    ? UserDefinedSourceSafeClientExePath
                    : null;
            }

            // Default directory in Program Files
            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Microsoft Visual Studio\VSS\win32\ss.exe");
            if (File.Exists(defaultPath)) return defaultPath;

            // Another version uses a different default folder
            defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Microsoft Visual SourceSafe\ss.exe");
            if (File.Exists(defaultPath)) return defaultPath;

            // Search by registry
            RegistryKey ssKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\SourceSafe");
            if (ssKey != null && !string.IsNullOrEmpty(ssKey.GetValue("SCCServerPath") as string))
            {
                string regPath = Path.Combine(Path.GetDirectoryName(ssKey.GetValue("SCCServerPath").ToString()), "ss.exe");
                if (File.Exists(regPath)) return regPath;
            }

            return null;
        }

        private string RunCommand(string ssCommand, params string[] parameters)
        {
            if (string.IsNullOrEmpty(ssCommand)) throw new ArgumentNullException("A SourceSafe command is required.");

            string ssPath = FindSourceSafeClientExePath();
            if (string.IsNullOrEmpty(ssPath)) throw new NotAvailableException("SourceSafe client exe path not set.");
            if (!File.Exists(DbFilePath)) throw new ArgumentException("The database file at '" + DbFilePath + "' does not exist.");

            // Prepare the command
            StringBuilder parametersText = new StringBuilder();
            parametersText.Append(ssCommand);
            if (parameters.Length > 0) parametersText.Append(" " + string.Join(" ", parameters));

            // Set user/pass if provided
            if (!string.IsNullOrEmpty(Username))
            {
                parametersText.Append(" -Y" + Username);
                if (!string.IsNullOrEmpty(Password))
                    parametersText.Append("," + Password);
            }

            // Check access to DB file
            FileIOPermission perm = new FileIOPermission(FileIOPermissionAccess.Read, DbFilePath);
            try { perm.Demand(); }
            catch (SecurityException)
            {
                throw new SecurityException("Read access to the database file '" + DbFilePath + "' is denied.");
            }
            perm = new FileIOPermission(FileIOPermissionAccess.Write, DbFilePath);
            try { perm.Demand(); }
            catch (SecurityException)
            {
                throw new SecurityException("Write access to the database file '" + DbFilePath + "' is denied.");
            }

            // Run command exe
            Process cmdProc = new Process();
            cmdProc.StartInfo.EnvironmentVariables["SSDIR"] = Path.GetDirectoryName(DbFilePath);
            cmdProc.StartInfo.FileName = ssPath;
            cmdProc.StartInfo.Arguments = parametersText.ToString();
            cmdProc.StartInfo.RedirectStandardOutput = true;
            cmdProc.StartInfo.RedirectStandardError = true;
            cmdProc.StartInfo.CreateNoWindow = true;
            cmdProc.StartInfo.UseShellExecute = false;

            this.LogProcessExecution(cmdProc.StartInfo);

            var sbErrors = new StringBuilder();
            var sbInfo = new StringBuilder();

            cmdProc.ErrorDataReceived += (s, e) => 
            {
                lock (sbErrors)
                {
                    if (!string.IsNullOrEmpty(e.Data)) sbErrors.Append(e.Data);
                }
            };
            cmdProc.OutputDataReceived += (s, e) => 
            {
                lock (sbInfo)
                {
                    if (!string.IsNullOrEmpty(e.Data)) sbInfo.Append(e.Data);
                }
            };

            // Run the command
            cmdProc.Start();
            cmdProc.BeginOutputReadLine();
            cmdProc.BeginErrorReadLine();

            cmdProc.WaitForExit(this.Timeout * 1000);
            if (!cmdProc.HasExited)
            {
                LogWarning("The SourceSafe (ss.exe) process was running longer than the specified timeout ({0} secs) and therefore the process was forcibly killed.", this.Timeout);
                cmdProc.Kill();
            }
            var cmdResult = sbInfo.ToString();
            var cmdError = sbErrors.ToString();

            // Validate that process didn't end waiting for username/pass input
            if (cmdResult.StartsWith("Username:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidCredentialException("An invalid username/password was supplied for database '" + DbFilePath + "'.");
            }
            // Validate provided path
            if (cmdResult.Contains("is not an existing filename or project"))
            {
                throw new ArgumentException("The path within the database is invalid.  SourceSafe returned: " + cmdResult);
            }

            // SourceSafe will write its Y/N prompts to STDERR instead of STDOUT, this checks if part of the language in one of these prompts
            // matches 'as the default folder for project' since that is part of an acceptable prompt
            if (!string.IsNullOrEmpty(cmdError) && !cmdError.Contains("as the default folder for project"))
            {
                throw new InvalidOperationException("SourceSafe returned an error: " + cmdError);
            }

            return cmdResult;
        }

        private DirectoryEntryInfo[] GetSubNodes(SsNode rootNode, Queue<SsNode> nodes)
        {
            List<DirectoryEntryInfo> dirList = new List<DirectoryEntryInfo>();

            if (nodes.Count > 0)
            {
                while (nodes.Peek().IsChildOf(rootNode))
                {
                    SsNode nextNode = nodes.Dequeue();
                    dirList.Add(new DirectoryEntryInfo(
                        nextNode.Name,
                        nextNode.Path,
                        GetSubNodes(nextNode, nodes),
                        nextNode.Files));
                }
            }

            return dirList.ToArray();
        }
    }
}
