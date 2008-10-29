//-----------------------------------------------------------------------
// <copyright file="Iis6VirtualDirectory.cs">(c) http://www.codeplex.com/MSBuildExtensionPack. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.Web
{
    using System;
    using System.DirectoryServices;
    using System.Globalization;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using Microsoft.Build.Framework;

    /// <summary>
    /// <b>Valid TaskActions are:</b>
    /// <para><i>Create</i> (<b>Required: </b> Website <b>Optional:</b> Name, Parent, RequireApplication, DirectoryType, AppPool, Propertied)</para>
    /// <para><b>Remote Execution Support:</b> Yes</para>
    /// </summary>
    /// <example>
    /// <code lang="xml"><![CDATA[
    /// <Project ToolsVersion="3.5" DefaultTargets="Default" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    ///     <PropertyGroup>
    ///         <TPath>$(MSBuildProjectDirectory)\..\MSBuild.ExtensionPack.tasks</TPath>
    ///         <TPath Condition="Exists('$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks')">$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks</TPath>
    ///     </PropertyGroup>
    ///     <Import Project="$(TPath)"/>
    ///     <Target Name="Default">
    ///         <!-- Create an IIsWebVirtualDir at the ROOT of the website -->
    ///         <MSBuild.ExtensionPack.Web.Iis6VirtualDirectory TaskAction="Create" Website="awebsite" AppPool="AnAppPool" Properties="Path=C:\Demo1"/>
    ///     </Target>
    /// </Project>
    /// ]]></code>    
    /// </example>
    public class Iis6VirtualDirectory : BaseTask, IDisposable
    {
        private DirectoryEntry websiteEntry;
        private string properties;
        private string directoryType = "IIsWebVirtualDir";
        private bool requireApplication = true;
        private string appPool = "DefaultAppPool";
        private string name = "ROOT";

        /// <summary>
        /// Sets the Parent
        /// </summary>
        public string Parent { get; set; }

        /// <summary>
        /// Sets whether an Application is required. Defaults to true.
        /// </summary>
        public bool RequireApplication
        {
            get { return this.requireApplication; }
            set { this.requireApplication = value; }
        }

        /// <summary>
        /// Sets the DirectoryType. Supports IIsWebDirectory and IIsWebVirtualDir. Default is IIsWebVirtualDir.
        /// </summary>
        public string DirectoryType
        {
            get { return this.directoryType; }
            set { this.directoryType = value; }
        }

        /// <summary>
        /// Sets the AppPool to run in. Default is 'DefaultAppPool'
        /// </summary>
        public string AppPool
        {
            get { return this.appPool; }
            set { this.appPool = value; }
        }

        /// <summary>
        /// Sets the Properties. Use a semi-colon delimiter.
        /// </summary>
        public string Properties
        {
            get { return System.Web.HttpUtility.HtmlDecode(this.properties); }
            set { this.properties = value; }
        }

        /// <summary>
        /// Sets the name of the Virtual Directory. Defaults to 'ROOT'
        /// </summary>
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        /// <summary>
        /// Sets the name of the Website to add the Virtual Directory to.
        /// </summary>
        [Required]
        public string Website { get; set; }

        internal string IISPath
        {
            get { return "IIS://" + this.MachineName + "/W3SVC"; }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.websiteEntry.Dispose();
            }
        }

        protected override void InternalExecute()
        {
            switch (this.TaskAction)
            {
                case "Create":
                    this.Create();
                    break;
                default:
                    this.Log.LogError(string.Format(CultureInfo.CurrentCulture, "Invalid TaskAction passed: {0}", this.TaskAction));
                    return;
            }
        }

        private static void UpdateMetabaseProperty(DirectoryEntry entry, string metabasePropertyName, string metabaseProperty)
        {
            if (metabaseProperty.IndexOf('|') == -1)
            {
                entry.Invoke("Put", metabasePropertyName, metabaseProperty);
                entry.Invoke("SetInfo");
            }
            else
            {
                entry.Invoke("Put", metabasePropertyName, string.Empty);
                entry.Invoke("SetInfo");
                string[] metabaseProperties = metabaseProperty.Split('|');
                foreach (string metabasePropertySplit in metabaseProperties)
                {
                    entry.Properties[metabasePropertyName].Add(metabasePropertySplit);
                }

                entry.CommitChanges();
            }
        }

        private DirectoryEntry LoadWebService()
        {
            DirectoryEntry webService = new DirectoryEntry(this.IISPath);
            if (webService == null)
            {
                throw new ApplicationException(string.Format(CultureInfo.CurrentUICulture, "Iis DirectoryServices Unavailable: {0}", this.IISPath));
            }

            return webService;
        }

        private DirectoryEntry LoadWebsite(string websiteName)
        {
            DirectoryEntry webService = null;

            try
            {
                webService = this.LoadWebService();
                DirectoryEntries webEntries = webService.Children;

                foreach (DirectoryEntry webEntry in webEntries)
                {
                    if (webEntry.SchemaClassName == "IIsWebServer")
                    {
                        if (string.Compare(websiteName, webEntry.Properties["ServerComment"][0].ToString(), StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            return webEntry;
                        }
                    }

                    webEntry.Dispose();
                }

                return null;
            }
            finally
            {
                if (webService != null)
                {
                    webService.Dispose();
                }
            }
        }

        private DirectoryEntry LoadVirtualRoot(string websiteName)
        {
            DirectoryEntry webService = null;
            try
            {
                webService = this.LoadWebService();
                DirectoryEntries webEntries = webService.Children;

                foreach (DirectoryEntry webEntry in webEntries)
                {
                    if (webEntry.SchemaClassName == "IIsWebServer")
                    {
                        if (string.Compare(webEntry.Properties["ServerComment"][0].ToString(), websiteName, StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            int websiteIdentifier = int.Parse(webEntry.Name, CultureInfo.InvariantCulture);
                            string rootVdirPath = string.Format(CultureInfo.InvariantCulture, "{0}/{1}/ROOT", this.IISPath, websiteIdentifier);
                            DirectoryEntry vdirEntry = new DirectoryEntry(rootVdirPath);
                            return vdirEntry;
                        }
                    }

                    webEntry.Dispose();
                }

                return null;
            }
            finally
            {
                if (webService != null)
                {
                    webService.Dispose();
                }
            }
        }

        private void Create()
        {
            DirectoryEntry vdirEntry = null;

            try
            {
                Log.LogMessage(MessageImportance.High, string.Format(CultureInfo.CurrentUICulture, "Creating Virtual Directory: {0} under {1}", this.Name, this.Website));

                // Locate the website.
                this.websiteEntry = this.LoadWebsite(this.Website);
                if (this.websiteEntry == null)
                {
                    throw new ApplicationException(string.Format(CultureInfo.CurrentUICulture, "Website not found: {0}", this.Website));
                }

                if (this.Name == "ROOT")
                {
                    vdirEntry = this.LoadVirtualRoot(this.Website);
                }
                else
                {
                    // Otherwise we create it.
                    string parentPath = string.Format(CultureInfo.InvariantCulture, "{0}{1}", this.websiteEntry.Path, this.Parent);
                    this.websiteEntry = new DirectoryEntry(parentPath);
                    try
                    {
                        vdirEntry = (DirectoryEntry)this.websiteEntry.Invoke("Create", this.DirectoryType, this.Name);
                    }
                    catch (TargetInvocationException tie)
                    {
                        Exception e = tie.InnerException;
                        COMException ce = (COMException)e;
                        if (ce != null)
                        {
                            // HRESULT 0x800700B7, "Cannot create a file when that file already exists. "
                            if (ce.ErrorCode == -2147024713)
                            {
                                // The child already exists, so let's get it.
                                string childPath = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", parentPath, this.Name);
                                vdirEntry = new DirectoryEntry(childPath);
                            }
                            else
                            {
                                throw;
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }

                    this.websiteEntry.CommitChanges();
                    vdirEntry.CommitChanges();
                    UpdateMetabaseProperty(vdirEntry, "AppFriendlyName", this.Name);
                }

                // Now loop through all the metabase properties specified.
                if (string.IsNullOrEmpty(this.Properties) == false)
                {
                    string[] propList = this.Properties.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string s in propList)
                    {
                        string[] propPair = s.Split(new[] { '=' });
                        string propName = propPair[0];
                        string propValue = propPair.Length > 1 ? propPair[1] : string.Empty;

                        Log.LogMessage(string.Format(CultureInfo.CurrentUICulture, "Adding Property: {0}({1})", propName, propValue));

                        UpdateMetabaseProperty(vdirEntry, propName, propValue);
                    }
                }

                vdirEntry.CommitChanges();

                if (this.RequireApplication)
                {
                    if (string.IsNullOrEmpty(this.AppPool))
                    {
                        vdirEntry.Invoke("AppCreate2", 1);
                    }
                    else
                    {
                        vdirEntry.Invoke("AppCreate3", 1, this.AppPool, false);
                    }
                }
                else
                {
                    vdirEntry.Invoke("AppDelete");
                }

                vdirEntry.CommitChanges();
            }
            finally
            {
                if (this.websiteEntry != null)
                {
                    this.websiteEntry.Dispose();
                }

                if (vdirEntry != null)
                {
                    vdirEntry.Dispose();
                }
            }
        }
    }
}
