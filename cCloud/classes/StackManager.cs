using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Threading;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Data.SQLite;

namespace cCloud {

    /// <summary>
    ///   Represents a FileBehavior and its stack flag
    /// </summary>
    [Serializable]
    class FileValuePair {
        public FileBehavior file;
        public int flag; // 0 => base, 1 => processing, -1 => to delete
        public FileValuePair(FileBehavior f) {
            file = f;
            flag = 0;
        }
    }

    /// <summary>
    ///   Holds the stack data itself (Items) and associated methods
    ///   It also holds the event triggered when the collection grows
    /// </summary>
    class CustomStack{
        public delegate void CustomStackEventHandler();
        public event CustomStackEventHandler collectionGrows;

        public volatile List<FileValuePair> Items;
        
        public CustomStack(){
            Items = new List<FileValuePair>();
        }
        public void Push(FileBehavior file){

            FileValuePair last = Items.LastOrDefault();

            // If same filename: must be save with different fileDT (LastWriteTime)
            if(last == null || file.fullPath != last.file.fullPath || 
               (file.fileDT - last.file.fileDT).TotalMilliseconds > 400) {
                
                Items.Add(new FileValuePair(file));
                NotificationManager.Inform("Added: " + file.fullPath);
                collectionGrows.Invoke();

            } else {
                NotificationManager.Inform("Doublet detected: " + file.fullPath);
            }
        }
        public FileValuePair FirstBaseFlagged(){
            return Items.ToList().FirstOrDefault(i => i.flag.Equals(0)); // .ToList() => take the current state of Items (not alterable)
        }
        public void MarkActive(int key){
            Items[key].flag = 1;
        }
        public void MarkDeletable(int key) {
            Items[key].flag = -1;
            // As soon as they are deletable -> save in history (means it has been processed)
            writeHistoryEntry(Items[key].file);
        }

        private void writeHistoryEntry(FileBehavior file) {
            string datetime = file.sysTime.ToString("yyyy-MM-dd HH:mm:ss");

            if(!(file is FileIgnored) && !(file is FileRenamed) && !(file is FileDeleted) && !(file is FileMissing)) {
                object res = Init.Singleton.sqlite.SingleResult("SELECT filepath, datetime FROM history WHERE lower(filepath) = '" + file.fullPath.ToLower() + "'");
                if(res != null) {
                    Init.Singleton.sqlite.ExecNoResult("UPDATE history SET datetime = '" + datetime + "', type = '"+ file.GetType().Name +"' WHERE lower(filepath) = '" + file.fullPath.ToLower() + "' ");
                } else {
                    Init.Singleton.sqlite.ExecNoResult("INSERT INTO history(filepath, datetime, type) VALUES ('" + file.fullPath + "', '" + datetime + "', '"+ file.GetType().Name +"')");
                }
            } else if(file is FileRenamed) {
                Init.Singleton.sqlite.ExecNoResult("UPDATE history SET filepath = '" + file.fullPath + "', type = '"+ ((FileRenamed)file).subtype.GetType().Name +"' WHERE lower(filepath) = '" + ((FileRenamed)file).oldPath.ToLower() + "' ");
            } else if(file is FileDeleted) {
                Init.Singleton.sqlite.ExecNoResult("DELETE FROM history WHERE lower(filepath) = '" + file.fullPath.ToLower() + "' ");
            }
        }
    }

    /// <summary>
    ///   Manages the threads that process the file data
    /// </summary>
    static class ThreadManager {
        // The maximum allowed threads to run at the same time
        private const int MAX_ALLOWED_THREADS = 9;

        public delegate void ThreadManagerEventHandler();
        public static event ThreadManagerEventHandler noMoreActiveThread;
        private static int threadCounter = 0;

        private static object _lock1 = new object();
        private static object _lock2 = new object();
       
        public static void retain(){
            lock(_lock1) {
                while(threadCounter >= MAX_ALLOWED_THREADS) {
                    Thread.Sleep(300); // Check every 300ms
                }
                threadCounter++;
            }
        }
        public static void release() {
            lock(_lock2) {
                if(--threadCounter <= 0) {
                    noMoreActiveThread.Invoke();
                }
                NotificationManager.Inform(threadCounter + " active thread(s)");
            }
        }
    }

    /// <summary>
    ///   Its the core of the stack. It manages the dispatch of the stack items into managed threads
    ///   Only one reference of that object is kept in the Init singleton
    /// </summary>
    class StackManager {
        public CustomStack stack = new CustomStack();
        private bool isProcessingStack = false;

        public StackManager(){
            stack.collectionGrows += collectionChanged;
            ThreadManager.noMoreActiveThread += noMoreThreads;
        }
        
        private void collectionChanged(){
            if(!isProcessingStack) {
                isProcessingStack = true;
                Thread t = new Thread(new ThreadStart(processStack));
                t.Priority = ThreadPriority.AboveNormal;
                t.Start();
            }
        }

        // Main function, dispatch the stack into threads
        private void processStack() {
            
            NotificationManager.Inform("Checking internet connection...");
            bool isConnected = Init.Singleton.isNetworkUpAndRunning();
            NotificationManager.Inform(isConnected ? "Connection available" : "No connection available!");

            FileValuePair vp = stack.FirstBaseFlagged();
            while(vp != null){
                int currentKey = stack.Items.IndexOf(vp);
                
                if(isConnected){
                    UISharing.form.notifyIcon1.Icon = new System.Drawing.Icon("res/working.ico");
                    stack.MarkActive(currentKey);
                    Tuple<FileBehavior, int> args = new Tuple<FileBehavior, int>(vp.file, currentKey);

                    Thread processThread = new Thread(new ParameterizedThreadStart(stackItemThread));
                    ThreadManager.retain();
                    NotificationManager.Inform("New thread: " + vp.file.fullPath);
                    processThread.Start(args);
                } else {
                    // SQLite dump
                    StackDB.DBBinarySerialize(vp.file);
                    NotificationManager.Inform("This file has been stored offline: " + vp.file.fullPath);
                    stack.MarkDeletable(currentKey);
                    // Clear the stack
                    if(stack.Items.ToList().Count(i => i.flag != -1) == 0) {
                        NotificationManager.Inform("All offline files processed; will clear the stack");
                        stack.Items.RemoveAll(i => i.flag.Equals(-1));
                    }
                }
                vp = stack.FirstBaseFlagged();
            }
            NotificationManager.Inform("Stack completely dispatched");
            isProcessingStack = false;
        }
        private void stackItemThread(object args) {
            Tuple<FileBehavior, int> fileItem = (Tuple<FileBehavior, int>)args;
            NotificationManager.Inform("*** Started working at " + fileItem.Item1.fullPath + "...");

            bool willContinue = fileItem.Item1.Process();
            if(willContinue) {
                NotificationManager.Inform("*** Successfully finished working at " + fileItem.Item1.fullPath);
                // IMPORTANT thread and stack operations: mark the current item as deletable and release the thread counter
                stack.MarkDeletable(fileItem.Item2);
                ThreadManager.release();
            } else {
                NotificationManager.Inform("*** Cannot process file " + fileItem.Item1.fullPath, NotificationTypes.Warning);
                ThreadManager.release();
            }
        }

        // The stack is completely processed (flag -1)
        private void noMoreThreads(){
            UISharing.form.notifyIcon1.Icon = new System.Drawing.Icon("res/mainicon.ico");
            NotificationManager.Inform("No more alive threads; will clear the stack");
            if(stack.Items.Count(i => i.flag != -1) == 0) {
                stack.Items.RemoveAll(i => i.flag.Equals(-1));
            }
        }

    }
}
