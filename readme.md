High performance .NET FFmpeg Restful Server
=========================== 

### Information
This provides a restful server to create and manage transcoding jobs, this more or less wraps FFmpeg to provide
a high performance transcoding server.

### Usage

## App.config
```xml
  <appSettings>
    <add key="bindhost" value="*"/>
    <add key="bindport" value="5121"/>
    <add key="maxtasks" value="3"/> <!-- Typical one less than the number of cpus/cores you have -->
    <add key="file-root" value="/mnt/media"/>
    <add key="video-destination" value="/videos"/>
    <add key="thumb-destination" value="/thumbs"/>
    <add key="serve-url" value="https://transcoded.myserver.com"/>
    <add key="mode" value="move"/>
    <add key="statsfile" value="/opt/ffrest/ffrest.stats"/>
    <add key="workingdir" value="/var/ffrest/working"/>
    <add key="ffmpeg-location" value="/usr/local/bin/ffmpeg"/>
    <add key="ffprobe-location" value="/usr/local/bin/ffprobe"/>
  </appSettings>
```

## Creating Jobs
```
 POST server:5121 /jobs
 jobid=myJob&taskid=task1&tag=My description&video=https://myserver.com/myfile.mp4&preset=x264&callbackUrl=https://myserver.com/transcodecomplete.php&extension=mp4
```

## Jobs Status
```
 GET server:5121 /jobs
```

### FAQ

**Q: Why doesn't it do X Y Z? Why isn't X feature supported? I found a bug!**

A: Create an issue and I will look into it for sure!

**Q: If you do X you get can get a Y% performance increase!**

A: Not really a questions is it? Also great! Submit a pull request!