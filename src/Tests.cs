using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
namespace Vaultline {
 public static class SelfTest {
  static int count;static void Assert(bool x,string name){if(!x)throw new Exception("FAIL: "+name);count++;}
  static void Reject(Action a,string name){bool failed=false;try{a();}catch{failed=true;}Assert(failed,name);}
  public static void Run(){string root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests"));Directory.CreateDirectory(root);string sandbox=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sandbox);var lines=new List<string>();try{
   Assert(StoragePaths.DefaultData==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Vaultline","Data"),"per-user local storage");Assert(StoragePaths.ResolveData(sandbox)==StoragePaths.DefaultData,"default independent of executable location");File.WriteAllText(Path.Combine(sandbox,"data-path.txt"),Path.Combine(sandbox,"custom-data"));Assert(StoragePaths.ResolveData(sandbox)==Path.Combine(sandbox,"custom-data"),"custom absolute storage");File.WriteAllText(Path.Combine(sandbox,"data-path.txt"),"relative-data");Reject(()=>StoragePaths.ResolveData(sandbox),"reject relative storage");File.WriteAllText(Path.Combine(sandbox,"data-path.txt"),"");Reject(()=>StoragePaths.ResolveData(sandbox),"reject empty storage");File.Delete(Path.Combine(sandbox,"data-path.txt"));var v=new Vault();var e=new Entry{Path="account/wechat/work-account",Level="机密",Fields=Engine.Json.Deserialize<Dictionary<string,object>>("{\"password\":\"test-secret-中文\",\"nested\":{\"a/b\":42},\"cards\":[\"1\",\"2\"],\"empty\":null}"),Updated="test"};Engine.Put(v,e,null);Engine.Validate(v);Assert(v.Folders.Count==2,"parent folders");Entry found;Assert((string)Engine.Resolve(v,e.Path+"/password",out found)=="test-secret-中文","password resolution");Assert(Convert.ToInt32(Engine.Resolve(v,e.Path+"/nested/a~1b",out found))==42,"escaped key");Assert((string)Engine.Resolve(v,e.Path+"/cards/1",out found)=="2","array index");Assert(Engine.Resolve(v,e.Path+"/empty",out found)==null&&found==e,"null field");Reject(()=>Engine.Resolve(v,e.Path+"/missing",out found),"missing field");Reject(()=>Engine.Put(v,new Entry{Path=e.Path,Fields=e.Fields},null),"duplicate");Reject(()=>Engine.ValidatePath("a/../b"),"invalid path");Reject(()=>Engine.AddFolder(v,e.Path+"/child"),"record-folder collision");
   var login=new Dictionary<string,object>{{"address","demo@example.test"},{"password","demo-pass"}};
   Assert(Engine.CopyLines(login)=="demo@example.test\r\ndemo-pass","copy account without JSON or labels");
   Assert(Engine.CopyLines("00secret\"value")=="00secret\"value","copy scalar unchanged");
   Assert(Engine.CopyLines(new Dictionary<string,object>{{"nested",new object[]{"中文",true,12,null}}})=="中文\r\ntrue\r\n12\r\n","copy nested values in order");
   Assert(Engine.CopyLines(new Dictionary<string,object>())=="","empty account copy");
   Assert(Engine.DisplayLines(login)=="address：demo@example.test\r\n\r\npassword：demo-pass","detail uses rows");
   var exportVault=Engine.Json.Deserialize<Vault>(Engine.Json.Serialize(v));
   Engine.AddFolder(exportVault,"account/wechat/empty");
   Engine.Put(exportVault,new Entry{Path="account/wechat/child/work-account",Level="敏感",Fields=login,Updated="test"},null);
   Engine.Put(exportVault,new Entry{Path="account/wechat-other/work-account",Level="普通",Fields=login,Updated="test"},null);
   string exported=Engine.ExportGroup(exportVault,"ACCOUNT/WECHAT");var obj=Engine.Json.Deserialize<Dictionary<string,object>>(exported);
   var records=(System.Collections.IList)obj["entries"];Assert(records.Count==2,"export recursively includes children, excludes prefix sibling");
   var first=(Dictionary<string,object>)records[0];var secondExport=(Dictionary<string,object>)records[1];
   Assert((string)first["label"]=="work-account"&&(string)secondExport["label"]=="work-account"&&(string)first["path"]!=(string)secondExport["path"],"export preserves duplicate labels via paths");
   Assert(exported.Contains("account/wechat/empty"),"export preserves empty group");
   Assert(((System.Collections.IList)Engine.Json.Deserialize<Dictionary<string,object>>(Engine.ExportGroup(exportVault,""))["entries"]).Count==3,"root exports all accounts");
   Assert(((System.Collections.IList)Engine.Json.Deserialize<Dictionary<string,object>>(Engine.ExportGroup(exportVault,"account/wechat/empty"))["entries"]).Count==0,"empty group export is valid JSON");
   Reject(()=>Engine.ExportGroup(exportVault,"missing"),"missing group rejected");
   string exportFile=Path.Combine(sandbox,"export.json"),vaultFile=Path.Combine(sandbox,"vault.jx");
   Engine.WriteExport(exportFile,exported,vaultFile);Assert(File.ReadAllText(exportFile)==exported,"UTF-8 export round trip");
   Engine.WriteExport(exportFile,Engine.ExportGroup(exportVault,""),vaultFile);Assert(((System.Collections.IList)Engine.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(exportFile))["entries"]).Count==3,"export overwrite");
   Reject(()=>Engine.WriteExport(vaultFile,exported,vaultFile),"export cannot overwrite live vault");
   Assert(!Directory.GetFiles(sandbox,".vaultline-export-*.tmp").Any(),"no export temporary files left");
   string all=Engine.ExportAll(exportVault);var imported=Engine.ParseImport(all);
   Assert(imported.Entries.Count==3&&imported.Folders.Count==exportVault.Folders.Count,"full export round trip");
   Assert(Engine.Json.Serialize(imported.Entries.First(x=>x.Path==e.Path).Fields)==Engine.Json.Serialize(e.Fields),"import retains nested JSON types");
   var merged=Engine.MergeImport(new Vault(),imported);Assert(merged.Added==3&&merged.Value.Entries.Count==3,"cross-device import into empty vault");
   var again=Engine.MergeImport(merged.Value,imported);Assert(again.Added==0&&again.Skipped==3,"repeat import skips duplicates");
   var changed=Engine.ParseImport(all);changed.Entries.First(x=>x.Path==e.Path).Fields["password"]="another-secret";
   var conflict=Engine.MergeImport(imported,changed);Assert(conflict.Added==1&&conflict.Renamed==1&&conflict.Value.Entries.Count==4,"conflicting account preserved under unique tag");
   Assert((string)conflict.Value.Entries.First(x=>x.Path==e.Path).Fields["password"]=="test-secret-中文","existing account not overwritten");
   Assert(Engine.MergeImport(conflict.Value,changed).Added==0,"repeat conflicting import is idempotent");
   var occupied=new Vault();Engine.Put(occupied,new Entry{Path="account",Fields=login,Level="敏感"},null);
   var structure=Engine.MergeImport(occupied,imported);Assert(structure.Added==3&&structure.Value.Entries.Any(x=>x.Path.StartsWith("account-import-2/")),"folder-account collision preserves hierarchy");
   Assert(occupied.Folders.Count==0&&occupied.Entries.Count==1,"merge never mutates original");
   Assert(Engine.ParseImport(exported).Entries.Count==2,"legacy group export import supported");
   Assert(Engine.ParseImport(Engine.ExportAll(new Vault())).Entries.Count==0,"empty vault transfer");
   Reject(()=>Engine.ParseImport(all.Replace("Vaultline.Export","Other.Format")),"foreign format rejected");
   Reject(()=>Engine.ParseImport("{\"format\":\"Vaultline.Export\",\"version\":1,\"folders\":[],\"entries\":[{}]}"),"malformed account rejected");
   Reject(()=>Engine.ParseImport(all.Replace("\"version\": 1","\"version\": 99")),"future format version rejected");
   byte[] salt=Crypto.Random(16),k=Crypto.Derive("a strong test password",salt),plain=Encoding.UTF8.GetBytes(Engine.Json.Serialize(v)),blob=Crypto.Seal(plain,k,salt);Assert(Crypto.Open(blob,k).SequenceEqual(plain),"encryption round trip");Assert(!Encoding.UTF8.GetString(blob).Contains("test-secret"),"no plaintext in ciphertext");byte[] corrupt=(byte[])blob.Clone();corrupt[44]^=1;Reject(()=>Crypto.Open(corrupt,k),"tamper rejection");Reject(()=>Crypto.Open(blob,new byte[64]),"wrong key");Reject(()=>Crypto.Salt(new byte[8]),"truncation");Assert(!Crypto.Seal(plain,k,salt).SequenceEqual(blob),"random IV");
   using(var s=new Store(sandbox)){s.Unlock("a strong test password");s.Value=v;s.Save();s.Save();Assert(File.Exists(s.FilePath+".bak"),"atomic backup");Reject(()=>{using(var second=new Store(sandbox)){}},"single writer");s.Lock();Reject(()=>s.Unlock("wrong password"),"incorrect password");s.Unlock("a strong test password");Assert(s.Value.Entries.Count==1,"persistence");s.ChangePassword("a different long password");s.Lock();Reject(()=>s.Unlock("a strong test password"),"old password rejected");s.Unlock("a different long password");Assert(s.Value.Entries.Count==1,"password rotation");s.Value=Engine.MergeImport(s.Value,imported).Value;s.Save();s.Lock();s.Unlock("a different long password");Assert(s.Value.Entries.Count==3&&s.Value.Folders.Contains("account/wechat/empty"),"imported accounts and empty groups persist encrypted");}
   lines.Add("PASS "+count+" assertions");lines.Add("Validated path resolution, JSON, authenticated encryption, tampering, atomic saves, locks and password rotation.");
  }catch(Exception e){lines.Add(e.ToString());Environment.ExitCode=1;}finally{File.WriteAllLines(Path.Combine(root,"test-results.txt"),lines);if(Path.GetFullPath(sandbox).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(sandbox,true);}}
 }
 public static class Preview {
  public static void Run(){string root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests"));Directory.CreateDirectory(root);string dir=Path.Combine(root,"preview-data");using(var s=new Store(dir)){s.Unlock("preview-only-password");s.Value=new Vault();Engine.AddFolder(s.Value,"工作");Engine.AddFolder(s.Value,"项目/api-keys");Engine.AddFolder(s.Value,"account/wechat");Engine.AddFolder(s.Value,"account/游戏");foreach(string name in new[]{"work-account","social-account"})Engine.Put(s.Value,new Entry{Path="account/wechat/"+name,Level="机密",Updated="2026-09-29 22:00:00",Fields=new Dictionary<string,object>{{"微信号","演示数据"},{"password","demo-only"},{"绑定的手机号","演示数据"},{"绑定的银行卡","演示数据"}}},null);using(var f=new MainForm(s)){f.Show();Application.DoEvents();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(MainForm).GetMethod("Navigate",flags).Invoke(f,new object[]{"account/wechat"});var query=(TextBox)typeof(MainForm).GetField("search",flags).GetValue(f);query.Text="account/wechat/work-account/password";typeof(MainForm).GetMethod("Search",flags).Invoke(f,null);var detail=(TextBox)typeof(MainForm).GetField("detail",flags).GetValue(f);if(detail.Text.Contains("demo-only"))throw new Exception("UI secret leaked before reveal");typeof(MainForm).GetMethod("Reveal",flags).Invoke(f,null);if(detail.Text!="demo-only")throw new Exception("UI field resolution failed");typeof(MainForm).GetMethod("Reveal",flags).Invoke(f,null);query.Text="account/wechat/work-account";typeof(MainForm).GetMethod("Search",flags).Invoke(f,null);Application.DoEvents();
var tree=(TreeView)typeof(MainForm).GetField("tree",flags).GetValue(f);
if(tree.DrawMode!=TreeViewDrawMode.OwnerDrawAll)throw new Exception("Tree selection is not fully owner drawn");
tree.Focus();Application.DoEvents();
using(var shot=new Bitmap(tree.Width,tree.Height)){tree.DrawToBitmap(shot,tree.ClientRectangle);int y=tree.SelectedNode.Bounds.Top+3;if(shot.GetPixel(2,y).ToArgb()!=Theme.Gold.ToArgb()||shot.GetPixel(tree.Width-3,y).ToArgb()!=Theme.Gold.ToArgb())throw new Exception("Selection does not cover full row");shot.Save(Path.Combine(root,"tree-focused.png"));}
query.Focus();Application.DoEvents();
using(var shot=new Bitmap(tree.Width,tree.Height)){tree.DrawToBitmap(shot,tree.ClientRectangle);int y=tree.SelectedNode.Bounds.Top+3;if(shot.GetPixel(2,y).ToArgb()!=Theme.Gold.ToArgb()||shot.GetPixel(tree.Width-3,y).ToArgb()!=Theme.Gold.ToArgb())throw new Exception("Unfocused selection color changed");}
if(typeof(MainForm).GetMethod("ShowGroupMenu",flags)!=null||tree.ContextMenuStrip!=null)throw new Exception("Group export menu still exists");
if(typeof(MainForm).GetMethod("ClearClipboard",flags)!=null)throw new Exception("Automatic clipboard clearing still exists");
var toolbar=f.Controls[0].Controls.OfType<FlowLayoutPanel>().Single();
var captions=toolbar.Controls.Cast<Control>().Select(c=>c.Text).ToArray();int exp=Array.IndexOf(captions,"导出数据");
if(exp<0||captions[exp+1]!="一键导入"||captions[exp+2]!="帮助")throw new Exception("Toolbar order incorrect");
f.Size=f.MinimumSize;Application.DoEvents();
if(toolbar.Controls.Cast<Control>().Any(c=>c.Bottom>toolbar.ClientSize.Height))throw new Exception("Toolbar clips: "+toolbar.ClientSize+"; "+string.Join(";",toolbar.Controls.Cast<Control>().Select(c=>c.Text+":"+c.Bounds)));
f.ClientSize=new Size(1280,800);Application.DoEvents();
typeof(MainForm).GetMethod("Reveal",flags).Invoke(f,null);
if(detail.Text.StartsWith("{")||!detail.Text.Contains("password：demo-only"))throw new Exception("Account detail did not use row display");
Application.DoEvents();File.WriteAllText(Path.Combine(root,"ui-results.txt"),"PASS: navigation, field search, masking/reveal, full-row gold selection focused/unfocused, row details, full export/import toolbar, responsive layout, removed clipboard clearing and rendering.");using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(root,"preview.png"));}f.Close();}}}
 }
}
