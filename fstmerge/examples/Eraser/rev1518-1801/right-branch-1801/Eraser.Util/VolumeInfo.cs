

using System;
using System.Collections.Generic;
using System.Text;

using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Collections.ObjectModel;

namespace Eraser.Util
{
 public class VolumeInfo
 {





  public VolumeInfo(string volumeId)
  {

   if (!(volumeId.StartsWith("\\\\?\\") || volumeId.StartsWith("\\\\")))
    throw new ArgumentException("The volumeId parameter only accepts volume GUID " +
     "and UNC paths", "volumeId");


   if (!volumeId.EndsWith("\\"))
    throw new ArgumentException("The volumeId parameter must end with a trailing " +
     "backslash.", "volumeId");


   VolumeId = volumeId;


   StringBuilder volumeName = new StringBuilder(NativeMethods.MaxPath * sizeof(char)),
    fileSystemName = new StringBuilder(NativeMethods.MaxPath * sizeof(char));
   uint serialNumber, maxComponentLength, filesystemFlags;
   if (NativeMethods.GetVolumeInformation(volumeId, volumeName, NativeMethods.MaxPath,
    out serialNumber, out maxComponentLength, out filesystemFlags, fileSystemName,
    NativeMethods.MaxPath))
   {
    IsReady = true;
   }



   VolumeLabel = volumeName.Length == 0 ? null : volumeName.ToString();
   VolumeFormat = fileSystemName.Length == 0 ? null : fileSystemName.ToString();


   if (VolumeFormat == "FAT")
   {
    uint clusterSize, sectorSize, freeClusters, totalClusters;
    if (NativeMethods.GetDiskFreeSpace(VolumeId, out clusterSize,
     out sectorSize, out freeClusters, out totalClusters))
    {
     if (totalClusters <= 0xFF0)
      VolumeFormat += "12";
     else
      VolumeFormat += "16";
    }
   }
  }





  private List<string> GetLocalVolumeMountPoints()
  {
   List<string> result = new List<string>();


   string pathNames;
   {
    uint returnLength = 0;
    StringBuilder pathNamesBuffer = new StringBuilder();
    pathNamesBuffer.EnsureCapacity(NativeMethods.MaxPath);
    while (!NativeMethods.GetVolumePathNamesForVolumeName(VolumeId,
     pathNamesBuffer, (uint)pathNamesBuffer.Capacity, out returnLength))
    {
     int errorCode = Marshal.GetLastWin32Error();
     switch (errorCode)
     {
      case Win32ErrorCode.MoreData:
       pathNamesBuffer.EnsureCapacity((int)returnLength);
       break;
      default:
       throw Win32ErrorCode.GetExceptionForWin32Error(errorCode);
     }
    }

    if (pathNamesBuffer.Length < returnLength)
     pathNamesBuffer.Length = (int)returnLength;
    pathNames = pathNamesBuffer.ToString().Substring(0, (int)returnLength);
   }





   for (int lastIndex = 0, i = 0; i != pathNames.Length; ++i)
   {
    if (pathNames[i] == '\0')
    {


     if (i - lastIndex == 0)
      break;

     result.Add(pathNames.Substring(lastIndex, i - lastIndex));

     lastIndex = i + 1;
     if (pathNames[lastIndex] == '\0')
      break;
    }
   }

   return result;
  }





  private List<string> GetNetworkMountPoints()
  {
   List<string> result = new List<string>();
   foreach (KeyValuePair<string, string> mountpoint in GetNetworkDrivesInternal())
    if (mountpoint.Value == VolumeId)
     result.Add(mountpoint.Key);

   return result;
  }






  public static IList<VolumeInfo> Volumes
  {
   get
   {
    List<VolumeInfo> result = new List<VolumeInfo>();
    StringBuilder nextVolume = new StringBuilder(NativeMethods.LongPath * sizeof(char));
    SafeHandle handle = NativeMethods.FindFirstVolume(nextVolume, NativeMethods.LongPath);
    if (handle.IsInvalid)
     return result;

    try
    {

     do
      result.Add(new VolumeInfo(nextVolume.ToString()));
     while (NativeMethods.FindNextVolume(handle, nextVolume, NativeMethods.LongPath));
    }
    finally
    {

     NativeMethods.FindVolumeClose(handle);
    }

    return result.AsReadOnly();
   }
  }




