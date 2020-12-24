using System;
using System.ServiceProcess;
using System.IO;
using System.Threading;
using System.Timers;
using System.Text;
using System.IO.Compression;
using System.Collections.Generic;

namespace SharpLab2
{
    public partial class Service1 : ServiceBase
    {
        string sourcePath = "D:\\SourceDirectory";
        string targetPath = "D:\\TargetDirectory";

        string filePath;

        Logger logger;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            filePath = Path.Combine(targetPath, "SourceLog.txt");

            logger = new Logger(sourcePath, targetPath, filePath);

            Thread loggerThread = new Thread(new ThreadStart(logger.Start));

            loggerThread.Start();
        }
        protected override void OnStop()
        {
            logger.Stop();
        }
    }

    class Logger
    {
        FileSystemWatcher watcher;

        System.Timers.Timer timer = new System.Timers.Timer();

        StringBuilder messages = new StringBuilder();

        List<string> createdFiles = new List<string>();

        string sourcePath;
        string targetPath;
        string logFilePath;

        object obj = new object();

        public Logger(string sourcePath, string targetPath, string logFilePath)
        {
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            watcher = new FileSystemWatcher(sourcePath);
            watcher.Deleted += OnDeleted;
            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;

            this.sourcePath = sourcePath;
            this.targetPath = targetPath;
            this.logFilePath = logFilePath;

            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = 1000;
        }

        public void Start()
        {
            WriteToFile($"Service was started at {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n");

            watcher.EnableRaisingEvents = true;
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            watcher.EnableRaisingEvents = false;
            messages.Clear();
            WriteToFile($"Service was stopped at {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n");
        }

        private void OnElapsedTime(object sender, ElapsedEventArgs e)
        {
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);

                watcher = new FileSystemWatcher(sourcePath);
                watcher.Deleted += OnDeleted;
                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Renamed += OnRenamed;

                watcher.EnableRaisingEvents = true;
            }

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            if (messages.Length > 0)
            {
                WriteToFile(messages.ToString());

                messages.Clear();
            }

            if (createdFiles.Count == 0) return;

            lock (obj)
            {
                watcher.EnableRaisingEvents = false;

                for (int i = 0; i < createdFiles.Count; i++)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(createdFiles[i]);
                        string newFileName = $"Sales_{fileInfo.CreationTime:dd_MM_yyyy_HH_mm_ss}";

                        newFileName += fileInfo.Extension;

                        string temp = newFileName;

                        newFileName += ".gz";

                        string newFilePath = Path.Combine(sourcePath, newFileName);

                        string newTargetPath = Path.Combine(targetPath, newFileName);

                        int counter = 1;

                        while (File.Exists(newFilePath) || File.Exists(newTargetPath))
                        {
                            newFileName = "(" + counter.ToString() + ")" + temp + ".gz";
                            newFilePath = Path.Combine(sourcePath, newFileName);
                            newTargetPath = Path.Combine(targetPath, newFileName);
                            counter++;
                        }

                        Compress(createdFiles[i], newFilePath);

                        File.Encrypt(newFilePath);

                        File.Move(newFilePath, newTargetPath);

                        File.Decrypt(newTargetPath);

                        string decompressedFilePath = Path.Combine(targetPath, "archive");

                        decompressedFilePath = Path.Combine(decompressedFilePath, fileInfo.CreationTime.Year.ToString());

                        decompressedFilePath = Path.Combine(decompressedFilePath, fileInfo.CreationTime.Month.ToString());

                        decompressedFilePath = Path.Combine(decompressedFilePath, fileInfo.CreationTime.Day.ToString());

                        if (!Directory.Exists(decompressedFilePath))
                            Directory.CreateDirectory(decompressedFilePath);

                        decompressedFilePath = Path.Combine(decompressedFilePath, newFileName.Remove(newFileName.Length - 3, 3));

                        Decompress(newTargetPath, decompressedFilePath);
                    }
                    catch
                    {
                        continue;
                    }
                }

                watcher.EnableRaisingEvents = true;

                createdFiles.Clear();
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;

            string fileEvent = "created";
            AddToMessages(filePath, fileEvent);

            createdFiles.Add(filePath);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            string filePath = e.OldFullPath;
            string fileEvent = "renamed to " + e.FullPath;
            AddToMessages(filePath, fileEvent);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string fileEvent = "changed";
            AddToMessages(filePath, fileEvent);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string fileEvent = "deleted";
            AddToMessages(filePath, fileEvent);
        }

        void AddToMessages(string filePath, string fileEvent)
        {
            messages.Append($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} file {filePath} was {fileEvent}\n");
        }

        public void WriteToFile(string message)
        {
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);

                watcher = new FileSystemWatcher(sourcePath);
                watcher.Deleted += OnDeleted;
                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Renamed += OnRenamed;

                watcher.EnableRaisingEvents = true;
            }

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            lock (obj)
            {
                using (StreamWriter sw = new StreamWriter(logFilePath, true))
                {
                    sw.Write(message);
                }
            }
        }

        void Compress(string sourceFile, string compressedFile)
        {
            using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open))
            {
                using (FileStream targetStream = new FileStream(compressedFile, FileMode.OpenOrCreate))
                {
                    using (GZipStream compressionStream = new GZipStream(targetStream, CompressionMode.Compress))
                    {
                        sourceStream.CopyTo(compressionStream);
                    }
                }
            }
        }

        void Decompress(string compressedFile, string targetFile)
        {
            using (FileStream sourceStream = new FileStream(compressedFile, FileMode.Open))
            {
                using (FileStream targetStream = new FileStream(targetFile, FileMode.OpenOrCreate))
                {
                    using (GZipStream decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(targetStream);
                    }
                }
            }
        }
    }
}
