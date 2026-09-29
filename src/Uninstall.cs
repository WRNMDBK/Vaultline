using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
class Uninstall {
 [STAThread] static void Main(){Application.EnableVisualStyles();if(MessageBox.Show("卸载Vaultline？\r\n保险库和备份会保留，方便以后重新安装。","Vaultline",MessageBoxButtons.YesNo,MessageBoxIcon.None)!=DialogResult.Yes)return;try{string dir=AppDomain.CurrentDomain.BaseDirectory;string exe=Path.Combine(dir,"Vaultline.exe");using(var check=new FileStream(exe,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}foreach(string name in new[]{"Vaultline.exe","Vaultline.ico","Vaultline.exe.config"}){string f=Path.Combine(dir,name);if(File.Exists(f))File.Delete(f);}string shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Vaultline.lnk");if(File.Exists(shortcut))File.Delete(shortcut);string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Vaultline","Vaultline.lnk");if(File.Exists(menu))File.Delete(menu);Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Vaultline",false);MessageBox.Show("Vaultline已卸载。\r\n数据和数据位置配置已保留。安装目录中的卸载程序可手动删除。","Vaultline");}catch(Exception e){MessageBox.Show("无法完成卸载，请先关闭Vaultline。\r\n"+e.Message,"Vaultline");}}
}
