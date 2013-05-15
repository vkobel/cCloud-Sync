<?php

session_start();
require_once(dirname(__FILE__) . "/config.inc.php");

$GLOBALS['DB'] = new PDO(SQL_TYPE . ':sslmode=disable;dbname=' . SQL_DATABASE . ';host=' . SQL_SERVER . ';', SQL_USER, SQL_PASSWORD);

function DB() {
    return $GLOBALS['DB'];
}

function SQLLine($SQL, $oneResult = false) {
    if (SQL_DEBUG) UDPDebug($SQL);
    try {
        $DS = DB()->query($SQL);
        if($DS == null)
         return null;
        $Result = $DS->fetch();
        if($oneResult)
           return $Result[0];
        return $Result;
    } catch (PDOException $e) {
        Debug($e->getMessage());
        return null;
    }
}

function SQLArray($SQL) {
    if (SQL_DEBUG) UDPDebug($SQL);
    return DB()->query($SQL);
}

function SQLExec($SQL, $seqName='') {
    //Debug($SQL);
    if (SQL_DEBUG) UDPDebug($SQL);
    DB()->exec($SQL);
    if($seqName != '')
       return DB()->lastInsertId($seqName);
}

function getLastID($seqName){
   return DB()->lastInsertId($seqName);
}

function UDPDebug($txt) {
    $fp = fsockopen("udp://127.0.0.1", 2000, $errno, $errstr);
    if ($fp) {
        fputs($fp, "$txt\r\n");
        //echo "Envoyé"; 
    } else {
        echo "ERREUR: $errno - $errstr<br />\n";
    }
}

function myErrorHandler($errno, $errstr, $errfile, $errline) {
    $msg = "ERROR : $errstr - $errno - $errfile / $errline";
    UDPDebug($msg);
}

//set_error_handler("myErrorHandler");

UDPDebug("-----------------------------");
?>
