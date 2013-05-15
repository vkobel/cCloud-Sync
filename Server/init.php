<?php

   // from http://www.fileinfo.com/filetypes/common
   $allowedExt = array(
                    "doc", "docx", "rtf", "txt", "pdf",
                    "xls", "xlsx",
                    "csv", "pps", "ppt", "pptx", "vcf", "xml",
                    "gif", "jpg", "png",
                    "7z", "zip", "rar",
                    "accdb", "db", "dbf", "mdb",
                    "bat", "exe"
                 );
   
   $regexIgnoreFiles = ".*\.tmp$|~.*|^[^.]*$";  // files with no extension, .tmp extension, and contain a tilde ~
   
   // TODO: parser les nom de dossier standard
   $foldersToSync = array("c:\watched", 
                          "c:\watched2");
   
   $maxSize = 5242880; // in bytes (5MB)
   
   $unauthorizedUsers = array("johnnybravo", "johnwayne");
   
   
   // Send output in JSON format
   $init = array("allowedExt" => $allowedExt,
                 "regexIgnoreFiles" => $regexIgnoreFiles,
                 "folders" => $foldersToSync,
                 "maxSize" => $maxSize,
                 "unauthorizedUsers" => $unauthorizedUsers
                );             
   echo json_encode($init);
?>