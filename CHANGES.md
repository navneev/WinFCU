<p align="center">
  <img src="logo\Total.png" alt="Total Productions"/>
</p>

# WinFCU
WinFCU is a rule based utility which keeps the file system clean by archiving/deleting/moving unwanted/unneeded files from the file system

WinFCU can be run interactivly, as a scheduled task or as a service

## V2.4.15.0

Replacement keyword handling has been rewritten. Purpose is to create auto-documentation of the keywords.  
WinFCU.exe now has a '-show keywords' option which will show all default, application and user defined keywords including a description and value example per keyword.

- Changes to WinFCU.exe.config  
  `<includeFiles>` is now a multi element XML node (aka multiple `<path=.... />` elements can be provided)  
  `<keyWords>` elements now have next to the key and value attributes a description attribute (see 'purpose' above)  

## 2.4.16.0

Fixed a bug with reloading schedules  
Added some example data to the WinFCU.exe.config file  

## 2.4.16.1

Fixed `-show keywords` output formatting  
Stripped version from installer filename  
Fixed LogFiles folder spec in example file  

## 2.4.16.2

Clear total counts before a run starts  

## 2.4.17.x

Made exclude into a inherritable default attribute  
Installer can install basic include files  
Fix `-show status` output (added default exclude status)  
Fix error in counting deleted bytes  
Fix PathTooLong breaking error, fullname of offerender is logged  
Fix typos in config files

## 2.4.18.x

Code refactoring, eliminating obsolete pieces  
Service handling improved, now also start, stop, restart and status (requires elevation!)

## 2.4.19.x

More code refactoring  
- Include files now can have a system attribute in the `<applicationSettings>` root node, limiting the inclusion of these files to matching systems only `<applicationSettings system="^myserver-[abc]$" >`  
- Include files now can contain keyword and schedule definitions, allowing to limit these definitions to the content of these files only  
- `-show status` will show all include files found, but marks excluded files with a preceeding '*'  
  
For include file changes see `WinFCU_SharePoint.config` example file (only in repo, not part of the installer!)  
  
Installer changes  
- The installer no longer creates a StartMenu item  
- The installer now uses sc.exe to create/delete/start/stop the WinFCU service  
- Updated WinFCU_Users.config, cleanup CrashDumps folder

Bug Fix  
- Collecting file information could generate an uncatched IOException  

New functionality  
- Added LongPathsEnabled detection, issue a warning when not enabled  

## 2.4.20.2

New functionality  
- A schedule can be strict or not (strict=[true/false]). A non-strict schedule will add a delta (based on the system mac address) to the startime. This will allow for distribution of load when running in an environment with multiple servers and shared storage.  
