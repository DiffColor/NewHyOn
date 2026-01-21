@echo off
:########################################################################### 
:# File name: zilla_start.bat
:# Edited Last By: Mike Gleaves (ric) 
:# V 1.0 19-11-2009
:# Comment: Saves return path and creates current folder as working diectory
:#          Runs FileZilla server in compatible mode (x95)
:#          Restores return path and returns must use exit to return.
:############################################################################

pushd %~dp0

pskill.exe "FileZilla server.exe"
if not errorlevel 1 goto :STARTED

start uniserv.exe "FileZilla Server.exe -compat-start"

:STARTED
popd
exit