@echo off
:########################################################################### 
:# File name: zilla_stop.bat
:# Edited Last By: Mike Gleaves (ric) 
:# V 1.0 7-4-2009
:# Comment: Saves return path and creates current folder as working diectory
:#          Stops FileZilla server in compatible mode (x95) 
:#          Restores return path and returns must use exit to return.
:############################################################################

pushd %~dp0
"FileZilla Server.exe" -compat-stop

popd
exit