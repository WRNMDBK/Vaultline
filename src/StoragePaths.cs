using System;
using System.IO;

namespace Vaultline {
 public static class StoragePaths {
  public static string DefaultData {
   get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vaultline", "Data"); }
  }
  public static string DefaultInstall {
   get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Vaultline"); }
  }
  public static string ResolveData(string executableDirectory) {
   string config=Path.Combine(executableDirectory,"data-path.txt");
   if(!File.Exists(config))return DefaultData;
   string path=File.ReadAllText(config).Trim();
   if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new Exception("数据位置配置无效，请将 data-path.txt 设置为本地数据目录的绝对路径。");
   return Path.GetFullPath(path);
  }
 }
}
