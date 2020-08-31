<?php

/**
 * Include every single file in the autoinclude directory.
 * */
$location = __DIR__ . "/autoinclude/*.php";
foreach (glob($location) as $filename) {
  include_once $filename;
}
