<?php
require_once("./paths.php");
$path = $STATUS_PATH;
$json = json_decode(file_get_contents($path), true);
$json["begin"] = false;
$json["readyDevices"] = 0;
$json = json_encode($json);
file_put_contents($path, $json);
print_r($json);
?>