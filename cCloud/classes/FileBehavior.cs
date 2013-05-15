using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace cCloud {

    /// <summary>
    ///   Abstract class representing a file with its Process method (to send it to the server)
    ///   Also contains the filter logic 
    /// </summary>
    [Serializable]
    abstract class FileBehavior {
        delegate FileBehavior FilterOperationDelegate(string path, WatcherChangeTypes type);
        // List ALL the wanted filters here
        // the order is important as it reflects the priority
        private static FilterOperationDelegate[] filtersOrder = {
            new FilterOperationDelegate(FilterIgnored),
            new FilterOperationDelegate(FilterAllowedExt),
            new FilterOperationDelegate(FilterFileSize)
        };

        public string fullPath;
        public WatcherChangeTypes changeType;
        public DateTime fileDT;
        public DateTime sysTime;

        protected FileBehavior(string fp, WatcherChangeTypes ct) {
            fullPath = fp;
            changeType = ct;
            fileDT = new FileInfo(fp).LastWriteTime;
            sysTime = DateTime.Now;
        }
        public abstract bool Process();

        // Browse the filters located in filtersOrder and return the correct FileBehavior or null in case of an Exception
        public static FileBehavior Filter(string fullpath, WatcherChangeTypes type) {
            FileBehavior file;
            foreach(FilterOperationDelegate d in filtersOrder) {
                file = filter(fullpath, type, d);
                if(!(file is FileOK))
                    return file;
            }
            return new FileOK(fullpath, type);
        }

        // Delegate processor
        private static FileBehavior filter(string fullpath, WatcherChangeTypes type, FilterOperationDelegate del) {
            try {
                return del(fullpath, type);
            } catch(FileNotFoundException ex) {
                NotificationManager.Inform("A thread attempted to read a non-existent file: " + ex.FileName, NotificationTypes.Notice);
                return null;
            }
        }

        /*******************************/
        /*    Filter Logic Methods     */
        /*******************************/
        // Any new filter must be implemented here and must be FilterOperationDelegate compliant
        #region Delegate Methods
        protected static FileBehavior FilterIgnored(string path, WatcherChangeTypes type) {
            if(Regex.IsMatch(path, Init.Singleton.JsonData.regexIgnoreFiles, RegexOptions.IgnoreCase) || new FileInfo(path).Length <= 0)
                return new FileIgnored(path, type);
            else
                return new FileOK(path, type);
        }
        protected static FileBehavior FilterAllowedExt(string path, WatcherChangeTypes type) {
            string coreRegex = String.Join("|", Init.Singleton.JsonData.allowedExt);
            if(Regex.IsMatch(path, @".*\.(" + coreRegex + ")$", RegexOptions.IgnoreCase))
                return new FileOK(path, type);
            else
                return new FileExtensionNotAllowed(path, type);
        }
        protected static FileBehavior FilterFileSize(string path, WatcherChangeTypes type) {
            if(new FileInfo(path).Length <= Init.Singleton.JsonData.maxSize)
                return new FileOK(path, type);
            else
                return new FileTooLarge(path, type);
        }
        #endregion
    }

    /*******************************/
    /* FileBehavior implementation */
    /*******************************/
    // Here lies the logic (in Process()) to inform the server of the file
    // The Process() method in called in the file's thread
    // Any new behavior must be implemented here
    #region FileBehavior subclasses

    [Serializable]
    class FileOK:FileBehavior {
        public FileOK(string fp, WatcherChangeTypes ct)
            : base(fp, ct) {
        }
        public override bool Process() {
            NotificationManager.Inform("Send file to server: *" + fullPath);
            return FileUploader.UploadFile(fullPath, new string[,] { 
                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name},
                {"path", fullPath},
                {"datetime", fileDT.ToString("yyyy-MM-dd HH:mm:ss")},
                {"type", "FileOK"}
            });
        }
    }
    [Serializable]
    class FileTooLarge:FileBehavior {
        public FileTooLarge(string fp, WatcherChangeTypes ct)
            : base(fp, ct) {
        }
        public override bool Process() {
            NotificationManager.Inform("File too large *" + fullPath);
            return FileUploader.CustomPOST(new string[,]{
                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name},
                {"notfile", "1"},
                {"path", fullPath},
                {"datetime", fileDT.ToString("yyyy-MM-dd HH:mm:ss")},
                {"type", "FileTooLarge"}
            });
        }
    }
    [Serializable]
    class FileExtensionNotAllowed:FileBehavior {
        public FileExtensionNotAllowed(string fp, WatcherChangeTypes ct)
            : base(fp, ct) {
        }
        public override bool Process() {
            NotificationManager.Inform("Extension not allowed *" + fullPath);
            return FileUploader.CustomPOST(new string[,]{
                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name},
                {"notfile", "1"},
                {"path", fullPath},
                {"datetime", fileDT.ToString("yyyy-MM-dd HH:mm:ss")},
                {"type", "FileExtensionNotAllowed"}
            });
        }
    }
    [Serializable]
    class FileIgnored:FileBehavior {
        public FileIgnored(string fp, WatcherChangeTypes ct)
            : base(fp, ct) {
        }
        public override bool Process() {
            NotificationManager.Inform("File completely ignored *" + fullPath);
            return true;
        }
    }

    [Serializable]
    class FileRenamed:FileBehavior {
        public string oldPath;
        public FileBehavior subtype;
        public FileRenamed(string op, string fp)
            : base(fp, WatcherChangeTypes.Renamed) {
                oldPath = op;
                subtype = Filter(fullPath, WatcherChangeTypes.Renamed);
        }
        public override bool Process() {
            try {
                if((File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory) { // is not directory
                    if(!(subtype is FileIgnored)) {

                        // If the new type is FileOK and not the same as the old type, we send the file
                        if(subtype is FileOK && FilterAllowedExt(oldPath, WatcherChangeTypes.Renamed) is FileExtensionNotAllowed) {
                            return FileUploader.UploadFile(fullPath, new string[,]{
                                {"rename", "1"},
                                {"from", oldPath},
                                {"to", fullPath},
                                {"datetime", fileDT.ToString("yyyy-MM-dd HH:mm:ss")},
                                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name},
                                {"type", subtype.ToString().Split('.')[1]}
                            });
                        } else {
                            return FileUploader.CustomPOST(new string[,]{
                                {"rename", "1"},
                                {"from", oldPath},
                                {"to", fullPath},
                                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name},
                                {"type", subtype.ToString().Split('.')[1]}
                            });
                        }
                    } else return true;

                } else {
                    return FileUploader.CustomPOST(new string[,]{
                        {"rename", "1"},
                        {"from", oldPath},
                        {"to", fullPath},
                        {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name},
                        {"type", "FolderRenamed"}
                    });
                }
            } catch { // Exception raised if a temp file is deleted just before processed
                NotificationManager.Inform("File access aborted (mainly caused by MS Office temp files)");
                return true;
            }
        }
    }
    [Serializable]
    class FileDeleted:FileBehavior {
        public FileDeleted(string fp)
            : base(fp, WatcherChangeTypes.Deleted) {
        }
        public override bool Process() {
            return FileUploader.CustomPOST(new string[,]{
                {"delete", "1"},
                {"delpath", fullPath},
                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name}
            });
        }
    }

    [Serializable]
    class FileMissing:FileBehavior {
        public FileMissing(string fp)
            : base(fp, WatcherChangeTypes.Deleted) {
        }
        public override bool Process() {
            return FileUploader.CustomPOST(new string[,]{
                {"setMissing", "1"},
                {"missingPath", fullPath},
                {"user", System.Security.Principal.WindowsIdentity.GetCurrent().Name}
            });
        }
    }
    
    #endregion

}
