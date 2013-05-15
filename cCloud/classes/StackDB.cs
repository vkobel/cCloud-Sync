using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Data;

namespace cCloud {
    /// <summary>
    ///   Manages all the interaction with the database stack (offline stack).
    ///   Contains methods to serialize/deserialize the FileBehavior objects
    /// </summary>
    static class StackDB {

        static public void LoadAndProcess(){
            StackManager stackMgr = Init.Singleton.stackManager;
            List<FileBehavior> offlineFiles = null;
            try {
                offlineFiles = DBBinaryDeserialize();
                Init.Singleton.sqlite.ExecNoResult("DELETE FROM stack");
            } catch(Exception ex) {
                NotificationManager.Inform("Offline deserialization error: " + ex.Message, NotificationTypes.FATAL);
            }

            // offlineFiles can't be null, due to FATAL notification in the catch above
            if(offlineFiles.Count > 0) {
                NotificationManager.Inform("Offline database will now be processed...");
                foreach(FileBehavior f in offlineFiles) {
                    stackMgr.stack.Push(f);
                }
            }
        }

        static public void DBBinarySerialize(FileBehavior target) {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, target);

            SQLiteConnection db = new SQLiteConnection(Init.Singleton.sqliteConnectionString);
            db.Open();

            SQLiteCommand command = new SQLiteCommand(db);
            command.CommandText = "INSERT INTO stack(filename, binaryfile) VALUES ('" + target.fullPath + "', @blob)";
            command.Parameters.Add("@blob", DbType.Binary).Value = ms.ToArray();
            command.ExecuteNonQuery();
            db.Close();
            ms.Close();
        }

        static private List<FileBehavior> DBBinaryDeserialize() {
            SQLiteConnection db = new SQLiteConnection(Init.Singleton.sqliteConnectionString);
            List<FileBehavior> lvp = new List<FileBehavior>();
            db.Open();
            SQLiteCommand command = new SQLiteCommand(db);
            command.CommandText = "SELECT binaryfile FROM stack GROUP BY filename";
            byte[] buffer = null;
            using(var reader = command.ExecuteReader()){
                while(reader.Read()){
                    buffer = GetBytes(reader);
                    MemoryStream ms = new MemoryStream(buffer);
                    BinaryFormatter formatter = new BinaryFormatter();
                    lvp.Add((FileBehavior)formatter.Deserialize(ms));
                }
            }
            return lvp;
        }
        // Read bytes for a SQLite reader
        static private byte[] GetBytes(SQLiteDataReader reader) {
            const int CHUNK_SIZE = 2 * 1024;
            byte[] buffer = new byte[CHUNK_SIZE];
            long bytesRead;
            long fieldOffset = 0;
            using(MemoryStream stream = new MemoryStream()) {
                while((bytesRead = reader.GetBytes(0, fieldOffset, buffer, 0, buffer.Length)) > 0) {
                    byte[] actualRead = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, actualRead, 0, (int)bytesRead);
                    stream.Write(actualRead, 0, actualRead.Length);
                    fieldOffset += bytesRead;
                }
                return stream.ToArray();
            }
        }
    }
}
