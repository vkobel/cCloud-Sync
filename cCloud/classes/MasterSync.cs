using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace cCloud {
    /// <summary>
    ///   Holds the synchronization logic of a main folder
    /// </summary>
    static class MasterSync {

        // Checks recursively each file with the SQLite DB 
        public static void ProcessDir(string sourceDir) {
            NotificationManager.Inform("Scan and synchronization will now start...");
            checkFilesToDB(sourceDir);
            checkDBToFiles();
        }

        private static void checkDBToFiles() {
            // Scan from SQLite table => compare to files
            foreach(object[] itm in Init.Singleton.sqlite.All("SELECT filepath FROM history")) {
                if(!File.Exists(itm[0].ToString())) {
                    NotificationManager.Inform("File " + itm[0] + " is missing");
                    Init.Singleton.stackManager.stack.Push(new FileMissing(itm[0].ToString()));
                    Init.Singleton.sqlite.ExecNoResult("DELETE FROM history WHERE lower(filepath) = '" + itm[0].ToString().ToLower() + "'");
                }
            }
        }

        private static void checkFilesToDB(string sourceDir) {

            string[] fileEntries = Directory.GetFiles(sourceDir);
            
            // Scan from file => compare to SQLite table
            foreach(string fileName in fileEntries) {
                KeyValuePair<int, FileBehavior> typeOfFile = getTypeOfFile(fileName);
                if(typeOfFile.Value != null) {
                    if(typeOfFile.Key >= 0) {
                        NotificationManager.Inform("File: " + fileName + " has been " + typeOfFile.Value.changeType);
                        Init.Singleton.stackManager.stack.Push(typeOfFile.Value);
                    } else {
                        NotificationManager.Inform("File: " + fileName + " is up to date");
                    }
                }
            }

            // Recurse into subdirectories of this directory.
            string[] subdirEntries = Directory.GetDirectories(sourceDir);
            foreach(string subdir in subdirEntries) {
                // Do not iterate through reparse points
                if((File.GetAttributes(subdir) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint) {
                    checkFilesToDB(subdir);
                }
            }
        }

        private static KeyValuePair<int, FileBehavior> getTypeOfFile(string path) {
            object[] res = Init.Singleton.sqlite.SingleLine("SELECT filepath, datetime, type FROM history WHERE lower(filepath) = '" + path.ToLower() + "'");
            FileBehavior checkedFile = FileBehavior.Filter(path, WatcherChangeTypes.Changed);

            if(res != null) {
                FileInfo inf = new FileInfo(path);
                DateTime dbDatetime = DateTime.Parse(res[1].ToString());
                int secSpan = (int)(inf.LastWriteTime - dbDatetime).TotalSeconds;
                
                if(res[2].ToString() != checkedFile.GetType().Name)
                    return new KeyValuePair<int,FileBehavior>(2, checkedFile);
                
                if(secSpan > 0)
                    return new KeyValuePair<int, FileBehavior>(0, checkedFile); // updated file
                else
                    return new KeyValuePair<int, FileBehavior>(-1, checkedFile); // existing file
            } else {
                checkedFile.changeType = WatcherChangeTypes.Created;
                return new KeyValuePair<int, FileBehavior>(1, checkedFile); // new file
            }
        }
        
    }
}
