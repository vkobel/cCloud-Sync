using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

namespace cCloud {
    public partial class Form1:Form {

        bool closeNotif = false;
        
        public Form1() {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            UISharing.form = this;  // Share the UI with all classes. Must be the FIRST thing !
        }

        private void Form1_Shown(object sender, EventArgs e) {

            //NotificationManager.Inform("========== Welcome to cCloud Client ==========");

            Init.create(); // Init referencing, varaibles initializers are called (just one in this case)
            Init.Singleton.checkOfflineStack();

            foreach(string path in Init.Singleton.JsonData.folders) {
                new Watcher(path).Start();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e) {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }
        private void ouvrirToolStripMenuItem_Click(object sender, EventArgs e) {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void Form1_SizeChanged(object sender, EventArgs e) {
            if(this.WindowState == FormWindowState.Minimized) {
                this.ShowInTaskbar = false;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            if(!closeNotif) {
                DialogResult r = MessageBox.Show("Êtes-vous sûr de vouloir fermer l'application ?\n\rLa fermeture entrainera l'arrêt de la synchronisation.", "Fermeture",
                                                 MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if(r != System.Windows.Forms.DialogResult.Yes)
                    e.Cancel = true;
            }
        }
        private void fermerToolStripMenuItem_Click(object sender, EventArgs e) {
            DialogResult r = MessageBox.Show("Êtes-vous sûr de vouloir fermer l'application ?\n\rLa fermeture entrainera l'arrêt de la synchronisation.", "Fermeture",
                                            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if(r == System.Windows.Forms.DialogResult.Yes) {
                closeNotif = true;
                Application.Exit();
            }
        }

    }
}