  public static IList<VolumeInfo> NetworkDrives
  {
   get
   {
    Dictionary<string, string> localToRemote = GetNetworkDrivesInternal();
    Dictionary<string, string> remoteToLocal = new Dictionary<string, string>();



    foreach (KeyValuePair<string, string> mountpoint in localToRemote)
    {

     if (!remoteToLocal.ContainsKey(mountpoint.Value))
      remoteToLocal.Add(mountpoint.Value, mountpoint.Key);


     else if (remoteToLocal[mountpoint.Value].Length > mountpoint.Key.Length)
      remoteToLocal[mountpoint.Value] = mountpoint.Key;
    }


    List<VolumeInfo> result = new List<VolumeInfo>();
    foreach (string uncPath in remoteToLocal.Keys)
     result.Add(new VolumeInfo(uncPath));

    return result.AsReadOnly();
   }
  }





  private static Dictionary<string, string> GetNetworkDrivesInternal()
  {
   Dictionary<string, string> result = new Dictionary<string, string>();


   IntPtr enumHandle;
   uint errorCode = NativeMethods.WNetOpenEnum(NativeMethods.RESOURCE_CONNECTED,
    NativeMethods.RESOURCETYPE_DISK, 0, IntPtr.Zero, out enumHandle);
   if (errorCode != Win32ErrorCode.Success)
    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());

   try
   {
    int resultBufferCount = 32;
    int resultBufferSize = resultBufferCount *
     Marshal.SizeOf(typeof(NativeMethods.NETRESOURCE));
    IntPtr resultBuffer = Marshal.AllocHGlobal(resultBufferSize);

    try
    {
     for ( ; ; )
     {
      uint resultBufferStored = (uint)resultBufferCount;
      uint resultBufferRequiredSize = (uint)resultBufferSize;
      errorCode = NativeMethods.WNetEnumResource(enumHandle,
       ref resultBufferStored, resultBuffer,
       ref resultBufferRequiredSize);

      if (errorCode == Win32ErrorCode.NoMoreItems)
       break;
      else if (errorCode != Win32ErrorCode.Success)
       throw new Win32Exception((int)errorCode);

      unsafe
      {

       byte* pointer = (byte*)resultBuffer.ToPointer();

       for (uint i = 0; i < resultBufferStored;
        ++i, pointer += Marshal.SizeOf(typeof(NativeMethods.NETRESOURCE)))
       {
        NativeMethods.NETRESOURCE resource =
         (NativeMethods.NETRESOURCE)Marshal.PtrToStructure(
          (IntPtr)pointer, typeof(NativeMethods.NETRESOURCE));



        if (string.IsNullOrEmpty(resource.lpRemoteName))
         continue;
        if (resource.lpRemoteName[resource.lpRemoteName.Length - 1] != '\\')
         resource.lpRemoteName += '\\';
        result.Add(resource.lpLocalName, resource.lpRemoteName);
       }
      }
     }
    }
    finally
    {
     Marshal.FreeHGlobal(resultBuffer);
    }
   }
   finally
   {
    NativeMethods.WNetCloseEnum(enumHandle);
   }

   return result;
  }







  public static VolumeInfo FromMountPoint(string mountPoint)
  {


   DirectoryInfo mountpointDir = new DirectoryInfo(mountPoint);
   if (!mountpointDir.Exists)
    throw new DirectoryNotFoundException();

   do
   {

    string currentDir = mountpointDir.FullName;
    if (currentDir.Length > 0 && currentDir[currentDir.Length - 1] != '\\')
     currentDir += '\\';


    if (string.IsNullOrEmpty(currentDir))
     throw new DirectoryNotFoundException();


    DriveType driveType = (DriveType)NativeMethods.GetDriveType(currentDir);




    StringBuilder volumeID = new StringBuilder(NativeMethods.MaxPath);
    if (driveType == DriveType.Network)
    {

     uint bufferCapacity = (uint)volumeID.Capacity;
     uint errorCode = NativeMethods.WNetGetConnection(
      currentDir.Substring(0, currentDir.Length - 1),
      volumeID, ref bufferCapacity);

     switch (errorCode)
     {
      case Win32ErrorCode.Success:
       return new VolumeInfo(volumeID.ToString() + '\\');

      case Win32ErrorCode.BadDevice:
       break;

      default:
       throw new Win32Exception((int)errorCode);
     }
    }
    else
    {
     if (!NativeMethods.GetVolumeNameForVolumeMountPoint(currentDir, volumeID, 50))
     {
      int errorCode = Marshal.GetLastWin32Error();
      switch (errorCode)
      {
       case Win32ErrorCode.InvalidFunction:
       case Win32ErrorCode.FileNotFound:
       case Win32ErrorCode.PathNotFound:
       case Win32ErrorCode.NotAReparsePoint:
        break;
       default:
        throw Win32ErrorCode.GetExceptionForWin32Error(
         Marshal.GetLastWin32Error());
      }
     }
     else
     {
      return new VolumeInfo(volumeID.ToString());
     }
    }

    mountpointDir = mountpointDir.Parent;
   }
   while (mountpointDir != null);

   throw Win32ErrorCode.GetExceptionForWin32Error(Win32ErrorCode.NotAReparsePoint);
  }




