using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;

namespace cCloud {
    /// <summary>
    ///   Manages SQL requests and data handling
    /// </summary>
    class SQLiteManager {

        private SQLiteConnection db;

        public SQLiteManager(string connectionStr){
            db = new SQLiteConnection(connectionStr);
            db.Open();
        }
        ~SQLiteManager(){
            db.Close();
            db.Dispose();
        }

        public object[] SingleLine(string strcmd){
            using(SQLiteDataReader reader = GetReaderFromCmd(strcmd)) {
                if(reader.HasRows) {
                    object[] retObjs = new Object[reader.FieldCount];
                    reader.GetValues(retObjs);
                    return retObjs;
                } else {
                    return null;
                }
            }
        }
        public object SingleResult(string strcmd) {
            SQLiteCommand cmd = new SQLiteCommand(strcmd, db);
            return cmd.ExecuteScalar();
        }
        public List<object[]> All(string strcmd) {
            List<object[]> retList = new List<object[]>();
            using(SQLiteDataReader reader = GetReaderFromCmd(strcmd)) {
                while(reader.Read()) {
                    object[] line = new Object[reader.FieldCount];
                    reader.GetValues(line);
                    retList.Add(line);
                }
            }
            return retList;
        }
        public bool ExecNoResult(string strcmd) {
            try {
                SQLiteCommand cmd = new SQLiteCommand(strcmd, db);
                cmd.ExecuteNonQuery();
                return true;
            } catch {
                return false;
            }
        }

        public Dictionary<string, int> GetFieldsMap(string tableName){
            Dictionary<string, int> dico = new Dictionary<string, int>();
            using(SQLiteDataReader reader = GetReaderFromCmd("SELECT * FROM " + tableName)) {
                for(int i = 0; i < reader.FieldCount; i++) {
                    dico.Add(reader.GetName(i), i);
                }
            }
            return dico;
        }

        private SQLiteDataReader GetReaderFromCmd(string strcmd) {
            SQLiteCommand cmd = new SQLiteCommand(strcmd, db);
            return cmd.ExecuteReader();
        }
        
    }
}
