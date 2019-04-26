### Linux Performance Tracing for dotnet core on Linux. 
 (※) This forced version support Amazon Linux 2

   You need read this guideline at first.
   https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/linux-performance-tracing.md


### Download shell script
```bash
   $curl -OL  https://raw.githubusercontent.com/lbthanh/corefx-tools/master/src/performance/perfcollect/perfcollect
   $chmod +x perfcollect
```

### Install
    $sudo ./perfcollect install

### Run

    Collect data from the specified pid
```bash
    $sudo ./perfcollect collect <trace_name> -pid ??? 
```

    Collect context switch events
```bash
    $sudo ./perfcollect collect <trace_name> -threadtime
```

### View result
    lttng view
 ```bash
    $sudo ./perfcollect view testtrace.trace.zip --viewer=lttng
```

    perf view
```bash
    $sudo ./perfcollect view testtrace.trace.zip --viewer=perf  # default view
```

### Notes:
   * Enables tracing configuration inside of CoreCLR. Run it before collect data
``` bash
       export COMPlus_PerfMapEnabled=1
       export COMPlus_EnableEventLog=1
```

   * Solving error of "Crossgen not found. Framework symbols will be unavailable."
   Follow [this guide](https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/linux-performance-tracing.md#resolving-framework-symbols) for detail. 

    Download the CoreCLR nuget package.
``` bash 
        dotnet publish --self-contained -r linux-x64
``` 

    Copy crossgen next to libcoreclr.so
``` bash 
        sudo cp ~/.nuget/packages/runtime.linux-x64.microsoft.netcore.app/<version>/tools/crossgen /usr/share/dotnet/shared/Microsoft.NETCore.App/<version>/
```

* For running application, you need new dotnet project then copy crossgen file
```bash
        mkdir /tmp/dotnetsample
        cd /tmp/dotnetsample
        dotnet new webapi
        dotnet restore
        dotnet publish --self-contained -r linux-x64

        sudo cp ~/.nuget/packages/runtime.linux-x64.microsoft.netcore.app/<version>/tools/crossgen /usr/share/dotnet/shared/Microsoft.NETCore.App/<version>/
```