  public string VolumeId { get; private set; }




  public string VolumeLabel { get; private set; }




  public string VolumeFormat { get; private set; }




  public DriveType VolumeType
  {
   get
   {
    return (DriveType)NativeMethods.GetDriveType(VolumeId);
   }
  }




  public int ClusterSize
  {
   get
   {
    uint clusterSize, sectorSize, freeClusters, totalClusters;
    if (NativeMethods.GetDiskFreeSpace(VolumeId, out clusterSize,
     out sectorSize, out freeClusters, out totalClusters))
    {
     return (int)(clusterSize * sectorSize);
    }

    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   }
  }




  public int SectorSize
  {
   get
   {
    uint clusterSize, sectorSize, freeClusters, totalClusters;
    if (NativeMethods.GetDiskFreeSpace(VolumeId, out clusterSize,
     out sectorSize, out freeClusters, out totalClusters))
    {
     return (int)sectorSize;
    }

    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   }
  }




  public bool HasQuota
  {
   get
   {
    ulong freeBytesAvailable, totalNumberOfBytes, totalNumberOfFreeBytes;
    if (NativeMethods.GetDiskFreeSpaceEx(VolumeId, out freeBytesAvailable,
     out totalNumberOfBytes, out totalNumberOfFreeBytes))
    {
     return totalNumberOfFreeBytes != freeBytesAvailable;
    }
    else if (Marshal.GetLastWin32Error() == Win32ErrorCode.NotReady)
    {

     return false;
    }

    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   }
  }




  public bool IsReady { get; private set; }




  public long TotalFreeSpace
  {
   get
   {
    ulong result, dummy;
    if (NativeMethods.GetDiskFreeSpaceEx(VolumeId, out dummy, out dummy, out result))
    {
     return (long)result;
    }

    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   }
  }




  public long TotalSize
  {
   get
   {
    ulong result, dummy;
    if (NativeMethods.GetDiskFreeSpaceEx(VolumeId, out dummy, out result, out dummy))
    {
     return (long)result;
    }

    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   }
  }




  public long AvailableFreeSpace
  {
   get
   {
    ulong result, dummy;
    if (NativeMethods.GetDiskFreeSpaceEx(VolumeId, out result, out dummy, out dummy))
    {
     return (long)result;
    }

    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   }
  }





  public IList<VolumeInfo> MountedVolumes
  {
   get
   {
    List<VolumeInfo> result = new List<VolumeInfo>();
    StringBuilder nextMountpoint = new StringBuilder(NativeMethods.LongPath * sizeof(char));

    SafeHandle handle = NativeMethods.FindFirstVolumeMountPoint(VolumeId,
     nextMountpoint, NativeMethods.LongPath);
    if (handle.IsInvalid)
     return result;

    try
    {

     while (NativeMethods.FindNextVolumeMountPoint(handle, nextMountpoint,
      NativeMethods.LongPath))
     {
      result.Add(new VolumeInfo(nextMountpoint.ToString()));
     }
    }
    finally
    {

     NativeMethods.FindVolumeMountPointClose(handle);
    }

    return result.AsReadOnly();
   }
  }






  public ReadOnlyCollection<string> MountPoints
  {
   get
   {
    return (VolumeType == DriveType.Network ?
     GetNetworkMountPoints() : GetLocalVolumeMountPoints()).AsReadOnly();
   }
  }




