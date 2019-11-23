<?php
    require_once("./paths.php");
    $path = $CONFIG_PATH;
    $json = file_get_contents($path);
    print_r($json);
?>