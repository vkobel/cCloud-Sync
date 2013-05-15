<?php    
   include_once "lib/common.db.inc.php";
   //error_reporting(0);
   
   define("SERVER_BASE_FOLDER", "cCloudFiles/");
   
   function saveFile($fileid, $path, $datetime){
      // Save the file
      $uid = uniqid($fileid);
      if(move_uploaded_file($_FILES["thefile"]["tmp_name"], SERVER_BASE_FOLDER . $uid)){
         SQLExec("INSERT INTO ccloud.data(uniquefile, datetime, fk_fileid) VALUES ('$uid', '$datetime', $fileid)");
         echo "File $path successfully saved";
         return true;
      }else{
         echo "\nERROR on file $path (move_uploaded_file)";
         return false;
      }
   }
   
   if(isset($_POST["path"])){
   
      $user = pg_escape_string($_POST["user"]);
      $path = pg_escape_string($_POST["path"]);
      $datetime = pg_escape_string($_POST["datetime"]);
      $type = pg_escape_string($_POST["type"]);
      
      $fileid = SQLLine("SELECT id FROM ccloud.file WHERE path = '$path' AND \"user\" = '$user'", true);
      SQLExec("BEGIN");
      if($fileid == null)
         $fileid = SQLExec("INSERT INTO ccloud.file(path, \"user\", type) VALUES ('$path', '$user', '$type')", "ccloud.file_id_seq");
      else
         SQLExec("UPDATE ccloud.file SET status = 'OK', renamed_id = NULL, type = '$type' WHERE id = $fileid");
      
      // Need to create a file (aka FileOK) ?
      if(isset($_FILES["thefile"])){
         // SQL Transaction: if file is not successfully save => rollback
         if(saveFile($fileid, $path, $datetime))
            SQLExec("COMMIT");
         else
            SQLExec("ROLLBACK");
      }else{
         SQLExec("COMMIT");
         echo "File $path processed but not saved";
      }
      
   }else if(isset($_POST["delete"])){
      $delpath = pg_escape_string($_POST["delpath"]);
      SQLExec("UPDATE ccloud.file SET status = 'Deleted' WHERE path = '$delpath'");
      echo "Delete successful: $delpath";
   
   }else if(isset($_POST["rename"])){
      $newPath = pg_escape_string($_POST["to"]);
      $oldPath = pg_escape_string($_POST["from"]);
      
      $user = pg_escape_string($_POST["user"]);
      $type = pg_escape_string($_POST["type"]);
      
      if($type == "FolderRenamed"){
         $res = SQLArray("SELECT id, path FROM ccloud.file WHERE \"user\" = '$user' AND path @@ '$oldPath\\*'");
         if($res != NULL){
            while($result = $res->fetch()){
               $newReplacedPath = str_replace($oldPath, $newPath, pg_escape_string($result[1]));
               SQLExec("UPDATE ccloud.file SET path = '$newReplacedPath' WHERE id = " . $result[0]);
            }
         }
      }else{
         // Create new entry in file table + change fk_fileid old du last (in time) data file for the new one
         $oldFileId = SQLLine("SELECT id FROM ccloud.file WHERE path = '$oldPath'", true);
         
         $fileid = SQLLine("SELECT id FROM ccloud.file WHERE \"user\" = '$user' AND path = '$newPath'", true);
         SQLExec("BEGIN");
         if($fileid == null)
            $fileid = SQLExec("INSERT INTO ccloud.file(path, \"user\", type) VALUES ('$newPath', '$user', '$type')", "ccloud.file_id_seq");
         else
            SQLExec("UPDATE ccloud.file SET status = 'OK', renamed_id = NULL WHERE id = $fileid");
        
         SQLExec("UPDATE ccloud.file SET status = 'Renamed', renamed_id = $fileid WHERE id = $oldFileId");
         
         if($type == "FileOK")
            SQLExec("UPDATE ccloud.data SET fk_fileid = $fileid WHERE fk_fileid = $oldFileId AND datetime IN (SELECT MAX(datetime) FROM ccloud.data WHERE fk_fileid = $oldFileId)");

         // Need to save a file ?
         if(isset($_FILES["thefile"])){
            $datetime = pg_escape_string($_POST["datetime"]);
            if(saveFile($fileid, $newPath, $datetime))
               SQLExec("COMMIT");
            else
               SQLExec("ROLLBACK");
         }else
            SQLExec("COMMIT");
      }      
      echo "Rename done: $oldPath => $newPath";
    
   }else if(isset($_POST["setMissing"])){
      $filePath = pg_escape_string($_POST["missingPath"]);
      $user = pg_escape_string($_POST["user"]);
      
      SQLExec("UPDATE ccloud.file SET status = 'Missing' WHERE \"user\" = '$user' AND path = '$filePath'");
      echo "File $filePath successfully reported as missing";
      
   }else{
      print_r($_POST);
      echo "Info: no file has been sent, only POST data...";
   }
   
?>
