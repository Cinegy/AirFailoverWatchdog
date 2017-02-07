# AirFailoverWatchdog

This tool is designed to allow a Cinegy Air Control + Engine system to monitor other playouts, and in the event of a failure of a multicast stream from any monitored box to deploy the last-known-good playlist from that failed machine to itself. 

##What can I do with it?

You can set up a single machine to keep an eye on other machines - normally if you did not make a primary / secondary cluster pair for a channel in the first place. Then, if the IP stream fails from any monitored engine, your 'hot spare' machine will take the last playlist that worked (the .BAK playlist) and bring it up and start playing. Of course, secondary events or configurations which required discreet connections to your original engine will not follow (e.g. SDI or GPIs) but anything without a strong binding should just work (files, graphics, IP-based live items).

##Sounds too good to be true!

It's a decent, simple solution that is designed to be reliable rather than overly complex. That means it is not particularly full of features - but it should be bulletproof!


##Getting the tool

Just to make your life easier, we auto-build this using AppVeyor - here is how we are doing right now: 

[![Build status](https://ci.appveyor.com/api/projects/status/3ea6ew30q6gsmoh9?svg=true)](https://ci.appveyor.com/project/cinegy/airfailoverwatchdog)

You can check out the latest compiled binary from the master or pre-master code here:

[AppVeyor AirFailoverWatchdog Project Builder](https://ci.appveyor.com/project/cinegy/airfailoverwatchdog/build/artifacts)