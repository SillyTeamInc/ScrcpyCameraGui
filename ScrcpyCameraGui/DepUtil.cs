namespace ScrcpyCameraGui;

using System.Collections.Generic;
using System.Diagnostics;

public class DepUtil
{
    /* required stuff that isn't automatically installed 
     *  adb (android-tools or platform-tools package)
     *  the v4l2loopback kernel module
     *  v4l2loopback-ctl (v4l2loopback-utils package)
     *  pkexec/polkit
    */
    
    private static readonly List<string> RequiredExecutables = new()
    {
        "adb",
        "v4l2loopback-ctl",
        "pkexec"
    };
    
    private static readonly List<string> RequiredKernelModules = new()
    {
        "v4l2loopback"
    };
    
    public static List<string> CheckMissingDependencies()
    {
        var missing = new List<string>();
        
        foreach (var executable in RequiredExecutables)
        {
            if (!IsExecutableAvailable(executable))
            {
                missing.Add($"Executable: {executable}");
            }
        }
        
        foreach (var module in RequiredKernelModules)
        {
            if (!IsKernelModuleLoaded(module))
            {
                missing.Add($"Kernel module: {module}");
            }
        }
        
        return missing;
    }
    
    private static bool IsExecutableAvailable(string executable)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = executable,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            process.WaitForExit();
            
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsKernelModuleLoaded(string moduleName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "lsmod",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            return output.Contains(moduleName);
        }
        catch
        {
            return false;
        }
    }
}