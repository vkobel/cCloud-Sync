using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace cCloud {

    /// <summary>
    ///   Implementation of FileSystemWatcher; it watches a folder to detect file modifications 
    /// </summary>
    class Watcher {
        private FileSystemWatcher watcher;
        private StackManager stackMgr;
        public Watcher(string path) {
            try {
                watcher = new FileSystemWatcher(path);
            } catch(ArgumentException e) {
                NotificationManager.Inform(e.Message, NotificationTypes.Warning, true);
            }
            if(watcher != null) {
                watcher.Changed += new FileSystemEventHandler(OnChanged);
                watcher.Created += new FileSystemEventHandler(OnChanged);
                watcher.Deleted += new FileSystemEventHandler(OnDeleted);
                watcher.Renamed += new RenamedEventHandler(OnRenamed);

                stackMgr = Init.Singleton.stackManager;

                // ====== Synchronization ======
                MasterSync.ProcessDir(path);
            }
        }
        ~Watcher() {
            Stop();
        }
        public void Start() {
            if(watcher != null) {
                NotificationManager.Inform("Wait for events... (" + watcher.Path + ")");
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
            }
        }
        public void Stop() {
            if(watcher != null)
                watcher.EnableRaisingEvents = false;
        }

        // **** Events ****
        private void OnChanged(object source, FileSystemEventArgs e) {
            FileBehavior fileBehavior = FileBehavior.Filter(e.FullPath, e.ChangeType);
            if(fileBehavior != null) // Exception detection and self-repair (null = exception has been raised)
                stackMgr.stack.Push(fileBehavior);
        }

        private void OnRenamed(object source, RenamedEventArgs e) {
            NotificationManager.Inform(String.Format("File: {0} renamed to {1}", e.OldFullPath, e.FullPath));
            // First: "MS Office check", if it has been renamed just after (500ms) a LastWriteTime update => treated as a change
            FileBehavior fileBehavior;
            if((DateTime.Now - (new FileInfo(e.FullPath).LastWriteTime)).TotalMilliseconds > 500) {
                fileBehavior = new FileRenamed(e.OldFullPath, e.FullPath);
            } else {
                NotificationManager.Inform("File: " + e.FullPath + " has been changed not renamed");
                fileBehavior = FileBehavior.Filter(e.FullPath, e.ChangeType);
            }
            if(fileBehavior != null) 
                stackMgr.stack.Push(fileBehavior);
        }

        private void OnDeleted(object source, FileSystemEventArgs e) {
            NotificationManager.Inform(String.Format("File: {0} deleted", e.FullPath));

            FileBehavior fileBehavior = new FileDeleted(e.FullPath);
            stackMgr.stack.Push(fileBehavior);
        }
    }
}
