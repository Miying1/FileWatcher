using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace TestConsole
{
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
}
