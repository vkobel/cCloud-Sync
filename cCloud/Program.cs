using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace cCloud {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {

            bool ok;
            System.Threading.Mutex m = new System.Threading.Mutex(true, "SingleInstanceMutex", out ok);
            if(!ok) {
                MessageBox.Show("Seule une instance du cCloud Client est autorisée !", "cCloud Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
