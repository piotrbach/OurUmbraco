﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Xml;
using Examine;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using umbraco.BusinessLogic;

namespace OurUmbraco.Documentation.Busineslogic.GithubSourcePull
{
    public class ZipDownloader
    {
        private const string rootFolder = @"~\Documentation";
        private const string config = @"~\config\githubpull.config";
        private string rootFolderPath = HttpContext.Current.Server.MapPath(rootFolder);
        private string configPath = HttpContext.Current.Server.MapPath(config);


        public string RootFolder { get; set; }
        public XmlDocument Configuration { get; set; }
        public bool IsProjectDocumentation { get; set; }

        public ZipDownloader(string rootFolder, XmlDocument configuration)
        {
            Configuration = configuration;
            RootFolder = rootFolder;
            IsProjectDocumentation = false;
        }

        public ZipDownloader()
        {
            XmlDocument xd = new XmlDocument();
            xd.Load(configPath);

            Configuration = xd;
            RootFolder = rootFolderPath;
            IsProjectDocumentation = false;
        }

        public ZipDownloader(string rootFolder, string configurationPath)
        {
            XmlDocument xd = new XmlDocument();
            xd.Load(configurationPath);

            Configuration = xd;
            RootFolder = rootFolder;
            IsProjectDocumentation = false;
        }

