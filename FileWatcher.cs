using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestConsole
{
    /// <summary>
    /// 文件监控类，用于监控指定目录下文件以及文件夹的变化
    /// </summary>
    public class FileWatcher
    {
        private FileSystemWatcher _watcher = null;
        private string path = string.Empty;
        private string filter = string.Empty;
        private bool isWatch = false;
        private object obj = new object();
        private ConcurrentQueue<FileChangeInformation> queue = null;

        public NotifyFilters NotifyFilter { get; set; }
        /// <summary>
        /// 监控是否正在运行
        /// </summary>
        public bool IsWatch
        {
            get
            {
                return isWatch;
            }
        }
        public ILogger Logger { get; set; }
        /// <summary>
        /// 文件变更信息队列
        /// </summary>
        public ConcurrentQueue<FileChangeInformation> FileChangeQueue
        {
            get
            {
                return queue;
            }
        }

        /// <summary>
        /// 初始化FileWatcher类
        /// </summary>
        /// <param name="path">监控路径</param>
        public FileWatcher(string path)
        {
            this.path = path;
            queue = new ConcurrentQueue<FileChangeInformation>();
        }

        /// <summary>
        /// 初始化FileWatcher类，并指定是否监控指定类型文件
        /// </summary>
        /// <param name="path">监控路径</param>
        /// <param name="filter">指定类型文件，格式如:*.txt,*.doc,*.rar</param>
        public FileWatcher(string path, string filter)
        {
            this.path = path;
            this.filter = filter;
            queue = new ConcurrentQueue<FileChangeInformation>();
        }



        /// <summary>
        /// 打开文件监听器
        /// </summary>
        public void Open()
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (string.IsNullOrEmpty(filter))
            {
                _watcher = new FileSystemWatcher(path);
            }
            else
            {
                _watcher = new FileSystemWatcher(path, filter);
            }
            //注册监听事件
            _watcher.Created += new FileSystemEventHandler(OnProcess);
            _watcher.Changed += new FileSystemEventHandler(OnProcess);
            _watcher.Deleted += new FileSystemEventHandler(OnProcess);
            _watcher.Renamed += new RenamedEventHandler(OnFileRenamed);
            if (NotifyFilter != 0)
                _watcher.NotifyFilter = NotifyFilter;
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
            _watcher.InternalBufferSize = 1024 * 100;
            isWatch = true;
            if (Logger != null)
                Logger.LogInformation("文件监听器开启：" + path);
        }

        /// <summary>
        /// 关闭监听器
        /// </summary>
        public void Close()
        {
            isWatch = false;
            _watcher.Created -= new FileSystemEventHandler(OnProcess);
            _watcher.Changed -= new FileSystemEventHandler(OnProcess);
            _watcher.Deleted -= new FileSystemEventHandler(OnProcess);
            _watcher.Renamed -= new RenamedEventHandler(OnFileRenamed);
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            if (Logger != null)
                Logger.LogInformation("文件监听器关闭：" + path);
        }
        /// <summary>
        /// 获取队列消息
        /// </summary>
        /// <returns></returns>
        public (bool, FileChangeInformation) GetDequeue()
        {
            FileChangeInformation file;
            lock (obj)
            {
                return (queue.TryDequeue(out file), file);
            }
          
        }

        /// <summary>
        /// 监听事件触发的方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnProcess(object sender, FileSystemEventArgs e)
        {
            try
            {

                FileChangeType changeType = FileChangeType.None;
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    var fileAttr = File.GetAttributes(e.FullPath);

                    if (fileAttr == FileAttributes.Directory)
                    {
                        changeType = FileChangeType.NewFolder;
                    }
                    else
                    {
                        while (true)
                        {
                            try
                            {
                                using (var f=File.Open(e.FullPath, FileMode.Open))
                                {
                                    f.Close();
                                }
                                break;
                            }
                            catch  
                            {
                                Task.Delay(100).Wait();

                            }
                        }
                        Task.Run(() => {
                            if (Monitor.TryEnter(obj))
                            {
                                Task.Delay(500).Wait();
                                Monitor.Exit(obj);
                            }
                        });
                       
                        changeType = FileChangeType.NewFile;
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    //文件夹的变化，只针对创建，重命名和删除动作，修改不做任何操作。
                    //因为文件夹下任何变化同样会触发文件的修改操作，没有任何意义.
                    if (File.GetAttributes(e.FullPath) == FileAttributes.Directory)
                    {
                        return;
                    }
                    changeType = FileChangeType.Change;
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    changeType = FileChangeType.Delete;
                }
                //创建消息，并压入队列中
                FileChangeInformation info = new FileChangeInformation(changeType, e.FullPath, e.Name);
                if (!queue.Any(x => x.FullPath == e.FullPath))
                {
                    queue.Enqueue(info);
                }
            }
            catch (Exception ex)
            {
                if (Logger != null)
                    Logger.LogError(ex, "文件监听异常：" + path);
                Close();
            }
        }

        /// <summary>
        /// 文件或目录重命名时触发的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                //创建消息，并压入队列中
                FileChangeInformation info = new FileChangeInformation(FileChangeType.Rename, e.OldFullPath, e.FullPath, e.OldName, e.Name);
                queue.Enqueue(info);
            }
            catch
            {
                Close();
            }
        }
    }
    public enum FileChangeType
    {
        None,
        NewFile,
        NewFolder,
        Change,
        Delete,
        Rename
    }
    /// <summary>
    /// 文件或目录更变信息
    /// </summary>
    public class FileChangeInformation
    {
        public FileChangeInformation(FileChangeType fileChangeType, string fullPath, string name)
        {
            ChangeType = fileChangeType;
            FullPath = fullPath;
            Name = name;
        }
        public FileChangeInformation(FileChangeType fileChangeType, string oldFullPath, string fullPath, string oldName, string name)
        {
            ChangeType = fileChangeType;
            OldFullPath = oldFullPath;
            FullPath = fullPath;
            OldName = oldName;
            Name = name;
        }
        public string OldName { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string OldFullPath { get; set; }
        public FileChangeType ChangeType { get; set; }


    }
}