  public bool IsMounted
  {
   get { return MountPoints.Count != 0; }
  }
  public FileStream Open(FileAccess access)
  {
   return Open(access, FileShare.None, FileOptions.None);
  }
  public FileStream Open(FileAccess access, FileShare share)
  {
   return Open(access, share, FileOptions.None);
  }
  public FileStream Open(FileAccess access, FileShare share, FileOptions options)
  {
   SafeFileHandle handle = OpenHandle(access, share, options);
   if (handle.IsInvalid)
    throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
   return new FileStream(handle, access);
  }
  private SafeFileHandle OpenHandle(FileAccess access, FileShare share, FileOptions options)
  {
   uint iAccess = 0;
   switch (access)
   {
    case FileAccess.Read:
     iAccess = NativeMethods.GENERIC_READ;
     break;
    case FileAccess.ReadWrite:
     iAccess = NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE;
     break;
    case FileAccess.Write:
     iAccess = NativeMethods.GENERIC_WRITE;
     break;
   }
   if ((share & FileShare.Inheritable) != 0)
    throw new NotSupportedException("Inheritable handles are not supported.");
   if ((options & FileOptions.Asynchronous) != 0)
    throw new NotSupportedException("Asynchronous handles are not implemented.");
   string openPath = VolumeId;
   if (openPath.Length > 0 && openPath[openPath.Length - 1] == '\\')
    openPath = openPath.Remove(openPath.Length - 1);
   SafeFileHandle result = NativeMethods.CreateFile(openPath, iAccess,
    (uint)share, IntPtr.Zero, (uint)FileMode.Open, (uint)options, IntPtr.Zero);
   if (result.IsInvalid)
   {
    int errorCode = Marshal.GetLastWin32Error();
    result.Close();
    throw Win32ErrorCode.GetExceptionForWin32Error(errorCode);
   }
   return result;
  }
  public DiskPerformanceInfo Performance
  {
   get
   {
    using (SafeFileHandle handle = OpenHandle(FileAccess.Read,
     FileShare.ReadWrite, FileOptions.None))
    {
     if (handle.IsInvalid)
      throw Win32ErrorCode.GetExceptionForWin32Error(Marshal.GetLastWin32Error());
     NativeMethods.DiskPerformanceInfoInternal result =
      new NativeMethods.DiskPerformanceInfoInternal();
     uint bytesReturned = 0;
     if (NativeMethods.DeviceIoControl(handle, NativeMethods.IOCTL_DISK_PERFORMANCE,
      IntPtr.Zero, 0, out result, (uint)Marshal.SizeOf(result),
      out bytesReturned, IntPtr.Zero))
     {
      return new DiskPerformanceInfo(result);
     }
     return null;
    }
   }
  }
  public override string ToString()
  {
   ReadOnlyCollection<string> mountPoints = MountPoints;
   return mountPoints.Count == 0 ? VolumeId : mountPoints[0];
  }
  public VolumeLock LockVolume(FileStream stream)
  {
   return new VolumeLock(stream);
  }
 }
 public sealed class VolumeLock : IDisposable
 {
  internal VolumeLock(FileStream stream)
  {
   uint result = 0;
   for (int i = 0; !NativeMethods.DeviceIoControl(stream.SafeFileHandle,
     NativeMethods.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero,
     0, out result, IntPtr.Zero); ++i)
   {
    if (i > 100)
     throw new IOException("Could not lock volume.");
    System.Threading.Thread.Sleep(100);
   }
   Stream = stream;
  }
  ~VolumeLock()
  {
   Dispose(false);
  }
  public void Dispose()
  {
   Dispose(true);
   GC.SuppressFinalize(this);
  }
  [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "disposing")]
  private void Dispose(bool disposing)
  {
   if (Stream == null)
    return;
   Stream.Flush();
   uint result = 0;
   if (!NativeMethods.DeviceIoControl(Stream.SafeFileHandle,
    NativeMethods.FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero,
    0, out result, IntPtr.Zero))
   {
    throw new IOException("Could not unlock volume.");
   }
   Stream = null;
  }
  private FileStream Stream;
 }
 public class DiskPerformanceInfo
 {
  internal DiskPerformanceInfo(NativeMethods.DiskPerformanceInfoInternal info)
  {
   BytesRead = info.BytesRead;
   BytesWritten = info.BytesWritten;
   ReadTime = info.ReadTime;
   WriteTime = info.WriteTime;
   IdleTime = info.IdleTime;
   ReadCount = info.ReadCount;
   WriteCount = info.WriteCount;
   QueueDepth = info.QueueDepth;
   SplitCount = info.SplitCount;
   QueryTime = info.QueryTime;
   StorageDeviceNumber = info.StorageDeviceNumber;
   StorageManagerName = info.StorageManagerName;
  }
  public long BytesRead { get; private set; }
  public long BytesWritten { get; private set; }
  public long ReadTime { get; private set; }
  public long WriteTime { get; private set; }
  public long IdleTime { get; private set; }
  public uint ReadCount { get; private set; }
  public uint WriteCount { get; private set; }
  public uint QueueDepth { get; private set; }
  public uint SplitCount { get; private set; }
  public long QueryTime { get; private set; }
  public uint StorageDeviceNumber { get; private set; }
  public string StorageManagerName { get; private set; }
 }
}