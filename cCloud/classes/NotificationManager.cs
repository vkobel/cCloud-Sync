using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace cCloud {
    enum NotificationTypes { Notice, Warning, FATAL };
    
    /// <summary>
    ///   Simple notification class (display, log...).
    /// </summary>
    static class NotificationManager {

        private static object _lock = new object();

        public static void Inform(string message, NotificationTypes notifType=NotificationTypes.Notice, bool isModal=false){

            printMsg(notifType.ToString(), message);

            if(isModal)
                MessageBox.Show(message, notifType.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if(notifType == NotificationTypes.FATAL) {
                MessageBox.Show(message + "\nL'application va maintenant s'arrêter de fonctionner.", "Erreur critique", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Process.GetCurrentProcess().Kill();
            }
        }

        private static void printMsg(string notifType, string message) {

            string completeMsg = String.Format("* [{0}] {1} : {2}\r\n", notifType, DateTime.Now.ToString("dd.MM HH:mm:ss.fff"), message);
            
            UISharing.form.Invoke((MethodInvoker)delegate {
                UISharing.form.txtBxLog.AppendText(completeMsg);
            });

            // Log file
            TextWriter tw;
            bool append = true;
            if(new FileInfo("application.log").Length > 26214400) // 25MB
                append = false;

            // Mutex for file writing
            lock(_lock) {
                tw = new StreamWriter("application.log", append);
                tw.WriteLine(completeMsg.Substring(0, completeMsg.Length - 2));
                tw.Close();
            }
        }
    }
}