        public ZipDownloader(int projectId)
        {
            var project = new umbraco.NodeFactory.Node(projectId);
            var githubRepo = project.GetProperty("documentationGitRepo");
            var xd = new XmlDocument();

            xd.LoadXml(string.Format(
                "<?xml version=\"1.0\"?><configuration><sources><add url=\"{0}/zipball/master\" folder=\"\" /></sources></configuration>",
                githubRepo));

            Configuration = xd;
            RootFolder = HttpContext.Current.Server.MapPath(@"~" + project.Url.Replace("/",@"\") + @"\Documentation");
            IsProjectDocumentation = true;
        }

        /// <summary>
        /// This will ensure that the docs exist, this checks by the existence of the /Documentation/sitemap.js file
        /// </summary>
        public static void EnsureGitHubDocs(bool overwrite = false)
        {
            var rootFolderPath = HttpContext.Current.Server.MapPath(rootFolder);
            var configPath = HttpContext.Current.Server.MapPath(config);

            //Check if it exists, if it does then exit
            if (!overwrite && File.Exists(Path.Combine(rootFolderPath, "sitemap.js"))) return;

            if (!Directory.Exists(rootFolderPath))
            {
                Directory.CreateDirectory(rootFolderPath);
            }

            var unzip = new ZipDownloader(rootFolderPath, configPath)
            {
                IsProjectDocumentation = true
            };
            unzip.Run();
        }

        public void Run()
        {
            Trace.WriteLine("Started git sync", "Gitsyncer");

            foreach (XmlNode node in Configuration.SelectNodes("//add"))
            {
                var url = node.Attributes["url"].Value;
                var folder = node.Attributes["folder"].Value;
                var path = Path.Combine(RootFolder, folder);

                Trace.WriteLine("Loading: " + url + " to " + path, "Gitsyncer");

                Process(url, folder);
            }


            FinishEventArgs ev = new FinishEventArgs();
            FireOnFinish(ev);
        }

        public dynamic DocumentationSiteMap(string folder = "")
        {
            var path = Path.Combine(RootFolder, folder, "sitemap.js");
            var json = File.ReadAllText(path);
            dynamic d = JsonConvert.DeserializeObject<dynamic>(json);
            return d;
        }

        public void Process(string url, string foldername)
        {
            var zip = Download(url, foldername);
            Unzip(zip, foldername, RootFolder);
            BuildSitemap(foldername);

            //YUCK, this is horrible but unfortunately the way that the doc indexes are setup are not with 
            // a consistent integer id per document. I'm sure we can make that happen but I don't have time right now.
            ExamineManager.Instance.IndexProviderCollection["documentationIndexer"].RebuildIndex();
        }

        public void BuildSitemap(string foldername)
        {
            var folder = new DirectoryInfo(Path.Combine(RootFolder, foldername));
            dynamic root = GetFolderStructure(folder, folder.FullName, 0);

            var serializedRoot = JsonConvert.SerializeObject(root, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(folder.FullName, "sitemap.js"), serializedRoot); 
        }

        private class SiteMapItem
        {
            public string name { get; set; }
            public string path { get; set; }
            public int level { get; set; }
            public int sort { get; set; }
            public bool hasChildren { get; set; }
            public List<SiteMapItem> directories { get; set; }
        }

        private SiteMapItem GetFolderStructure(DirectoryInfo dir, string rootPath, int level)
        {
            var d = new SiteMapItem();
            d.name = dir.Name;
            d.path = dir.FullName.Substring(rootPath.Length).Replace('\\','/');
            d.level = level;            
            d.sort = GetSort(dir.Name, level)
                //default
                ?? 100;

            if (dir.GetDirectories().Any())
            {
                var list = new List<SiteMapItem>();
                foreach (var child in dir.GetDirectories().Where(x => x.Name != "images"))
                {
                    list.Add(GetFolderStructure(child, rootPath, level + 1));
                }
                
                d.hasChildren = true;
                d.directories = list.OrderBy(x => x.sort).ToList();
            }

            return d;
        }

        private int? GetSort(string name, int level)
        {
            switch (level)
            {
                case 1:
                    switch (name.ToLowerInvariant())
                    {
                        case "getting-started":
                            return 0;
                        case "implementation":
                            return 1;
                        case "extending":
                            return 3;
                        case "reference":
                            return 4;
                        case "tutorials":
                            return 5;
                        case "add-ons":
                            return 6;
                    }
                    break;
                case 2:
                    switch (name.ToLowerInvariant())
                    {
                        //Getting Started
                        case "setup":
                            return 0;
                        case "backoffice":
                            return 1;
                        case "data":
                            return 3;
                        case "design":
                            return 4;

                        //Implementation
                        case "default-routing":
                            return 0;

                        //Extending
                        case "dashboards":
                            return 0;
                        case "section-trees":
                            return 1;
                        case "property-editors":
                            return 2;
                        case "macro-parameter-editors":
                            return 3;

                        //Reference
                        case "config":
                            return 0;
                        case "templating":
                            return 1;
                        case "querying":
                            return 2;
                        case "routing":
                            return 3;
                        case "searching":
                            return 4;
                        case "events":
                            return 5;
                        case "management":
                            return 6;
                        case "plugins":
                            return 7;
                        case "cache":
                            return 8;

                        //Tutorials
                        case "creating-basic-site":
                            return 0;

                        //Add ons
                        case "umbracoforms":
                            return 0;
                    }
                    break;
            }
            return null;
        }

        private string Download(string url, string foldername)
        {
            var dir = new DirectoryInfo(Path.Combine(RootFolder, foldername));
            var dirsToCreate = new List<DirectoryInfo>();

            while (!dir.Exists)
            {
               dirsToCreate.Add(dir);
               dir = dir.Parent;
            }

            if (dirsToCreate.Any())
            {
                dirsToCreate.Reverse();
                foreach (var d in dirsToCreate)
                    d.Create();
            }

            var path = Path.Combine(RootFolder, foldername + "archive.zip");
            
            if (File.Exists(path))
                File.Delete(path);

            WebClient Client = new WebClient();
            Client.DownloadFile(url, path);

            return path;
        }


        private void Unzip(string path, string foldername, string rootFolder)
        {
            ZipInputStream s = new ZipInputStream(File.OpenRead(path));
            ZipEntry theEntry;

            var stopDir = "\\Documentation";
            string serverFolder = rootFolder + "\\" + foldername;

            List<string> existingFiles = new List<string>();

            if (Directory.Exists(serverFolder))
            {
                foreach (var folder in Directory.GetDirectories(serverFolder))
                    Directory.Delete(folder, true);

                foreach (var mdfile in Directory.GetFiles(serverFolder, "*.md"))
                    File.Delete(mdfile);

//                var files = string.Join("|", Directory.GetFiles(serverFolder, "*.md", SearchOption.AllDirectories)).ToLower().Split('|');
//                existingFiles.AddRange(files);
            }
            else
                Directory.CreateDirectory(serverFolder);

            try
            {
                bool stopDirSet = false;

                while ((theEntry = s.GetNextEntry()) != null)
                {
                    if (IsProjectDocumentation && !stopDirSet)
                    {
                        stopDir = Path.GetDirectoryName(theEntry.Name);
                        stopDirSet = true;
                    }

                    string directoryName = Path.GetDirectoryName(theEntry.Name);
                    string fileName = Path.GetFileName(theEntry.Name);

                    HttpContext.Current.Trace.Write("git", theEntry.Name + "  " + fileName + " - " + directoryName);

                    if (directoryName.Contains(stopDir))
                    {
                        var startIndex = directoryName.LastIndexOf(stopDir) + stopDir.Length;
                        directoryName = directoryName.Substring(startIndex);

                        // create directory
                        Directory.CreateDirectory(serverFolder + directoryName);

                        if (fileName != String.Empty)
                        {
                            bool update = false;
                            var filepath = serverFolder + directoryName + "\\" + fileName;


                            if (existingFiles.Contains(filepath.ToLower()))
                            {
                                update = true;
                                existingFiles.Remove(filepath.ToLower());
                            }

                            FileStream streamWriter = File.Create(filepath);
                            int size = 2048;
                            byte[] data = new byte[2048];
                            while (true)
                            {
                                size = s.Read(data, 0, data.Length);
                                if (size > 0)
                                {
                                    streamWriter.Write(data, 0, size);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            streamWriter.Close();

                            if (update)
                            {
                                UpdateEventArgs ev = new UpdateEventArgs();
                                ev.FilePath = filepath;
                                FireOnUpdate(ev);
                            }
                            else
                            {
                                CreateEventArgs ev = new CreateEventArgs();
                                ev.FilePath = filepath;
                                FireOnCreate(ev);
                            }
                        }
                    }
                }

                s.Close();
                foreach (var file in Directory.GetFiles(rootFolder, "*.zip"))
                {
                    //File.Delete(file);
                }

            }
            catch (Exception ex)
            {
                Log.Add(LogTypes.Error, -1, ex.ToString());
            }
        }

        private Events _e = new Events();

        public static event EventHandler<UpdateEventArgs> OnUpdate;
        protected virtual void FireOnUpdate(UpdateEventArgs e)
        {
            try
            {
                _e.FireCancelableEvent(OnUpdate, this, e);
            }
            catch (Exception ex) { }
        }


        public static event EventHandler<CreateEventArgs> OnCreate;
        protected virtual void FireOnCreate(CreateEventArgs e)
        {
            try
            {
                _e.FireCancelableEvent(OnCreate, this, e);
            }
            catch (Exception ex) { }
        }


        public static event EventHandler<DeleteEventArgs> OnDelete;
        protected virtual void FireOnDelete(DeleteEventArgs e)
        {
            try
            {
                _e.FireCancelableEvent(OnDelete, this, e);
            }
            catch (Exception ex) { }
        }

        public static event EventHandler<FinishEventArgs> OnFinish;
        protected virtual void FireOnFinish(FinishEventArgs e)
        {
            try
            {
                _e.FireCancelableEvent(OnFinish, this, e);
            }
            catch (Exception ex) { }
        }

    }
}