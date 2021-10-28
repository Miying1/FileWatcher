# FileWatcher
C# 基于FileSystemWatcher类对目录和文件夹的改变监控.
解决了同一文件在复制或新建文件时会触发多次 new 和 changed 事件的问题
# 示例
    class Program
    {
        static void Main(string[] args)
        {
            //目录下的文件
            FileWatcher fileWatcher = new FileWatcher("C:\\test", "*.xml");
            fileWatcher.Open();
            var task = FileChangedQueue(fileWatcher);
            //task.Wait();
            //目录
            FileWatcher fileWatcher2 = new FileWatcher("C:\\test");
            fileWatcher2.NotifyFilter = NotifyFilters.DirectoryName;
            fileWatcher2.Open();
            var task2 = FileChangedQueue(fileWatcher2);
            task2.Wait();
        }

        private static async Task FileChangedQueue(FileWatcher fileWatcher)
        {
             
            while (true)
            {
                if (fileWatcher.IsWatch)
                {
                    if (fileWatcher.FileChangeQueue.Count == 0)
                    {
                        await Task.Delay(100);
                        continue;
                    }
                    var (isok,fci)= fileWatcher.GetDequeue();
                    if(!isok || fci==null)
                    {
                        await Task.Delay(100);
                        continue;
                    }
                    if(fci.ChangeType== FileChangeType.Rename)
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} ：{fci.ChangeType.ToString()} >> {fci.OldFullPath} >>> {fci.FullPath}");
                    }
                    else
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} ：{fci.ChangeType.ToString()} >> {fci.FullPath} >>> {fci.Name}");

                }
                else
                {
                    fileWatcher.Open();
                    await Task.Delay(100);
                }
            }
        }
    }
