using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Reflection;
using System.Configuration;
using System.Diagnostics;
using System.Deployment.Application;

namespace cCloud {
    
    /// <summary>
    ///   Represent the JSON data (bind the JSON keys into)
    /// </summary>
    // When adding or removing data from PHP file, just update here, nowhere else.
    // Fields MUST have the same name as the JSON properties
    class InitData {
        public string[] folders;
        public string[] allowedExt;
        public string regexIgnoreFiles;
        public int maxSize;
        public List<string> unauthorizedUsers;
    }

    /// <summary>
    ///    Holds a reference to the UI form for all the classes to access it
    /// </summary>
    static class UISharing {
        public static Form1 form;
    }

    /// <summary>
    ///   Singleton init class
    ///   It contains all shared single-referenced objects of the application
    /// </summary>
    sealed class Init{
        
        private static readonly Init SINGLETON = new Init(); // As soon as the class is referenced it creates the singleton (variable initializer)
        
        private bool isNetworkAvailable;
        private string initURL;
        private bool isJSONFromDB = false;

        public string sqliteConnectionString;
        public SQLiteManager sqlite;
        public StackManager stackManager;
        public string uploadURL;
        public NetworkCredential netCredentials;
        public InitData JsonData;

        #region Read only properties
        public static Init Singleton {
            get {
                return SINGLETON;
            }
        }
        public bool IsNetworkAvailable {
            get {
                return isNetworkAvailable;
            }
        }
        #endregion

        private Init() {

            ServicePointManager.ServerCertificateValidationCallback = Init.ValidateSSLCertificate; // Allows self-signed SSL certificates
            
            // From app.config
            initURL = ConfigurationManager.AppSettings["initURL"];
            uploadURL = ConfigurationManager.AppSettings["uploadURL"];
            netCredentials = new NetworkCredential(ConfigurationManager.AppSettings["userNetCredentials"], 
                                                   ConfigurationManager.AppSettings["passwordNetCredentials"]);
            
            #if DEBUG
            sqliteConnectionString = "Data Source=data/AppDB.s3db";
            #else
            sqliteConnectionString = "Data Source=" + ApplicationDeployment.CurrentDeployment.DataDirectory + @"\data\AppDB.s3db";
            #endif

            isNetworkAvailable = isNetworkUpAndRunning(NetworkInterface.GetIsNetworkAvailable());
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetChanged);

            sqlite = new SQLiteManager(sqliteConnectionString);

            #region JSON init part
            string initJSON = null;
            if(isNetworkAvailable){
                try {
                    // Get the JSON initialization data
                    WebClient webClient = new WebClient() {
                        Credentials = netCredentials
                    };
                    initJSON = new StreamReader(webClient.OpenRead(initURL)).ReadToEnd();

                } catch(Exception e) {
                    NotificationManager.Inform("WebClient read error " + e.Message, NotificationTypes.Warning);
                    initJSON = getJSONFromDB("WebClient read error");
                }
            } else {
                NotificationManager.Inform("Network not available!");
                // Get rules from SQLite
                initJSON = getJSONFromDB("Network not available");
            }

            processJSON(initJSON);

            if(isNetworkAvailable && !isJSONFromDB){
                // If everything's ok, save json string in SQLite
                if(sqlite.SingleResult("SELECT jsonstring FROM rule") == null)
                    sqlite.ExecNoResult("INSERT INTO rule(jsonstring) VALUES ('" + initJSON + "')");
                else
                    sqlite.ExecNoResult("UPDATE rule SET jsonstring = '" + initJSON + "', lastupdated = CURRENT_TIMESTAMP");
                NotificationManager.Inform("New valid JSON string has been stored in DB", NotificationTypes.Notice);
            }
            #endregion

            if(JsonData.unauthorizedUsers.Contains(System.Security.Principal.WindowsIdentity.GetCurrent().Name)) {
                NotificationManager.Inform("Vous n'êtes pas autorisé à utiliser cette application !", NotificationTypes.FATAL);
                Process.GetCurrentProcess().Kill(); // Already done in NotificationManager, but just a security
            }

            stackManager = new StackManager();
        }

        #region JSON related functions
        private void processJSON(string initJSON) {
            try {
                // InitData initialization
                JsonData = new JavaScriptSerializer().Deserialize<InitData>(initJSON);
                checkData();
            } catch(Exception e) {
                if(!isJSONFromDB){ // If it don't work, try to use DB JSON string
                    processJSON(getJSONFromDB("Deserialization error"));
                    return;
                } 
                NotificationManager.Inform("JSON deserialization error! " + e.Message, NotificationTypes.FATAL);
            }
        }
        private void checkData(){
            // When loaded, check if every field is filled
            foreach(FieldInfo info in JsonData.GetType().GetFields()) {
                if(isDefaultValue(info.GetValue(JsonData))) {
                    if(!isJSONFromDB){ // If it don't work, try to use DB JSON string
                        processJSON(getJSONFromDB("JSON data is not reflecting the current configuration"));
                        return;
                    }
                    NotificationManager.Inform("JSON data is not reflecting the current configuration! First concerned field: " + info.Name, NotificationTypes.FATAL);
                }
            }
        }
        private string getJSONFromDB(string cause=""){
            // Get rules from SQLite
            object resJson = sqlite.SingleResult("SELECT jsonstring FROM rule");
            if(resJson == null)
                NotificationManager.Inform("Cannot get the rules!", NotificationTypes.FATAL);
            isJSONFromDB = true;
            string msg = "JSON string from DB will be used.";
            if(cause != "")
                msg += " Cause: " + cause;
            NotificationManager.Inform(msg, NotificationTypes.Warning);
            return resJson.ToString();
        }
        #endregion

        public void checkOfflineStack() {
            // Check if offline stack contains something
            var stackContents = sqlite.SingleResult("SELECT * FROM stack");
            if(stackContents != null) {
                NotificationManager.Inform("Offline stack will now be processed...");
                StackDB.LoadAndProcess();
            }
        }

        // Event 
        private void NetChanged(Object sender, NetworkAvailabilityEventArgs e) {
            isNetworkAvailable = isNetworkUpAndRunning(e.IsAvailable);
            NotificationManager.Inform("Network is now " + (isNetworkAvailable ? "available" : "not available"), NotificationTypes.Warning);
            if(isNetworkAvailable) {
                StackDB.LoadAndProcess();
            }
        }

        public bool isNetworkUpAndRunning(bool firstCheck=true) {

            if(firstCheck) {
                WebRequest webReq = WebRequest.Create(initURL);
                webReq.Credentials = netCredentials;
                webReq.Timeout = 5000;
                WebResponse webResp;
                try {
                    webResp = webReq.GetResponse();
                    webResp.Close();
                    webReq = null;
                    return true;
                } catch {
                    webReq = null;
                    return false;
                }
            } else {
                return false;
            }
        }

        // Empty method to be called for the initialization of the singleton
        public static void create() { }

        private static bool isDefaultValue<T>(T val) {
            if(val is ValueType) {
                object obj = Activator.CreateInstance(val.GetType());
                return obj.Equals(val);
            }
            return object.Equals(val, default(T));
        }
        private static bool ValidateSSLCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            // All certificates are considered valid, no matter what
            return true;
        }
    }
}
