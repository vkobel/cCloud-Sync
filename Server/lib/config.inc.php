<?php
// DEV
define("SQL_DEBUG", false);

// SQL
define("SQL_TYPE", "pgsql");
//define("SQL_TYPE", "mysql");

define("SQL_SERVER", "");
define("SQL_DATABASE", "");
define("SQL_USER", "");
define("SQL_PASSWORD", "");
define("SQL_SEPARATOR", ((SQL_TYPE == "mysql") ? "_" : "."));
    
?>
